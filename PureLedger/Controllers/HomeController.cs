using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using System.Diagnostics;

namespace PureLedger.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly LedgerDbContext _context;


        public HomeController(LedgerDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // GET: /Home/Entry
        [HttpGet]
        public async Task<IActionResult> Entry()
        {
            ViewBag.Projects = await _context.Projects.ToListAsync();
            return View();
        }

        // AJAX: Get categories for a project
        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            var categories = await _context.Categories
                .Where(c => c.ProjectId == projectId)
                .Select(c => new { c.CategoryId, c.CategoryName })
                .ToListAsync();
            return Json(categories);
        }

        // AJAX: Get accounts for a category
        [HttpGet]
        public async Task<IActionResult> GetAccountsByCategory(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.AccountType)
                    .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

                var accounts = await _context.Accounts
                    .Where(a => a.CategoryId == categoryId)
                    .Select(a => new {
                        a.AccountId,
                        a.AccountName,
                        a.ProductData.Type,
                        AccountTypeName = category != null ? category.AccountType.AccountTypeName : null
                    })
                    .ToListAsync();

                return Json(accounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts for category {CategoryId}", categoryId);
                return Json(new List<object>());
            }
        }

        // AJAX: Get product details for an account
        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int accountId)
        {
            var productData = await _context.Accounts
                .Where(a => a.AccountId == accountId)
                .Select(a => new {
                    a.ProductData.SaleRate,
                    a.ProductData.Pkg,
                    a.ProductData.PkgForm,
                    a.ProductData.Type
                })
                .FirstOrDefaultAsync();

            return Json(productData);
        }

        [HttpPost]
        public async Task<IActionResult> Entry(JournalEntryViewModel model)
        {
            // Remove unnecessary validations for optional fields
            foreach (var entry in model.Entries)
            {
                // Optional fields in JournalEntryRowModel that may trigger validation errors
                ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Type");
                ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Tags");
                ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Des");
                ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Rbp");

                // Get the account to determine its group
                var account = await _context.Accounts
                    .Include(a => a.AccountGroup)
                    .FirstOrDefaultAsync(a => a.AccountId == entry.AccountId);

                if (account != null)
                {
                    // If the account group is customer, remove rate, discount, and quantity validations
                    if (account.AccountGroup?.AccountGroupName?.ToLower() == "account")
                    {
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Rate");
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Disc");
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Percent");
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Qty");
                    }
                    // If the account group is product, only remove discount validation as it's optional
                    else if (account.AccountGroup?.AccountGroupName?.ToLower() == "product")
                    {
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Percent");
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Disc");
                    }
                }

                // Remove validation for fields that might be conditionally required
                // (e.g., if only one of Debit or Credit should be filled)
                if (entry.Debit > 0 || entry.Credit > 0)
                {
                    if (entry.Debit > 0)
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Credit");
                    else if (entry.Credit > 0)
                        ModelState.Remove($"Entries[{model.Entries.IndexOf(entry)}].Debit");
                }
            }

            // Remove validations for optional parent fields
            ModelState.Remove("Booker");
            ModelState.Remove("DueDate");
            ModelState.Remove("CreditDays");


            if (!ModelState.IsValid)
            {
                ViewBag.Projects = await _context.Projects.ToListAsync();
                ViewBag.Error = "Please correct the validation errors.";
                return View(model);
            }

            try
            {
                // Create journal voucher
                var journalVoucher = new JournalVoucher
                {
                    ProjectId = model.ProjectId,
                    EntryDate = DateOnly.FromDateTime(DateTime.Now),
                    IsProcessed = false,
                    CreditableDate = DateOnly.FromDateTime(DateTime.Now.AddDays(model.CreditDays))
                };

                _context.JournalVouchers.Add(journalVoucher);
                await _context.SaveChangesAsync(); // Save to get the JvId

                // Process each entry row
                foreach (var entry  in model.Entries)
                {
                    var journalEntry = new JournalEntry
                    {
                        JournalVoucherId = journalVoucher.JvId,
                        AccountId = entry.AccountId,
                        CategoryId = entry.CategoryId,
                        Description = entry.Des,
                        Quantity = entry.Qty,
                        Rate = entry.Rate,
                        DiscountPercent = entry.Percent,
                        Discount = entry.Disc,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        Rbp = entry.Rbp
                    };

                    // Process tags if provided
                    if (!string.IsNullOrWhiteSpace(entry.Tags))
                    {
                        // Check if tag exists, if not create it
                        var tagName = entry.Tags.Trim();
                        var existingTag = await _context.Tags
                            .FirstOrDefaultAsync(t => t.TagName == tagName);

                        if (existingTag != null)
                        {
                            journalEntry.TagId = existingTag.TagId;
                        }
                        else
                        {
                            var newTag = new Tag { TagName = tagName };
                            _context.Tags.Add(newTag);
                            await _context.SaveChangesAsync();
                            journalEntry.TagId = newTag.TagId;
                        }
                    }

                    _context.JournalEntries.Add(journalEntry);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Journal entries created successfully.";
                return RedirectToAction(nameof(Entry));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                ViewBag.Projects = await _context.Projects.ToListAsync();
                return View(model);
            }
        }
    }

    // View models for the Entry form
    public class JournalEntryViewModel
    {
        public int ProjectId { get; set; }
        public string? Booker { get; set; }
        public int CreditDays { get; set; }
        public DateOnly DueDate { get; set; }
        public List<JournalEntryRowModel> Entries { get; set; } = new List<JournalEntryRowModel>();
    }

    public class JournalEntryRowModel
    {
        public int CategoryId { get; set; }
        public int AccountId { get; set; }
        public string? Type { get; set; }
        public string? Rbp { get; set; } = "Y";
        public string? Tags { get; set; }
        public string? Des { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal Percent { get; set; }
        public decimal Disc { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}