using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;
using System.Threading.Tasks;

namespace PureLedger.Controllers
{
    public class ReportingController : Controller
    {
        private readonly IReportingService _reportingService;

        public ReportingController(IReportingService reportingService)
        {
            _reportingService = reportingService;
        }

        [HttpGet]
        public IActionResult SingleDoc()
        {
            return View("SingleDoc");
        }

        [HttpGet]
        public async Task<IActionResult> PrintVoucher(int journalVoucherId, string type)
        {
            if (string.IsNullOrEmpty(type) || journalVoucherId <= 0)
            {
                TempData["Error"] = "Invalid input.";
                return RedirectToAction("SingleDoc");
            }

            var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type);
            if (report == null)
            {
                TempData["Error"] = type == "DC"
                    ? "No Delivery Challan exists for this Voucher ID."
                    : "Voucher not found.";
                return RedirectToAction("SingleDoc");
            }

            ViewBag.Type = type;
            return View("PrintVoucher", report); // This must match the .cshtml file name
        }

    }
}
