using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Repositories;
using PureLedger.Services;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.PaymentReceipt;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class PaymentReceiptTests
{
    [Fact]
    public async Task ApprovedAggregate_AutomaticallyResolvesFromJvToPaymentReceipt()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var classifier = new PaymentReceiptClassifier(context);

        Assert.True(await classifier.IsStructurallyValidAsync(voucher.JournalVoucherId));
        Assert.True(await classifier.IsApprovedAsync(voucher.JournalVoucherId));
        Assert.Equal(VoucherType.PaymentReceipt,
            await classifier.ResolvePrintableTypeAsync(voucher.JournalVoucherId, VoucherType.JV));
    }

    [Fact]
    public async Task StructuralValidation_RejectsIncorrectReferenceNormalization()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var detail = await context.JournalEntryInstrumentDetails.OrderBy(x => x.JournalEntryInstrumentDetailId).FirstAsync();
        detail.InstrumentType = PaymentInstrumentType.Cheque;
        detail.ReferenceNumber = "cheque-007";
        detail.NormalizedReferenceNumber = "WRONG";
        detail.InstrumentDate = new DateOnly(2026, 7, 1);
        await context.SaveChangesAsync();

        Assert.False(await new PaymentReceiptClassifier(context).IsStructurallyValidAsync(voucher.JournalVoucherId));
    }

    [Fact]
    public async Task ExplicitPaymentReceiptPrint_RejectsOrdinaryVoucher()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var voucher = new JournalVoucher
        {
            ProjectId = 1,
            EntryDate = new DateOnly(2026, 7, 1),
            CreditableDate = new DateOnly(2026, 7, 1),
            IsCompleted = true
        };
        context.JournalVouchers.Add(voucher);
        await context.SaveChangesAsync();

        var classifier = new PaymentReceiptClassifier(context);
        await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            classifier.ResolvePrintableTypeAsync(voucher.JournalVoucherId, VoucherType.PaymentReceipt));
    }

    [Fact]
    public async Task Submission_CashClearsReferenceAndInstrumentDate()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var service = CreateService(context);
        var request = BalancedRequest(PaymentInstrumentType.Cash, "SHOULD-CLEAR", new DateOnly(2026, 7, 2));

        await service.SubmitAsync(request, userId: 1, isAdmin: true);

        var rows = await context.ApprovalVoucherEntries.OrderBy(x => x.ApprovalVoucherEntryId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Null(row.LineNumber);
            Assert.Equal(PaymentInstrumentType.Cash, row.InstrumentType);
            Assert.Null(row.ReferenceNumber);
            Assert.Null(row.NormalizedReferenceNumber);
            Assert.Null(row.InstrumentDate);
        });
    }

    [Fact]
    public async Task Submission_NoncashRequiresReferenceAndDate()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var service = CreateService(context);
        var request = BalancedRequest(PaymentInstrumentType.Cheque, null, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitAsync(request, userId: 1, isAdmin: true));
        Assert.Contains("reference or cheque number is required", error.Message);
        Assert.Empty(context.ApprovalVouchers);
    }

    [Fact]
    public async Task DuplicateReferences_AreWarningsAndDoNotBlockSubmission()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var service = CreateService(context);
        var request = BalancedRequest(PaymentInstrumentType.BankTransfer, "  ref-001  ", new DateOnly(2026, 7, 2));

        var warnings = await service.FindDuplicateWarningsAsync(request.Entries, null, null);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.True(warning.MatchesCurrentRequest));

        await service.SubmitAsync(request, userId: 1, isAdmin: true);
        var approval = Assert.Single(context.ApprovalVouchers);
        Assert.True(approval.HadDuplicateWarningsAtSubmission);
        Assert.Equal(ApprovalStatuses.Pending, approval.Status);
    }

    [Fact]
    public void DuplicateWarning_DoesNotSerializeExternalSourceFlags()
    {
        var warning = new PaymentReceiptDuplicateWarning
        {
            InstrumentType = PaymentInstrumentType.Cheque,
            ReferenceNumber = "0007",
            MatchesExistingVoucher = true,
            MatchesPendingRequest = true,
            Message = "A matching instrument reference exists in another Payment & Receipt voucher or request."
        };

        var json = JsonSerializer.Serialize(warning);
        var roundTripped = JsonSerializer.Deserialize<PaymentReceiptDuplicateWarning>(json);

        Assert.DoesNotContain("MatchesExistingVoucher", json);
        Assert.DoesNotContain("MatchesPendingRequest", json);
        Assert.Equal(warning.Message, roundTripped!.Message);
    }

    [Fact]
    public async Task Report_RejectsAnInvertedDateRange()
    {
        await using var context = CreateContext();
        var request = new PaymentReceiptReportRequest
        {
            StartDate = new DateOnly(2026, 8, 2),
            EndDate = new DateOnly(2026, 8, 1)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(context).GetReportAsync(request, userId: 1, isAdmin: true));

        Assert.Contains("Start date cannot be after end date", error.Message);
    }

    [Fact]
    public async Task Report_RequiresAProject()
    {
        await using var context = CreateContext();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(context).GetReportAsync(
                new PaymentReceiptReportRequest(), userId: 1, isAdmin: true));

        Assert.Contains("Project is required", error.Message);
    }

    [Fact]
    public async Task ReportWithoutLineFilters_DoesNotHighlightEveryEntry()
    {
        await using var context = CreateContext();
        await SeedPaymentReceiptAsync(context, completed: true, approved: true);

        var response = await CreateService(context).GetReportAsync(
            new PaymentReceiptReportRequest { ProjectId = 1 }, userId: 1, isAdmin: true);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var entries = json.RootElement.GetProperty("vouchers")[0].GetProperty("entries");

        Assert.Equal(2, entries.GetArrayLength());
        Assert.All(entries.EnumerateArray(), entry =>
            Assert.False(entry.GetProperty("isFilterMatch").GetBoolean()));
    }

    [Fact]
    public async Task ReportInitialization_IncludesProjectOnAccountOptions()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);

        var response = await CreateService(context).GetReportInitAsync(userId: 1, isAdmin: true);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var accounts = json.RootElement.GetProperty("accounts");

        Assert.Equal(2, accounts.GetArrayLength());
        Assert.All(accounts.EnumerateArray(), account =>
            Assert.Equal(1, account.GetProperty("projectId").GetInt32()));
    }

    [Fact]
    public async Task Approval_RejectsCorruptSnapshotInsteadOfDefaultingNullInstrumentToCash()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var version = Guid.NewGuid();
        var approval = new ApprovalVoucher
        {
            VoucherType = ApprovalVoucherType.PaymentReceipt,
            Status = ApprovalStatuses.Pending,
            RequestKind = ApprovalRequestKinds.New,
            ProjectId = 1,
            RequestedEntryDate = new DateOnly(2026, 7, 1),
            SnapshotVersion = version,
            SubmittedDate = new DateOnly(2026, 7, 1),
            PostedByUserId = 1,
            Entries = new List<ApprovalVoucherEntry>
            {
                new() { AccountId = 1, CategoryId = 1, Debit = 100, InstrumentType = null },
                new() { AccountId = 2, CategoryId = 1, Credit = 100, InstrumentType = PaymentInstrumentType.Cash }
            }
        };
        context.ApprovalVouchers.Add(approval);
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            CreateService(context).ApproveAsync(approval, version, userId: 1, isAdmin: true));

        Assert.Contains("invalid instrument type", error.Message);
        Assert.Empty(context.JournalVouchers);
        Assert.Equal(ApprovalStatuses.Pending, approval.Status);
    }

    [Fact]
    public async Task Approval_PersistsEntriesThenInstrumentDetailsAndCompletesTheAggregate()
    {
        await using var context = CreateContext();
        await SeedMastersAsync(context);
        var service = CreateService(context);
        await service.SubmitAsync(
            BalancedRequest(PaymentInstrumentType.Cheque, "  000712  ", new DateOnly(2026, 7, 2)),
            userId: 1,
            isAdmin: true);
        var approval = await context.ApprovalVouchers.Include(x => x.Entries).SingleAsync();

        var result = await service.ApproveAsync(
            approval,
            approval.SnapshotVersion,
            userId: 1,
            isAdmin: true);

        Assert.NotNull(result);
        var voucher = await context.JournalVouchers
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .SingleAsync();
        Assert.True(voucher.IsCompleted);
        Assert.Equal(2, voucher.JournalEntries.Count);
        Assert.All(voucher.JournalEntries, entry =>
        {
            Assert.NotNull(entry.InstrumentDetail);
            Assert.Equal("000712", entry.InstrumentDetail!.ReferenceNumber);
            Assert.Equal("000712", entry.InstrumentDetail.NormalizedReferenceNumber);
            Assert.Equal(PaymentInstrumentType.Cheque, entry.InstrumentDetail.InstrumentType);
        });
        Assert.Equal(ApprovalStatuses.Approved, approval.Status);
        Assert.Equal(voucher.JournalVoucherId, approval.JournalVoucherId);
    }

    [Fact]
    public async Task CompletionRecalculation_DoesNotChangePaymentReceiptState()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: false, approved: false);
        var classifier = new PaymentReceiptClassifier(context);
        var unitOfWork = new UnitOfWork(context);
        var service = new SharedProcessingService(
            unitOfWork,
            NullLogger<SharedProcessingService>.Instance,
            new ApprovalGuardService(context),
            classifier);

        Assert.False(await service.RecalculateVoucherCompletionAsync(voucher.JournalVoucherId));
        Assert.False((await context.JournalVouchers.FindAsync(voucher.JournalVoucherId))!.IsCompleted);
    }

    [Fact]
    public async Task GenericVoucherDeletion_IsBlockedForAnyInstrumentLinkedVoucher()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var repository = new JournalVoucherRepository(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(voucher.JournalVoucherId));
        Assert.NotNull(await context.JournalVouchers.FindAsync(voucher.JournalVoucherId));
    }

    [Fact]
    public async Task DeliveryChallanSelection_ExcludesAnyInstrumentLinkedVoucher()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: false, approved: false);
        var repository = new JournalVoucherRepository(context);

        var selected = await repository.GetFilteredVouchersAsync(
            sinceDate: null,
            toDate: null,
            projectId: voucher.ProjectId,
            includeEntries: true);

        Assert.DoesNotContain(selected, item => item.JournalVoucherId == voucher.JournalVoucherId);
    }

    [Fact]
    public async Task GenericVoucherEditorAndBaseProcessingLoads_AreBlockedForInstrumentLinkedVouchers()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var classifier = new PaymentReceiptClassifier(context);
        var unitOfWork = new UnitOfWork(context);
        var guard = new ApprovalGuardService(context);
        var processing = new SharedProcessingService(
            unitOfWork,
            NullLogger<SharedProcessingService>.Instance,
            guard,
            classifier);
        var journalEntries = new JournalEntryService(
            unitOfWork,
            NullLogger<JournalEntryService>.Instance,
            processing,
            guard,
            classifier);

        await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            journalEntries.GetVoucherForEditAsync(voucher.JournalVoucherId, userId: 1));
        await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            journalEntries.GetBaseVoucherForProcessingAsync(voucher.JournalVoucherId));
    }

    [Fact]
    public async Task ReferenceCreation_IsBlockedWhenEitherVoucherHasInstrumentDetails()
    {
        await using var context = CreateContext();
        var paymentReceipt = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var ordinary = new JournalVoucher
        {
            ProjectId = paymentReceipt.ProjectId,
            EntryDate = paymentReceipt.EntryDate,
            CreditableDate = paymentReceipt.EntryDate,
            IsCompleted = true
        };
        context.JournalVouchers.Add(ordinary);
        await context.SaveChangesAsync();
        var classifier = new PaymentReceiptClassifier(context);
        var service = new SharedProcessingService(
            new UnitOfWork(context),
            NullLogger<SharedProcessingService>.Instance,
            new ApprovalGuardService(context),
            classifier);

        await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            service.CreateVoucherReferenceAsync(paymentReceipt.JournalVoucherId, ordinary.JournalVoucherId, "DerivedVoucher"));
        await Assert.ThrowsAsync<PaymentReceiptIntegrityException>(() =>
            service.CreateVoucherReferenceAsync(ordinary.JournalVoucherId, paymentReceipt.JournalVoucherId, "DerivedVoucher"));
    }

    [Fact]
    public async Task PostedRevision_ApprovalRetainsMappedRowsDeletesOmittedRowsAndAddsNewRows()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var ordered = voucher.JournalEntries.OrderBy(x => x.JournalEntryId).ToList();
        var retainedId = ordered[0].JournalEntryId;
        var omittedId = ordered[1].JournalEntryId;
        var service = CreateService(context);
        var request = new PaymentReceiptSubmissionRequest
        {
            ProjectId = voucher.ProjectId,
            EntryDate = voucher.EntryDate.AddDays(1),
            VoucherVersion = voucher.RowVersion!.ToArray(),
            Entries = new()
            {
                new()
                {
                    OriginalJournalEntryId = retainedId, CategoryId = 1, AccountId = 1,
                    Description = "Retained debit", InstrumentType = PaymentInstrumentType.Cash, Debit = 100
                },
                new()
                {
                    CategoryId = 1, AccountId = 2, Description = "New credit",
                    InstrumentType = PaymentInstrumentType.Cash, Credit = 100
                }
            }
        };

        await service.SubmitRevisionAsync(voucher.JournalVoucherId, request, userId: 1, isAdmin: true);
        var approval = await context.ApprovalVouchers.Include(x => x.Entries)
            .SingleAsync(x => x.Status == ApprovalStatuses.Pending);
        Assert.False((await context.JournalVouchers.FindAsync(voucher.JournalVoucherId))!.IsCompleted);

        await service.ApproveAsync(approval, approval.SnapshotVersion, userId: 1, isAdmin: true);

        context.ChangeTracker.Clear();
        var revised = await context.JournalVouchers
            .Include(x => x.JournalEntries).ThenInclude(x => x.InstrumentDetail)
            .SingleAsync(x => x.JournalVoucherId == voucher.JournalVoucherId);
        Assert.True(revised.IsCompleted);
        Assert.Equal(request.EntryDate, revised.EntryDate);
        Assert.Equal(2, revised.JournalEntries.Count);
        Assert.Contains(revised.JournalEntries, x => x.JournalEntryId == retainedId && x.Description == "Retained debit");
        Assert.DoesNotContain(revised.JournalEntries, x => x.JournalEntryId == omittedId);
        Assert.Contains(revised.JournalEntries, x => x.JournalEntryId != retainedId && x.Description == "New credit" && x.IsEdited);
        Assert.All(revised.JournalEntries, x => Assert.NotNull(x.InstrumentDetail));
    }

    [Fact]
    public async Task PostedRevision_RejectionRestoresCompletionWithoutChangingLiveRows()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var originalDate = voucher.EntryDate;
        var originalRows = voucher.JournalEntries.OrderBy(x => x.JournalEntryId)
            .Select(x => new { x.JournalEntryId, x.Description, x.Debit, x.Credit }).ToList();
        var service = CreateService(context);
        var ordered = voucher.JournalEntries.OrderBy(x => x.JournalEntryId).ToList();
        var request = new PaymentReceiptSubmissionRequest
        {
            ProjectId = voucher.ProjectId,
            EntryDate = originalDate.AddDays(5),
            VoucherVersion = voucher.RowVersion!.ToArray(),
            Entries = ordered.Select(x => new PaymentReceiptLineRequest
            {
                OriginalJournalEntryId = x.JournalEntryId,
                CategoryId = x.CategoryId,
                AccountId = x.AccountId,
                Description = $"Proposed {x.Description}",
                InstrumentType = PaymentInstrumentType.Cash,
                Debit = x.Debit,
                Credit = x.Credit
            }).ToList()
        };

        await service.SubmitRevisionAsync(voucher.JournalVoucherId, request, userId: 1, isAdmin: true);
        var approval = await context.ApprovalVouchers.Include(x => x.Entries)
            .SingleAsync(x => x.Status == ApprovalStatuses.Pending);
        await service.RejectAsync(approval, "Keep the posted voucher", approval.SnapshotVersion, userId: 1, isAdmin: true);

        context.ChangeTracker.Clear();
        var restored = await context.JournalVouchers.Include(x => x.JournalEntries)
            .SingleAsync(x => x.JournalVoucherId == voucher.JournalVoucherId);
        Assert.True(restored.IsCompleted);
        Assert.Equal(originalDate, restored.EntryDate);
        Assert.Equal(originalRows, restored.JournalEntries.OrderBy(x => x.JournalEntryId)
            .Select(x => new { x.JournalEntryId, x.Description, x.Debit, x.Credit }).ToList());
        Assert.Equal(ApprovalStatuses.Rejected,
            (await context.ApprovalVouchers.FindAsync(approval.ApprovalVoucherId))!.Status);
    }

    [Fact]
    public async Task PostedRevision_StaleVoucherVersionChangesNothing()
    {
        await using var context = CreateContext();
        var voucher = await SeedPaymentReceiptAsync(context, completed: true, approved: true);
        var request = BalancedRequest(PaymentInstrumentType.Cash, null, null);
        request.VoucherVersion = Guid.NewGuid().ToByteArray();

        await Assert.ThrowsAsync<ApprovalConflictException>(() =>
            CreateService(context).SubmitRevisionAsync(voucher.JournalVoucherId, request, userId: 1, isAdmin: true));

        Assert.True((await context.JournalVouchers.FindAsync(voucher.JournalVoucherId))!.IsCompleted);
        Assert.DoesNotContain(context.ApprovalVouchers, x => x.Status == ApprovalStatuses.Pending);
    }

    private static LedgerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase($"payment-receipt-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LedgerDbContext(options);
    }

    private static PaymentReceiptService CreateService(LedgerDbContext context) =>
        new(context, new PaymentReceiptClassifier(context));

    private static PaymentReceiptSubmissionRequest BalancedRequest(
        PaymentInstrumentType type, string? reference, DateOnly? instrumentDate) => new()
    {
        ProjectId = 1,
        EntryDate = new DateOnly(2026, 7, 1),
        Entries = new()
        {
            new()
            {
                CategoryId = 1, AccountId = 1, Description = "Debit",
                InstrumentType = type, ReferenceNumber = reference, InstrumentDate = instrumentDate,
                Debit = 100, Credit = 0
            },
            new()
            {
                CategoryId = 1, AccountId = 2, Description = "Credit",
                InstrumentType = type, ReferenceNumber = reference, InstrumentDate = instrumentDate,
                Debit = 0, Credit = 100
            }
        }
    };

    private static async Task SeedMastersAsync(LedgerDbContext context)
    {
        if (await context.Projects.AnyAsync()) return;
        context.AccountGroups.Add(new AccountGroup { AccountGroupId = 1, AccountGroupName = "Account" });
        context.AccountTypes.Add(new AccountType { AccountTypeId = 1, AccountTypeName = "Assets" });
        context.Projects.Add(new Project { ProjectId = 1, ProjectName = "Test Project" });
        context.Categories.Add(new Category
        {
            CategoryId = 1, ProjectId = 1, AccountTypeId = 1, CategoryName = "Cash and Bank"
        });
        context.Accounts.AddRange(
            new Account { AccountId = 1, CategoryId = 1, AccountGroupId = 1, AccountName = "Cash", IsEnabled = true },
            new Account { AccountId = 2, CategoryId = 1, AccountGroupId = 1, AccountName = "Bank", IsEnabled = true });
        await context.SaveChangesAsync();
    }

    private static async Task<JournalVoucher> SeedPaymentReceiptAsync(
        LedgerDbContext context, bool completed, bool approved)
    {
        await SeedMastersAsync(context);
        var date = new DateOnly(2026, 7, 1);
        var voucher = new JournalVoucher
        {
            ProjectId = 1,
            EntryDate = date,
            CreditableDate = date,
            IsCompleted = completed,
            BookerId = null,
            IsBaseVoucher = false,
            IsStockTransfer = false,
            PendingForCharges = false,
            RowVersion = new byte[] { 1, 2, 3 }
        };
        voucher.JournalEntries.Add(new JournalEntry
        {
            AccountId = 1, CategoryId = 1, Description = "Debit", Debit = 100,
            Quantity = 0, Rate = 0, Discount = 0, DiscountPercent = null,
            Credit = 0, Rbp = "N", IsProcessed = true,
            InstrumentDetail = new JournalEntryInstrumentDetail
            {
                InstrumentType = PaymentInstrumentType.Cash
            }
        });
        voucher.JournalEntries.Add(new JournalEntry
        {
            AccountId = 2, CategoryId = 1, Description = "Credit", Credit = 100,
            Quantity = 0, Rate = 0, Discount = 0, DiscountPercent = null,
            Debit = 0, Rbp = "N", IsProcessed = true,
            InstrumentDetail = new JournalEntryInstrumentDetail
            {
                InstrumentType = PaymentInstrumentType.Cash
            }
        });
        context.JournalVouchers.Add(voucher);
        await context.SaveChangesAsync();
        if (approved)
        {
            context.ApprovalVouchers.Add(new ApprovalVoucher
            {
                VoucherType = ApprovalVoucherType.PaymentReceipt,
                Status = ApprovalStatuses.Approved,
                RequestKind = ApprovalRequestKinds.New,
                JournalVoucherId = voucher.JournalVoucherId,
                ProjectId = 1,
                RequestedEntryDate = date,
                SnapshotVersion = Guid.NewGuid(),
                SubmittedDate = date,
                DecisionDate = date,
                PostedByUserId = 1,
                DecisionByUserId = 1,
                Entries = new List<ApprovalVoucherEntry>
                {
                    new() { AccountId = 1, CategoryId = 1, Debit = 100, InstrumentType = PaymentInstrumentType.Cash },
                    new() { AccountId = 2, CategoryId = 1, Credit = 100, InstrumentType = PaymentInstrumentType.Cash }
                }
            });
            await context.SaveChangesAsync();
        }
        return voucher;
    }
}
