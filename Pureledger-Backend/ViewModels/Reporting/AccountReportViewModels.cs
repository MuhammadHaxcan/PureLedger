using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Reporting
{
    public class AccountReportViewModels
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
    }

    public class AccountReportResponseViewModel
    {
        public List<AccountReportViewModels> Entries { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }
    public class AccountReportRequestViewModel
    {
        [Required(ErrorMessage = "Project selection is required")]
        public string ProjectId { get; set; } = string.Empty;
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
        public string? BookerId { get; set; }
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 25;
    }
}
