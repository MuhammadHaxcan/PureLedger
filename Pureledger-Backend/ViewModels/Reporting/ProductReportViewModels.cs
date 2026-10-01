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
        public bool ExcludeNilProducts { get; set; } = false;
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
        public decimal Balance { get; set; }  // TotalDebit - TotalCredit
        public int TotalDebitQty { get; set; } // Current-period physical stock in, including transfer receipts/returns
        public int TotalCreditQty { get; set; } // Current-period physical stock out, including transfer dispatches
        public int NetQuantity { get; set; } // TotalDebitQty - TotalCreditQty
        public int BalanceQuantity { get; set; } // Sum of available stock across filtered products
        public decimal TotalCOGS { get; set; }
        public decimal TotalMargin { get; set; }
        public decimal MarginPercentage { get; set; }
    }

    public class ProductSummaryViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }

        // Previous balance data (before start date)
        public int PreviousDebitQty { get; set; }
        public int PreviousCreditQty { get; set; }
        public int PreviousBalance { get; set; }
        public decimal PreviousDebit { get; set; }
        public decimal PreviousCredit { get; set; }
        public decimal PreviousAvgRate { get; set; }
        public bool HasPreviousBalance { get; set; }

        // Current period quantity includes transfers; monetary debit/credit excludes
        // transfer base postings but includes product receiving charges.
        public int TotalDebitQty { get; set; }
        public int TotalCreditQty { get; set; }
        public int QtyBalance { get; set; }
        public decimal AverageRate { get; set; }
        public decimal InventoryValue { get; set; }
        public int PhysicalOutwardQuantity { get; set; }
        public int TransferOutwardQuantity { get; set; }
        public int TransferInwardQuantity { get; set; }
        public int InTransitQuantity { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }  // TotalDebit - TotalCredit
        public decimal COGS { get; set; }
        public decimal Margin { get; set; }
        public decimal MarginPercentage { get; set; }
    }

    public class ProductStockBreakdownViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public DateOnly? AsOfDate { get; set; }
        public int TotalQuantity { get; set; }
        public int InTransitQuantity { get; set; }
        public int TotalStockPosition { get; set; }
        public int TransitRouteIssueCount { get; set; }
        public int UnassignedQuantity { get; set; }
        public int AmbiguousQuantity { get; set; }
        public int ReconciliationDifference { get; set; }
        public int AssignmentIssueCount { get; set; }
        public List<ProductStockLocationViewModel> Locations { get; set; } = new();
        public List<ProductTransitRouteViewModel> InTransitRoutes { get; set; } = new();
    }

    public class ProductTransitRouteViewModel
    {
        public int DispatchJournalVoucherId { get; set; }
        public DateOnly DispatchDate { get; set; }
        public int? SourceLocationId { get; set; }
        public string SourceLocationName { get; set; } = string.Empty;
        public int? DestinationLocationId { get; set; }
        public string DestinationLocationName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool HasRouteIssue { get; set; }
    }

    public class ProductTransitMovementDto : ProductTransitRouteViewModel
    {
        public int ProductId { get; set; }
    }

    public class ProductStockLocationViewModel
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class ProductStockLocationMovementDto
    {
        public int JournalEntryId { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public int Quantity { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    /// <summary>
    /// DTO for database-level product aggregation - eliminates loading all entries into memory
    /// </summary>
    public class ProductAggregateDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int PrevDebitQty { get; set; }
        public int PrevCreditQty { get; set; }
        public decimal PrevDebit { get; set; }
        public decimal PrevCredit { get; set; }
        public int CurrentDebitQty { get; set; }
        public int CurrentCreditQty { get; set; }
        public decimal CurrentDebit { get; set; }
        public decimal CurrentCredit { get; set; }
    }
}
