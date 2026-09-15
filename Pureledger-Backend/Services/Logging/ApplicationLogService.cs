using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PureLedger.Data;
using PureLedger.Models;
using PureLedger.Services.Interface;
using PureLedger.ViewModels.Logging;

namespace PureLedger.Services.Logging
{
    public class ApplicationLogService : IApplicationLogService
    {
        private const int MaxTextLength = 12000;
        private readonly IDbContextFactory<LedgerDbContext> _dbContextFactory;
        private readonly ILogger<ApplicationLogService> _logger;

        public ApplicationLogService(
            IDbContextFactory<LedgerDbContext> dbContextFactory,
            ILogger<ApplicationLogService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task LogExceptionAsync(HttpContext context, Exception exception, string errorId, string level, int statusCode, string? safeMessage = null)
        {
            var log = BuildBaseLog(context, errorId, level, "Backend");
            log.ExceptionType = exception.GetType().FullName;
            log.Message = Truncate(exception.Message, MaxTextLength);
            log.StackTrace = Truncate(exception.ToString(), MaxTextLength);
            log.StatusCode = statusCode;
            log.RequestBodySummary = context.Items.TryGetValue("RequestBodySummary", out var summary) ? summary?.ToString() : null;
            log.ExtraPropertiesJson = SerializeExtra(new
            {
                safeMessage,
                traceIdentifier = context.TraceIdentifier
            });

            await SaveLogAsync(log);
        }

        public async Task LogClientErrorAsync(HttpContext context, ClientErrorLogRequest request, string errorId)
        {
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? GetCorrelationId(context)
                : request.CorrelationId.Trim();

            var log = BuildBaseLog(context, errorId, "Error", "Frontend");
            log.CorrelationId = Truncate(correlationId, 128) ?? log.CorrelationId;
            log.ExceptionType = "ClientError";
            log.Message = Truncate(request.Message ?? "Frontend error", MaxTextLength);
            log.StackTrace = Truncate(request.StackTrace, MaxTextLength);
            log.Path = Truncate(request.Path ?? context.Request.Headers.Referer.ToString(), 512);
            log.StatusCode = null;
            log.ExtraPropertiesJson = SanitizeJsonObject(request.ExtraPropertiesJson);

            await SaveLogAsync(log);
        }

        public async Task<(IReadOnlyList<ApplicationErrorLog> Items, int TotalCount)> SearchAsync(ApplicationErrorLogSearchRequest request)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var query = db.ApplicationErrorLogs.AsNoTracking().AsQueryable();

            if (request.FromUtc.HasValue)
            {
                query = query.Where(l => l.CreatedAtUtc >= request.FromUtc.Value.ToUniversalTime());
            }

            if (request.ToUtc.HasValue)
            {
                query = query.Where(l => l.CreatedAtUtc <= request.ToUtc.Value.ToUniversalTime());
            }

            if (!string.IsNullOrWhiteSpace(request.Level))
            {
                query = query.Where(l => l.Level == request.Level.Trim());
            }

            if (request.StatusCode.HasValue)
            {
                query = query.Where(l => l.StatusCode == request.StatusCode.Value);
            }

            if (request.UserId.HasValue)
            {
                query = query.Where(l => l.UserId == request.UserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                var username = request.Username.Trim().ToLower();
                query = query.Where(l => l.Username != null && l.Username.ToLower().Contains(username));
            }

            if (!string.IsNullOrWhiteSpace(request.Path))
            {
                var path = request.Path.Trim().ToLower();
                query = query.Where(l => l.Path != null && l.Path.ToLower().Contains(path));
            }

            if (!string.IsNullOrWhiteSpace(request.ErrorId))
            {
                var errorId = request.ErrorId.Trim();
                query = query.Where(l => l.ErrorId == errorId);
            }

            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                var correlationId = request.CorrelationId.Trim();
                query = query.Where(l => l.CorrelationId == correlationId);
            }

            if (!string.IsNullOrWhiteSpace(request.Source))
            {
                query = query.Where(l => l.Source == request.Source.Trim());
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<ApplicationErrorLog?> GetByErrorIdAsync(string errorId)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            return await db.ApplicationErrorLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ErrorId == errorId);
        }

        private async Task SaveLogAsync(ApplicationErrorLog log)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                db.ApplicationErrorLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist application error log {ErrorId}", log.ErrorId);
            }
        }

        private static ApplicationErrorLog BuildBaseLog(HttpContext context, string errorId, string level, string source)
        {
            var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(userIdClaim, out var userId);

            return new ApplicationErrorLog
            {
                ErrorId = Truncate(errorId, 32) ?? errorId,
                CorrelationId = Truncate(GetCorrelationId(context), 128) ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                Level = Truncate(level, 32) ?? "Error",
                Source = Truncate(source, 64) ?? "Backend",
                Method = Truncate(context.Request.Method, 16),
                Path = Truncate(context.Request.Path.Value, 512),
                UserId = userId > 0 ? userId : null,
                Username = Truncate(context.User?.Identity?.Name, 256),
                IpAddress = Truncate(context.Connection.RemoteIpAddress?.ToString(), 128),
                UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 512)
            };
        }

        private static string GetCorrelationId(HttpContext context)
        {
            return context.Items.TryGetValue("CorrelationId", out var item) && item is string value
                ? value
                : context.TraceIdentifier;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string SerializeExtra(object value)
        {
            return JsonSerializer.Serialize(value);
        }

        private static string? SanitizeJsonObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(SensitiveDataRedactor.Redact(document.RootElement));
            }
            catch
            {
                return JsonSerializer.Serialize(new { raw = SensitiveDataRedactor.RedactText(json) });
            }
        }
    }
}
