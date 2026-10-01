// Permission constants for the application
// Page-based permissions - maps to Controller.Action format in backend

export const PERMISSIONS = {
    // Home Controller Pages
    HOME_ENTRY: 'Home.Entry',
    HOME_VOUCHER_ENTRY: 'Home.VoucherEntry',
    HOME_BASE_VOUCHERS: 'Home.BaseVouchers',
    HOME_MODIFIED_ENTRIES: 'Home.ModifiedEntries',
    HOME_RECIPES: 'Home.Recipes',
    HOME_PENDING_APPROVALS: 'Home.PendingApprovals',
    HOME_PAYMENT_AND_RECEIPT: 'Home.PaymentAndReceipt',
    
    // MetaData Controller Pages
    METADATA_PROJECTS: 'MetaData.Projects',
    METADATA_CATEGORIES: 'MetaData.Categories',
    METADATA_ACCOUNTS: 'MetaData.Accounts',
    METADATA_LOCATIONS: 'MetaData.Locations',
    METADATA_BOOKERS: 'MetaData.Bookers',
    
    // Reporting Controller Pages
    REPORTING_SINGLE_DOC: 'Reporting.SingleDoc',
    REPORTING_AGING_REPORT: 'Reporting.AgingReport',
    REPORTING_ACCOUNT_REPORT: 'Reporting.AccountReport',
    REPORTING_MASTER_REPORT: 'Reporting.MasterReport',
    REPORTING_USER_REPORT: 'Reporting.UserReport',
    REPORTING_PRODUCT_REPORT: 'Reporting.ProductReport',
    REPORTING_MISSED_ENTRIES: 'Reporting.MissedEntriesReport',
    REPORTING_OLD_MASTER_REPORT: 'Reporting.OldMasterReport',
    REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT: 'Reporting.PaymentReceiptVoucherReport',
    
    // Delivery Controller Pages
    DELIVERY_PROCESS_DC: 'DeliveryChallan.ProcessDC',
    DELIVERY_STOCK_TRANSFERS: 'DeliveryChallan.StockTransfers',
    
    // Admin Pages (typically admin-only)
    ADMIN_USERS: 'Admin.Users',
    ADMIN_CARTAGE_SETTINGS: 'Admin.CartageSettings',
    ADMIN_EDIT_PROFILE: 'Admin.EditProfile',
};

// Page access requirements - maps route paths to required permissions
export const PAGE_PERMISSIONS = {
    // Home routes
    '/home/entry': [PERMISSIONS.HOME_ENTRY],
    '/home/voucher-entry': [PERMISSIONS.HOME_VOUCHER_ENTRY],
    '/home/base-vouchers': [PERMISSIONS.HOME_BASE_VOUCHERS],
    '/home/modified-entries': [PERMISSIONS.HOME_MODIFIED_ENTRIES],
    '/home/recipes': [PERMISSIONS.HOME_RECIPES],
    '/home/payment-and-receipt': [PERMISSIONS.HOME_PAYMENT_AND_RECEIPT],
    '/approvals/pending': [PERMISSIONS.HOME_PENDING_APPROVALS],
    '/approvals/pending/payment-receipt/edit': [PERMISSIONS.HOME_PENDING_APPROVALS],
    
    // MetaData routes
    '/metadata/projects': [PERMISSIONS.METADATA_PROJECTS],
    '/metadata/categories': [PERMISSIONS.METADATA_CATEGORIES],
    '/metadata/accounts': [PERMISSIONS.METADATA_ACCOUNTS],
    '/metadata/locations': [PERMISSIONS.METADATA_LOCATIONS],
    '/metadata/bookers': [PERMISSIONS.METADATA_BOOKERS],
    
    // Reporting routes
    '/reporting/single-doc': [PERMISSIONS.REPORTING_SINGLE_DOC],
    '/reporting/aging-report': [PERMISSIONS.REPORTING_AGING_REPORT],
    '/reporting/account-report': [PERMISSIONS.REPORTING_ACCOUNT_REPORT],
    '/reporting/master-report': [PERMISSIONS.REPORTING_MASTER_REPORT],
    '/reporting/user-report': [PERMISSIONS.REPORTING_USER_REPORT],
    '/reporting/product-report': [PERMISSIONS.REPORTING_PRODUCT_REPORT],
    '/reporting/missed-entries': [PERMISSIONS.REPORTING_MISSED_ENTRIES],
    '/reporting/old-master-report': [PERMISSIONS.REPORTING_OLD_MASTER_REPORT],
    '/reporting/payment-receipt-vouchers': [PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT],
    
    // Delivery routes
    '/delivery/process-dc': [PERMISSIONS.DELIVERY_PROCESS_DC],
    '/stock-transfers': [PERMISSIONS.DELIVERY_STOCK_TRANSFERS],
    
    // Admin routes
    '/admin/users': [PERMISSIONS.ADMIN_USERS],
    '/admin/cartage-settings': [PERMISSIONS.ADMIN_CARTAGE_SETTINGS],
    '/admin/edit-profile': [PERMISSIONS.ADMIN_EDIT_PROFILE],
};

// Action permissions for specific operations (if needed for future features)
export const ACTION_PERMISSIONS = {
    // Can be extended for specific actions within pages
    // For now, page access is sufficient
};

// Helper to get permission from route path
export const getPermissionFromPath = (path) => {
    return PAGE_PERMISSIONS[path]?.[0] || null;
};

// Helper to check if path requires admin
export const isAdminRoute = (path) => {
    return path.startsWith('/admin/');
};

export default PERMISSIONS;

