public class ModifiedEntriesSummaryViewModel
{
    public int JvId { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool HasModifiedEntries { get; set; }
}

public class VoucherModifiedEntriesViewModel
{
    public int JvId { get; set; }
    public List<VoucherEntryDetailViewModel> Entries { get; set; } = new();
}

public class VoucherEntryDetailViewModel
{
    public int EntryId { get; set; }
    public DateTime? ModifyTime { get; set; }
    public string Category { get; set; } = "";
    public string Account { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public bool IsModified { get; set; }
}