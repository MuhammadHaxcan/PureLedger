using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services;
using PureLedger.Services.Interface;
using Xunit;

namespace PureLedger.Backend.Tests;

/// <summary>
/// Opt-in tests for an isolated, already Expense-enabled PostgreSQL database.
/// Set PURELEDGER_TEST_POSTGRES to its connection string. These tests never
/// create/drop a database and roll back or delete every row that they create.
/// </summary>
public sealed class PaymentReceiptPostgresIntegrationTests
{
    [PostgresTheory]
    [InlineData(PaymentReceiptApprovalCheckpoint.VoucherSaved)]
    [InlineData(PaymentReceiptApprovalCheckpoint.EntriesSaved)]
    [InlineData(PaymentReceiptApprovalCheckpoint.InstrumentDetailsSaved)]
    public async Task ApprovalFault_RollsBackTheCompleteGraph(PaymentReceiptApprovalCheckpoint checkpoint)
    {
        var approvalId = 0;
        var beforeVoucherCount = 0;
        var beforeEntryCount = 0;
        var beforeDetailCount = 0;
        try
        {
            await using var context = CreateContext();
            var candidates = await context.Accounts.AsNoTracking()
                .Where(x => x.IsEnabled && x.AccountGroupId == 1)
                .Select(x => new { x.AccountId, x.CategoryId, ProjectId = x.Category!.ProjectId })
                .ToListAsync();
            var accounts = candidates.GroupBy(x => x.ProjectId).FirstOrDefault(x => x.Count() >= 2)?.Take(2).ToList()
                ?? throw new InvalidOperationException("The isolated test database needs two enabled group-1 accounts in one project.");
            var userId = await context.Users.AsNoTracking().Select(x => x.UserId).FirstAsync();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var version = Guid.NewGuid();
            var approval = new ApprovalVoucher
            {
                VoucherType = ApprovalVoucherType.PaymentReceipt,
                Status = ApprovalStatuses.Pending,
                RequestKind = ApprovalRequestKinds.New,
                ProjectId = accounts[0].ProjectId,
                RequestedEntryDate = date,
                SnapshotVersion = version,
                SubmittedDate = date,
                PostedByUserId = userId,
                Entries = new List<ApprovalVoucherEntry>
                {
                    new()
                    {
                        CategoryId = accounts[0].CategoryId, AccountId = accounts[0].AccountId,
                        Description = "Fault injection debit", InstrumentType = PaymentInstrumentType.Cash, Debit = 1
                    },
                    new()
                    {
                        CategoryId = accounts[1].CategoryId, AccountId = accounts[1].AccountId,
                        Description = "Fault injection credit", InstrumentType = PaymentInstrumentType.Cash, Credit = 1
                    }
                }
            };
            context.ApprovalVouchers.Add(approval);
            await context.SaveChangesAsync();
            approvalId = approval.ApprovalVoucherId;
            beforeVoucherCount = await context.JournalVouchers.CountAsync();
            beforeEntryCount = await context.JournalEntries.CountAsync();
            beforeDetailCount = await context.JournalEntryInstrumentDetails.CountAsync();

            var paymentReceipts = new PaymentReceiptService(
                context,
                new PaymentReceiptClassifier(context),
                new ThrowingApprovalCheckpoint(checkpoint));
            var approvals = new ApprovalService(context, new UnusedProcessingService(), paymentReceipts);

            var error = await Assert.ThrowsAsync<InjectedApprovalFaultException>(() =>
                approvals.ApproveAsync(approvalId, version, userId, isAdmin: true));
            Assert.Equal(checkpoint, error.Checkpoint);

            context.ChangeTracker.Clear();
            var persisted = await context.ApprovalVouchers.AsNoTracking().SingleAsync(x => x.ApprovalVoucherId == approvalId);
            Assert.Equal(ApprovalStatuses.Pending, persisted.Status);
            Assert.Null(persisted.JournalVoucherId);
            Assert.Equal(beforeVoucherCount, await context.JournalVouchers.CountAsync());
            Assert.Equal(beforeEntryCount, await context.JournalEntries.CountAsync());
            Assert.Equal(beforeDetailCount, await context.JournalEntryInstrumentDetails.CountAsync());
        }
        finally
        {
            if (approvalId > 0)
            {
                await using var cleanup = CreateContext();
                await cleanup.ApprovalVouchers.Where(x => x.ApprovalVoucherId == approvalId).ExecuteDeleteAsync();
            }
        }
    }

