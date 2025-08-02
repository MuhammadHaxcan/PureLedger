using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

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

        public async Task<Project> UpdateProjectAsync(Project project)
        {
            await _unitOfWork.Projects.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();
            return project;
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

        public async Task<Category> UpdateCategoryAsync(Category category)
        {
            await _unitOfWork.Categories.UpdateAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return category;
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

        public async Task<Account> UpdateAccountAsync(Account account)
        {
            var existingAccount = await _unitOfWork.Accounts.GetByIdWithDetailsAsync(account.AccountId);
            if (existingAccount == null)
                throw new KeyNotFoundException($"Account with ID {account.AccountId} not found.");

            // Update basic properties
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
                            existingAccount.AccountData = account.AccountData;
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
                            existingAccount.ProductData = account.ProductData;
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

            await _unitOfWork.Accounts.UpdateAsync(existingAccount);
            await _unitOfWork.SaveChangesAsync();
            return existingAccount;
        }
        #endregion

        #region Tag Operations
        public async Task<List<Tag>> GetAllTagsAsync()
        {
            return await _unitOfWork.Tags.GetAllAsync();
        }

        public async Task<List<string>> CreateTagAsync(Tag tag)
        {
            var errors = new List<string>();

            var existing = await _unitOfWork.Tags.GetAllAsync();
            if (existing.Any(t => t.TagName.ToLower().Trim() == tag.TagName.ToLower().Trim()))
            {
                errors.Add("A tag with this name already exists.");
                return errors;
            }

            try
            {
                await _unitOfWork.Tags.AddAsync(tag);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating tag.");
                errors.Add("An unexpected error occurred while creating the tag.");
            }

            return errors;
        }


        public async Task<bool> DeleteTagAsync(int tagId)
        {
            var tag = await _unitOfWork.Tags.GetByIdAsync(tagId);
            if (tag == null)
                return false;

            await _unitOfWork.Tags.DeleteAsync(tag);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsTagInUseAsync(int tagId)
        {
            return await _unitOfWork.Tags.IsTagInUseAsync(tagId);
        }

        public async Task<List<object>> SearchTagsAsync(string term, int take = 20)
        {
            var tags = await _unitOfWork.Tags.SearchByNameAsync(term, take);
            return tags.Select(t => new { id = t.TagId, text = t.TagName })
                .Cast<object>()
                .ToList();
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


        #endregion
    }
}