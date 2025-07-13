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
                return RedirectToAction("SingleDocView");
            }

            var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type);
            if (report == null)
            {
                TempData["Error"] = type == "DC"
            ? $"No Delivery Challan exists for Voucher ID {journalVoucherId}."
            : $"No Journal Voucher exists for Voucher ID {journalVoucherId}.";
                return RedirectToAction("SingleDoc");
            }

            ViewBag.Type = type;
            return View("PrintVoucher", report); // This must match the .cshtml file name
        }

        [HttpGet]
        public async Task<IActionResult> ValidateVoucher(int journalVoucherId, string type)
        {
            var report = await _reportingService.GetSingleDocReportAsync(journalVoucherId, type);
            if (report == null)
            {
                return Json(new { exists = false });
            }

            return Json(new { exists = true });
        }


    }
}
