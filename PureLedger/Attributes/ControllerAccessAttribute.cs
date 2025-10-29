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
                "GetMissedEntries",
                "GetRecipeItems"
            },
            ["DeliveryChallan"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetLocations",
                "GetFilteredEntries",
                "GetVoucherEntries",
                "CheckExistingChallan",
                "GetTransporterCategoryId",
                "CheckForChargesAvailable"
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
                "GetMissedEntriesData",
                "GetMissedEntriesSummary",
                "GetMissedEntriesByCustomer",
                "GetMissedEntriesByProduct"
            },
            ["MetaData"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetCategoriesByProjectForView",
                "GetAllProjectsForCategories",
                "GetAllProjectsForAccounts",
                "GetAccountsByCriteria",
                "CanDeleteCategory",
                "CanDeleteAccount",
                "ToggleAccountStatusAjax",
                "ToggleBookerStatusAjax"
            },
            ["Admin"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GetCategoriesByProject",
                "GetAccountsByCategory",
                "GetCategoryForAccount",
                "GetProjectCartageSettingsByProject"
            }
        };

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get controller and action names
            var controllerName = context.RouteData.Values["controller"]?.ToString();
            var actionName = context.RouteData.Values["action"]?.ToString();

            // Skip check for Account controller
            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase))
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
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            // Get user ID
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            if (string.IsNullOrEmpty(controllerName) || string.IsNullOrEmpty(actionName))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            // Get the AuthService from DI
            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

            // Check if this is a utility API endpoint
            bool isUtilityEndpoint = ApiUtilityEndpoints.TryGetValue(controllerName, out var utilityActions) &&
                                    utilityActions != null &&
                                    utilityActions.Contains(actionName);

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

            // Check for controller-level access first
            bool hasControllerAccess = await authService.HasControllerAccessAsync(userId, controllerName);
            if (hasControllerAccess)
            {
                await next();
                return;
            }

            // If no controller-level access, check action-specific access
            bool hasActionAccess = await authService.HasActionAccessAsync(userId, controllerName, actionName);
            if (hasActionAccess)
            {
                await next();
                return;
            }

            // Special case for utility endpoints: Check if user has access to ANY action in this controller
            if (isUtilityEndpoint)
            {
                // Check if user has access to any action in current controller
                bool hasAnyActionInController = await HasAccessToAnyActionInControllerAsync(authService, userId, controllerName);
                
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
            // Check if this is an AJAX/API request
            bool isAjax = string.Equals(context.HttpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
                         isApiRequest ||
                         context.HttpContext.Request.Path.ToString().Contains("/api/", StringComparison.OrdinalIgnoreCase);

            if (isAjax)
            {
                // For API requests, return 403 Forbidden status with JSON
                context.Result = new JsonResult(new { error = "Access denied" })
                {
                    StatusCode = 403
                };
            }
            else
            {
                // For regular requests, redirect to access denied page
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }

            return Task.CompletedTask;
        }
    }
}