using PureLedger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Reporting
{
    public class MissedEntryViewModel
    {
        public int MissedEntryId { get; set; }
        public string CustomerName { get; set; } = "Unknown Customer";
        public string ProductName { get; set; } = "Unknown Product";
        public string ProjectName { get; set; } = "Unknown Project";
        public DateOnly MissedDate { get; set; }
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Discount { get; set; }
        public string Rbp { get; set; } = "Y";
        public string? Description { get; set; }
        public decimal TotalValue { get; set; }
        public int CustomerAccountId { get; set; }
        public int ProductAccountId { get; set; }
        public int ProjectId { get; set; }
    }

    public class MissedEntriesPagedResultViewModel
    {
        public List<MissedEntryViewModel> Entries { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }
    public class MissedCustomerGroupDto
    {
        public int CustomerAccountId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int UniqueProductCount { get; set; }
        public decimal AverageQuantityPerEntry { get; set; }
    }

    public class MissedProductGroupDto
    {
        public int ProductAccountId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int UniqueCustomerCount { get; set; }
        public decimal AverageRate { get; set; }
        public decimal AverageDiscount { get; set; }
    }
}