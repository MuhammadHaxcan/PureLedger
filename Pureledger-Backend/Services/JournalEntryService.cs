using Microsoft.EntityFrameworkCore;
using PureLedger.Helpers;
using PureLedger.Exceptions;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Home;

namespace PureLedger.Services
{
    public class JournalEntryService
    {
        #region Fields and Constructor

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JournalEntryService> _logger;
        private readonly ISharedProcessingService _sharedProcessingService;
        private readonly IApprovalGuardService _approvalGuard;
        private readonly IPaymentReceiptClassifier _paymentReceiptClassifier;

        private const int ACCOUNT_GROUP_ACCOUNT = 1;
        private const int ACCOUNT_GROUP_PRODUCT = 2;
        private const int ACCOUNT_GROUP_EXPENSE = 3;

        public JournalEntryService(
            IUnitOfWork unitOfWork,
            ILogger<JournalEntryService> logger,
            ISharedProcessingService sharedProcessingService,
            IApprovalGuardService approvalGuard,
            IPaymentReceiptClassifier paymentReceiptClassifier)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _sharedProcessingService = sharedProcessingService ?? throw new ArgumentNullException(nameof(sharedProcessingService));
            _approvalGuard = approvalGuard;
            _paymentReceiptClassifier = paymentReceiptClassifier;
        }

        #endregion

        #region Simple Data Retrieval

        public Task<List<Project>> GetUserProjectsAsync(int userId)
            => _unitOfWork.Projects.GetAllForUserAsync(userId);

        public Task<List<Booker>> GetActiveBookersAsync()
            => _unitOfWork.Bookers.GetAllEnabledAsync();

        public Task<Project?> GetProjectByIdAsync(int projectId)
             => _unitOfWork.Projects.GetByIdAsync(projectId);

        public Task<List<Location>> GetAllLocationsAsync()
            => _unitOfWork.Locations.GetAllAsync();

        public Task<List<JournalVoucher>> GetAllVouchersAsync()
            => _unitOfWork.JournalVouchers.GetAllWithEntriesAsync();

        public Task<JournalVoucher?> GetVoucherWithEntriesAsync(int jvId)
            => _unitOfWork.JournalVouchers.GetWithEntriesAsync(jvId);

        public Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetModifiedEntriesPaginatedAsync(int? projectId, int page, int pageSize)
            => _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesPaginatedAsync(projectId, page, pageSize);

        public Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetBaseVouchersPaginatedAsync(
            int? projectId, DateOnly? startDate, DateOnly? endDate, bool hideFullyProcessed, int page, int pageSize)
            => _unitOfWork.JournalVouchers.GetAllBaseVouchersPaginatedAsync(projectId, startDate, endDate, hideFullyProcessed, page, pageSize);

        #endregion

        #region Voucher Details and View Models

        public async Task<object?> GetVoucherDetailsAsync(int id)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(id);
            if (voucher == null) return null;
            var isStockTransferGenerated = await _unitOfWork.IsStockTransferVoucherAsync(id);
            var hasPaymentReceiptDetails = await _paymentReceiptClassifier.HasAnyInstrumentDetailsAsync(id);
            var isValidPaymentReceipt = hasPaymentReceiptDetails && await _paymentReceiptClassifier.IsStructurallyValidAsync(id);
            var isManualExpenseVoucher = !hasPaymentReceiptDetails &&
                                         await IsManualExpenseVoucherAsync(voucher);
            var isPendingApproval = await _approvalGuard.IsVoucherPendingAsync(id);

