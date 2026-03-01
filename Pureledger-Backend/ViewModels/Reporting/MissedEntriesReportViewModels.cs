using PureLedger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.Reporting
{
    // Base view model for the Missed Entries Report page
    public class MissedEntriesReportViewModel
    {
        public List<ProjectViewModel> Projects { get; set; } = new List<ProjectViewModel>();
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
    }

    // Request models
    public class MissedEntriesReportViewModels
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ProjectId { get; set; }
        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 100;
        public int? CustomerAccountId { get; set; } // Added to filter by customer
    }

    public class MissedEntriesSummaryRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ProjectId { get; set; }
    }

    // View models for detailed report data
    public class MissedEntriesDetailViewModel
    {
        public List<MissedEntryDetailViewModel> Entries { get; set; } = new();
        public int TotalEntries { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMoreData { get; set; }
    }

    public class MissedEntryDetailViewModel
    {
        public int MissedEntryId { get; set; }
        public string CustomerName { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public DateOnly MissedDate { get; set; }
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Discount { get; set; }
        public string? Description { get; set; }
        public decimal TotalValue { get; set; }
        public int CustomerAccountId { get; set; }
        public int ProductAccountId { get; set; }
        public int ProjectId { get; set; }
    }

    public class MissedEntriesSummaryViewModel
    {
        public int TotalMissedEntries { get; set; }
        public int TotalMissedQuantity { get; set; }
        public decimal TotalMissedValue { get; set; }
        public int UniqueCustomers { get; set; }
        public int UniqueProducts { get; set; }
        public decimal AverageQuantityPerEntry { get; set; }
        public decimal AverageValuePerEntry { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? ProjectId { get; set; }
        public string TopMissedProduct { get; set; } = "";
        public int TopMissedProductQuantity { get; set; }
        public string TopCustomer { get; set; } = "";
        public int TopCustomerMissedQuantity { get; set; }
    }

    public class MissedEntriesByCustomerViewModel
    {
        public int CustomerAccountId { get; set; }
        public string CustomerName { get; set; } = "";
        public int EntryCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int UniqueProductCount { get; set; }
        public decimal AverageQuantityPerEntry { get; set; }
        public decimal PercentOfTotal { get; set; }
        public List<MissedEntryDetailViewModel> CustomerEntries { get; set; } = new();
    }

    public class MissedEntriesByProductViewModel
    {
        public int ProductAccountId { get; set; }
        public string ProductName { get; set; } = "";
        public int EntryCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
        public int UniqueCustomerCount { get; set; }
        public decimal AverageRate { get; set; }
        public decimal AverageDiscount { get; set; }
    }

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

    public class MissedEntriesListViewModel
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? ProjectId { get; set; }
        public List<Project> Projects { get; set; } = new();
        public List<MissedEntryViewModel> Entries { get; set; } = new();
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