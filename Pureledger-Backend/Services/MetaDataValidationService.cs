using Microsoft.EntityFrameworkCore;
using PureLedger.Models;
using PureLedger.Repositories.Interfaces;

namespace PureLedger.Services
{
    public class MetaDataValidationService 
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MetaDataValidationService> _logger;

        public MetaDataValidationService(IUnitOfWork unitOfWork, ILogger<MetaDataValidationService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<string>> ValidateProjectAsync(Project project)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(project.ProjectName))
                errors.Add("Project name is required.");

            // Additional validation rules as needed

            return errors;
        }

        public async Task<List<string>> ValidateCategoryAsync(Category category)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(category.CategoryName))
                errors.Add("Category name is required.");

            if (category.ProjectId <= 0)
                errors.Add("A project must be selected.");

            if (category.AccountTypeId <= 0)
                errors.Add("An account type must be selected.");

            // Check if project exists
            if (category.ProjectId > 0)
            {
                var project = await _unitOfWork.Projects.GetByIdAsync(category.ProjectId);
                if (project == null)
                    errors.Add("The selected project does not exist.");
            }

            return errors;
        }

        public async Task<List<string>> ValidateAccountAsync(Account account)
        {
            var errors = new List<string>();

            // Base validations
            if (string.IsNullOrWhiteSpace(account.AccountName))
                errors.Add("Account name is required.");

            if (account.CategoryId <= 0)
                errors.Add("A category must be selected.");

            if (account.AccountGroupId <= 0)
                errors.Add("An account group must be selected.");

            // Get the selected AccountGroup for conditional validation
            var accountGroup = await _unitOfWork.AccountGroups.GetByIdAsync(account.AccountGroupId);

            if (accountGroup == null)
            {
                errors.Add("A valid account group must be selected.");
            }
            else
            {
                var groupName = accountGroup.AccountGroupName?.Trim().ToLower();

                if (groupName == "account")
                {
                    // Validate only account data fields
                    if (account.AccountData == null)
                        errors.Add("Account data is required for account type.");
                }
                else if (groupName == "product")
                {
                    // Validate only product data fields
                    if (account.ProductData == null)
                    {
                        errors.Add("Product data is required for product type.");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(account.ProductData.PkgForm))
                            errors.Add("Pkg Form is required.");
                        if (account.ProductData.Pkg == null)
                            errors.Add("Pkg is required.");
                        if (string.IsNullOrWhiteSpace(account.ProductData.Type))
                            errors.Add("Type is required.");
                    }
                }
            }

            return errors;
        }

        public async Task<List<string>> ValidateLocationAsync(Location location)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(location.LocationName))
                errors.Add("Location name is required.");

            if (location.LocationName?.Length > 100)
                errors.Add("Location name must be at most 100 characters.");

            if (location.Phone?.Length > 20)
                errors.Add("Phone number must be at most 20 characters.");

            if (location.Region?.Length > 100)
                errors.Add("Region must be at most 100 characters.");

            return errors;
        }
    }
}