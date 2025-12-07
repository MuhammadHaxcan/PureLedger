using System;
using System.Collections.Generic;

namespace PureLedger.ViewModels.Reporting
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
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 50;
    }

    public class ProductReportViewModel
    {
        public List<ProductSummaryViewModel> ProductSummaries { get; set; } = new List<ProductSummaryViewModel>();
        public ProductReportTotalsViewModel Totals { get; set; } = new ProductReportTotalsViewModel();
        
        // Pagination metadata
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }

    public class ProductReportTotalsViewModel
    {
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public int TotalDebitQty { get; set; }
        public int TotalCreditQty { get; set; }
        public int NetQuantity { get; set; }
        public decimal TotalCOGS { get; set; }
        public decimal TotalMargin { get; set; }
        public decimal MarginPercentage { get; set; }
    }

    public class ProductSummaryViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int LastVoucherId { get; set; }

        // Previous balance data (before start date)
        public int PreviousDebitQty { get; set; }
        public int PreviousCreditQty { get; set; }
        public int PreviousBalance { get; set; }
        public decimal PreviousDebit { get; set; }
        public decimal PreviousCredit { get; set; }
        public decimal PreviousAvgRate { get; set; }
        public bool HasPreviousBalance { get; set; }

        // Current period data (within date range)
        public int TotalDebitQty { get; set; }
        public int TotalCreditQty { get; set; }
        public int QtyBalance { get; set; }
        public decimal AverageRate { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal COGS { get; set; }
        public decimal Margin { get; set; }
        public decimal MarginPercentage { get; set; }
    }
}