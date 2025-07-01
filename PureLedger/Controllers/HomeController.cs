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

        [HttpGet]
        [ActionName("Entry")]
        public async Task<IActionResult> Entry(int? projectId = null)
        {
            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.SelectedProjectId = projectId;
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
                    .Select(a => new
                    {
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
                .Select(a => new
                {
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
                await _context.SaveChangesAsync();

                foreach (var entry in model.Entries)
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

                    _context.JournalEntries.Add(journalEntry);
                    await _context.SaveChangesAsync(); // Save to get EntryId for the join table

                    // Process tags by ID only
                    if (entry.Tags != null && entry.Tags.Any())
                    {
                        foreach (var tagId in entry.Tags)
                        {
                            // Only process if tagId is a valid positive integer
                            if (tagId > 0)
                            {
                                var journalEntryTag = new JournalEntryTag
                                {
                                    JournalEntryTagId = tagId,
                                    JournalEntryId = journalEntry.EntryId
                                };
                                _context.JournalEntryTags.Add(journalEntryTag);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Journal entries created. Jv-{journalVoucher.JvId}";
                return RedirectToAction(nameof(Entry), new { projectId = model.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving journal entries");
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                ViewBag.Projects = await _context.Projects.ToListAsync();
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> VoucherEntry(int? jvId = null)
        {
            // Get all projects for dropdown
            ViewBag.Projects = await _context.Projects.ToListAsync();

            // If jvId is provided, load that voucher and its entries for summary
            JournalVoucher? voucher = null;
            List<JournalEntry>? entries = null;
            if (jvId != null)
            {
                voucher = await _context.JournalVouchers
                    .Include(jv => jv.JournalEntries)
                        .ThenInclude(e => e.Account)
                    .FirstOrDefaultAsync(jv => jv.JvId == jvId);

                entries = voucher?.JournalEntries.ToList();
            }

            // For new entry, just show empty form and no summary
            var model = new JournalEntryViewModel();

            ViewBag.Voucher = voucher;
            ViewBag.Entries = entries;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> VoucherSummary()
        {
            var vouchers = await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .OrderByDescending(jv => jv.EntryDate)
                .ToListAsync();

            return View(vouchers);
        }

        [HttpGet]
        public async Task<IActionResult> EditVoucher(int jvId)
        {
            var voucher = await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync(jv => jv.JvId == jvId);

            if (voucher == null)
                return NotFound();

            var model = new JournalEntryViewModel
            {
                ProjectId = voucher.ProjectId,
                CreditDays = voucher.CreditableDate.HasValue ? (voucher.CreditableDate.Value.DayNumber - voucher.EntryDate.DayNumber) : 0,
                DueDate = voucher.CreditableDate ?? DateOnly.FromDateTime(DateTime.Now),
                Booker = "", // Set if you have this info
                Entries = voucher.JournalEntries.Select(e => new JournalEntryRowModel
                {
                    CategoryId = e.CategoryId,
                    AccountId = e.AccountId,
                    Des = e.Description,
                    Qty = e.Quantity,
                    Rate = e.Rate,
                    Percent = e.DiscountPercent ?? 0,
                    Disc = e.Discount,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Rbp = e.Rbp,
                    // Tags: If you have tags, load and map them here
                }).ToList()
            };

            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.SelectedProjectId = voucher.ProjectId;
            return View("Entry", model);
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherSummary(int voucherId)
        {
            var voucher = await _context.JournalVouchers
                .Include(jv => jv.JournalEntries)
                    .ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(jv => jv.JvId == voucherId);

            if (voucher == null)
                return Json(new { entries = new List<object>(), jvId = voucherId });

            var entries = voucher.JournalEntries.Select(e => new {
                entryId = e.EntryId,
                accountName = e.Account?.AccountName,
                debit = e.Debit,
                credit = e.Credit
            }).ToList();

            return Json(new { entries, jvId = voucher.JvId });
        }

        [HttpGet]
        public async Task<IActionResult> GetJournalEntry(int entryId)
        {
            var entry = await _context.JournalEntries
                .Include(e => e.Account)
                .Include(e => e.Category)
                .Include(e => e.JournalVoucher)
                .FirstOrDefaultAsync(e => e.EntryId == entryId);

            if (entry == null)
                return NotFound();

            return Json(new
            {
                entryId = entry.EntryId,
                accountName = entry.Account?.AccountName,
                debit = entry.Debit,
                credit = entry.Credit
            });
        }
        [HttpPost]
        public async Task<IActionResult> UpdateJournalEntry([FromForm] int EntryId, [FromForm] decimal Debit, [FromForm] decimal Credit)
        {
            var entry = await _context.JournalEntries.FindAsync(EntryId);
            if (entry == null)
                return Json(new { success = false });

            entry.Debit = Debit;
            entry.Credit = Credit;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
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
        public List<int>? Tags { get; set; } // Now a list of tag IDs
        public string? Des { get; set; }
        public int Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal Percent { get; set; }
        public decimal Disc { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}