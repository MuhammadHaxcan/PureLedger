using System;

namespace PureLedger.Models
{
    public class LegacyJournalEntry
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? Rbp { get; set; }
        public string? PkgType { get; set; }
        public DateOnly? EntryDate { get; set; }
        public string? DefaultTags { get; set; }
        public string? Tags { get; set; }
        public string? Des { get; set; }
        public string? Pp2 { get; set; }
        public string? Nic { get; set; }
        public string? Contact { get; set; }
        public string? Enom { get; set; }
        public int? Qty { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
        public string? Dcn { get; set; }
        public string? Location { get; set; }
        public string? DcRemarks { get; set; }
        public string? Dc { get; set; }
        public string? Station { get; set; }
        public string? Otherref { get; set; }
        public DateOnly? DcDate { get; set; }
        public string? CartageRef { get; set; }
        public string EntryStatus { get; set; } = "jv";
        public string? EntryType { get; set; }
        public decimal? Fcrate { get; set; }
        public decimal? Roe { get; set; }
        public decimal? FcAmount { get; set; }
        public int? Discount { get; set; }
        public decimal? Disper { get; set; }
        public decimal? Cas { get; set; }
        public decimal? Nm { get; set; }
        public decimal? Tpc { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EModify { get; set; }
        public DateTime? Modifytime { get; set; }
        public DateOnly? DueDate { get; set; }
        public int? BookerId { get; set; }
        public string? BookerName { get; set; }
    }
}
