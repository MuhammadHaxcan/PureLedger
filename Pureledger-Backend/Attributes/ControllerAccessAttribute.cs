using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PureLedger.Services.Interface;
using System.Security.Claims;

namespace PureLedger.Attributes
{
    /// <summary>
    /// Simplified controller access attribute that checks user permissions.
    /// 
    /// Access Rules:
    /// 1. Admin users bypass all checks
    /// 2. Account controller is always accessible (for auth)
    /// 3. Global lookup endpoints (locations, bookers, etc.) are accessible to any authenticated user
    /// 4. If user has controller-level access → allow all endpoints
    /// 5. If user has ANY action permission in the controller → allow all GET requests (read-only/utility)
    /// 6. For specific action permissions, map the route to the permission action name
    /// </summary>
    public class ControllerAccessAttribute : ActionFilterAttribute
    {
        // Global lookup endpoints that any authenticated user can access
        // These are common dropdowns/selects needed across multiple features
        private static readonly HashSet<string> GlobalLookupEndpoints = new(StringComparer.OrdinalIgnoreCase)
        {
            // MetaData lookups (also available in HomeApi)
            "bookers",
            "account-types",
            "account-groups",
            "projects-for-categories",
            "projects-for-accounts",
            "categories-by-project",
            "categories-by-project-for-view",
            "accounts-by-criteria",
            "accounts-by-category",
            // Home lookups (needed for entry forms)
            "product-details",
            "voucher-summary",
            "locations",
            // Reporting lookups
            "report-data",
            // DeliveryChallan lookups
            "transporter-category-id",
            "check-for-charges-available"
        };

        // Simple mapping from route patterns to permission action names
        // Key: controller name, Value: dictionary of route-to-permission mappings
        private static readonly Dictionary<string, Dictionary<string, string>> ActionPermissionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = new(StringComparer.OrdinalIgnoreCase)
            {
                // Entry action
                ["entry"] = "Entry",
                ["entry-data"] = "Entry",
                ["journal-entry"] = "Entry",
                // VoucherEntry action
                ["voucher-entry-data"] = "VoucherEntry",
                ["voucher"] = "VoucherEntry",
                ["edit-voucher"] = "VoucherEntry",
                // BaseVouchers action
                ["base-vouchers"] = "BaseVouchers",
                ["base-vouchers-init"] = "BaseVouchers",
                ["process-base-voucher"] = "BaseVouchers",
                ["create-derived-voucher"] = "BaseVouchers",
                ["mark-voucher-as-missed"] = "BaseVouchers",
                // ModifiedEntries action
                ["modified-entries"] = "ModifiedEntries",
                ["modified-entries-init"] = "ModifiedEntries",
                ["voucher-modified-entries"] = "ModifiedEntries",
                // Recipes action
                ["recipes"] = "Recipes",
                ["recipe-items"] = "Recipes",
                ["create-from-recipe"] = "Recipes",
                // Locations lookup (for delivery challan location editing)
                ["locations"] = "Entry"
            },
            ["DeliveryChallan"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["process-dc-data"] = "ProcessDC",
                ["filtered-entries"] = "ProcessDC",
                ["voucher-entries"] = "ProcessDC",
                ["check-existing-challan"] = "ProcessDC",
                ["single-entry"] = "ProcessDC",
                ["process-all"] = "ProcessDC",
                ["add-cartage"] = "ProcessDC",
                ["locations"] = "ProcessDC"
            },
            ["Reporting"] = new(StringComparer.OrdinalIgnoreCase)
            {
                // SingleDoc action
                ["validate-voucher"] = "SingleDoc",
                ["print-voucher"] = "SingleDoc",
                // AgingReport action
                ["report-data"] = "AgingReport",
                ["aging-report"] = "AgingReport",
                // AccountReport action
                ["account-report"] = "AccountReport",
                ["account-report-init"] = "AccountReport",
                // MasterReport action
                ["master-report"] = "MasterReport",
                ["master-report-init"] = "MasterReport",
                // ProductReport action
                ["product-report"] = "ProductReport",
                ["product-report-init"] = "ProductReport",
                ["product-report-locations"] = "ProductReport",
                // MissedEntriesReport action
                ["missed-entries"] = "MissedEntriesReport",
                ["missed-entries-init"] = "MissedEntriesReport",
                ["missed-entries-summary"] = "MissedEntriesReport",
                ["missed-entries-by-customer"] = "MissedEntriesReport",
                ["missed-entries-by-product"] = "MissedEntriesReport",
                ["missed-entries-data"] = "MissedEntriesReport"
            },
            ["MetaData"] = new(StringComparer.OrdinalIgnoreCase)
            {
                // Projects action
                ["projects"] = "Projects",
                // Categories action
                ["categories"] = "Categories",
                ["categories-by-project-for-view"] = "Categories",
                // Accounts action
                ["accounts"] = "Accounts",
                ["accounts-by-criteria"] = "Accounts",
                // Locations action
                ["locations"] = "Locations",
                // Bookers action
                ["bookers"] = "Bookers"
            }
        };

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
            var normalizedControllerName = controllerName.Replace("Api", "", StringComparison.OrdinalIgnoreCase);
            
            // Get the route path to determine the action
            var requestPath = context.HttpContext.Request.Path.Value ?? "";
            var httpMethod = context.HttpContext.Request.Method;

