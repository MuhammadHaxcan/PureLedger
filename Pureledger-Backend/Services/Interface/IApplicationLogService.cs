using PureLedger.Models;
using PureLedger.ViewModels.Logging;

namespace PureLedger.Services.Interface
{
    public interface IApplicationLogService
    {
        Task LogExceptionAsync(HttpContext context, Exception exception, string errorId, string level, int statusCode, string? safeMessage = null);
        Task LogClientErrorAsync(HttpContext context, ClientErrorLogRequest request, string errorId);
        Task<(IReadOnlyList<ApplicationErrorLog> Items, int TotalCount)> SearchAsync(ApplicationErrorLogSearchRequest request);
        Task<ApplicationErrorLog?> GetByErrorIdAsync(string errorId);
    }
}
