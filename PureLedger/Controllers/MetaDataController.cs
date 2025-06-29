using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using System.Threading.Tasks;
using System.Linq;

namespace PureLedger.Controllers
{
    public class MetaDataController : Controller
    {
        private readonly LedgerDbContext _context;

        public MetaDataController(LedgerDbContext context)
        {
            _context = context;
        }

        // ---------------- PROJECT ----------------

        // GET: /MetaData/Project
        [HttpGet]
        public async Task<IActionResult> Project()
        {
            var projects = await _context.Projects.ToListAsync();
            return View(projects);
        }

        // POST: /MetaData/Project
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Project(Project project)
        {
            ModelState.Remove("ProjectId");
            ModelState.Remove("Categories");

            if (ModelState.IsValid)
            {
                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Project));
            }
            var projects = await _context.Projects.ToListAsync();
            return View(projects);
        }

        // GET: /MetaData/EditProject/5
        [HttpGet]
        public async Task<IActionResult> EditProject(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return NotFound();
            return View(project);
        }

        // POST: /MetaData/EditProject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProject(Project project)
        {
            if (ModelState.IsValid)
            {
                _context.Projects.Update(project);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Project));
            }
            return View(project);
        }

        // ---------------- CATEGORY ----------------

        // GET: /MetaData/Category
        [HttpGet]
        public async Task<IActionResult> Category()
        {
            var categories = await _context.Categories
                .Include(c => c.Project)
                .Include(c => c.AccountType)
                .ToListAsync();
            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.AccountTypes = await _context.AccountTypes.ToListAsync();
            return View(categories);
        }

        // POST: /MetaData/Category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Category(Category category)
        {
            ModelState.Remove("CategoryId");
            ModelState.Remove("Accounts");
            ModelState.Remove("Project");
            ModelState.Remove("AccountType");

            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Category));
            }
            var categories = await _context.Categories
                .Include(c => c.Project)
                .Include(c => c.AccountType)
                .ToListAsync();
            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.AccountTypes = await _context.AccountTypes.ToListAsync();
            return View(categories);
        }

        // GET: /MetaData/EditCategory/5
        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.AccountTypes = await _context.AccountTypes.ToListAsync();
            return View(category);
        }

        // POST: /MetaData/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(Category category)
        {
            ModelState.Remove("Accounttype");
            ModelState.Remove("Project");
            ModelState.Remove("Accounts");
            if (ModelState.IsValid)
            {
                _context.Categories.Update(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Category));
            }
            ViewBag.Projects = await _context.Projects.ToListAsync();
            ViewBag.AccountTypes = await _context.AccountTypes.ToListAsync();
            return View(category);
        }

        // ---------------- ACCOUNT ----------------

        // GET: /MetaData/Account
        [HttpGet]
        public async Task<IActionResult> Account()
        {
            var accounts = await _context.Accounts
                .Include(a => a.AccountData)
                .Include(a => a.ProductData)
                .Include(a => a.Category)
                .ThenInclude(c => c.Project)
                .Include(a => a.AccountGroup)
                .ToListAsync();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            ViewBag.AccountGroups = await _context.AccountGroups.ToListAsync();
            ViewBag.Projects = await _context.Projects.ToListAsync();
            return View(accounts);
        }

        // POST: /MetaData/Account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Account(Account account)
        {
            // Remove navigation properties from validation
            ModelState.Remove("Category");
            ModelState.Remove("AccountGroup");
            ModelState.Remove("AccountData.Account");
            ModelState.Remove("ProductData.Account");

            // Get the selected AccountGroup
            var accountGroup = await _context.AccountGroups
                .FirstOrDefaultAsync(g => g.AccountGroupId == account.AccountGroupId);

            if (accountGroup == null)
            {
                ModelState.AddModelError("AccountGroupId", "A valid account group must be selected.");
            }
            else
            {
                var groupName = accountGroup.AccountGroupName?.Trim().ToLower();

                if (groupName == "account")
                {
                    // Validate only account data fields
                    if (string.IsNullOrWhiteSpace(account.AccountName))
                        ModelState.AddModelError("AccountName", "Account Name is required.");
                    if (account.AccountData == null)
                        ModelState.AddModelError("", "Account data is required.");
                    // Optionally validate other account fields as needed

                    // Remove product data if present
                    account.ProductData = null;
                }
                else if (groupName == "product")
                {
                    // Validate only product data fields
                    if (account.ProductData == null)
                        ModelState.AddModelError("", "Product data is required.");
                    else
                    {
                        if (string.IsNullOrWhiteSpace(account.ProductData.PkgForm))
                            ModelState.AddModelError("ProductData.PkgForm", "Pkg Form is required.");
                        if (account.ProductData.Pkg == null)
                            ModelState.AddModelError("ProductData.Pkg", "Pkg is required.");
                        if (string.IsNullOrWhiteSpace(account.ProductData.Type))
                            ModelState.AddModelError("ProductData.Type", "Type is required.");
                    }

                    // Remove account data if present
                    account.AccountData = null;
                }
            }

            if (ModelState.IsValid)
            {
                // Attach nested data if present
                if (account.AccountData != null)
                    account.AccountData.Account = account;
                if (account.ProductData != null)

                    account.ProductData.Account = account;

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Account));
            }

            // Repopulate ViewBags for the view
            var accounts = await _context.Accounts
                .Include(a => a.AccountData)
                .Include(a => a.ProductData)
                .Include(a => a.Category)
                .ThenInclude(c => c.Project)
                .Include(a => a.AccountGroup)
                .ToListAsync();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            ViewBag.AccountGroups = await _context.AccountGroups.ToListAsync();
            ViewBag.Projects = await _context.Projects.ToListAsync();
            return View(accounts);
        }

        // GET: /MetaData/EditAccount/5
        [HttpGet]
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _context.Accounts
                .Include(a => a.AccountData)
                .Include(a => a.ProductData)
                .FirstOrDefaultAsync(a => a.AccountId == id);
            if (account == null) return NotFound();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            ViewBag.AccountGroups = await _context.AccountGroups.ToListAsync();
            return View(account);
        }

        // POST: /MetaData/EditAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(Account account)
        {
            ModelState.Remove("Category");
            ModelState.Remove("AccountGroup");
            ModelState.Remove("AccountData.Account");
            ModelState.Remove("ProductData.Account");

            var accountGroup = await _context.AccountGroups
                .FirstOrDefaultAsync(g => g.AccountGroupId == account.AccountGroupId);

            if (accountGroup == null)
            {
                ModelState.AddModelError("AccountGroupId", "A valid account group must be selected.");
            }
            else
            {
                var groupName = accountGroup.AccountGroupName?.Trim().ToLower();

                if (groupName == "account")
                {
                    if (string.IsNullOrWhiteSpace(account.AccountName))
                        ModelState.AddModelError("AccountName", "Account Name is required.");
                    if (account.AccountData == null)
                        ModelState.AddModelError("", "Account data is required.");
                    account.ProductData = null;
                }
                else if (groupName == "product")
                {
                    if (account.ProductData == null)
                        ModelState.AddModelError("", "Product data is required.");
                    else
                    {
                        if (string.IsNullOrWhiteSpace(account.ProductData.PkgForm))
                            ModelState.AddModelError("ProductData.PkgForm", "Pkg Form is required.");
                        if (account.ProductData.Pkg == null)
                            ModelState.AddModelError("ProductData.Pkg", "Pkg is required.");
                        if (string.IsNullOrWhiteSpace(account.ProductData.Type))
                            ModelState.AddModelError("ProductData.Type", "Type is required.");
                    }
                    account.AccountData = null;
                }
            }

            if (ModelState.IsValid)
            {
                // Fetch the existing account and its data
                var existingAccount = await _context.Accounts
                    .Include(a => a.AccountData)
                    .Include(a => a.ProductData)
                    .FirstOrDefaultAsync(a => a.AccountId == account.AccountId);

                if (existingAccount == null)
                    return NotFound();

                // Update scalar properties
                existingAccount.AccountName = account.AccountName;
                existingAccount.AccountGroupId = account.AccountGroupId;
                existingAccount.CategoryId = account.CategoryId;

                // Update AccountData or ProductData as appropriate
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
                    // Remove ProductData if switching from product to account
                    if (existingAccount.ProductData != null)
                        _context.ProductDatas.Remove(existingAccount.ProductData);
                }
                else if (account.ProductData != null)
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
                    // Remove AccountData if switching from account to product
                    if (existingAccount.AccountData != null)
                        _context.AccountDatas.Remove(existingAccount.AccountData);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Account));
            }

            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            ViewBag.AccountGroups = await _context.AccountGroups.ToListAsync();
            return View(account);
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriesByProject(int projectId)
        {
            var categories = await _context.Categories
                .Where(c => c.ProjectId == projectId)
                .Select(c => new { categoryId = c.CategoryId, categoryName = c.CategoryName })
                .ToListAsync();
            return Json(categories);
        }
    }
}