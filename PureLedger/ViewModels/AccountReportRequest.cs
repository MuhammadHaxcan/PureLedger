using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels
{
    public class AccountReportRequestViewModel
    {
        [Required(ErrorMessage = "Project selection is required")]
        public string ProjectId { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 25;
    }
}