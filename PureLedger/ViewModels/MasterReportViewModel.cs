using System;
using System.Collections.Generic;

namespace PureLedger.ViewModels
{
    public class MasterReportPageViewModel
    {
        public List<ProjectViewModel> Projects { get; set; } = new();
        public List<CategoryViewModel> Categories { get; set; } = new();
    }

    public class MasterReportRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 100;
    }

    public class MasterReportViewModel
    {
        public List<MasterReportEntryViewModel> Entries { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
        
        // Pagination metadata
        public decimal PreviousBalance { get; set; }
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }

    }

    public class MasterReportEntryViewModel
    {
        public int Id { get; set; }  // This is the JournalEntryId
        public int JournalVoucherId { get; set; }
        public string FormattedId { get; set; } = string.Empty; // For x-y-z format
        public DateOnly TransactionDate { get; set; }
        public string? Description { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
    }
}