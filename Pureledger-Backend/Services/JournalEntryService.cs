using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.ViewModels.Home;
using PureLedger.ViewModels;
namespace PureLedger.Services
{
    public class JournalEntryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JournalEntryService> _logger;

        public JournalEntryService(
            IUnitOfWork unitOfWork,
            ILogger<JournalEntryService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Project & Booker Retrieval
        public Task<List<Project>> GetAllProjectsForUserAsync(int userId)
            => _unitOfWork.Projects.GetAllForUserAsync(userId);

        public Task<List<Booker>> GetAllEnabledBookersAsync()
            => _unitOfWork.Bookers.GetAllEnabledAsync();

        public Task<Project?> GetProjectByIdAsync(int projectId)
             => _unitOfWork.Projects.GetByIdAsync(projectId);

        public Task<List<Location>> GetAllLocationsAsync()
            => _unitOfWork.Locations.GetAllAsync();

        #endregion

        #region Voucher Retrieval

        public Task<List<JournalVoucher>> GetAllVouchersAsync()
            => _unitOfWork.JournalVouchers.GetAllWithEntriesAsync();

        public Task<JournalVoucher?> GetVoucherWithEntriesAsync(int jvId)
            => _unitOfWork.JournalVouchers.GetWithEntriesAsync(jvId);

        public Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync()
            => _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesAsync(null);

        public Task<List<JournalVoucher>> GetVouchersWithModifiedEntriesAsync(int? projectId)
            => _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesAsync(projectId);

        public Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetVouchersWithModifiedEntriesPaginatedAsync(int? projectId, int page, int pageSize)
            => _unitOfWork.JournalVouchers.GetVouchersWithModifiedEntriesPaginatedAsync(projectId, page, pageSize);

        public async Task<List<JournalVoucher>> GetAllBaseVouchersAsync()
        {
            try
            {
                var baseVouchers = await _unitOfWork.JournalVouchers.GetAllBaseVouchersAsync();

                // Load entries for vouchers that don't have them
                foreach (var voucher in baseVouchers.Where(v => v.JournalEntries == null || !v.JournalEntries.Any()))
                {
                    _logger.LogWarning("Voucher {VoucherId} has no entries loaded, loading them explicitly", voucher.JournalVoucherId);

                    var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, null, null);
                    voucher.JournalEntries ??= new List<JournalEntry>();

                    foreach (var entry in entries)
                    {
                        voucher.JournalEntries.Add(entry);
                    }
                }

                _logger.LogInformation("Retrieved {Count} base vouchers with their entries", baseVouchers.Count);
                return baseVouchers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving base vouchers");
                throw;
            }
        }

        public Task<(List<JournalVoucher> Vouchers, int TotalCount)> GetAllBaseVouchersPaginatedAsync(int? projectId, int page, int pageSize)
            => _unitOfWork.JournalVouchers.GetAllBaseVouchersPaginatedAsync(projectId, page, pageSize);

        public Task<JournalVoucher?> GetBaseVoucherWithUnprocessedEntriesAsync(int id)
            => _unitOfWork.JournalVouchers.GetBaseVoucherWithUnprocessedEntriesAsync(id);

        #endregion

        #region Voucher Mapping & Summary

        public async Task<JournalEntryViewModel> MapVoucherToViewModelAsync(JournalVoucher voucher)
        {
            try
            {
                _logger.LogInformation("Mapping voucher ID {VoucherId} to view model", voucher.JournalVoucherId);

                int creditDays = voucher.CreditableDate > voucher.EntryDate
                    ? voucher.CreditableDate.DayNumber - voucher.EntryDate.DayNumber
                    : 0;

                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucher.JournalVoucherId, null, null);
                _logger.LogInformation("Found {Count} entries for voucher ID {VoucherId}", entries.Count, voucher.JournalVoucherId);

                var viewModel = new JournalEntryViewModel
                {
                    ProjectId = voucher.ProjectId,
                    EntryDate = voucher.EntryDate,
                    DueDate = voucher.CreditableDate,
                    CreditDays = creditDays,
                    IsBaseVoucher = voucher.IsBaseVoucher,
                    BookerId = voucher.BookerId,
                    Entries = entries.Select(MapToRowModel).ToList()
                };

                _logger.LogInformation("Successfully mapped voucher to view model with {EntriesCount} entries", viewModel.Entries.Count);
                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping voucher to view model");
                throw;
            }
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

