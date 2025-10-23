using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class AccountReportRequest
    {
        [Required(ErrorMessage = "Project selection is required")]
        public string ProjectId { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
    }
}