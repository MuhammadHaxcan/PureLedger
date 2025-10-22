using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;
using PureLedger.Services.Interface;

namespace PureLedger.Services
{
    public class MetaDataService : IMetaDataService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MetaDataService> _logger;

        public MetaDataService(
            IUnitOfWork unitOfWork,
            ILogger<MetaDataService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Project Operations
        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Project>> GetAllProjectsForUserAsync(int userId)
        {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
        }

        public async Task<Project?> GetProjectByIdAsync(int id)
        {
            return await _unitOfWork.Projects.GetByIdAsync(id);
        }
        public async Task<List<string>> CreateProjectAsync(Project project)
        {
            var errors = new List<string>();

            // Check for duplicate project name (case-insensitive)
            var existing = await _unitOfWork.Projects.GetAllAsync();
            if (existing.Any(p => p.ProjectName.ToLower().Trim() == project.ProjectName.ToLower().Trim()))
            {
                errors.Add("A project with this name already exists.");
                return errors;
            }

            try
            {
                await _unitOfWork.Projects.AddAsync(project);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Optional: log detailed internal error
                _logger.LogError(ex, "Error while creating project.");
                errors.Add("An error occurred while saving the project.");
            }

            return errors;
        }

        //public async Task<Project> CreateProjectAsync(Project project)
        //{
        //    await _unitOfWork.Projects.AddAsync(project);
        //    await _unitOfWork.SaveChangesAsync();
        //    return project;
        //}

        public async Task<List<string>> UpdateProjectAsync(Project project)
        {
            var errors = new List<string>();

            // Check for duplicate name excluding current
            var existingProjects = await _unitOfWork.Projects.GetAllAsync();
            if (existingProjects.Any(p =>
                p.ProjectId != project.ProjectId &&
                p.ProjectName.ToLower().Trim() == project.ProjectName.ToLower().Trim()))
            {
                errors.Add("A project with this name already exists.");
                return errors;
            }

            var existing = await _unitOfWork.Projects.GetByIdAsync(project.ProjectId);
            if (existing == null)
            {
                errors.Add("Project not found.");
                return errors;
            }

            existing.ProjectName = project.ProjectName;

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while updating project.");
                errors.Add("An unexpected error occurred while updating the project.");
            }

            return errors;
        }


        #endregion