            return new { entries, jvId = voucher.JournalVoucherId };
        }

        #endregion

        #region Voucher Creation & Update

        public async Task<JournalVoucher> CreateJournalVoucherAsync(JournalEntryViewModel model)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var journalVoucher = new JournalVoucher
                {
                    ProjectId = model.ProjectId,
                    EntryDate = model.EntryDate,
                    IsCompleted = false,
                    IsBaseVoucher = model.IsBaseVoucher,
                    CreditableDate = model.EntryDate.AddDays(model.CreditDays),
                    BookerId = model.BookerId ?? 0
                };

                journalVoucher = await _unitOfWork.JournalVouchers.AddAsync(journalVoucher);
                await CreateJournalEntriesAsync(journalVoucher.JournalVoucherId, model.Entries);
                await UpdateVoucherCompletionStatusAsync(journalVoucher.JournalVoucherId);
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

        public async Task<JournalVoucher> UpdateJournalVoucherAsync(int voucherId, JournalEntryViewModel model)
        {
            try
            {
                var existingVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (existingVoucher == null)
                    throw new InvalidOperationException($"Voucher with ID {voucherId} not found");

                bool wasBaseVoucher = existingVoucher.IsBaseVoucher;
                bool wasCompleted = existingVoucher.IsCompleted;

                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Update voucher properties
                    existingVoucher.ProjectId = model.ProjectId;
                    existingVoucher.EntryDate = model.EntryDate;
                    existingVoucher.CreditableDate = model.DueDate;
                    existingVoucher.BookerId = model.BookerId;

                    // Check if voucher has references (derived vouchers exist)
                    var hasReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(voucherId);
                    existingVoucher.IsBaseVoucher = hasReferences.Any() ? wasBaseVoucher : model.IsBaseVoucher;

                    // Process entries: match existing, add new, delete orphaned
                    var existingEntries = existingVoucher.JournalEntries.ToList();
                    bool newEntryAdded = false;

                    foreach (var entryModel in model.Entries)
                    {
                        var existingEntry = existingEntries.FirstOrDefault(e =>
                            e.AccountId == entryModel.AccountId &&
                            e.CategoryId == entryModel.CategoryId &&
                            string.Equals(e.Description, entryModel.Des, StringComparison.OrdinalIgnoreCase));

                        if (existingEntry != null)
                        {
                            UpdateExistingEntry(existingEntry, entryModel);
                            await _unitOfWork.JournalEntries.UpdateAsync(existingEntry);
                            existingEntries.Remove(existingEntry);
                        }
                        else
                        {
                            await CreateNewEntry(voucherId, entryModel);
                            newEntryAdded = true;
                        }
                    }

                    // Delete orphaned entries
                    foreach (var entryToDelete in existingEntries)
                    {
                        await DeleteJournalEntryInternalAsync(entryToDelete.JournalEntryId);
                    }

                    // Update completion status
                    existingVoucher.IsCompleted = newEntryAdded ? false : wasCompleted;
                    await UpdateVoucherCompletionStatusAsync(existingVoucher.JournalVoucherId);

                    await _unitOfWork.JournalVouchers.UpdateAsync(existingVoucher);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully updated voucher {VoucherId} with {EntryCount} entries", voucherId, model.Entries.Count);
                    return existingVoucher;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal voucher {VoucherId}", voucherId);
                throw;
            }
        }

        #endregion

        #region Entry CRUD Operations

        public async Task<JournalEntryUpdateViewModel?> GetJournalEntryForEditAsync(int entryId)
        {
            var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
            if (entry == null)
                return null;

            var account = entry.AccountId > 0
                ? await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId)
                : null;
            int accountGroupId = account?.AccountGroupId ?? 0;

            // Load delivery challan data for any entry that has a delivery challan
            int? deliveryChallanEntryId = null;
            int? locationId = null;
            List<Location>? allLocations = null;

            // Check if this entry has a delivery challan entry (regardless of account group)
            var dcEntry = await _unitOfWork.DeliveryChallanEntries
                .GetByJournalEntryIdAsync(entry.JournalEntryId);

            if (dcEntry != null)
            {
                deliveryChallanEntryId = dcEntry.DeliveryChallanEntryId;
                locationId = dcEntry.LocationId;
                // Load all locations if delivery challan exists
                allLocations = await _unitOfWork.Locations.GetAllAsync();
            }
            else if (accountGroupId == 2)
            {
                // For inventory items without delivery challan yet, still load locations
                allLocations = await _unitOfWork.Locations.GetAllAsync();
            }

            return new JournalEntryUpdateViewModel
            {
                EntryId = entry.JournalEntryId,
                AccountName = entry.Account?.AccountName,
                Description = entry.Description,
                Quantity = entry.Quantity,
                Rate = entry.Rate,
                Discount = entry.Discount,
                DiscountPercent = entry.DiscountPercent ?? 0,
                Debit = entry.Debit,
                Credit = entry.Credit,
                AccountId = entry.AccountId,
                CategoryId = entry.CategoryId,
                ProjectId = entry.JournalVoucher?.ProjectId ?? 0,
                AccountGroupId = accountGroupId,
                DeliveryChallanEntryId = deliveryChallanEntryId,
                LocationId = locationId,
                Locations = allLocations
            };
        }

        public async Task<bool> UpdateJournalEntryAsync(JournalEntryUpdateViewModel model)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(model.EntryId);
                if (entry == null)
                    return false;

                UpdateEntryFromViewModel(entry, model);
                await _unitOfWork.JournalEntries.UpdateAsync(entry);

                // Update delivery challan location if applicable (for any entry with delivery challan)
                if (model.DeliveryChallanEntryId.HasValue && model.LocationId.HasValue)
                {
                    await UpdateDeliveryChallanEntryLocationAsync(model.DeliveryChallanEntryId.Value, model.LocationId.Value);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating journal entry {EntryId}", model.EntryId);
                return false;
            }
        }

        public async Task<bool> DeleteJournalEntryAsync(int entryId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var result = await DeleteJournalEntryInternalAsync(entryId);

                if (result)
                {
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting journal entry {EntryId}", entryId);
                return false;
            }
        }

        private async Task<bool> DeleteJournalEntryInternalAsync(int entryId)
        {
            try
            {
                var entry = await _unitOfWork.JournalEntries.GetByIdWithDetailsAsync(entryId);
                if (entry == null)
                    return false;

                var voucherId = entry.JournalVoucherId;
                var entries = await _unitOfWork.JournalEntries.GetEntriesByVoucherIdAsync(voucherId, null, null);

                if (entries.Count == 1)
                {
                    // Delete entire voucher if this is the last entry
                    await DeleteVoucherWithLastEntryAsync(entryId, voucherId);
                }
                else
                {
                    // Delete only the entry
                    await _unitOfWork.JournalEntries.DeleteAsync(entryId);
                    await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);
                    await UpdateVoucherCompletionStatusAsync(voucherId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in internal delete for journal entry {EntryId}", entryId);
                throw;
            }
        }

        #endregion

        #region Category & Account Lookups

        public async Task<IEnumerable<Category>> GetCategoriesByProjectAsync(int projectId)
        {
            try
            {
                return await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<IEnumerable<Account>> GetEnabledAccountsByCategoryAsync(int categoryId)
        {
            try
            {
                return await _unitOfWork.Accounts.GetEnabledByCategoryIdAsync(categoryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                throw;
            }
        }

        public Task<object?> GetProductDetailsAsync(int accountId)
            => _unitOfWork.Accounts.GetProductDetailsAsync(accountId);

        #endregion

        #region Derived Voucher Operations

        public async Task<int> CreateDerivedVoucherAsync(
            int baseVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess,
            decimal accountEntryAmount = 0,
            bool accountEntryIsDebit = false,
            DateOnly? creditableDate = null,
            int? bookerId = null)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Check if we should process as full voucher (convert base to regular)
                if (await ShouldProcessAsFullVoucherAsync(baseVoucherId, entriesToProcess))
                {
                    var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                    if (baseVoucher != null)
                    {
                        baseVoucher.IsBaseVoucher = false;
                        // Update booker if provided (don't use existing voucher's booker)
                        if (bookerId.HasValue)
                        {
                            baseVoucher.BookerId = bookerId.Value;
                        }
                        await _unitOfWork.JournalVouchers.UpdateAsync(baseVoucher);
                        await _unitOfWork.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Base voucher {VoucherId} processed completely and converted to regular voucher", baseVoucherId);
                        return baseVoucher.JournalVoucherId;
                    }
                }

                // Create derived voucher
                var baseVoucherData = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
                if (baseVoucherData == null || !baseVoucherData.IsBaseVoucher)
                    throw new InvalidOperationException("Base voucher not found");

                var derivedVoucher = new JournalVoucher
                {
                    ProjectId = baseVoucherData.ProjectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    CreditableDate = creditableDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    IsCompleted = false,
                    BookerId = bookerId ?? 0
                };

                derivedVoucher = await _unitOfWork.JournalVouchers.AddAsync(derivedVoucher);
                await _unitOfWork.SaveChangesAsync();

                // Create reference between base and derived voucher
                await _unitOfWork.JournalVoucherReferences.AddAsync(new JournalVoucherReference
                {
                    MainVoucherId = baseVoucherId,
                    ReferencedVoucherId = derivedVoucher.JournalVoucherId,
                    ReferenceType = "DerivedVoucher"
                });

                // Process entries
                await ProcessDerivedVoucherEntriesAsync(
                    baseVoucherData,
                    derivedVoucher.JournalVoucherId,
                    entriesToProcess,
                    accountEntryAmount,
                    accountEntryIsDebit);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return derivedVoucher.JournalVoucherId;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Missed Entries

        public async Task MarkEntriesAsMissedAsync(int voucherId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(voucherId);
                if (voucher == null || !voucher.IsBaseVoucher)
                    throw new InvalidOperationException("Invalid voucher");

                var customerAccount = await GetCustomerAccountFromEntriesAsync(voucher.JournalEntries);
                if (customerAccount == null)
                    throw new InvalidOperationException("No customer account found in voucher");

                var unprocessedEntries = voucher.JournalEntries
                    .Where(e => !e.IsProcessed && e.Quantity > 0 && e.Account.AccountGroupId == 2)
                    .ToList();

                foreach (var entry in unprocessedEntries)
                {
                    var missedEntry = new MissedEntry
                    {
                        CustomerAccountId = customerAccount.AccountId,
                        ProductAccountId = entry.AccountId,
                        ProjectId = voucher.ProjectId,
                        MissedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        Quantity = entry.Quantity,
                        Rate = entry.Rate,
                        Discount = entry.DiscountPercent ?? 0,
                        Description = entry.Description
                    };

                    await _unitOfWork.MissedEntries.AddAsync(missedEntry);
                    entry.Quantity = 0;
                    await _unitOfWork.JournalEntries.UpdateAsync(entry);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Voucher {VoucherId} marked as missed with {Count} entries against customer {CustomerName}",
                    voucherId, unprocessedEntries.Count, customerAccount.AccountName);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Recipe Management

        public async Task<int> CreateRecipeFromVoucherAsync(JournalEntryViewModel model, int journalVoucherId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (!model.SaveAsRecipe || string.IsNullOrWhiteSpace(model.RecipeName))
                    return 0;

                _logger.LogInformation("Creating recipe '{RecipeName}' from voucher {VoucherId}", model.RecipeName, journalVoucherId);

                var recipe = new Recipe
                {
                    RecipeName = model.RecipeName,
                    ProjectId = model.ProjectId
                };

                recipe = await _unitOfWork.Recipes.AddAsync(recipe);

                var productEntries = model.Entries
                    .Where(e => e.AccountId.HasValue && e.CategoryId.HasValue)
                    .ToList();

                foreach (var entry in productEntries)
                {
                    if (!entry.CategoryId.HasValue || !entry.AccountId.HasValue)
                    {
                        _logger.LogWarning("Skipping recipe item creation due to missing CategoryId or AccountId");
                        continue;
                    }

                    var recipeItem = new RecipeItem
                    {
                        RecipeId = recipe.RecipeId,
                        CategoryId = entry.CategoryId.Value,
                        AccountId = entry.AccountId.Value
                    };

                    await _unitOfWork.RecipeItems.AddAsync(recipeItem);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully created recipe {RecipeId} with {ItemCount} items",
                    recipe.RecipeId, productEntries.Count);

                return recipe.RecipeId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating recipe from voucher {VoucherId}", journalVoucherId);
                throw;
            }
        }

        public async Task<List<Recipe>> GetRecipesByProjectAsync(int projectId)
        {
            try
            {
                _logger.LogInformation("Retrieving recipes for project {ProjectId}", projectId);
                var recipes = await _unitOfWork.Recipes.GetAllByProjectIdAsync(projectId);
                _logger.LogInformation("Found {Count} recipes for project {ProjectId}", recipes.Count, projectId);
                return recipes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recipes for project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<Recipe?> GetRecipeWithItemsAsync(int recipeId)
        {
            try
            {
                _logger.LogInformation("Retrieving recipe with items for recipe ID {RecipeId}", recipeId);
                var recipe = await _unitOfWork.Recipes.GetByIdWithItemsAsync(recipeId);

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe with ID {RecipeId} not found", recipeId);
                    return null;
                }

                _logger.LogInformation("Found recipe '{RecipeName}' with {ItemCount} items", recipe.RecipeName, recipe.RecipeItems?.Count ?? 0);
                return recipe;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recipe with ID {RecipeId}", recipeId);
                throw;
            }
        }

        public async Task<JournalEntryViewModel> CreateViewModelFromRecipeAsync(int recipeId, int projectId)
        {
            try
            {
                _logger.LogInformation("Creating journal entry view model from recipe {RecipeId}", recipeId);

                var recipe = await _unitOfWork.Recipes.GetByIdWithItemsAsync(recipeId);
                if (recipe == null)
                {
                    _logger.LogWarning("Recipe with ID {RecipeId} not found", recipeId);
                    throw new ArgumentException($"Recipe with ID {recipeId} not found");
                }

                if (recipe.ProjectId != projectId)
                {
                    _logger.LogWarning("Recipe project ID mismatch. Recipe {RecipeId} belongs to project {RecipeProjectId}, but was requested for project {RequestedProjectId}",
                        recipeId, recipe.ProjectId, projectId);
                    throw new ArgumentException("Recipe does not belong to the selected project");
                }

                var viewModel = new JournalEntryViewModel
                {
                    ProjectId = projectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    CreditDays = 0,
                    IsBaseVoucher = false,
                    BookerId = 0,
                    Entries = new List<JournalEntryRowModel>()
                };

                foreach (var item in recipe.RecipeItems)
                {
                    var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(item.AccountId);

                    var entry = new JournalEntryRowModel
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
                    };

                    viewModel.Entries.Add(entry);
                }

                _logger.LogInformation("Created journal entry view model from recipe {RecipeId} with {EntryCount} entries", recipeId, viewModel.Entries.Count);
                return viewModel;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.LogError(ex, "Error creating view model from recipe {RecipeId}", recipeId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private static JournalEntryRowModel MapToRowModel(JournalEntry e) => new()
        {
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

        private async Task CreateJournalEntriesAsync(int journalVoucherId, List<JournalEntryRowModel> entryModels)
        {
            foreach (var entryModel in entryModels)
            {
                try
                {
                    _logger.LogInformation("Creating journal entry with AccountId={AccountId}", entryModel.AccountId);

                    int quantity = entryModel.Qty ?? 0;
                    var journalEntry = new JournalEntry
                    {
                        JournalVoucherId = journalVoucherId,
                        AccountId = entryModel.AccountId ?? 0,
                        CategoryId = entryModel.CategoryId ?? 0,
                        Description = entryModel.Des,
                        Quantity = quantity,
                        Rate = entryModel.Rate ?? 0,
                        DiscountPercent = entryModel.Percent,
                        Discount = entryModel.Disc ?? 0,
                        Debit = entryModel.Debit ?? 0,
                        Credit = entryModel.Credit ?? 0,
                        Rbp = entryModel.Rbp ?? "Y",
                        IsProcessed = quantity == 0 // <-- Set processed if quantity is 0
                    };

                    await _unitOfWork.JournalEntries.AddAsync(journalEntry);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating journal entry for account {AccountId}", entryModel.AccountId);
                    throw;
                }
            }
        }

        private async Task CreateNewEntry(int voucherId, JournalEntryRowModel entryModel)
        {
            int quantity = entryModel.Qty ?? 0;
            var newEntry = new JournalEntry
            {
                JournalVoucherId = voucherId,
                AccountId = entryModel.AccountId ?? 0,
                CategoryId = entryModel.CategoryId ?? 0,
                Description = entryModel.Des,
                Quantity = quantity,
                Rate = entryModel.Rate ?? 0,
                DiscountPercent = entryModel.Percent,
                Discount = entryModel.Disc ?? 0,
                Debit = entryModel.Debit ?? 0,
                Credit = entryModel.Credit ?? 0,
                Rbp = entryModel.Rbp ?? "Y",
                IsProcessed = quantity == 0, // <-- Set processed if quantity is 0
                IsEdited = true,
                ModifiedDateTime = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            await _unitOfWork.JournalEntries.AddAsync(newEntry);
        }

        private static void UpdateExistingEntry(JournalEntry entry, JournalEntryRowModel model)
        {
            entry.Description = model.Des;
            entry.Quantity = model.Qty ?? 0;
            entry.Rate = model.Rate ?? 0;
            entry.DiscountPercent = model.Percent;
            entry.Discount = model.Disc ?? 0;
            entry.Debit = model.Debit ?? 0;
            entry.Credit = model.Credit ?? 0;
            entry.Rbp = model.Rbp ?? "Y";
            entry.IsEdited = true;
            entry.ModifiedDateTime = DateOnly.FromDateTime(DateTime.UtcNow);
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
            entry.ModifiedDateTime = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        public async Task<DeliveryChallanEntry?> GetDeliveryChallanEntryByJournalEntryIdAsync(int journalEntryId)
        {
            return await _unitOfWork.DeliveryChallanEntries.GetByJournalEntryIdAsync(journalEntryId);
        }

        private async Task UpdateDeliveryChallanEntryLocationAsync(int deliveryChallanEntryId, int locationId)
        {
            var dcEntry = await _unitOfWork.DeliveryChallanEntries.GetByIdAsync(deliveryChallanEntryId);
            if (dcEntry != null)
            {
                dcEntry.LocationId = locationId;
                await _unitOfWork.DeliveryChallanEntries.UpdateAsync(dcEntry);
            }
        }

        private async Task UpdateVoucherCompletionStatusAsync(int journalVoucherId)
        {
            var voucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(journalVoucherId);
            if (voucher == null || !voucher.JournalEntries.Any())
                return;

            try
            {
                // Get all unique account IDs and fetch accounts in batch
                var accountIds = voucher.JournalEntries.Select(e => e.AccountId).Distinct().ToList();
                var accountsDict = new Dictionary<int, Account>();

                foreach (var accountId in accountIds)
                {
                    var account = await _unitOfWork.Accounts.GetByIdAsync(accountId);
                    if (account != null)
                        accountsDict[accountId] = account;
                }

                var unprocessedEntries = voucher.JournalEntries.Where(e => !e.IsProcessed).ToList();

                // If there are no unprocessed entries, the voucher is completed
                if (!unprocessedEntries.Any())
                {
                    voucher.IsCompleted = true;
                }
                else
                {
                    bool allUnprocessedAreAccounts = unprocessedEntries.All(e =>
                        accountsDict.ContainsKey(e.AccountId) &&
                        accountsDict[e.AccountId].AccountGroupId == 1);

                    if (allUnprocessedAreAccounts)
                    {
                        _logger.LogInformation("All unprocessed entries in voucher {VoucherId} are Accounts, marking as processed", journalVoucherId);

                        foreach (var entry in unprocessedEntries)
                        {
                            entry.IsProcessed = true;
                            await _unitOfWork.JournalEntries.UpdateAsync(entry);
                        }

                        voucher.IsCompleted = true;
                    }
                    else
                    {
                        voucher.IsCompleted = false;
                    }
                }

                await _unitOfWork.JournalVouchers.UpdateAsync(voucher);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Voucher {VoucherId} completion status updated. IsCompleted: {IsCompleted}",
                    journalVoucherId,
                    voucher.IsCompleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating completion status for voucher {VoucherId}", journalVoucherId);
                throw;
            }
        }

        private async Task DeleteVoucherWithLastEntryAsync(int entryId, int voucherId)
        {
            await _unitOfWork.JournalEntries.DeleteAsync(entryId);
            await DeleteRelatedDeliveryChallanEntriesAsync(voucherId, entryId);

            var deliveryChallanVoucher = await _unitOfWork.DeliveryChallanVouchers.GetByJournalVoucherIdAsync(voucherId);
            if (deliveryChallanVoucher != null)
            {
                await _unitOfWork.DeliveryChallanVouchers.DeleteAsync(deliveryChallanVoucher.DeliveryChallanVoucherId);
            }

            await _unitOfWork.JournalVouchers.DeleteAsync(voucherId);
        }

        private async Task DeleteRelatedDeliveryChallanEntriesAsync(int voucherId, int entryId)
        {
            var deliveryChallanEntries = await _unitOfWork.DeliveryChallanEntries.GetByDeliveryChallanIdAsync(voucherId);
            if (deliveryChallanEntries != null && deliveryChallanEntries.Any())
            {
                var entriesToDelete = deliveryChallanEntries.Where(e => e.JournalEntryId == entryId).ToList();
                foreach (var dcEntry in entriesToDelete)
                {
                    await _unitOfWork.DeliveryChallanEntries.DeleteAsync(dcEntry.DeliveryChallanEntryId);
                }
            }
        }

        private async Task<bool> ShouldProcessAsFullVoucherAsync(
            int baseVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess)
        {
            var baseVoucher = await _unitOfWork.JournalVouchers.GetWithEntriesAsync(baseVoucherId);
            if (baseVoucher == null || !baseVoucher.IsBaseVoucher)
                return false;

            // Check if this base voucher has already been partially processed
            var existingReferences = await _unitOfWork.JournalVoucherReferences.GetByMainVoucherIdAsync(baseVoucherId);
            if (existingReferences.Any())
                return false;

            // Get all unprocessed regular entries (non-account entries)
            var regularEntries = baseVoucher.JournalEntries
                .Where(e => !e.IsProcessed)
                .ToList();

            if (!regularEntries.Any())
                return false;

            // Check if all regular entries are being processed with full quantities
            foreach (var entry in regularEntries)
            {
                var processEntry = entriesToProcess.FirstOrDefault(e => e.EntryId == entry.JournalEntryId);
                if (processEntry == null || processEntry.ProcessQuantity != entry.Quantity)
                    return false;
            }

            return true;
        }

        private async Task ProcessDerivedVoucherEntriesAsync(
            JournalVoucher baseVoucher,
            int derivedVoucherId,
            List<BaseVoucherEntryViewModel> entriesToProcess,
            decimal accountEntryAmount,
            bool accountEntryIsDebit)
        {
            bool accountEntryAdded = false;

            foreach (var entryToProcess in entriesToProcess)
            {
                var baseEntry = baseVoucher.JournalEntries
                    .FirstOrDefault(e => e.JournalEntryId == entryToProcess.EntryId);

                if (baseEntry == null )
                    continue;

                var account = await _unitOfWork.Accounts.GetByIdAsync(baseEntry.AccountId);
                bool isAccountEntry = account?.AccountGroupId == 1;

                // Validate regular entry quantities
                if (!isAccountEntry && (entryToProcess.ProcessQuantity <= 0 ||
                    entryToProcess.ProcessQuantity > baseEntry.Quantity))
                    continue;

                if (isAccountEntry)
                {
                    // Process account entry (customer/supplier)
                    if (!accountEntryAdded)
                    {
                        accountEntryAdded = true;
                        await CreateAccountEntryForDerivedVoucherAsync(
                            derivedVoucherId,
                            baseEntry,
                            accountEntryAmount,
                            accountEntryIsDebit);
                    }
                }
                else
                {
                    // Process regular product/service entry
                    // Use calculated values from frontend to ensure accuracy
                    await CreateRegularEntryForDerivedVoucherAsync(
                        derivedVoucherId,
                        baseEntry,
                        entryToProcess);

                    // Update base entry quantity
                    baseEntry.Quantity -= entryToProcess.ProcessQuantity;
                    if (baseEntry.Quantity <= 0)
                    {
                        baseEntry.IsProcessed = true;
                    }
                    await _unitOfWork.JournalEntries.UpdateAsync(baseEntry);
                }
            }
        }

        private async Task CreateAccountEntryForDerivedVoucherAsync(
            int derivedVoucherId,
            JournalEntry baseEntry,
            decimal accountEntryAmount,
            bool accountEntryIsDebit)
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
                IsProcessed = false
            };

            await _unitOfWork.JournalEntries.AddAsync(newEntry);
        }

        private async Task CreateRegularEntryForDerivedVoucherAsync(
            int derivedVoucherId,
            JournalEntry baseEntry,
            BaseVoucherEntryViewModel entryToProcess)
        {

            decimal calculatedDebit = entryToProcess.CalculatedDebit;
            decimal calculatedCredit = entryToProcess.CalculatedCredit;
            
            if (calculatedDebit == 0 && calculatedCredit == 0)
            {
                decimal quantityRatio = (decimal)entryToProcess.ProcessQuantity / (decimal)baseEntry.Quantity;
                calculatedDebit = baseEntry.Debit > 0 ? baseEntry.Debit * quantityRatio : 0m;
                calculatedCredit = baseEntry.Credit > 0 ? baseEntry.Credit * quantityRatio : 0m;
            }

            decimal discount = 0m;
            if (entryToProcess.Percent > 0)
            {
                bool rbp = (entryToProcess.Rbp ?? "Y").Equals("Y", StringComparison.OrdinalIgnoreCase);
                decimal pkg = entryToProcess.Pkg;
                decimal totalAmount = 0m;
                
                if (rbp && pkg > 0)
                {
                    totalAmount = pkg * entryToProcess.ProcessQuantity * entryToProcess.Rate;
                }
                else
                {
                    totalAmount = entryToProcess.ProcessQuantity * entryToProcess.Rate;
                }
                
                discount = Math.Round((totalAmount * entryToProcess.Percent / 100m) * 100m) / 100m;
            }
            else
            {
                // Fall back to ratio-based discount if percent is 0
                decimal quantityRatio = (decimal)entryToProcess.ProcessQuantity / (decimal)baseEntry.Quantity;
                discount = baseEntry.Discount * quantityRatio;
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

            await _unitOfWork.JournalEntries.AddAsync(newEntry);
        }

        private async Task<Account?> GetCustomerAccountFromEntriesAsync(IEnumerable<JournalEntry> entries)
        {
            foreach (var entry in entries)
            {
                var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(entry.AccountId);
                if (account?.AccountGroupId == 1)
                    return account;
            }
            return null;
        }

        #endregion
    }
}