            var projectName = voucher.Project?.ProjectName ?? "Unknown Project";
            if (voucher.Project == null && voucher.ProjectId > 0)
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(voucher.ProjectId);
                if (project != null) projectName = project.ProjectName;
            }

            var entriesList = new List<object>();
            foreach (var e in voucher.JournalEntries)
            {
                var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByJournalEntryIdAsync(e.JournalEntryId);
                entriesList.Add(new
                {
                    entryId = e.JournalEntryId,
                    categoryId = e.CategoryId,
                    categoryName = e.Category?.CategoryName ?? "N/A",
                    accountId = e.AccountId,
                    accountName = e.Account?.AccountName ?? "N/A",
                    description = e.Description ?? "",
                    quantity = e.Quantity,
                    rate = e.Rate,
                    discount = e.Discount,
                    discountPercent = e.DiscountPercent,
                    debit = e.Debit,
                    credit = e.Credit,
                    hasDeliveryChallan = dcEntry != null,
                    locationId = dcEntry?.LocationId,
                    locationName = dcEntry?.Location?.LocationName,
                    deliveryChallanEntryId = dcEntry?.DeliveryChallanEntryId
                });
            }

            return new
            {
                journalVoucherId = voucher.JournalVoucherId,
                projectId = voucher.ProjectId,
                projectName,
                entryDate = voucher.EntryDate.ToString("yyyy-MM-dd"),
                creditableDate = voucher.CreditableDate.ToString("yyyy-MM-dd"),
                totalDebit = voucher.TotalDebit,
                totalCredit = voucher.TotalCredit,
                isStockTransfer = voucher.IsStockTransfer,
                isStockTransferGenerated,
                isManualExpenseVoucher,
                isPendingApproval,
                hasPaymentReceiptDetails,
                isPaymentReceipt = isValidPaymentReceipt,
                paymentReceiptIntegrityError = hasPaymentReceiptDetails && !isValidPaymentReceipt
                    ? "The voucher has partial or invalid Payment & Receipt data."
                    : null,
                entries = entriesList
            };
        }

        public async Task<object?> GetVoucherForEditAsync(int id, int? userId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(id);
            if (voucher == null) return null;
            await EnsureVoucherIsMutableAsync(id);
            var isManualExpenseVoucher = await IsManualExpenseVoucherAsync(voucher);

            var model = await MapVoucherToViewModelAsync(voucher);
            var projects = userId.HasValue
                ? await _unitOfWork.Projects.GetAllForUserAsync(userId.Value)
                : new List<Project>();
            var bookers = await _unitOfWork.Bookers.GetAllEnabledAsync();

            return new
            {
                success = true,
                model,
                projects = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName }),
                bookers = bookers.Select(b => new { id = b.BookerID, name = b.BookerName }),
                editingVoucherId = id,
                selectedProjectId = voucher.ProjectId,
                isManualExpenseVoucher
            };
        }

        public async Task<object?> GetVoucherSummaryDataAsync(int jvId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(jvId);
            if (voucher == null) return null;

            return new
            {
                voucherId = voucher.JournalVoucherId,
                projectId = voucher.ProjectId,
                projectName = voucher.Project?.ProjectName ?? "Unknown Project",
                entryDate = voucher.EntryDate,
                creditableDate = voucher.CreditableDate,
                totalDebit = voucher.TotalDebit,
                totalCredit = voucher.TotalCredit,
                entries = voucher.JournalEntries.Select(e => new
                {
                    e.JournalEntryId,
                    e.Description,
                    e.Quantity,
                    e.Rate,
                    e.Debit,
                    e.Credit
                })
            };
        }

        public async Task<object?> GetBaseVoucherForProcessingAsync(int id)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                id, "loaded for base-voucher processing");
            var voucher = await _unitOfWork.JournalVouchers.GetBaseVoucherWithUnprocessedEntriesAsync(id);
            if (voucher == null) return null;

            return new
            {
                baseVoucherId = id,
                projectId = voucher.ProjectId,
                projectName = voucher.Project?.ProjectName ?? "Unknown Project",
                entryDate = voucher.EntryDate.ToString("yyyy-MM-dd"),
                creditableDate = voucher.CreditableDate.ToString("yyyy-MM-dd"),
                totalDebit = voucher.TotalDebit,
                totalCredit = voucher.TotalCredit,
                entries = voucher.JournalEntries
                    .OrderBy(e => e.IsProcessed)
                    .ThenBy(e => e.JournalEntryId)
                    .Select(e => new
                    {
                        entryId = e.JournalEntryId,
                        description = e.Description,
                        accountName = e.Account?.AccountName ?? "N/A",
                        categoryName = e.Category?.CategoryName ?? "N/A",
                        accountId = e.AccountId,
                        categoryId = e.CategoryId,
                        availableQuantity = e.IsProcessed ? 0 : e.Quantity,
                        processQuantity = 0,
                        rate = e.Rate,
                        debit = e.Debit,
                        credit = e.Credit,
                        percent = e.DiscountPercent ?? 0,
                        disc = e.Discount,
                        rbp = e.Rbp,
                        pkg = e.Account?.ProductData?.Pkg ?? 0,
                        accountGroupId = e.Account?.AccountGroupId ?? 0,
                        isProcessed = e.IsProcessed
                    }).ToList()
            };
        }

        private async Task<JournalEntryViewModel> MapVoucherToViewModelAsync(JournalVoucher voucher)
        {
            _logger.LogInformation("Mapping voucher ID {VoucherId} to view model", voucher.JournalVoucherId);

            int creditDays = voucher.CreditableDate > voucher.EntryDate
                ? voucher.CreditableDate.DayNumber - voucher.EntryDate.DayNumber
                : 0;

            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, null, null);

            return new JournalEntryViewModel
            {
                ProjectId = voucher.ProjectId,
                VoucherVersion = voucher.RowVersion,
                EntryDate = voucher.EntryDate,
                DueDate = voucher.CreditableDate,
                CreditDays = creditDays,
                IsBaseVoucher = voucher.IsBaseVoucher,
                BookerId = voucher.BookerId,
                Entries = entries.Select(MapToRowModel).ToList()
            };
        }

        public async Task<object> GetVoucherSummaryAsync(int voucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
            if (voucher == null)
                return new { entries = new List<object>(), jvId = voucherId };

            var entries = voucher.JournalEntries.Select(e => new
            {
                entryId = e.JournalEntryId,
                categoryName = e.Category?.CategoryName ?? "N/A",
                accountName = e.Account?.AccountName ?? "N/A",
                description = e.Description,
                quantity = e.Quantity,
                rate = e.Rate,
                discount = e.Discount,
                discountPercent = e.DiscountPercent,
                debit = e.Debit,
                credit = e.Credit
            }).ToList();

            return new { entries, jvId = voucher.JournalVoucherId, projectName = voucher.Project?.ProjectName ?? "Unknown Project" };
        }

        #endregion

        #region Voucher CRUD Operations

        public async Task<JournalVoucher> CreateVoucherAsync(JournalEntryViewModel model)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var journalVoucher = new JournalVoucher
                {
                    ProjectId = model.ProjectId,
                    EntryDate = model.EntryDate,
                    IsCompleted = false,
                    IsBaseVoucher = model.IsBaseVoucher,
                    PendingForCharges = model.IsBaseVoucher,
                    CreditableDate = model.EntryDate.AddDays(model.CreditDays),
                    BookerId = await ResolveBookerAsync(
                        model.Entries.Select(e => e.AccountId ?? 0), model.BookerId ?? 0)
                };

                journalVoucher = _unitOfWork.JournalVouchers.Add(journalVoucher);
                // Materialize the generated voucher ID before adding journal entries.
                await _unitOfWork.SaveChangesAsync();
                await CreateEntriesAsync(journalVoucher.JournalVoucherId, model.Entries);

                // Persist entries before completion recalculation queries the voucher graph.
                await _unitOfWork.SaveChangesAsync();
                await RecalculateCompletionAsync(journalVoucher.JournalVoucherId);

                if (model.SaveAsRecipe && !string.IsNullOrWhiteSpace(model.RecipeName))
                    await SaveRecipeAsync(model, journalVoucher.JournalVoucherId);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return journalVoucher;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating journal voucher");
                throw;
            }
        }

        public async Task<JournalVoucher> UpdateVoucherAsync(int voucherId, JournalEntryViewModel model)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _approvalGuard.EnsureVoucherNotPendingAsync(voucherId, lockVoucher: true);
                var existingVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (existingVoucher == null)
                    throw new InvalidOperationException($"Voucher with ID {voucherId} not found");
                await EnsureVoucherIsMutableAsync(voucherId);
                if (await IsManualExpenseVoucherAsync(existingVoucher))
                    throw new InvalidOperationException(
                        "Manual Expense vouchers must be edited through Pending Approvals.");

                bool wasBaseVoucher = existingVoucher.IsBaseVoucher;
                bool wasCompleted = existingVoucher.IsCompleted;

                existingVoucher.ProjectId = model.ProjectId;
                existingVoucher.EntryDate = model.EntryDate;
                existingVoucher.CreditableDate = model.DueDate;
                existingVoucher.BookerId = model.BookerId;

                var hasReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(voucherId);
                existingVoucher.IsBaseVoucher = hasReferences.Any() ? wasBaseVoucher : model.IsBaseVoucher;

                if (existingVoucher.IsBaseVoucher != wasBaseVoucher)
                    existingVoucher.PendingForCharges = existingVoucher.IsBaseVoucher;

                var existingEntries = existingVoucher.JournalEntries.ToList();
                bool newEntryAdded = false;
                bool anyEntryChanged = false;

                foreach (var entryModel in model.Entries)
                {
                    // Match by EntryId for existing entries (like single entry edit)
                    // This allows AccountId/CategoryId changes to be properly tracked
                    var existingEntry = entryModel.EntryId.HasValue
                        ? existingEntries.FirstOrDefault(e => e.JournalEntryId == entryModel.EntryId.Value)
                        : null;

                    if (existingEntry != null)
                    {
                        // Check if entry has actually changed before processing
                        if (!HasEntryChanged(existingEntry, entryModel))
                        {
                            // Entry unchanged - just remove from list, no processing needed
                            existingEntries.Remove(existingEntry);
                            continue;
                        }

                        anyEntryChanged = true;

                        // Capture old values before update (like single entry edit does)
                        var oldAccount = await _unitOfWork.Accounts.GetByIdAsync(existingEntry.AccountId);
                        var oldAccountGroupId = oldAccount?.AccountGroupId ?? 0;
                        var oldQuantity = existingEntry.Quantity;

                        // Update entry data (now allows AccountId/CategoryId changes)
                        UpdateExistingEntry(existingEntry, entryModel);

                        // Capture NEW account group after update (like single entry edit)
                        var newAccount = await _unitOfWork.Accounts.GetByIdAsync(existingEntry.AccountId);
                        var newAccountGroupId = newAccount?.AccountGroupId ?? 0;
                        // Keep the navigation aligned before EF Update traverses the tracked graph.
                        existingEntry.Account = newAccount;

                        // Process entry update logic (IsProcessed, DC cleanup, voucher completion)
                        await HandleEntryUpdateAsync(
                            existingEntry, voucherId, existingVoucher, wasCompleted,
                            oldAccountGroupId, newAccountGroupId, oldQuantity,
                            deferCompletionRecalculation: true);

                        existingEntries.Remove(existingEntry);
                    }
                    else
                    {
                        await CreateNewEntryAsync(voucherId, entryModel);
                        newEntryAdded = true;
                    }
                }

                int deletedCount = existingEntries.Count;
                foreach (var entryToDelete in existingEntries)
                    await DeleteEntryInternalAsync(entryToDelete.JournalEntryId);

                // Only recalculate completion if something actually changed
                bool hasChanges = anyEntryChanged || newEntryAdded || deletedCount > 0;
                if (hasChanges)
                {
                    // Only set to false for new entries - let RecalculateCompletionAsync determine final state
                    // Don't overwrite with wasCompleted as HandleEntryUpdateAsync may have already set the correct state
                    if (newEntryAdded)
                        existingVoucher.IsCompleted = false;

                    // Persist added/deleted rows before completion recalculation queries the voucher graph.
                    await _unitOfWork.SaveChangesAsync();
                    await RecalculateCompletionAsync(existingVoucher.JournalVoucherId);
                }

                // Resolve from the final rows, including retained IDs when an edit omits AccountId.
                await ApplyLinkedBookerAsync(existingVoucher);
                _unitOfWork.JournalVouchers.Update(existingVoucher);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Updated voucher {VoucherId} with {EntryCount} entries (changed: {AnyChanged}, added: {NewAdded}, deleted: {Deleted})",
                    voucherId, model.Entries.Count, anyEntryChanged, newEntryAdded, deletedCount);
                return existingVoucher;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Journal Entry CRUD Operations

        public async Task<JournalEntryUpdateViewModel?> GetEntryForEditAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null) return null;
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                entry.JournalVoucherId, "loaded for individual entry editing");

            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(entry.JournalVoucherId);
            if (voucher == null) return null;
            await _approvalGuard.EnsureVoucherNotPendingAsync(entry.JournalVoucherId);
            if (await _unitOfWork.IsStockTransferVoucherAsync(entry.JournalVoucherId))
                throw new InvalidOperationException(
                    "Stock-transfer journal vouchers are read-only here. Use the Stock Transfers screen.");
            if (await IsManualExpenseVoucherAsync(voucher))
                throw new InvalidOperationException(
                    "Manual Expense vouchers must be edited as a complete voucher through Pending Approvals.");

            var account = entry.AccountId > 0
                ? await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId)
                : null;
            int accountGroupId = account?.AccountGroupId ?? 0;

            int? deliveryChallanEntryId = null;
            int? locationId = null;
            List<Location>? allLocations = null;

            var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByJournalEntryIdAsync(entry.JournalEntryId);
            if (dcEntry != null)
            {
                deliveryChallanEntryId = dcEntry.DeliveryChallanEntryId;
                locationId = dcEntry.LocationId;
                allLocations = await _unitOfWork.Locations.GetAllAsync();
            }
            else if (accountGroupId == ACCOUNT_GROUP_PRODUCT)
            {
                allLocations = await _unitOfWork.Locations.GetAllAsync();
            }

            var model = new JournalEntryUpdateViewModel
            {
                EntryId = entry.JournalEntryId,
                AccountName = entry.Account?.AccountName,
                Description = entry.Description,
                Quantity = entry.Quantity,
                Rate = Math.Round(entry.Rate, 4),
                Discount = Math.Round(entry.Discount, 2),
                DiscountPercent = Math.Round(entry.DiscountPercent ?? 0, 2),
                Debit = Math.Round(entry.Debit, 2),
                Credit = Math.Round(entry.Credit, 2),
                AccountId = entry.AccountId,
                CategoryId = entry.CategoryId,
                ProjectId = entry.JournalVoucher?.ProjectId ?? 0,
                AccountGroupId = accountGroupId,
                DeliveryChallanEntryId = deliveryChallanEntryId,
                LocationId = locationId,
                Locations = allLocations
            };

            return model;
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateEntryAsync(JournalEntryUpdateViewModel model)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(model.EntryId);
                if (entry == null) return (false, "Entry not found.");

                var voucherId = entry.JournalVoucherId;
                await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(voucherId, "edited");
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (voucher == null) return (false, "Voucher not found.");
                await _approvalGuard.EnsureVoucherNotPendingAsync(voucherId, lockVoucher: true);
                if (await _unitOfWork.IsStockTransferVoucherAsync(voucherId))
                    return (false, "Stock-transfer journal vouchers are read-only here. Use the Stock Transfers screen.");
                if (await IsManualExpenseVoucherAsync(voucher))
                    return (false, "Manual Expense vouchers must be edited through Pending Approvals.");

                if (model.AccountId > 0 && model.AccountId != entry.AccountId &&
                    !voucher.IsStockTransfer && !voucher.IsBaseVoucher)
                {
                    var targetAccount = await _unitOfWork.Accounts.GetByIdAsync(model.AccountId);
                    var incoming = await _unitOfWork.JournalVoucherReferences.GetByReferencedVoucherIdAsync(voucherId);
                    // Vouchers generated by another workflow (FOR/cartage) have an incoming reference and stay editable.
                    if (targetAccount?.AccountGroupId == ACCOUNT_GROUP_EXPENSE && !incoming.Any())
                        return (false, "An Expense account cannot be introduced through a single-row edit. " +
                                       "Use Edit Entire Voucher so the change goes through Expense approval.");
                }

                var wasCompleted = voucher.IsCompleted;
                var oldAccount = await _unitOfWork.Accounts.GetByIdAsync(entry.AccountId);
                var oldAccountGroupId = oldAccount?.AccountGroupId ?? 0;
                var oldQuantity = entry.Quantity;

                UpdateEntryFromViewModel(entry, model);

                var newAccount = await _unitOfWork.Accounts.GetByIdAsync(entry.AccountId);
                var newAccountGroupId = newAccount?.AccountGroupId ?? 0;

                // Apply the location before the entry update: that step may unlink (delete) this DC entry when the
                // quantity changes. Calling Update() on a DC entry already staged for deletion would turn the delete
                // back into a modification, leaving the journal row unprocessed but still attached to the old DC row.
                if (model.DeliveryChallanEntryId.HasValue && model.LocationId.HasValue)
                    await UpdateDCEntryLocationAsync(model.DeliveryChallanEntryId.Value, model.LocationId.Value);

                await HandleEntryUpdateAsync(
                    entry, voucherId, voucher, wasCompleted,
                    oldAccountGroupId, newAccountGroupId, oldQuantity,
                    deferCompletionRecalculation: true);

                // Persist entry/DC mutations before completion recalculation queries the voucher graph.
                await _unitOfWork.SaveChangesAsync();
                await RecalculateCompletionAsync(voucherId);
                await ApplyLinkedBookerAsync(voucher);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating journal entry {EntryId}", model.EntryId);
                return (false, ExceptionClassifier.SafeMessage(ex, "Error updating entry."));
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteEntryAsync(int entryId)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Get voucher ID before deleting entry so we can mark remaining entries as edited
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                if (entry == null) return (false, "Entry not found.");
                var voucherId = entry.JournalVoucherId;
                await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                    voucherId, "deleted one row at a time");

                // Check if this is the last entry and voucher has references
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (voucher == null) return (false, "Voucher not found.");
                await _approvalGuard.EnsureVoucherNotPendingAsync(voucherId, lockVoucher: true);
                if (await _unitOfWork.IsStockTransferVoucherAsync(voucherId))
                    return (false, "Stock-transfer journal vouchers are read-only here. Use the Stock Transfers screen.");
                if (await IsManualExpenseVoucherAsync(voucher))
                    return (false, "Manual Expense vouchers must be edited through Pending Approvals.");

                var entriesCount = voucher.JournalEntries?.Count ?? 0;
                if (entriesCount == 1)
                {
                    // This is the last entry - check for references
                    var references = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(voucherId);
                    if (references.Any())
                    {
                        var referenceDetails = new List<string>();
                        foreach (var reference in references)
                        {
                            var refType = reference.ReferenceType switch
                            {
                                "DerivedVoucher" => "Derived Voucher",
                                "ForCharges" => "Charges Voucher",
                                "Cartage" => "Cartage Voucher",
                                _ => reference.ReferenceType
                            };
                            referenceDetails.Add($"JV-{reference.ReferencedVoucherId} ({refType})");
                        }

                        var message = $"Cannot delete last entry. This voucher (JV-{voucherId}) has linked vouchers that will be affected: {string.Join(", ", referenceDetails)}. " +
                                      "Please delete the referenced vouchers first or use 'Edit Entire Voucher' to manage entries.";
                        return (false, message);
                    }
                }

                var result = await DeleteEntryInternalAsync(entryId);
                if (result)
                {
                    // Repositories only stage changes, so persist the deletion before looking at the surviving
                    // rows. Otherwise the query below still returns the row being deleted, and calling Update()
                    // on it would turn the staged delete back into a modification (reported as success, row kept).
                    await _unitOfWork.SaveChangesAsync();

                    // Mark a surviving entry as edited to indicate the voucher was modified. The query has no
                    // inherent order, so pick deterministically and never the entry that was just deleted.
                    var remainingEntries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);
                    var firstEntry = remainingEntries
                        .Where(e => e.JournalEntryId != entryId)
                        .OrderBy(e => e.JournalEntryId)
                        .FirstOrDefault();
                    if (firstEntry is { IsEdited: false })
                    {
                        firstEntry.IsEdited = true;
                        firstEntry.ModifiedDateTime = BusinessClock.Today;
                        _unitOfWork.JournalEntries.Update(firstEntry);
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await RecalculateCompletionAsync(voucherId);
                    await ApplyLinkedBookerAsync(voucher);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                    return (false, "Failed to delete entry. The entry may have linked data.");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting journal entry {EntryId}", entryId);
                return (false, ExceptionClassifier.SafeMessage(ex, "The entry could not be deleted. Please try again."));
            }
        }

        #endregion

        #region Category and Account Operations

        public async Task<IEnumerable<Category>> GetCategoriesForUserAsync(int projectId, int userId)
            => await _unitOfWork.Categories.GetByProjectIdForUserAsync(projectId, userId);

        public async Task<IEnumerable<Account>> GetActiveAccountsByCategoryAsync(int categoryId)
            => await _unitOfWork.Accounts.GetEnabledByCategoryIdAsync(categoryId);

        public Task<object?> GetProductDetailsAsync(int accountId)
            => _unitOfWork.Accounts.GetProductDetailsAsync(accountId);

        #endregion

        #region Derived Voucher Processing

        public async Task<int> CreateDerivedVoucherAsync(
            int baseVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess,
            decimal accountEntryAmount = 0,
            bool accountEntryIsDebit = false,
            DateOnly? derivedEntryDate = null,
            DateOnly? derivedCreditableDate = null)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                baseVoucherId, "used to create a derived voucher");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (await ShouldProcessAsFullVoucherAsync(baseVoucherId, entriesToProcess))
                {
                    var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                    if (baseVoucher != null)
                    {
                        baseVoucher.BookerId = await ResolveBookerAsync(
                            baseVoucher.JournalEntries.Select(e => e.AccountId), baseVoucher.BookerId);
                        baseVoucher.IsBaseVoucher = false;
                        baseVoucher.PendingForCharges = true;
                        baseVoucher.EntryDate = derivedEntryDate ?? BusinessClock.Today;
                        baseVoucher.CreditableDate = derivedCreditableDate ?? baseVoucher.EntryDate.AddDays(0);

                        _unitOfWork.JournalVouchers.Update(baseVoucher);
                        await _unitOfWork.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Base voucher {VoucherId} converted to regular voucher", baseVoucherId);
                        return baseVoucher.JournalVoucherId;
                    }
                }

                var baseVoucherData = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                if (baseVoucherData == null || !baseVoucherData.IsBaseVoucher)
                    throw new InvalidOperationException("Base voucher not found");

                // Include the processed Account row; product quantities may already be partially used.
                var linkedBookerId = await ResolveBookerAsync(
                    baseVoucherData.JournalEntries.Select(e => e.AccountId), null);

                var derivedVoucher = new JournalVoucher
                {
                    ProjectId = baseVoucherData.ProjectId,
                    EntryDate = derivedEntryDate ?? BusinessClock.Today,
                    CreditableDate = derivedCreditableDate ?? (derivedEntryDate ?? BusinessClock.Today),
                    IsCompleted = false,
                    PendingForCharges = true,
                    BookerId = baseVoucherData.BookerId ?? 0
                };

                derivedVoucher = _unitOfWork.JournalVouchers.Add(derivedVoucher);
                // Materialize the generated derived voucher ID before adding references and entries.
                await _unitOfWork.SaveChangesAsync();
                await _sharedProcessingService.CreateVoucherReferenceAsync(baseVoucherId, derivedVoucher.JournalVoucherId, "DerivedVoucher");
                await ProcessDerivedVoucherEntriesAsync(baseVoucherData, derivedVoucher.JournalVoucherId, entriesToProcess, accountEntryAmount, accountEntryIsDebit);

                // Persist derived entries before completion recalculation queries the derived voucher graph.
                await _unitOfWork.SaveChangesAsync();
                // Both the source and the actual derived rows must meet the composition rule.
                if (linkedBookerId.HasValue)
                    await ApplyLinkedBookerAsync(derivedVoucher);
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(baseVoucherId);
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(derivedVoucher.JournalVoucherId);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return derivedVoucher.JournalVoucherId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Missed Entries

        public async Task MarkEntriesAsMissedAsync(int voucherId)
        {
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(
                voucherId, "processed as missed entries");
            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (voucher == null || !voucher.IsBaseVoucher)
                    throw new InvalidOperationException("Invalid voucher");

                var customerAccount = await GetCustomerAccountFromEntriesAsync(voucher.JournalEntries);
                if (customerAccount == null)
                    throw new InvalidOperationException("No customer account found in voucher");

                var unprocessedEntries = voucher.JournalEntries
                    .Where(e => !e.IsProcessed && e.Quantity > 0 && e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT)
                    .ToList();

                foreach (var entry in unprocessedEntries)
                {
                    var missedEntry = new MissedEntry
                    {
                        CustomerAccountId = customerAccount.AccountId,
                        ProductAccountId = entry.AccountId,
                        ProjectId = voucher.ProjectId,
                        MissedDate = BusinessClock.Today,
                        Quantity = entry.Quantity,
                        Rate = entry.Rate,
                        Discount = entry.DiscountPercent ?? 0,
                        Rbp = entry.Rbp ?? "Y",
                        Description = entry.Description
                    };

                    _unitOfWork.MissedEntries.Add(missedEntry);
                    entry.Quantity = 0;
                    _unitOfWork.JournalEntries.Update(entry);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Voucher {VoucherId} marked as missed with {Count} entries", voucherId, unprocessedEntries.Count);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Recipe Operations

        public async Task<List<Recipe>> GetRecipesByProjectAsync(int projectId)
        {
            _logger.LogInformation("Retrieving recipes for project {ProjectId}", projectId);
            return await _unitOfWork.Recipes.GetAllByProjectIdAsync(projectId);
        }

        public async Task<Recipe?> GetRecipeWithItemsAsync(int recipeId)
        {
            _logger.LogInformation("Retrieving recipe with items for recipe ID {RecipeId}", recipeId);
            return await _unitOfWork.Recipes.GetByIdWithItemsAsync(recipeId);
        }

        public async Task<JournalEntryViewModel> CreateViewModelFromRecipeAsync(int recipeId, int projectId)
        {
            var recipe = await _unitOfWork.Recipes.GetByIdWithItemsAsync(recipeId);
            if (recipe == null)
                throw new ArgumentException($"Recipe with ID {recipeId} not found");

            if (recipe.ProjectId != projectId)
                throw new ArgumentException("Recipe does not belong to the selected project");

            var viewModel = new JournalEntryViewModel
            {
                ProjectId = projectId,
                EntryDate = BusinessClock.Today,
                DueDate = BusinessClock.Today,
                CreditDays = 0,
                IsBaseVoucher = false,
                BookerId = 0,
                Entries = new List<JournalEntryRowModel>()
            };

            foreach (var item in recipe.RecipeItems)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(item.AccountId);
                viewModel.Entries.Add(new JournalEntryRowModel
                {
                    CategoryId = item.CategoryId,
                    AccountId = item.AccountId,
                    Pkg = account?.ProductData?.Pkg,
                    Des = "",
                    Qty = 0,
                    Rate = 0,
                    Percent = 0,
                    Disc = 0,
                    Debit = 0,
                    Credit = 0,
                    Rbp = "Y"
                });
            }

            return viewModel;
        }

        #endregion

        #region Private Helper Methods - Mapping

        private static JournalEntryRowModel MapToRowModel(JournalEntry e) => new()
        {
            EntryId = e.JournalEntryId,
            CategoryId = e.CategoryId,
            AccountId = e.AccountId,
            Pkg = e.Account?.ProductData?.Pkg,
            Des = e.Description,
            Qty = e.Quantity,
            Rate = e.Rate,
            Percent = e.DiscountPercent ?? 0,
            Disc = e.Discount,
            Debit = e.Debit,
            Credit = e.Credit,
            Rbp = e.Rbp ?? "Y"
        };

        private async Task<int?> ResolveBookerAsync(IEnumerable<int> rowAccountIds, int? fallbackBookerId)
        {
            var ids = rowAccountIds.ToList();
            var accounts = new Dictionary<int, Account?>();
            foreach (var id in ids.Distinct())
                accounts[id] = await _unitOfWork.Accounts.GetByIdAsync(id);

            return JournalVoucherBooker.Resolve(ids.Select(id => accounts[id]), fallbackBookerId);
        }

        private async Task ApplyLinkedBookerAsync(JournalVoucher voucher)
        {
            var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(
                voucher.JournalVoucherId, null, null);
            voucher.BookerId = await ResolveBookerAsync(
                entries.Select(e => e.AccountId), voucher.BookerId);
        }

        private async Task CreateEntriesAsync(int journalVoucherId, List<JournalEntryRowModel> entryModels)
        {
            foreach (var entryModel in entryModels)
            {
                var journalEntry = CreateEntryFromRowModel(journalVoucherId, entryModel);
                journalEntry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(journalEntry);
                _unitOfWork.JournalEntries.Add(journalEntry);
            }
        }

        private static JournalEntry CreateEntryFromRowModel(int journalVoucherId, JournalEntryRowModel model)
        {
            return new JournalEntry
            {
                JournalVoucherId = journalVoucherId,
                AccountId = model.AccountId ?? 0,
                CategoryId = model.CategoryId ?? 0,
                Description = model.Des,
                Quantity = model.Qty ?? 0,
                Rate = model.Rate ?? 0,
                DiscountPercent = model.Percent,
                Discount = model.Disc ?? 0,
                Debit = model.Debit ?? 0,
                Credit = model.Credit ?? 0,
                Rbp = model.Rbp ?? "Y",
                IsProcessed = false
            };
        }

        private async Task CreateNewEntryAsync(int voucherId, JournalEntryRowModel entryModel)
        {
            var newEntry = CreateEntryFromRowModel(voucherId, entryModel);
            newEntry.IsEdited = true;
            newEntry.ModifiedDateTime = BusinessClock.Today;
            newEntry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(newEntry);
            _unitOfWork.JournalEntries.Add(newEntry);
        }

        private static bool HasEntryChanged(JournalEntry entry, JournalEntryRowModel model)
        {
            // Check if any field has actually changed
            var newAccountId = model.AccountId.HasValue && model.AccountId.Value > 0 ? model.AccountId.Value : entry.AccountId;
            var newCategoryId = model.CategoryId.HasValue && model.CategoryId.Value > 0 ? model.CategoryId.Value : entry.CategoryId;
            var newQuantity = model.Qty ?? 0;
            var newRate = model.Rate ?? 0;
            var newDiscount = model.Disc ?? 0;
            var newDebit = model.Debit ?? 0;
            var newCredit = model.Credit ?? 0;
            var newRbp = model.Rbp ?? "Y";

            return entry.AccountId != newAccountId ||
                   entry.CategoryId != newCategoryId ||
                   entry.Description != model.Des ||
                   entry.Quantity != newQuantity ||
                   entry.Rate != newRate ||
                   entry.DiscountPercent != model.Percent ||
                   entry.Discount != newDiscount ||
                   entry.Debit != newDebit ||
                   entry.Credit != newCredit ||
                   entry.Rbp != newRbp;
        }

        private static void UpdateExistingEntry(JournalEntry entry, JournalEntryRowModel model)
        {
            // Allow AccountId change (like single entry edit)
            if (model.AccountId.HasValue && model.AccountId.Value > 0)
                entry.AccountId = model.AccountId.Value;

            // Allow CategoryId change (like single entry edit)
            if (model.CategoryId.HasValue && model.CategoryId.Value > 0)
                entry.CategoryId = model.CategoryId.Value;

            entry.Description = model.Des;
            entry.Quantity = model.Qty ?? 0;
            entry.Rate = model.Rate ?? 0;
            entry.DiscountPercent = model.Percent;
            entry.Discount = model.Disc ?? 0;
            entry.Debit = model.Debit ?? 0;
            entry.Credit = model.Credit ?? 0;
            entry.Rbp = model.Rbp ?? "Y";
            entry.IsEdited = true;
            entry.ModifiedDateTime = BusinessClock.Today;
        }

        private static void UpdateEntryFromViewModel(JournalEntry entry, JournalEntryUpdateViewModel model)
        {
            entry.Description = model.Description;
            entry.Quantity = model.Quantity;
            entry.Rate = model.Rate;
            entry.Discount = model.Discount;
            entry.DiscountPercent = model.DiscountPercent;
            entry.Debit = model.Debit;
            entry.Credit = model.Credit;

            if (model.AccountId > 0 && model.AccountId != entry.AccountId)
                entry.AccountId = model.AccountId;

            if (model.CategoryId > 0 && model.CategoryId != entry.CategoryId)
                entry.CategoryId = model.CategoryId;

            entry.IsEdited = true;
            entry.ModifiedDateTime = BusinessClock.Today;
        }

        #endregion

        #region Private Helper Methods - Entry Processing

        private async Task HandleEntryUpdateAsync(
            JournalEntry entry, int voucherId, JournalVoucher voucher,
            bool wasCompleted, int oldAccountGroupId, int newAccountGroupId, int oldQuantity,
            bool deferCompletionRecalculation = false)
        {
            if (oldAccountGroupId == ACCOUNT_GROUP_PRODUCT && newAccountGroupId != ACCOUNT_GROUP_PRODUCT)
            {
                await _sharedProcessingService.DeleteLinkedDCEntryAsync(entry.JournalEntryId);
                entry.IsProcessed = true;
                _unitOfWork.JournalEntries.Update(entry);
                if (!deferCompletionRecalculation)
                    await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
                return;
            }

            if (oldAccountGroupId != ACCOUNT_GROUP_PRODUCT && newAccountGroupId == ACCOUNT_GROUP_PRODUCT)
            {
                entry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(entry);
                _unitOfWork.JournalEntries.Update(entry);

                if (wasCompleted && !entry.IsProcessed)
                {
                    if (!deferCompletionRecalculation)
                    {
                        voucher.IsCompleted = false;
                        _unitOfWork.JournalVouchers.Update(voucher);
                        await _sharedProcessingService.ReopenReferenceVouchersAsync(voucherId);
                    }
                }
                else if (!deferCompletionRecalculation)
                {
                    await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
                }
                return;
            }

            if (oldAccountGroupId == ACCOUNT_GROUP_PRODUCT && newAccountGroupId == ACCOUNT_GROUP_PRODUCT)
            {
                await HandleProductQuantityChangeAsync(
                    entry, voucherId, voucher, wasCompleted, oldQuantity,
                    deferCompletionRecalculation);
                return;
            }

            entry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(entry);
            _unitOfWork.JournalEntries.Update(entry);
            if (!deferCompletionRecalculation)
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
        }

        private async Task HandleProductQuantityChangeAsync(
            JournalEntry entry, int voucherId, JournalVoucher voucher, bool wasCompleted, int oldQuantity,
            bool deferCompletionRecalculation = false)
        {
            if (oldQuantity > 0 && entry.Quantity <= 0)
            {
                await _sharedProcessingService.DeleteLinkedDCEntryAsync(entry.JournalEntryId);
                entry.IsProcessed = true;
                _unitOfWork.JournalEntries.Update(entry);
                if (!deferCompletionRecalculation)
                    await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
                return;
            }

            if (oldQuantity <= 0 && entry.Quantity > 0)
            {
                // Delete linked DC entry when entry becomes unprocessed to maintain 1:1 relationship
                await _sharedProcessingService.DeleteLinkedDCEntryAsync(entry.JournalEntryId);

                entry.IsProcessed = false;
                _unitOfWork.JournalEntries.Update(entry);

                if (wasCompleted)
                {
                    if (!deferCompletionRecalculation)
                    {
                        voucher.IsCompleted = false;
                        _unitOfWork.JournalVouchers.Update(voucher);
                        await _sharedProcessingService.ReopenReferenceVouchersAsync(voucherId);
                    }
                }
                else if (!deferCompletionRecalculation)
                {
                    await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
                }
                return;
            }

            // Qty unchanged — preserve current IsProcessed status and DC link
            if (oldQuantity == entry.Quantity)
            {
                _unitOfWork.JournalEntries.Update(entry);
                if (!deferCompletionRecalculation)
                    await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
                return;
            }

            var wasProcessed = entry.IsProcessed;
            entry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(entry);

            // If entry transitions from processed to unprocessed, delete linked DC entry to maintain 1:1
            if (wasProcessed && !entry.IsProcessed)
            {
                await _sharedProcessingService.DeleteLinkedDCEntryAsync(entry.JournalEntryId);
            }

            _unitOfWork.JournalEntries.Update(entry);
            if (!deferCompletionRecalculation)
                await _sharedProcessingService.RecalculateVoucherCompletionAsync(voucherId);
        }

        private async Task<bool> DeleteEntryInternalAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null) return false;

            var voucherId = entry.JournalVoucherId;

            await _sharedProcessingService.DeleteLinkedDCEntryAsync(entryId);
            await _unitOfWork.JournalEntries.DeleteAsync(entryId);

            return true;
        }

        private async Task UpdateDCEntryLocationAsync(int deliveryChallanEntryId, int locationId)
        {
            var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByIdAsync(deliveryChallanEntryId);
            if (dcEntry != null)
            {
                dcEntry.LocationId = locationId;
                _unitOfWork.DeliveryChallanEntries.Update(dcEntry);
            }
        }

        private async Task RecalculateCompletionAsync(int journalVoucherId)
            => await _sharedProcessingService.RecalculateVoucherCompletionAsync(journalVoucherId);

        private async Task EnsureVoucherIsMutableAsync(int journalVoucherId)
        {
            await _approvalGuard.EnsureVoucherNotPendingAsync(journalVoucherId);
            await _paymentReceiptClassifier.EnsureNotPaymentReceiptAsync(journalVoucherId, "edited");
            if (await _unitOfWork.IsStockTransferVoucherAsync(journalVoucherId))
                throw new InvalidOperationException("Stock-transfer journal vouchers are read-only here. Use the Stock Transfers screen.");
        }

        private async Task<bool> IsManualExpenseVoucherAsync(JournalVoucher voucher)
        {
            if (!voucher.JournalEntries.Any(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_EXPENSE))
                return false;
            var incoming = await _unitOfWork.JournalVoucherReferences.GetByReferencedVoucherIdAsync(voucher.JournalVoucherId);
            return !voucher.IsStockTransfer && !voucher.IsBaseVoucher && !incoming.Any();
        }

        #endregion

        #region Private Helper Methods - Derived Voucher

        private async Task<bool> ShouldProcessAsFullVoucherAsync(int baseVoucherId, List<BaseVoucherEntryViewModel> entriesToProcess)
        {
            var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
            if (baseVoucher == null || !baseVoucher.IsBaseVoucher) return false;

            var existingReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(baseVoucherId);
            if (existingReferences.Any()) return false;

            var regularEntries = baseVoucher.JournalEntries.Where(e => !e.IsProcessed).ToList();
            if (!regularEntries.Any()) return false;

            foreach (var entry in regularEntries)
            {
                var processEntry = entriesToProcess.FirstOrDefault(e => e.EntryId == entry.JournalEntryId);
                if (processEntry == null || processEntry.ProcessQuantity != entry.Quantity) return false;
            }

            return true;
        }

        private async Task ProcessDerivedVoucherEntriesAsync(
            JournalVoucher baseVoucher, int derivedVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess, decimal accountEntryAmount, bool accountEntryIsDebit)
        {
            bool accountEntryAdded = false;

            foreach (var entryToProcess in entriesToProcess)
            {
                var baseEntry = baseVoucher.JournalEntries.FirstOrDefault(e => e.JournalEntryId == entryToProcess.EntryId);
                if (baseEntry == null) continue;

                var account = await _unitOfWork.Accounts.GetByIdAsync(baseEntry.AccountId);
                bool isAccountEntry = account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT;

                if (!isAccountEntry && (entryToProcess.ProcessQuantity <= 0 || entryToProcess.ProcessQuantity > baseEntry.Quantity))
                    continue;

                if (isAccountEntry)
                {
                    if (!accountEntryAdded)
                    {
                        accountEntryAdded = true;
                        await CreateAccountEntryAsync(derivedVoucherId, baseEntry, accountEntryAmount, accountEntryIsDebit);
                    }
                }
                else
                {
                    await CreateRegularEntryAsync(derivedVoucherId, baseEntry, entryToProcess);
                    baseEntry.Quantity -= entryToProcess.ProcessQuantity;
                    baseEntry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(baseEntry);
                    _unitOfWork.JournalEntries.Update(baseEntry);
                }
            }
        }

        private async Task CreateAccountEntryAsync(int derivedVoucherId, JournalEntry baseEntry, decimal accountEntryAmount, bool accountEntryIsDebit)
        {
            var newEntry = new JournalEntry
            {
                JournalVoucherId = derivedVoucherId,
                AccountId = baseEntry.AccountId,
                CategoryId = baseEntry.CategoryId,
                Description = baseEntry.Description,
                Quantity = 0,
                Rate = baseEntry.Rate,
                DiscountPercent = baseEntry.DiscountPercent,
                Discount = 0,
                Debit = accountEntryIsDebit ? accountEntryAmount : 0,
                Credit = !accountEntryIsDebit ? accountEntryAmount : 0,
                Rbp = baseEntry.Rbp,
                IsProcessed = true
            };

            _unitOfWork.JournalEntries.Add(newEntry);
        }

        private async Task CreateRegularEntryAsync(int derivedVoucherId, JournalEntry baseEntry, BaseVoucherEntryViewModel entryToProcess)
        {
            decimal calculatedDebit = entryToProcess.CalculatedDebit;
            decimal calculatedCredit = entryToProcess.CalculatedCredit;

            if (calculatedDebit == 0 && calculatedCredit == 0)
            {
                decimal processQty = entryToProcess.ProcessQuantity;
                decimal baseQty = baseEntry.Quantity;

                if (baseQty > 0)
                {
                    decimal quantityRatio = processQty / baseQty;
                    calculatedDebit = baseEntry.Debit > 0 ? Math.Round(baseEntry.Debit * quantityRatio, 2, MidpointRounding.AwayFromZero) : 0m;
                    calculatedCredit = baseEntry.Credit > 0 ? Math.Round(baseEntry.Credit * quantityRatio, 2, MidpointRounding.AwayFromZero) : 0m;
                }
            }

            decimal discount = 0m;
            if (entryToProcess.Percent > 0)
            {
                bool rbp = (entryToProcess.Rbp ?? "Y").Equals("Y", StringComparison.OrdinalIgnoreCase);
                decimal pkg = entryToProcess.Pkg;
                decimal totalAmount = rbp && pkg > 0
                    ? pkg * entryToProcess.ProcessQuantity * entryToProcess.Rate
                    : entryToProcess.ProcessQuantity * entryToProcess.Rate;

                discount = Math.Round(totalAmount * entryToProcess.Percent / 100m, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                decimal processQty = entryToProcess.ProcessQuantity;
                decimal baseQty = baseEntry.Quantity;

                if (baseQty > 0)
                {
                    decimal quantityRatio = processQty / baseQty;
                    discount = Math.Round(baseEntry.Discount * quantityRatio, 2, MidpointRounding.AwayFromZero);
                }
            }

            var newEntry = new JournalEntry
            {
                JournalVoucherId = derivedVoucherId,
                AccountId = baseEntry.AccountId,
                CategoryId = baseEntry.CategoryId,
                Description = baseEntry.Description,
                Quantity = entryToProcess.ProcessQuantity,
                Rate = entryToProcess.Rate,
                DiscountPercent = entryToProcess.Percent,
                Discount = discount,
                Debit = calculatedDebit,
                Credit = calculatedCredit,
                Rbp = entryToProcess.Rbp ?? baseEntry.Rbp,
                IsProcessed = false
            };

            newEntry.IsProcessed = await _sharedProcessingService.DetermineEntryProcessedStatusAsync(newEntry);
            _unitOfWork.JournalEntries.Add(newEntry);
        }

        private async Task<Account?> GetCustomerAccountFromEntriesAsync(IEnumerable<JournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId);
                if (account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT)
                    return account;
            }
            return null;
        }

        #endregion

        #region Private Helper Methods - Recipe

        private async Task SaveRecipeAsync(JournalEntryViewModel model, int journalVoucherId)
        {
            var recipe = new Recipe
            {
                RecipeName = model.RecipeName!,
                ProjectId = model.ProjectId
            };

            recipe = _unitOfWork.Recipes.Add(recipe);
            // Materialize the generated recipe ID before adding recipe items.
            await _unitOfWork.SaveChangesAsync();

            var productEntries = model.Entries
                .Where(e => e.AccountId.HasValue && e.CategoryId.HasValue)
                .ToList();

            foreach (var entry in productEntries)
            {
                if (!entry.CategoryId.HasValue || !entry.AccountId.HasValue) continue;

                var recipeItem = new RecipeItem
                {
                    RecipeId = recipe.RecipeId,
                    CategoryId = entry.CategoryId.Value,
                    AccountId = entry.AccountId.Value
                };

                _unitOfWork.RecipeItems.Add(recipeItem);
            }

            _logger.LogInformation("Created recipe {RecipeId} with {ItemCount} items", recipe.RecipeId, productEntries.Count);
        }

        #endregion

        #region Base Vouchers Summary

        public async Task<BaseVouchersSummaryViewModel> GetBaseVouchersSummaryAsync(int? projectId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var vouchers = await _unitOfWork.JournalVouchers.GetBaseVouchersSummaryDataAsync(projectId, startDate, endDate);
            
            var summary = new BaseVouchersSummaryViewModel
            {
                TotalBaseVouchers = vouchers.Count
            };

            if (!vouchers.Any())
            {
                return summary;
            }

            var allEntries = vouchers.SelectMany(v => v.JournalEntries ?? new List<JournalEntry>()).ToList();
            var productEntries = allEntries.Where(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT).ToList();

            // Get processed data from derived vouchers (actual processed quantities)
            var derivedVoucherProducts = await _unitOfWork.JournalEntries.GetDerivedVoucherProductQuantitiesAsync(projectId, startDate, endDate);
            
            // Calculate totals from derived vouchers (processed) and base vouchers (unprocessed)
            decimal totalProcessedQuantity = derivedVoucherProducts.Sum(p => p.TotalQuantity);
            int totalProcessedEntries = derivedVoucherProducts.Sum(p => p.EntryCount);
            
            // Unprocessed = entries in base vouchers that still have quantity > 0 and are not marked as processed
            var unprocessedEntries = productEntries.Where(e => !e.IsProcessed && e.Quantity > 0).ToList();
            summary.TotalUnprocessedEntries = unprocessedEntries.Count;
            summary.TotalUnprocessedQuantity = unprocessedEntries.Sum(e => e.Quantity);
            
            summary.TotalProcessedEntries = totalProcessedEntries;
            summary.TotalProcessedQuantity = (int)totalProcessedQuantity;

            // Calculate processing rate based on quantities (processed / (processed + unprocessed))
            decimal totalQuantity = totalProcessedQuantity + summary.TotalUnprocessedQuantity;
            summary.ProcessingRate = totalQuantity > 0 
                ? Math.Round((totalProcessedQuantity / totalQuantity) * 100, 2) 
                : 0;

            // Most Ordered Products (by total quantity from derived vouchers) - limit to 3
            var mostOrderedProducts = derivedVoucherProducts
                .Select(p => new ProductSummaryViewModel
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    TotalQuantity = p.TotalQuantity,
                    UnprocessedQuantity = 0,
                    ProcessedQuantity = p.TotalQuantity,
                    EntryCount = p.EntryCount
                })
                .OrderByDescending(p => p.TotalQuantity)
                .ToList();

            summary.MostOrderedProducts = mostOrderedProducts;

            // Most Pending Products (by unprocessed quantity in base vouchers)
            var pendingProducts = unprocessedEntries
                .GroupBy(e => new { e.AccountId, ProductName = e.Account?.AccountName ?? "Unknown" })
                .Select(g => new ProductSummaryViewModel
                {
                    ProductId = g.Key.AccountId,
                    ProductName = g.Key.ProductName,
                    TotalQuantity = g.Sum(e => e.Quantity),
                    UnprocessedQuantity = g.Sum(e => e.Quantity),
                    ProcessedQuantity = 0,
                    EntryCount = g.Count()
                })
                .OrderByDescending(p => p.UnprocessedQuantity)
                .ToList();

            summary.MostPendingProducts = pendingProducts;

            // Top Customer (by voucher count)
            var customerVoucherMap = new Dictionary<int, (string Name, int VoucherCount, int ProductEntries)>();
            
            foreach (var voucher in vouchers)
            {
                var customerEntry = (voucher.JournalEntries ?? new List<JournalEntry>())
                    .FirstOrDefault(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_ACCOUNT);
                
                if (customerEntry != null && customerEntry.AccountId > 0)
                {
                    // Count only product entries (exclude account entries)
                    var productEntriesCount = (voucher.JournalEntries ?? new List<JournalEntry>())
                        .Count(e => e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT);
                    
                    if (!customerVoucherMap.ContainsKey(customerEntry.AccountId))
                    {
                        customerVoucherMap[customerEntry.AccountId] = (
                            customerEntry.Account?.AccountName ?? "Unknown",
                            0,
                            0
                        );
                    }
                    
                    var current = customerVoucherMap[customerEntry.AccountId];
                    customerVoucherMap[customerEntry.AccountId] = (
                        current.Name,
                        current.VoucherCount + 1,
                        current.ProductEntries + productEntriesCount
                    );
                }
            }

            var topCustomerData = customerVoucherMap
                .OrderByDescending(kvp => kvp.Value.VoucherCount)
                .FirstOrDefault();

            if (topCustomerData.Key > 0)
            {
                summary.TopCustomer = new CustomerSummaryViewModel
                {
                    CustomerId = topCustomerData.Key,
                    CustomerName = topCustomerData.Value.Name,
                    VoucherCount = topCustomerData.Value.VoucherCount,
                    TotalEntries = topCustomerData.Value.ProductEntries
                };
            }

            // Oldest Pending Voucher
            var pendingVouchers = vouchers
                .Where(v => (v.JournalEntries ?? new List<JournalEntry>()).Any(e => !e.IsProcessed && e.Quantity > 0 && e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT))
                .OrderBy(v => v.EntryDate)
                .ThenBy(v => v.JournalVoucherId)
                .FirstOrDefault();

            if (pendingVouchers != null)
            {
                var today = BusinessClock.Today;
                var daysPending = today.DayNumber - pendingVouchers.EntryDate.DayNumber;
                var unprocessedCount = (pendingVouchers.JournalEntries ?? new List<JournalEntry>())
                    .Count(e => !e.IsProcessed && e.Quantity > 0 && e.Account?.AccountGroupId == ACCOUNT_GROUP_PRODUCT);

                summary.OldestPendingVoucher = new VoucherSummaryViewModel
                {
                    VoucherId = pendingVouchers.JournalVoucherId,
                    EntryDate = pendingVouchers.EntryDate,
                    DaysPending = daysPending,
                    UnprocessedCount = unprocessedCount
                };
            }

            return summary;
        }

        #endregion
    }
}
