using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PureLedger.Services.Interface;
using System.Security.Claims;

namespace PureLedger.Attributes
{
    public class ControllerAccessAttribute : ActionFilterAttribute
    {
        // Define a list of "utility" API endpoints that should be accessible 
        // when a user has access to any part of the controller
        private static readonly Dictionary<string, HashSet<string>> ApiUtilityEndpoints = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetProductDetails",
                "GetJournalEntry",
                "GetVoucherSummary",
                "GetRecipeItems",
                "EntryData", // Allow entry-data endpoint
                "GetEntryData",
                "Entry", // Allow if user has Entry permission
                "entry-data" // Route name
            },
            ["DeliveryChallan"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // These utility endpoints are accessible when user has access to ProcessDC action
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetLocations",
                "GetFilteredEntries",
                "GetVoucherEntries",
                "CheckExistingChallan",
                "GetTransporterCategoryId",
                "CheckForChargesAvailable",
                "PostSingleEntry",
                "ProcessAll",
                "AddCartage",
                "ProcessDCData", // Allow process-dc-data endpoint
                "GetProcessDCData",
                "ProcessDC", // Allow if user has ProcessDC permission
                "process-dc-data" // Route name
            },
            ["Reporting"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetAllBookers",
                "ValidateVoucher",
                "GetAgingReportData",
                "GetAccountReportData",
                "GetMasterReportData",
                "GetProductReportData",
                "GetMissedEntries",
                "GetMissedEntriesData",
                "GetMissedEntriesSummary",
                "GetMissedEntriesByCustomer",
                "GetMissedEntriesByProduct",
                "GetReportData",
                "GetAccountReportInit",
                "GetMasterReportInit",
                "GetProductReportInit",
                "GetMissedEntriesInit",
                "GetProductReportLocations",
                "GetPrintVoucher",
                // Route names (kebab-case)
                "report-data",
                "account-report-init",
                "master-report-init",
                "product-report-init",
                "missed-entries-init",
                "product-report-locations",
                "validate-voucher",
                "print-voucher",
                "categories-by-project",
                "accounts-by-category",
                "bookers"
            },
            ["MetaData"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetCategoriesByProjectForView",
                "GetAllProjectsForCategories",
                "GetAllProjectsForAccounts",
                "GetProjectsForAccounts",
                "GetProjectsForCategories",
                "GetAccountTypes",
                "GetAccountGroups",
                "GetAccountsByCriteria",
                "GetAccountsByCategory",
                "CanDeleteCategory",
                "CanDeleteAccount",
                "ToggleAccountStatusAjax",
                "ToggleBookerStatusAjax",
                "GetProjects",
                "GetProject",
                "GetCategories",
                "GetCategory",
                "GetAccounts",
                "GetAccount",
                "GetLocations",
                "GetLocation",
                "GetBookers",
                "GetBooker",
                // Route names (kebab-case)
                "projects-for-categories",
                "projects-for-accounts",
                "account-types",
                "account-groups",
                "projects",
                "categories",
                "accounts",
                "locations",
                "bookers"
            },
            ["Admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetCategoryForAccount",
                "GetProjectCartageSettingsByProject"
            }
        };

        // Map route action names to permission action names
        private static string NormalizeActionName(string controllerName, string routeActionName)
        {
            if (string.IsNullOrEmpty(routeActionName))
                return routeActionName;
            
            // Remove "Api" suffix from controller name for matching
            var normalizedController = controllerName?.Replace("Api", "") ?? "";
            
            // Map common route patterns and method names to permission action names
            var actionMappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Home"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["entry-data"] = "Entry",
                    ["GetEntryData"] = "Entry",
                    ["EntryData"] = "Entry",
                    ["voucher-entry-data"] = "VoucherEntry",
                    ["GetVoucherEntryData"] = "VoucherEntry",
                    ["VoucherEntryData"] = "VoucherEntry",
                    ["base-vouchers"] = "BaseVouchers",
                    ["GetBaseVouchers"] = "BaseVouchers",
                    ["BaseVouchers"] = "BaseVouchers",
                    ["modified-entries"] = "ModifiedEntries",
                    ["GetModifiedEntries"] = "ModifiedEntries",
                    ["ModifiedEntries"] = "ModifiedEntries",
                    ["recipes"] = "Recipes",
                    ["GetRecipes"] = "Recipes",
                    ["Recipes"] = "Recipes"
                },
                ["DeliveryChallan"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["process-dc-data"] = "ProcessDC",
                    ["GetProcessDCData"] = "ProcessDC",
                    ["ProcessDCData"] = "ProcessDC",
                    ["process-dc"] = "ProcessDC",
                    ["ProcessDC"] = "ProcessDC"
                },
                ["Reporting"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["single-doc"] = "SingleDoc",
                    ["GetSingleDoc"] = "SingleDoc",
                    ["SingleDoc"] = "SingleDoc",
                    ["aging-report"] = "AgingReport",
                    ["GetAgingReport"] = "AgingReport",
                    ["GetAgingReportData"] = "AgingReport",
                    ["AgingReport"] = "AgingReport",
                    ["AgingReportData"] = "AgingReport",
                    ["account-report"] = "AccountReport",
                    ["GetAccountReport"] = "AccountReport",
                    ["GetAccountReportData"] = "AccountReport",
                    ["GetAccountReportInit"] = "AccountReport",
                    ["AccountReport"] = "AccountReport",
                    ["AccountReportData"] = "AccountReport",
                    ["AccountReportInit"] = "AccountReport",
                    ["master-report"] = "MasterReport",
                    ["GetMasterReport"] = "MasterReport",
                    ["GetMasterReportData"] = "MasterReport",
                    ["GetMasterReportInit"] = "MasterReport",
                    ["MasterReport"] = "MasterReport",
                    ["MasterReportData"] = "MasterReport",
                    ["MasterReportInit"] = "MasterReport",
                    ["product-report"] = "ProductReport",
                    ["GetProductReport"] = "ProductReport",
                    ["GetProductReportData"] = "ProductReport",
                    ["GetProductReportInit"] = "ProductReport",
                    ["GetProductReportLocations"] = "ProductReport",
                    ["ProductReport"] = "ProductReport",
                    ["ProductReportData"] = "ProductReport",
                    ["ProductReportInit"] = "ProductReport",
                    ["ProductReportLocations"] = "ProductReport",
                    ["missed-entries"] = "MissedEntriesReport",
                    ["GetMissedEntries"] = "MissedEntriesReport",
                    ["GetMissedEntriesData"] = "MissedEntriesReport",
                    ["GetMissedEntriesInit"] = "MissedEntriesReport",
                    ["GetMissedEntriesSummary"] = "MissedEntriesReport",
                    ["GetMissedEntriesByCustomer"] = "MissedEntriesReport",
                    ["GetMissedEntriesByProduct"] = "MissedEntriesReport",
                    ["MissedEntriesReport"] = "MissedEntriesReport",
                    ["report-data"] = "AgingReport", // Used by AgingReport for initial data
                    ["GetReportData"] = "AgingReport",
                    ["ReportData"] = "AgingReport",
                    ["account-report-init"] = "AccountReport",
                    ["master-report-init"] = "MasterReport",
                    ["product-report-init"] = "ProductReport",
                    ["missed-entries-init"] = "MissedEntriesReport",
                    ["product-report-locations"] = "ProductReport",
                    ["validate-voucher"] = "SingleDoc",
                    ["ValidateVoucher"] = "SingleDoc",
                    ["print-voucher"] = "SingleDoc",
                    ["GetPrintVoucher"] = "SingleDoc",
                    ["categories-by-project"] = "SingleDoc", // Utility endpoint
                    ["GetCategoriesByProject"] = "SingleDoc",
                    ["accounts-by-category"] = "SingleDoc", // Utility endpoint
                    ["GetAccountsByCategory"] = "SingleDoc",
                    ["bookers"] = "SingleDoc", // Utility endpoint
                    ["GetAllBookers"] = "SingleDoc"
                },
                ["MetaData"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["projects"] = "Projects",
                    ["GetProjects"] = "Projects",
                    ["Projects"] = "Projects",
                    ["categories"] = "Categories",
                    ["GetCategories"] = "Categories",
                    ["Categories"] = "Categories",
                    ["accounts"] = "Accounts",
                    ["GetAccounts"] = "Accounts",
                    ["Accounts"] = "Accounts",
                    ["locations"] = "Locations",
                    ["GetLocations"] = "Locations",
                    ["Locations"] = "Locations",
                    ["bookers"] = "Bookers",
                    ["GetBookers"] = "Bookers",
                    ["Bookers"] = "Bookers",
                    ["projects-for-categories"] = "Projects",
                    ["GetAllProjectsForCategories"] = "Projects",
                    ["AllProjectsForCategories"] = "Projects",
                    ["projects-for-accounts"] = "Projects",
                    ["GetAllProjectsForAccounts"] = "Projects",
                    ["AllProjectsForAccounts"] = "Projects",
                    ["account-types"] = "Accounts",
                    ["GetAccountTypes"] = "Accounts",
                    ["AccountTypes"] = "Accounts",
                    ["account-groups"] = "Accounts",
                    ["GetAccountGroups"] = "Accounts",
                    ["AccountGroups"] = "Accounts"
                }
            };

            // Try to find a mapping for this controller and action
            if (actionMappings.TryGetValue(normalizedController, out var mappings) &&
                mappings.TryGetValue(routeActionName, out var mappedAction))
            {
                return mappedAction;
            }

            // If no mapping found and it looks like a method name (starts with Get/Post/Put/Delete), try removing the prefix
            if (routeActionName.Length > 3 && 
                (routeActionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                 routeActionName.StartsWith("Post", StringComparison.OrdinalIgnoreCase) ||
                 routeActionName.StartsWith("Put", StringComparison.OrdinalIgnoreCase) ||
                 routeActionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)))
            {
                // Remove HTTP verb prefix
                var prefixLength = routeActionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? 6 : 3;
                var withoutPrefix = routeActionName.Substring(prefixLength);
                
                // Try mapping again without prefix
                if (mappings != null && mappings.TryGetValue(withoutPrefix, out var mappedWithoutPrefix))
                {
                    return mappedWithoutPrefix;
                }
                
                // If still no match, return without prefix (might match directly)
                return withoutPrefix;
            }

            // If no mapping found, return as-is (might be already in correct format)
            return routeActionName;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get controller and action names from route data
            var controllerName = context.RouteData.Values["controller"]?.ToString();
            var routeActionName = context.RouteData.Values["action"]?.ToString();
            
            // Also try to get action name from action descriptor (method name)
            var actionDescriptor = context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
            var methodActionName = actionDescriptor?.ActionName;
            
            // Get route template from action descriptor to find the actual route path
            var routeTemplate = actionDescriptor?.AttributeRouteInfo?.Template;
            var routePathSegment = "";
            if (!string.IsNullOrEmpty(routeTemplate))
            {
                // Extract the route segment (e.g., "account-groups" from "account-groups")
                routePathSegment = routeTemplate.Split('/').LastOrDefault() ?? "";
            }
            else
            {
                // Fallback: extract from request path
                var requestPath = context.HttpContext.Request.Path.Value ?? "";
                var pathSegments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathSegments.Length >= 3) // /api/ControllerName/action
                {
                    routePathSegment = pathSegments[2]; // Get the action segment
                }
            }
            
            // Normalize controller name (remove "Api" suffix)
            var normalizedControllerName = controllerName?.Replace("Api", "") ?? "";
            
            // Try to normalize action name - use route action name first, fallback to method name
            var actionNameToNormalize = routeActionName ?? methodActionName ?? "";
            var actionName = NormalizeActionName(normalizedControllerName, actionNameToNormalize);
            
            // If normalization didn't change it and we have a method name, try normalizing the method name
            if (actionName == actionNameToNormalize && !string.IsNullOrEmpty(methodActionName) && methodActionName != routeActionName)
            {
                var normalizedFromMethod = NormalizeActionName(normalizedControllerName, methodActionName);
                if (normalizedFromMethod != methodActionName)
                {
                    actionName = normalizedFromMethod;
                }
            }

            // Skip check for Account controller (check both original and normalized)
            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedControllerName, "Account", StringComparison.OrdinalIgnoreCase))
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

            if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(actionName))
            {
                context.Result = new JsonResult(new { success = false, message = "Invalid request." })
                {
                    StatusCode = 400
                };
                return;
            }

            // Get the AuthService from DI
            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

            // Check if this is a utility API endpoint (use normalized controller name)
            // Check both normalized action name and original route/method names
            // Also check the route path segment from the route template
            bool isUtilityEndpoint = ApiUtilityEndpoints.TryGetValue(normalizedControllerName, out var utilityActions) &&
                                    utilityActions != null &&
                                    (utilityActions.Contains(actionName) || 
                                     utilityActions.Contains(routeActionName ?? "") ||
                                     (!string.IsNullOrEmpty(methodActionName) && utilityActions.Contains(methodActionName)) ||
                                     (!string.IsNullOrEmpty(routePathSegment) && utilityActions.Contains(routePathSegment)));

            // Check project access if projectId is in route data or action parameters
            if (context.RouteData.Values.TryGetValue("projectId", out var projectIdObj) ||
                context.ActionArguments.TryGetValue("projectId", out projectIdObj))
            {
                if (projectIdObj != null && int.TryParse(projectIdObj.ToString(), out int projectId) && projectId > 0)
                {
                    bool hasProjectAccess = await authService.HasProjectAccessAsync(userId, projectId);
                    if (!hasProjectAccess)
                    {
                        await DenyAccess(context, isUtilityEndpoint);
                        return;
                    }
                }
            }

            // Check for controller-level access first (use normalized controller name)
            bool hasControllerAccess = await authService.HasControllerAccessAsync(userId, normalizedControllerName);
            if (hasControllerAccess)
            {
                await next();
                return;
            }

            // If no controller-level access, check action-specific access (use normalized names)
            bool hasActionAccess = await authService.HasActionAccessAsync(userId, normalizedControllerName, actionName);
            if (hasActionAccess)
            {
                await next();
                return;
            }

            // Special case for utility endpoints: Check if user has access to ANY action in this controller
            if (isUtilityEndpoint)
            {
                // Check if user has access to any action in current controller (use normalized controller name)
                bool hasAnyActionInController = await HasAccessToAnyActionInControllerAsync(authService, userId, normalizedControllerName);
                
                if (hasAnyActionInController)
                {
                    await next();
                    return;
                }
            }

            // Access denied
            await DenyAccess(context, isUtilityEndpoint);
        }

        private async Task<bool> HasAccessToAnyActionInControllerAsync(IAuthService authService, int userId, string controllerName)
        {
            try
            {
                // Get all user action permissions
                var userActionAccess = await authService.GetUserActionAccessAsync(userId);

                // Check if the user has access to any action in the specified controller
                if (userActionAccess != null && userActionAccess.TryGetValue(controllerName, out var actions))
                {
                    return actions != null && actions.Count > 0;
                }

                return false;
            }
            catch (Exception)
            {
                // In case of any exception, fail safely by denying access
                return false;
            }
        }

        private static Task DenyAccess(ActionExecutingContext context, bool isApiRequest)
        {
            // Always return JSON for API requests (this is a pure API project)
            context.Result = new JsonResult(new { success = false, message = "Access denied. You do not have permission to access this resource." })
            {
                StatusCode = 403
            };

            return Task.CompletedTask;
        }
    }
}