using PureLedger.ViewModels.StockTransfer;

namespace PureLedger.Services.Interface;

public interface IStockTransferService
{
    Task<object> GetInitAsync(int userId, bool isAdmin);
    Task<object> GetCategoriesAsync(int projectId, int? accountGroupId, int userId, bool isAdmin);
    Task<object> GetAccountsAsync(int categoryId, int? accountGroupId, int userId, bool isAdmin);
    Task<object> GetProjectAccountsAsync(int projectId, int accountGroupId, int userId, bool isAdmin);
    Task<object> GetAvailabilityAsync(int projectId, int sourceLocationId, int productAccountId,
        DateOnly? asOfDate, int? excludeDispatchJvId, int userId, bool isAdmin);
    Task<object> GetSettingsAsync(int projectId, int userId, bool isAdmin);
    Task<object> SaveSettingsAsync(StockTransferSettingsRequest request, int userId, bool isAdmin);
    Task<object> CreateAsync(CreateStockTransferRequest request, int userId, bool isAdmin);
    Task<object> EditAsync(int dispatchJvId, EditStockTransferRequest request, int userId, bool isAdmin);
    Task<object> GetListAsync(StockTransferListRequest request, int userId, bool isAdmin);
    Task<object?> GetDetailsAsync(int dispatchJvId, int userId, bool isAdmin);
    Task<object> ReceiveAsync(int dispatchJvId, ReceiveStockTransferRequest request, int userId, bool isAdmin);
    Task<object> CancelRemainingAsync(int dispatchJvId, StockTransferVersionRequest request, int userId, bool isAdmin);
    Task<object> DeleteAsync(int anyLinkedJvId, DeleteStockTransferRequest request, int userId, bool isAdmin);
}
