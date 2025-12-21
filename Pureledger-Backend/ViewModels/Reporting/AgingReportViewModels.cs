using System.Globalization;

namespace PureLedger.ViewModels.Reporting
{
    public class AgingReportViewModels
    {
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal DueTillDate { get; set; }
        public decimal TotalDue { get; set; }
    }

    public class AgingReportRequest
    {
        public DateOnly ReferenceDate { get; set; }
        public string? ProjectId { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 25;
    }

    public class AgingReportResponseViewModel
    {
        public List<AgingReportViewModels> Entries { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }
    public class AgingReportPageViewModel
    {
        public List<ProjectViewModel> Projects { get; set; } = new();
        public List<CategoryViewModel> Categories { get; set; } = new();
    }

    public class ProjectViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class CategoryViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}