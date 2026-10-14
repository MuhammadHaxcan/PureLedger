using System.ComponentModel.DataAnnotations;

namespace PureLedger.ViewModels.StockTransfer;

public class CreateStockTransferRequest
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    [Range(1, int.MaxValue)] public int SourceLocationId { get; set; }
    [Range(1, int.MaxValue)] public int DestinationLocationId { get; set; }
    [Required] public DateOnly TransferDate { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    [Required, MinLength(1)] public List<CreateStockTransferItemRequest> Items { get; set; } = new();
}

public sealed class EditStockTransferRequest : CreateStockTransferRequest
{
    [Required] public string ExpectedRowVersion { get; set; } = string.Empty;
}

public sealed class CreateStockTransferItemRequest
{
    [Range(1, int.MaxValue)] public int ProductAccountId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    // Null uses source carrying cost; an entered price sets destination valuation.
    [Range(typeof(decimal), "0.0001", "9999999999.9999")] public decimal? UnitCost { get; set; }
}

public sealed class ReceiveStockTransferRequest
{
    [Required] public string ExpectedRowVersion { get; set; } = string.Empty;
    [Required] public DateOnly ReceiptDate { get; set; }
    [Required, MinLength(1)] public List<int> SelectedProductAccountIds { get; set; } = new();
    public List<StockTransferChargeRequest> Charges { get; set; } = new();
}

public sealed class StockTransferChargeRequest
{
    [Range(1, int.MaxValue)] public int PayeeAccountId { get; set; }
    [Range(typeof(decimal), "0.0001", "9999999999.9999")] public decimal Amount { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    [Required] public string TargetType { get; set; } = "Products";
    public string AllocationMethod { get; set; } = "ByValue";
    public int? TargetAccountId { get; set; }
    public List<int> SelectedProductAccountIds { get; set; } = new();
    public List<StockTransferChargeAllocationRequest> Allocations { get; set; } = new();
}

public sealed class StockTransferChargeAllocationRequest
{
    [Range(1, int.MaxValue)] public int ProductAccountId { get; set; }
    [Range(typeof(decimal), "0", "9999999999.9999")] public decimal Amount { get; set; }
}

public sealed class StockTransferVersionRequest
{
    [Required] public string ExpectedRowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? Reason { get; set; }
}

public sealed class DeleteStockTransferRequest
{
    [Required] public string ExpectedRowVersion { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}

public sealed class StockTransferSettingsRequest
{
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    [Range(1, int.MaxValue)] public int InTransitCategoryId { get; set; }
    [Range(1, int.MaxValue)] public int InTransitAccountId { get; set; }
}

public sealed class StockTransferListRequest
{
    public string? Search { get; set; }
    public string? StatusGroup { get; set; }
    public int? ProjectId { get; set; }
    public int? LocationId { get; set; }
    public int? CategoryId { get; set; }
    public int? ProductAccountId { get; set; }
    public int? SourceLocationId { get; set; }
    public int? DestinationLocationId { get; set; }
    public int? LinkedJournalVoucherId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public DateOnly? ReceiptFromDate { get; set; }
    public DateOnly? ReceiptToDate { get; set; }
    public DateOnly? AsOfDate { get; set; }
    public string? Status { get; set; }
    public string? ItemState { get; set; }
    public string? TransferNumber { get; set; }
    public bool? HasCharges { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 2500;
}
