using Microsoft.EntityFrameworkCore;
using PureLedger.Controllers;
using PureLedger.Data;
using PureLedger.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PureLedger.ViewModels.Home;

namespace PureLedger.Services
{
    public class JournalEntryValidationService
    {
        private readonly TenantDbContext _context;
        private readonly ILogger<JournalEntryValidationService> _logger;

        public JournalEntryValidationService(TenantDbContext context, ILogger<JournalEntryValidationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Validates a journal entry model based on business rules
        /// </summary>
        /// <param name="model">The journal entry view model to validate</param>
        /// <returns>List of validation error messages; empty if validation passes</returns>
        public async Task<List<string>> ValidateAsync(JournalEntryViewModel model)
        {
            var errors = new List<string>();

            // Validate required fields at the parent level
            if (model.ProjectId <= 0)
                errors.Add("Project is required.");

            // Optional fields at the parent level are allowed to be null/empty
            // - Booker is optional
            // - DueDate is set automatically based on CreditDays
            // - CreditDays defaults to 0

            // Validate that there's at least one entry
            if (model.Entries == null || !model.Entries.Any())
            {
                errors.Add("At least one entry is required.");
                return errors; // Exit early if no entries to validate
            }

            // Track if entries are balanced (total debit = total credit)
            decimal totalDebit = 0;
            decimal totalCredit = 0;

            // Validate each entry
            for (int i = 0; i < model.Entries.Count; i++)
            {
                var entry = model.Entries[i];
                var entryPrefix = $"Entry {i + 1}: ";

                // Required fields in all cases
                if (entry.CategoryId <= 0)
                    errors.Add($"{entryPrefix}Category is required.");

                if (entry.AccountId <= 0)
                    errors.Add($"{entryPrefix}Account is required.");

                // Skip detailed validation if required fields are missing
                if (entry.AccountId <= 0 || entry.CategoryId <= 0)
                    continue;

                // Debit/Credit validation - either one must be filled, but not both
                if (entry.Debit <= 0 && entry.Credit <= 0)
                    errors.Add($"{entryPrefix}Either Debit or Credit must be filled.");
                else if (entry.Debit > 0 && entry.Credit > 0)
                    errors.Add($"{entryPrefix}Only one of Debit or Credit can be filled, not both.");

                // Track totals for balance validation
                totalDebit += entry.Debit ?? 0;
                totalCredit += entry.Credit ?? 0;

                try
                {
                    // Get the account to determine its group for conditional validations
                    var account = await _context.Accounts
                        .Include(a => a.AccountGroup)
                        .FirstOrDefaultAsync(a => a.AccountId == entry.AccountId);

                    if (account == null)
                    {
                        errors.Add($"{entryPrefix}Account not found.");
                        continue;
                    }

                    var group = account.AccountGroup?.AccountGroupName?.ToLower();

                    // Apply business rules based on account group
                    if (group == "account")
                    {
                        // For "account" group, rate, discount, percent, qty are optional
                        // No validation needed as they're optional
                    }
                    else if (group == "product")
                    {
                        // For "product" group, percent and disc are optional
                        // But rate and qty must be valid
                        if (entry.Rate <= 0)
                            errors.Add($"{entryPrefix}Rate must be greater than zero for product accounts.");

                        if (entry.Qty <= 0)
                            errors.Add($"{entryPrefix}Quantity must be greater than zero for product accounts.");

                        if (entry.Rbp == null)
                            errors.Add($"{entryPrefix}The Rbp must be Y or N");
                    }
                    // Other account groups can have their own rules here
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating account for entry at index {Index}", i);
                    errors.Add($"{entryPrefix}Error validating account details.");
                }

            }

            // Check if debits and credits are balanced
            if (totalDebit != totalCredit)
            {
                errors.Add($"Debits {totalDebit} and credits {totalCredit} must be equal. Difference: {Math.Abs(totalDebit - totalCredit)}");
            }

            return errors;
        }
    }
}