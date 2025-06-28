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
                .Include(a => a.Category)
                .ThenInclude(c => c.Project)
                .ToListAsync();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            return View(accounts);
        }

        // POST: /MetaData/Account
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Account(Account account)
        {
            ModelState.Remove("AccountId");
            ModelState.Remove("Category");
            if (ModelState.IsValid)
            {
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Account));
            }
            var accounts = await _context.Accounts
                .Include(a => a.Category)
                .ThenInclude(c => c.Project)
                .ToListAsync();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            return View(accounts);
        }

        // GET: /MetaData/EditAccount/5
        [HttpGet]
        public async Task<IActionResult> EditAccount(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            return View(account);
        }

        // POST: /MetaData/EditAccount/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAccount(Account account)
        {
            ModelState.Remove("Category");
            if (ModelState.IsValid)
            {
                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Account));
            }
            ViewBag.Categories = await _context.Categories
                .Include(c => c.Project)
                .ToListAsync();
            return View(account);
        }
    }
}