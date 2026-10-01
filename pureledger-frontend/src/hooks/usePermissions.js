import { useAuth } from '../contexts/AuthContext';
import { PERMISSIONS, PAGE_PERMISSIONS, ACTION_PERMISSIONS } from '../constants/permissions';

/**
 * Custom hook for permission checking throughout the app
 * Provides convenient methods to check various permission combinations
 */
export function usePermissions() {
    const { permissions, hasPermission, hasAnyPermission, userAccess, user } = useAuth();

    /**
     * Check if user can access a specific page by path
     * @param {string} path - The route path (e.g., '/home/entry')
     * @returns {boolean}
     */
    const canAccessPage = (path) => {
        const requiredPermissions = PAGE_PERMISSIONS[path];
        if (!requiredPermissions || requiredPermissions.length === 0) {
            // If no permission required, allow access
            return true;
        }

        // Admin users always have access
        if (user?.isAdmin) {
            return true;
        }

        // If permissions array is ready, use it
        if (permissions && permissions.length > 0) {
            return hasAnyPermission(requiredPermissions);
        }

        // If permissions aren't ready yet but we have userAccess, check directly
        // This handles the case on page reload where permissions array hasn't been built yet
        if (userAccess && (userAccess.controllers || userAccess.actions)) {
            // Extract controller and action from permission (e.g., "Home.Entry" -> "Home", "Entry")
            return requiredPermissions.some(perm => {
                const [controller, action] = perm.split('.');
                
                // Check controller-level access
                if (userAccess.controllers && userAccess.controllers.includes(controller)) {
                    return true;
                }
                
                // Check action-level access
                if (userAccess.actions && userAccess.actions[controller]) {
                    return userAccess.actions[controller].includes(action);
                }
                
                return false;
            });
        }

        // If we don't have userAccess data yet, deny access (safer)
        return false;
    };

    /**
     * Check if user can access a specific page by permission name
     * @param {string} permissionName - The permission name from PERMISSIONS
     * @returns {boolean}
     */
    const canAccessPageByPermission = (permissionName) => {
        return hasPermission(permissionName);
    };

    /**
     * Check if user can perform a specific action
     * @param {string} actionName - The action name from ACTION_PERMISSIONS
     * @returns {boolean}
     */
    const canPerformAction = (actionName) => {
        const requiredPermissions = ACTION_PERMISSIONS[actionName];
        if (!requiredPermissions || requiredPermissions.length === 0) return true;
        return hasAnyPermission(requiredPermissions);
    };

    // Home page permissions
    const canAccessHomeEntry = hasPermission(PERMISSIONS.HOME_ENTRY);
    const canAccessVoucherEntry = hasPermission(PERMISSIONS.HOME_VOUCHER_ENTRY);
    const canAccessBaseVouchers = hasPermission(PERMISSIONS.HOME_BASE_VOUCHERS);
    const canAccessModifiedEntries = hasPermission(PERMISSIONS.HOME_MODIFIED_ENTRIES);
    const canAccessRecipes = hasPermission(PERMISSIONS.HOME_RECIPES);
    const canAccessPendingApprovals = hasPermission(PERMISSIONS.HOME_PENDING_APPROVALS);
    const canAccessPaymentAndReceipt = hasPermission(PERMISSIONS.HOME_PAYMENT_AND_RECEIPT);

    // MetaData page permissions
    const canAccessProjects = hasPermission(PERMISSIONS.METADATA_PROJECTS);
    const canAccessCategories = hasPermission(PERMISSIONS.METADATA_CATEGORIES);
    const canAccessAccounts = hasPermission(PERMISSIONS.METADATA_ACCOUNTS);
    const canAccessLocations = hasPermission(PERMISSIONS.METADATA_LOCATIONS);
    const canAccessBookers = hasPermission(PERMISSIONS.METADATA_BOOKERS);

    // Reporting page permissions
    const canAccessSingleDoc = hasPermission(PERMISSIONS.REPORTING_SINGLE_DOC);
    const canAccessAgingReport = hasPermission(PERMISSIONS.REPORTING_AGING_REPORT);
    const canAccessAccountReport = hasPermission(PERMISSIONS.REPORTING_ACCOUNT_REPORT);
    const canAccessMasterReport = hasPermission(PERMISSIONS.REPORTING_MASTER_REPORT);
    const canAccessUserReport = hasPermission(PERMISSIONS.REPORTING_USER_REPORT);
    const canAccessProductReport = hasPermission(PERMISSIONS.REPORTING_PRODUCT_REPORT);
    const canAccessMissedEntries = hasPermission(PERMISSIONS.REPORTING_MISSED_ENTRIES);
    const canAccessOldMasterReport = hasPermission(PERMISSIONS.REPORTING_OLD_MASTER_REPORT);
    const canAccessPaymentReceiptVoucherReport = hasPermission(PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT);

    // Delivery page permissions
    const canAccessProcessDC = hasPermission(PERMISSIONS.DELIVERY_PROCESS_DC);
    const canAccessStockTransfers = hasPermission(PERMISSIONS.DELIVERY_STOCK_TRANSFERS);

    // Admin page permissions
    const canAccessUsers = hasPermission(PERMISSIONS.ADMIN_USERS);
    const canAccessCartageSettings = hasPermission(PERMISSIONS.ADMIN_CARTAGE_SETTINGS);
    const canAccessEditProfile = hasPermission(PERMISSIONS.ADMIN_EDIT_PROFILE);

    // Composite permissions for common use cases
    const canAccessHome = canAccessHomeEntry || canAccessPaymentAndReceipt || canAccessVoucherEntry || canAccessBaseVouchers ||
                         canAccessModifiedEntries || canAccessRecipes || canAccessPendingApprovals;
    const canAccessDelivery = canAccessProcessDC || canAccessStockTransfers;
    const canAccessMetaData = canAccessProjects || canAccessCategories || canAccessAccounts || 
                             canAccessLocations || canAccessBookers;
    const canAccessReporting = canAccessSingleDoc || canAccessAgingReport || canAccessAccountReport || 
                              canAccessMasterReport || canAccessUserReport || canAccessProductReport || canAccessMissedEntries ||
                              canAccessOldMasterReport || canAccessPaymentReceiptVoucherReport;
    const canAccessAdmin = canAccessUsers || canAccessCartageSettings || canAccessEditProfile;

    return {
        // Raw permissions array
        permissions,
        
        // User access data
        userAccess,
        
        // Base permission check functions
        hasPermission,
        hasAnyPermission,
        
        // Page/Action helpers
        canAccessPage,
        canAccessPageByPermission,
        canPerformAction,
        
        // Home page permissions
        canAccessHomeEntry,
        canAccessVoucherEntry,
        canAccessBaseVouchers,
        canAccessModifiedEntries,
        canAccessRecipes,
        canAccessPendingApprovals,
        canAccessPaymentAndReceipt,
        
        // MetaData page permissions
        canAccessProjects,
        canAccessCategories,
        canAccessAccounts,
        canAccessLocations,
        canAccessBookers,
        
        // Reporting page permissions
        canAccessSingleDoc,
        canAccessAgingReport,
        canAccessAccountReport,
        canAccessMasterReport,
        canAccessUserReport,
        canAccessProductReport,
        canAccessMissedEntries,
        canAccessOldMasterReport,
        canAccessPaymentReceiptVoucherReport,
        
        // Delivery page permissions
        canAccessProcessDC,
        canAccessStockTransfers,
        
        // Admin page permissions
        canAccessUsers,
        canAccessCartageSettings,
        canAccessEditProfile,
        
        // Composite permissions
        canAccessHome,
        canAccessDelivery,
        canAccessMetaData,
        canAccessReporting,
        canAccessAdmin,
    };
}

export default usePermissions;

