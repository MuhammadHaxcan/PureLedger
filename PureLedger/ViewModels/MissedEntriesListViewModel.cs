using System.ComponentModel.DataAnnotations;
using PureLedger.Models;

namespace PureLedger.ViewModels
{
    public class MissedEntriesListViewModel
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? ProjectId { get; set; }
        public List<Project> Projects { get; set; } = new();
        public List<MissedEntryViewModel> Entries { get; set; } = new();
    }

    public class MissedEntryViewModel
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
        public decimal Total => (Quantity * Rate) - Discount;
    }
}