        #region Category Operations
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _unitOfWork.Categories.GetAllWithDetailsAsync();
        }

        public async Task<List<Project>> GetAllProjectsForCategoriesAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Project>> GetAllProjectsForCategoriesForUserAsync(int userId)
        {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
        }

        public async Task<List<AccountType>> GetAllAccountTypesAsync()
        {
            return await _unitOfWork.AccountTypes.GetAllAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            return await _unitOfWork.Categories.GetByIdWithDetailsAsync(id);
        }

        public async Task<List<string>> CreateCategoryAsync(Category category)
        {
            var errors = new List<string>();

            var existing = await _unitOfWork.Categories.GetAllWithDetailsAsync();
            if (existing.Any(c => c.CategoryName.ToLower().Trim() == category.CategoryName.ToLower().Trim()
                               && c.ProjectId == category.ProjectId))
            {
                errors.Add("This category already exists under the selected project.");
                return errors;
            }

            try
            {
                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while creating category.");
                errors.Add("An error occurred while saving the category.");
            }

            return errors;
        }

        public async Task<List<string>> UpdateCategoryAsync(Category category)
        {
            var errors = new List<string>();

            // Check for duplicate name in same project, excluding the current category
            var existing = await _unitOfWork.Categories.GetAllWithDetailsAsync();
            if (existing.Any(c =>
                c.CategoryId != category.CategoryId &&
                c.ProjectId == category.ProjectId &&
                c.CategoryName.ToLower().Trim() == category.CategoryName.ToLower().Trim()))
            {
                errors.Add("This category name already exists under the selected project.");
                return errors;
            }

            var existingCategory = await _unitOfWork.Categories.GetByIdWithDetailsAsync(category.CategoryId);
            if (existingCategory == null)
            {
                errors.Add("Category not found.");
                return errors;
            }

            // Update properties safely
            existingCategory.CategoryName = category.CategoryName;
            existingCategory.ProjectId = category.ProjectId;
            existingCategory.AccountTypeId = category.AccountTypeId;

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while updating category.");
                errors.Add("An unexpected error occurred while updating the category.");
            }

            return errors;
        }
        public async Task<List<object>> GetCategoriesByProjectAsync(int projectId)
        {
            var categories = await _unitOfWork.Categories.GetByProjectIdAsync(projectId);
            return categories.Select(c => new { categoryId = c.CategoryId, categoryName = c.CategoryName })
                .Cast<object>()
                .ToList();
        }
        #endregion

        #region Account Operations
        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _unitOfWork.Accounts.GetAllWithDetailsAsync();
        }

        public async Task<List<Category>> GetCategoriesForAccountsAsync()
        {
            return await _unitOfWork.Categories.GetAllWithDetailsAsync();
        }

        public async Task<List<AccountGroup>> GetAccountGroupsAsync()
        {
            return await _unitOfWork.AccountGroups.GetAllAsync();
        }

        public async Task<List<Project>> GetProjectsForAccountsAsync()
        {
            return await _unitOfWork.Projects.GetAllAsync();
        }

        public async Task<List<Project>> GetProjectsForAccountsForUserAsync(int userId)
        {
            return await _unitOfWork.Projects.GetAllForUserAsync(userId);
        }

        public async Task<Account?> GetAccountByIdAsync(int id)
        {
            return await _unitOfWork.Accounts.GetByIdWithDetailsAsync(id);
        }

        public async Task<object> GetAllAccountsWithDetailsAsync()
        {
            var accounts = await _unitOfWork.Accounts.GetAllWithDetailsAsync();
            var categories = await _unitOfWork.Categories.GetAllWithDetailsAsync();
            var accountGroups = await _unitOfWork.AccountGroups.GetAllAsync();
            var projects = await _unitOfWork.Projects.GetAllAsync();
            
            return new { 
                Accounts = accounts, 
                Categories = categories, 
                AccountGroups = accountGroups, 
                Projects = projects 
            };
        }

        public async Task<List<string>> CreateAccountAsync(Account account)
        {
            var errors = new List<string>();

            // Check for duplicate account name (case-insensitive)
            var existing = await _unitOfWork.Accounts.GetAllAsync();
            if (existing.Any(a => a.AccountName.ToLower().Trim() == account.AccountName.ToLower().Trim() &&
            a.CategoryId == account.CategoryId))
            {
                errors.Add("An account with this name already exists.");
                return errors;
            }

            try
            {
                var accountGroup = await _unitOfWork.AccountGroups.GetByIdAsync(account.AccountGroupId);
                if (accountGroup != null)
                {
                    var groupName = accountGroup.AccountGroupName?.Trim().ToLower();

                    if (groupName == "account")
                    {
                        account.ProductData = null;
                    }
                    else if (groupName == "product")
                    {
                        account.AccountData = null;
                    }
                }

                await _unitOfWork.Accounts.AddAsync(account);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while creating account.");
                errors.Add("An error occurred while saving the account.");
            }

            return errors;
        }

        public async Task<List<string>> UpdateAccountAsync(Account account)
        {
            var errors = new List<string>();

            // Check for duplicates (excluding current)
            var existingAccounts = await _unitOfWork.Accounts.GetAllAsync();
            if (existingAccounts.Any(a =>
                a.AccountId != account.AccountId &&
                a.AccountName.ToLower().Trim() == account.AccountName.ToLower().Trim() &&
                a.CategoryId == account.CategoryId))
            {
                errors.Add("An account with this name already exists under the selected category.");
                return errors;
            }

            var existingAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(account.AccountId);
            if (existingAccount == null)
            {
                errors.Add($"Account with ID {account.AccountId} not found.");
                return errors;
            }

            try
            {
                // Update basic fields
                existingAccount.AccountName = account.AccountName;
                existingAccount.CategoryId = account.CategoryId;
                existingAccount.AccountGroupId = account.AccountGroupId;

                var accountGroup = await _unitOfWork.AccountGroups.GetByIdAsync(account.AccountGroupId);
                if (accountGroup != null)
                {
                    var groupName = accountGroup.AccountGroupName?.Trim().ToLower();

                    if (groupName == "account")
                    {
                        if (account.AccountData != null)
                        {
                            if (existingAccount.AccountData == null)
                            {
                                // Attach new AccountData
                                existingAccount.AccountData = new AccountData
                                {
                                    PhoneNumber = account.AccountData.PhoneNumber,
                                    Email = account.AccountData.Email,
                                    Address = account.AccountData.Address,
                                    Area = account.AccountData.Area,
                                    City = account.AccountData.City
                                };
                            }
                            else
                            {
                                existingAccount.AccountData.PhoneNumber = account.AccountData.PhoneNumber;
                                existingAccount.AccountData.Email = account.AccountData.Email;
                                existingAccount.AccountData.Address = account.AccountData.Address;
                                existingAccount.AccountData.Area = account.AccountData.Area;
                                existingAccount.AccountData.City = account.AccountData.City;
                            }
                        }

                        // Remove ProductData if it exists
                        if (existingAccount.ProductData != null)
                        {
                            await _unitOfWork.ProductDatas.DeleteAsync(existingAccount.ProductData);
                            existingAccount.ProductData = null;
                        }
                    }
                    else if (groupName == "product")
                    {
                        if (account.ProductData != null)
                        {
                            if (existingAccount.ProductData == null)
                            {
                                existingAccount.ProductData = new ProductData
                                {
                                    PkgForm = account.ProductData.PkgForm,
                                    Pkg = account.ProductData.Pkg,
                                    Type = account.ProductData.Type,
                                    SaleRate = account.ProductData.SaleRate
                                };
                            }
                            else
                            {
                                existingAccount.ProductData.PkgForm = account.ProductData.PkgForm;
                                existingAccount.ProductData.Pkg = account.ProductData.Pkg;
                                existingAccount.ProductData.Type = account.ProductData.Type;
                                existingAccount.ProductData.SaleRate = account.ProductData.SaleRate;
                            }
                        }

                        if (existingAccount.AccountData != null)
                        {
                            await _unitOfWork.AccountDatas.DeleteAsync(existingAccount.AccountData);
                            existingAccount.AccountData = null;
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while updating account.");
                errors.Add("An unexpected error occurred while updating the account.");
            }

            return errors;
        }

        public async Task<(bool Success, List<int> AssociatedVoucherIds)> DeleteAccountAsync(int id)
        {
            var account = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(id);
            if (account == null)
            {
                return (false, new List<int>());
            }

            // Delete account data and product data if they exist
            if (account.AccountData != null)
            {
                await _unitOfWork.AccountDatas.DeleteAsync(account.AccountData);
            }

            if (account.ProductData != null)
            {
                await _unitOfWork.ProductDatas.DeleteAsync(account.ProductData);
            }

            // Delete the account itself
            var result = await _unitOfWork.Accounts.DeleteAsync(account);
            return (result, new List<int>());
        }

        public async Task<List<int>> GetAssociatedVoucherIdsAsync(int accountId)
        {
            // Returns a list of JournalVoucherIds associated with the given account
            return await _unitOfWork.Accounts.GetAssociatedJournalVoucherIds(accountId);
        }
        #endregion

        #region Location Operations
        public async Task<List<Location>> GetAllLocationsAsync()
        {
            return await _unitOfWork.Locations.GetAllAsync();
        }

        public async Task<Location?> GetLocationByIdAsync(int id)
        {
            return await _unitOfWork.Locations.GetByIdAsync(id);
        }

        public async Task<List<string>> CreateLocationAsync(Location location)
        {
            var errors = new List<string>();

            var existing = await _unitOfWork.Locations.GetAllAsync();
            if (existing.Any(l => l.LocationName.ToLower().Trim() == location.LocationName.ToLower().Trim()))
            {
                errors.Add("A location with this name already exists.");
                return errors;
            }

            try
            {
                await _unitOfWork.Locations.AddAsync(location);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating location.");
                errors.Add("An unexpected error occurred while creating the location.");
            }

            return errors;
        }

        public async Task<List<string>> UpdateLocationAsync(Location location)
        {
            var errors = new List<string>();

            // Prevent duplicate name (case-insensitive, exclude current ID)
            var existing = await _unitOfWork.Locations.GetAllAsync();
            if (existing.Any(l =>
                l.LocationId != location.LocationId &&
                l.LocationName.ToLower().Trim() == location.LocationName.ToLower().Trim()))
            {
                errors.Add("A location with this name already exists.");
                return errors;
            }

            var existingLocation = await _unitOfWork.Locations.GetByIdAsync(location.LocationId);
            if (existingLocation == null)
            {
                errors.Add("Location not found.");
                return errors;
            }

            try
            {
                existingLocation.LocationName = location.LocationName;
                existingLocation.Phone = location.Phone;
                existingLocation.Region = location.Region;

                await _unitOfWork.Locations.UpdateAsync(existingLocation);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while updating location.");
                errors.Add("An unexpected error occurred while updating the location.");
            }

            return errors;
        }


        #endregion

        #region Booker Operations

        // Add these methods to MetaDataService class
        public async Task<List<Booker>> GetAllBookersAsync()
        {
            return await _unitOfWork.Bookers.GetAllAsync();
        }

        public async Task<Booker?> GetBookerByIdAsync(int id)
        {
            return await _unitOfWork.Bookers.GetByIdAsync(id);
        }

        public async Task<List<string>> CreateBookerAsync(Booker booker)
        {
            var errors = new List<string>();

            // Check for duplicate booker name (case-insensitive)
            var existing = await _unitOfWork.Bookers.GetAllAsync();
            if (existing.Any(b => b.BookerName.ToLower().Trim() == booker.BookerName.ToLower().Trim()))
            {
                errors.Add("A booker with this name already exists.");
                return errors;
            }

            try
            {
                await _unitOfWork.Bookers.AddAsync(booker);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while creating booker.");
                errors.Add("An error occurred while saving the booker.");
            }

            return errors;
        }

        public async Task<List<string>> UpdateBookerAsync(Booker booker)
        {
            var errors = new List<string>();

            // Check for duplicate name excluding current
            var existingBookers = await _unitOfWork.Bookers.GetAllAsync();
            if (existingBookers.Any(b =>
                b.BookerID != booker.BookerID &&
                b.BookerName.ToLower().Trim() == booker.BookerName.ToLower().Trim()))
            {
                errors.Add("A booker with this name already exists.");
                return errors;
            }

            var existing = await _unitOfWork.Bookers.GetByIdAsync(booker.BookerID);
            if (existing == null)
            {
                errors.Add("Booker not found.");
                return errors;
            }

            existing.BookerName = booker.BookerName;

            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error while updating booker.");
                errors.Add("An unexpected error occurred while updating the booker.");
            }

            return errors;
        }
        #endregion
    }
}