using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PureLedger.Models;
using PureLedger.Data;
using PureLedger.Repositories;
using PureLedger.Services;
using Microsoft.Extensions.Logging.Abstractions;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

[Collection("PostgreSQL integration")]
public sealed class AdminAutoApprovalPostgresTests
{
    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submission_PostsThroughControllerAndAppearsInHistoryAndReports(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true);
        await AdminAutoApprovalTests.CheckAdminPostingAsync(data, paymentReceipt);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revision_RetainsAggregateAndRejectsStaleEditor(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true);
        await AdminAutoApprovalTests.CheckRevisionAsync(data, paymentReceipt);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryUser_CannotAutoApproveWithStaleAdminClaim(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true);
        var result = await data.SubmitAsync(paymentReceipt, data.UserId, claimedAdmin: true);
        Assert.Equal("PendingApproval", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("autoApproved").GetBoolean());
        Assert.False(await data.Context.JournalVouchers.AnyAsync(x => x.ProjectId == data.ProjectId));
    }

    [PostgresTheory]
    [InlineData(PaymentReceiptApprovalCheckpoint.VoucherSaved, false)]
    [InlineData(PaymentReceiptApprovalCheckpoint.EntriesSaved, false)]
    [InlineData(PaymentReceiptApprovalCheckpoint.InstrumentDetailsSaved, false)]
    [InlineData(PaymentReceiptApprovalCheckpoint.EntriesSaved, true)]
    [InlineData(PaymentReceiptApprovalCheckpoint.InstrumentDetailsSaved, true)]
    public async Task PaymentReceiptFault_RollsBackSubmissionAndLiveRevision(
        PaymentReceiptApprovalCheckpoint stage, bool revision)
    {
        var fault = new PostingFault(stage);
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true, checkpoint: fault);
        JournalVoucher? original = null;
        if (revision)
        {
            await data.SubmitAsync(true, data.AdminId);
            original = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
                .SingleAsync(x => x.ProjectId == data.ProjectId);
        }
        var priorIds = original?.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray();
        var priorVersion = original?.RowVersion!.ToArray();
        fault.Armed = true;
        if (revision)
        {
            var request = data.PaymentRequest();
            request.VoucherVersion = priorVersion;
            request.Entries[0].Description = "Must roll back";
            await Assert.ThrowsAsync<PostingFaultException>(() => data.Payments.SubmitRevisionAsync(
                original!.JournalVoucherId, request, data.AdminId, true));
        }
        else
            await Assert.ThrowsAsync<PostingFaultException>(() => data.Payments.SubmitAsync(data.PaymentRequest(), data.AdminId, true));
        data.Context.ChangeTracker.Clear();
        var approvals = await data.Context.ApprovalVouchers.Where(x => x.ProjectId == data.ProjectId).ToListAsync();
        Assert.Equal(revision ? 1 : 0, approvals.Count);
        Assert.DoesNotContain(approvals, x => x.Status == ApprovalStatuses.Pending);
        var vouchers = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .ThenInclude(x => x.InstrumentDetail).Where(x => x.ProjectId == data.ProjectId).ToListAsync();
        if (revision)
        {
            var restored = Assert.Single(vouchers);
            Assert.True(restored.IsCompleted);
            Assert.Equal(priorVersion, restored.RowVersion);
            Assert.Equal(priorIds, restored.JournalEntries.Select(x => x.JournalEntryId).Order().ToArray());
            Assert.DoesNotContain(restored.JournalEntries, x => x.Description == "Must roll back");
            Assert.All(restored.JournalEntries, x => Assert.NotNull(x.InstrumentDetail));
        }
        else Assert.Empty(vouchers);
    }

    [PostgresTheory]
    [InlineData(false, "Cartage")]
    [InlineData(true, "Cartage")]
    [InlineData(false, "ForCharges")]
    [InlineData(true, "ForCharges")]
    public async Task ExpenseRevision_LinkedGraphCleanupIsAtomic(bool fail, string referenceType)
    {
        var fault = new ExpenseWriteFault();
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true, interceptor: fault);
        await data.SubmitAsync(false, data.AdminId);
        var parent = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.ProjectId == data.ProjectId);
        var child = new JournalVoucher
        {
            ProjectId = data.ProjectId, IsCompleted = true, EntryDate = data.EntryDate,
            CreditableDate = data.EntryDate,
            JournalEntries = new List<JournalEntry>
            {
                new() { AccountId = data.ExpenseId, CategoryId = data.CategoryId, Debit = 5, IsProcessed = true },
                new() { AccountId = data.CashId, CategoryId = data.CategoryId, Credit = 5, IsProcessed = true }
            }
        };
        data.Context.JournalVouchers.Add(child);
        await data.Context.SaveChangesAsync();
        data.Context.JournalVoucherReferences.Add(new JournalVoucherReference
        {
            MainVoucherId = parent.JournalVoucherId, ReferencedVoucherId = child.JournalVoucherId, ReferenceType = referenceType
        });
        data.Context.DeliveryChallanVouchers.AddRange(
            new DeliveryChallanVoucher { JournalVoucherId = parent.JournalVoucherId, IsCompleted = true },
            new DeliveryChallanVoucher { JournalVoucherId = child.JournalVoucherId, IsCompleted = true });
        await data.Context.SaveChangesAsync();
        var priorVersion = parent.RowVersion!.ToArray();
        var model = data.ExpenseModel();
        model.VoucherVersion = priorVersion;
        model.Entries[0].EntryId = parent.JournalEntries.Single(x => x.Debit > 0).JournalEntryId;
        model.Entries[0].Des = "Changed expense";
        fault.Armed = fail;
        if (fail)
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => data.Approvals.SubmitManualExpenseAsync(model, parent.JournalVoucherId, data.AdminId));
            Assert.IsType<PostingFaultException>(error.InnerException);
        }
        else
            await data.Approvals.SubmitManualExpenseAsync(model, parent.JournalVoucherId, data.AdminId);
        fault.Armed = false;
        data.Context.ChangeTracker.Clear();
        var restored = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.JournalVoucherId == parent.JournalVoucherId);
        Assert.True(restored.IsCompleted);
        Assert.Equal(fail, await data.Context.JournalVouchers.AnyAsync(x => x.JournalVoucherId == child.JournalVoucherId));
        Assert.Equal(fail, await data.Context.JournalVoucherReferences.AnyAsync(x => x.MainVoucherId == parent.JournalVoucherId));
        Assert.Equal(fail ? 2 : 0, await data.Context.DeliveryChallanVouchers.CountAsync(x => x.JournalVoucher.ProjectId == data.ProjectId));
        Assert.Equal(fail ? 1 : 2, await data.Context.ApprovalVouchers.CountAsync(x => x.ProjectId == data.ProjectId));
        if (fail)
        {
            Assert.Equal(priorVersion, restored.RowVersion);
            Assert.DoesNotContain(restored.JournalEntries, x => x.Description == "Changed expense");
        }
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentAdminRevisions_CommitOnlyOneVersion(bool paymentReceipt)
    {
        await using var data = await AutoApprovalTestData.CreateAsync(postgres: true);
        await data.SubmitAsync(paymentReceipt, data.AdminId);
        var voucher = await data.Context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.ProjectId == data.ProjectId);
        var version = voucher.RowVersion!.ToArray();
        async Task<Exception?> ReviseAsync()
        {
            await using var context = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!).Options);
            var classifier = new PaymentReceiptClassifier(context);
            var payments = new PaymentReceiptService(context, classifier);
            try
            {
                if (paymentReceipt)
                {
                    var request = data.PaymentRequest(); request.VoucherVersion = version;
                    await payments.SubmitRevisionAsync(voucher.JournalVoucherId, request, data.AdminId, true);
                }
                else
                {
                    var approvals = new ApprovalService(context, new SharedProcessingService(new UnitOfWork(context),
                        NullLogger<SharedProcessingService>.Instance, new ApprovalGuardService(context), classifier), payments);
                    var request = data.ExpenseModel(); request.VoucherVersion = version;
                    await approvals.SubmitManualExpenseAsync(request, voucher.JournalVoucherId, data.AdminId);
                }
                return null;
            }
            catch (Exception error) { return error; }
        }
        var outcomes = await Task.WhenAll(ReviseAsync(), ReviseAsync());
        Assert.Single(outcomes, x => x == null);
        var failure = Assert.Single(outcomes, x => x != null)!;
        Assert.True(failure is ApprovalConflictException ||
            failure is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure } ||
            failure.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure }, failure.ToString());
        data.Context.ChangeTracker.Clear();
        Assert.True((await data.Context.JournalVouchers.SingleAsync(x => x.JournalVoucherId == voucher.JournalVoucherId)).IsCompleted);
        Assert.Equal(2, await data.Context.ApprovalVouchers.CountAsync(x => x.ProjectId == data.ProjectId && x.Status == ApprovalStatuses.Approved));
        Assert.False(await data.Context.ApprovalVouchers.AnyAsync(x => x.ProjectId == data.ProjectId && x.Status == ApprovalStatuses.Pending));
    }

    private sealed class PostingFaultException : Exception { }

    private sealed class PostingFault(PaymentReceiptApprovalCheckpoint stage) : IPaymentReceiptApprovalCheckpoint
    {
        public bool Armed { get; set; }
        public Task ReachedAsync(PaymentReceiptApprovalCheckpoint checkpoint) =>
            Armed && stage == checkpoint ? Task.FromException(new PostingFaultException()) : Task.CompletedTask;
    }

    private sealed class ExpenseWriteFault : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && (command.CommandText.Contains("UPDATE \"JournalEntries\"") ||
                          command.CommandText.Contains("INSERT INTO \"JournalEntries\"")))
                throw new PostingFaultException();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
