using System;
using System.Collections.Generic;

namespace PureLedger.ViewModels.Reporting
{
    public class OldReportLookupOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class OldMasterReportRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
        public string? BookerId { get; set; }
        public string? LocationId { get; set; }
        public bool ShowQtyBalance { get; set; } = false;
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 2500;

        // Legacy aliases kept for compatibility with older clients.
        public string? ProjectName { get; set; }
        public string? CategoryName { get; set; }
        public string? AccountName { get; set; }
        public string? BookerName { get; set; }
        public string? Location { get; set; }
    }

    public class OldMasterReportResponse
    {
        public List<OldMasterReportEntryViewModel> Entries { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
        public decimal PreviousBalance { get; set; }
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }

    public class OldMasterReportEntryViewModel
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public int? JournalVoucherId { get; set; }
        public string FormattedId { get; set; } = string.Empty;
        public DateOnly TransactionDate { get; set; }
        public DateOnly CreditableDate { get; set; }
        public string? Description { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? AccountId { get; set; }
        public string? AccountName { get; set; }
        public int Quantity { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
        public decimal Balance { get; set; }
        public string? LocationName { get; set; }
        public int? BookerId { get; set; }
        public string? BookerName { get; set; }
        public int QtyBalance { get; set; }

        // Legacy aliases kept for compatibility with older clients.
        public DateOnly EntryDate => TransactionDate;
        public int Qty => Quantity;
        public string? Location => LocationName;
    }

    public class OldMasterReportInitResponse
    {
        public List<OldReportLookupOption> Projects { get; set; } = new();
        public List<OldReportLookupOption> Categories { get; set; } = new();
        public List<OldReportLookupOption> Accounts { get; set; } = new();
        public List<OldReportLookupOption> Bookers { get; set; } = new();
        public List<OldReportLookupOption> Locations { get; set; } = new();

        // Legacy aliases kept for compatibility with older clients.
        public List<string> ProjectNames { get; set; } = new();
        public List<string> CategoryNames { get; set; } = new();
        public List<string> BookerNames { get; set; } = new();
        public List<string> LocationsLegacy { get; set; } = new();
        public List<string> AccountIds { get; set; } = new();
    }
}
