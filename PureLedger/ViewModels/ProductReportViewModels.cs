using System;
using System.Collections.Generic;

namespace PureLedger.ViewModels
{
    public class ProductReportPageViewModel
    {
        public List<ProjectViewModel> Projects { get; set; } = new List<ProjectViewModel>();
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
        public List<LocationViewModel> Locations { get; set; } = new List<LocationViewModel>();
    }

    public class LocationViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class ProductReportRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public string? CategoryId { get; set; }
        public string? AccountId { get; set; }
        public string? LocationId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ProductReportViewModel
    {
        public List<ProductReportEntryViewModel> Entries { get; set; } = new List<ProductReportEntryViewModel>();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal NetBalance { get; set; }
        public int TotalQuantity { get; set; }
        public int TotalDebitQty { get; set; }
        public int TotalCreditQty { get; set; }
        public int NetQuantity { get; set; }
        // Pagination metadata
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
        public decimal TotalCOGS { get; set; }
        public decimal TotalMargin { get; set; }
    }

    public class ProductReportEntryViewModel
    {
        public int Id { get; set; }
        public string EntryDate { get; set; }
        public string DueDate { get; set; }
        public string Description { get; set; }
        public string AccountName { get; set; }
        public int Quantity { get; set; }
        public int QtyBalance { get; set; }
        public decimal Rate { get; set; }
        public int DebitQty { get; set; }
        public int CreditQty { get; set; }
        public int QuantityBalance { get; set; }
        public decimal AvgRate { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }
        public decimal COGS { get; set; }
        public decimal Margin { get; set; }
        public string LocationName { get; set; }
    }
}