            // Skip check for Account controller (authentication endpoints)
            if (normalizedControllerName.Equals("Account", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            // Admin users bypass all checks
            if (context.HttpContext.User.IsInRole("Admin"))
            {
                await next();
                return;
            }

            // Must be authenticated
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                context.Result = new JsonResult(new { success = false, message = "Unauthorized. Please login." })
                {
                    StatusCode = 401
                };
                return;
            }

            // Get user ID
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                context.Result = new JsonResult(new { success = false, message = "Invalid user session." })
                {
                    StatusCode = 401
                };
                return;
            }

            // Check if this is a global lookup endpoint (accessible to all authenticated users)
            // These are common dropdowns/selects needed across multiple features
            if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && IsGlobalLookupEndpoint(requestPath))
            {
                await next();
                return;
            }

            // Get the AuthService from DI
            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

            // Check project access if projectId is in route or query
            if (context.RouteData.Values.TryGetValue("projectId", out var projectIdObj) ||
                context.ActionArguments.TryGetValue("projectId", out projectIdObj))
            {
                if (projectIdObj != null && int.TryParse(projectIdObj.ToString(), out int projectId) && projectId > 0)
                {
                    if (!await authService.HasProjectAccessAsync(userId, projectId))
                    {
                        DenyAccess(context);
                        return;
                    }
                }
            }

            // Check for controller-level access first
            if (await authService.HasControllerAccessAsync(userId, normalizedControllerName))
            {
                await next();
                return;
            }

            // Check if user has ANY action permission in this controller
            bool hasAnyActionInController = await HasAccessToAnyActionInControllerAsync(authService, userId, normalizedControllerName);
            
            // For GET requests (read-only), allow if user has any action in the controller
            // This covers utility endpoints like categories-by-project, accounts-by-category, etc.
            if (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && hasAnyActionInController)
            {
                await next();
                return;
            }

            // For other requests or if no general access, check specific action permission
            var actionPermission = GetActionPermission(normalizedControllerName, requestPath);
            if (!string.IsNullOrEmpty(actionPermission))
            {
                if (await authService.HasActionAccessAsync(userId, normalizedControllerName, actionPermission))
                {
                    await next();
                    return;
                }
            }

            // If we have any action access and this is a POST/PUT/DELETE, still allow for utility operations
            // that support the main actions (like toggle-status, can-delete checks, etc.)
            if (hasAnyActionInController && IsUtilityOperation(requestPath))
            {
                await next();
                return;
            }

            // Access denied
            DenyAccess(context);
        }

        /// <summary>
        /// Get the permission action name for a given route path
        /// </summary>
        private static string? GetActionPermission(string controllerName, string requestPath)
        {
            if (!ActionPermissionMap.TryGetValue(controllerName, out var controllerMappings))
                return null;

            // Extract the route segment after the controller name
            // e.g., /api/HomeApi/entry-data -> entry-data
            var segments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
                return null;

            // Try to match the route segment (might have parameters after it)
            var routeSegment = segments[2].ToLowerInvariant();
            
            // Direct match
            if (controllerMappings.TryGetValue(routeSegment, out var permission))
                return permission;

            // Try partial match for routes with parameters (e.g., "voucher/123" -> "voucher")
            foreach (var mapping in controllerMappings)
            {
                if (routeSegment.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    return mapping.Value;
            }

            return null;
        }

        /// <summary>
        /// Check if the operation is a utility operation (toggle status, can-delete, etc.)
        /// </summary>
        private static bool IsUtilityOperation(string requestPath)
        {
            var lowerPath = requestPath.ToLowerInvariant();
            return lowerPath.Contains("toggle-status") ||
                   lowerPath.Contains("can-delete") ||
                   lowerPath.Contains("check-");
        }

        /// <summary>
        /// Check if the endpoint is a global lookup endpoint accessible to all authenticated users
        /// </summary>
        private static bool IsGlobalLookupEndpoint(string requestPath)
        {
            var segments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
                return false;

            // Check the third segment (route after controller)
            var routeSegment = segments[2];
            if (GlobalLookupEndpoints.Contains(routeSegment))
                return true;

            // Check for multi-segment routes like "categories/by-project" or "accounts/by-category"
            // These are used in MetaData controller
            if (segments.Length >= 4)
            {
                var combinedRoute = $"{segments[2]}/{segments[3]}";
                var combinedRouteAlt = $"{segments[2]}-{segments[3]}";
                
                // Handle routes like "categories/by-project" matching "categories-by-project"
                if (GlobalLookupEndpoints.Contains(combinedRouteAlt))
                    return true;
                    
                // Also check "by-project", "by-category" pattern in segment 3
                if (segments[3].StartsWith("by-", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static async Task<bool> HasAccessToAnyActionInControllerAsync(IAuthService authService, int userId, string controllerName)
        {
            try
            {
                var userActionAccess = await authService.GetUserActionAccessAsync(userId);
                if (userActionAccess != null && userActionAccess.TryGetValue(controllerName, out var actions))
                {
                    return actions != null && actions.Count > 0;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void DenyAccess(ActionExecutingContext context)
        {
            context.Result = new JsonResult(new { success = false, message = "Access denied. You do not have permission to access this resource." })
            {
                StatusCode = 403
            };
        }
    }
}