using System;
using System.Collections.Generic;

namespace PureLedger.ViewModels
{
    public class DeliveryChallanFilterViewModel
    {
        public DateOnly? SinceDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int? ProjectId { get; set; }
        public int? CategoryId { get; set; }
        public int? AccountId { get; set; }
        
        public List<JournalVoucherViewModel> Vouchers { get; set; } = new List<JournalVoucherViewModel>();
    }

    public class JournalVoucherViewModel
    {
        public int JvId { get; set; }
        public string ProjectName { get; set; }
        public DateOnly EntryDate { get; set; }
        public bool IsProcessed { get; set; }
        public List<JournalEntryDetailViewModel> Entries { get; set; } = new List<JournalEntryDetailViewModel>();
    }

    public class JournalEntryDetailViewModel
    {
        public int EntryId { get; set; }
        public int JournalVoucherId { get; set; }
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public bool IsProcessed { get; set; }
    }
}