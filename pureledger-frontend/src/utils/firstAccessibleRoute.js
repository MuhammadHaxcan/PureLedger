import { PERMISSIONS } from '../constants/permissions';

export const ACCESSIBLE_ROUTE_ORDER = [
    [PERMISSIONS.HOME_ENTRY, '/home/entry'],
    [PERMISSIONS.HOME_PAYMENT_AND_RECEIPT, '/home/payment-and-receipt'],
    [PERMISSIONS.HOME_VOUCHER_ENTRY, '/home/voucher-entry'],
    [PERMISSIONS.HOME_BASE_VOUCHERS, '/home/base-vouchers'],
    [PERMISSIONS.HOME_MODIFIED_ENTRIES, '/home/modified-entries'],
    [PERMISSIONS.HOME_RECIPES, '/home/recipes'],
    [PERMISSIONS.HOME_PENDING_APPROVALS, '/approvals/pending'],
    [PERMISSIONS.METADATA_PROJECTS, '/metadata/projects'],
    [PERMISSIONS.METADATA_CATEGORIES, '/metadata/categories'],
    [PERMISSIONS.METADATA_ACCOUNTS, '/metadata/accounts'],
    [PERMISSIONS.METADATA_LOCATIONS, '/metadata/locations'],
    [PERMISSIONS.METADATA_BOOKERS, '/metadata/bookers'],
    [PERMISSIONS.REPORTING_SINGLE_DOC, '/reporting/single-doc'],
    [PERMISSIONS.REPORTING_AGING_REPORT, '/reporting/aging-report'],
    [PERMISSIONS.REPORTING_ACCOUNT_REPORT, '/reporting/account-report'],
    [PERMISSIONS.REPORTING_MASTER_REPORT, '/reporting/master-report'],
    [PERMISSIONS.REPORTING_USER_REPORT, '/reporting/user-report'],
    [PERMISSIONS.REPORTING_PRODUCT_REPORT, '/reporting/product-report'],
    [PERMISSIONS.REPORTING_MISSED_ENTRIES, '/reporting/missed-entries'],
    [PERMISSIONS.REPORTING_OLD_MASTER_REPORT, '/reporting/old-master-report'],
    [PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT, '/reporting/payment-receipt-vouchers'],
    [PERMISSIONS.DELIVERY_PROCESS_DC, '/delivery/process-dc'],
    [PERMISSIONS.DELIVERY_STOCK_TRANSFERS, '/stock-transfers'],
];

export const getFirstAccessibleRoute = (hasPermission, isAdmin = false) => {
    if (isAdmin) return '/home/entry';
    return ACCESSIBLE_ROUTE_ORDER.find(([permission]) => hasPermission(permission))?.[1]
        || '/account/access-denied';
};