    [PostgresFact]
    public async Task CurrentMigration_EnforcesTrimmedReferenceCheck()
    {
        await using var context = CreateContext();
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260731010000_AddPaymentReceiptVouchers", migrations);
        var master = await context.Accounts.AsNoTracking()
            .Where(x => x.IsEnabled && x.AccountGroupId == 1)
            .Select(x => new { x.AccountId, x.CategoryId, ProjectId = x.Category!.ProjectId })
            .FirstAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var voucher = new JournalVoucher
        {
            ProjectId = master.ProjectId,
            EntryDate = date,
            CreditableDate = date,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        voucher.JournalEntries.Add(new JournalEntry
        {
            AccountId = master.AccountId,
            CategoryId = master.CategoryId,
            Debit = 1,
            IsProcessed = true,
            InstrumentDetail = new JournalEntryInstrumentDetail
            {
                InstrumentType = PaymentInstrumentType.Cheque,
                ReferenceNumber = " padded-reference ",
                NormalizedReferenceNumber = "PADDED-REFERENCE",
                InstrumentDate = date
            }
        });
        context.JournalVouchers.Add(voucher);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = FindPostgresException(error);
        Assert.NotNull(postgres);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres!.SqlState);
        Assert.Equal("CK_JournalEntryInstrumentDetails_InstrumentFields", postgres.ConstraintName);
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    public async Task JournalVoucherRowVersion_RejectsStaleWriter()
    {
        var voucherId = 0;
        try
        {
            await using (var setup = CreateContext())
            {
                var projectId = await setup.Projects.AsNoTracking().Select(x => x.ProjectId).FirstAsync();
                var date = DateOnly.FromDateTime(DateTime.UtcNow);
                var voucher = new JournalVoucher
                {
                    ProjectId = projectId,
                    EntryDate = date,
                    CreditableDate = date,
                    RowVersion = Guid.NewGuid().ToByteArray()
                };
                setup.JournalVouchers.Add(voucher);
                await setup.SaveChangesAsync();
                voucherId = voucher.JournalVoucherId;
            }

            await using var first = CreateContext();
            await using var stale = CreateContext();
            var firstCopy = await first.JournalVouchers.SingleAsync(x => x.JournalVoucherId == voucherId);
            var staleCopy = await stale.JournalVouchers.SingleAsync(x => x.JournalVoucherId == voucherId);
            firstCopy.PendingForCharges = true;
            firstCopy.RowVersion = Guid.NewGuid().ToByteArray();
            await first.SaveChangesAsync();

            staleCopy.IsBaseVoucher = true;
            staleCopy.RowVersion = Guid.NewGuid().ToByteArray();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        }
        finally
        {
            if (voucherId > 0)
            {
                await using var cleanup = CreateContext();
                await cleanup.JournalVouchers.Where(x => x.JournalVoucherId == voucherId).ExecuteDeleteAsync();
            }
        }
    }

    private static LedgerDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!;
        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(connectionString).Options;
        return new LedgerDbContext(options);
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        return null;
    }

    private sealed class ThrowingApprovalCheckpoint(PaymentReceiptApprovalCheckpoint target)
        : IPaymentReceiptApprovalCheckpoint
    {
        public Task ReachedAsync(PaymentReceiptApprovalCheckpoint checkpoint) =>
            checkpoint == target
                ? Task.FromException(new InjectedApprovalFaultException(checkpoint))
                : Task.CompletedTask;
    }

    private sealed class InjectedApprovalFaultException(PaymentReceiptApprovalCheckpoint checkpoint) : Exception
    {
        public PaymentReceiptApprovalCheckpoint Checkpoint { get; } = checkpoint;
    }

    private sealed class UnusedProcessingService : ISharedProcessingService
    {
        private static Exception Unexpected() => new InvalidOperationException("Expense processing was not expected.");
        public Task<bool> DetermineEntryProcessedStatusAsync(JournalEntry entry) => Task.FromException<bool>(Unexpected());
        public Task<bool> RecalculateVoucherCompletionAsync(int journalVoucherId) => Task.FromException<bool>(Unexpected());
        public Task CompleteReferenceVouchersAsync(int journalVoucherId) => Task.FromException(Unexpected());
        public Task ReopenReferenceVouchersAsync(int journalVoucherId) => Task.FromException(Unexpected());
        public Task<JournalVoucherReference> CreateVoucherReferenceAsync(int mainVoucherId, int referenceVoucherId, string referenceType) =>
            Task.FromException<JournalVoucherReference>(Unexpected());
        public Task<bool> DeleteReferenceVoucherIfEmptyAsync(int journalVoucherId) => Task.FromException<bool>(Unexpected());
        public Task DeleteLinkedDCEntryAsync(int journalEntryId) => Task.FromException(Unexpected());
        public Task HandleAllEntriesDeletedAsync(int journalVoucherId) => Task.FromException(Unexpected());
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")))
            Skip = "Set PURELEDGER_TEST_POSTGRES to an isolated, migrated PostgreSQL test database.";
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")))
            Skip = "Set PURELEDGER_TEST_POSTGRES to an isolated, migrated PostgreSQL test database.";
    }
}
