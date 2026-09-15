import { createContext, useContext, useState, useEffect, useMemo, useCallback } from 'react';
import apiClient, { setXsrfToken, clearXsrfToken } from '../services/api';
import { PERMISSIONS, PAGE_PERMISSIONS } from '../constants/permissions';
import { useToast } from './ToastContext';

const AuthContext = createContext(null);

// Module-level flag to prevent duplicate session restoration (React StrictMode runs useEffect twice)
let isRestoringSession = false;

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};

export const AuthProvider = ({ children }) => {
    const { showLoginSuccess, showLogoutSuccess } = useToast();
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [userAccess, setUserAccess] = useState({ controllers: [], actions: {} });
    const [navigationMenu, setNavigationMenu] = useState([]);
    const [permissions, setPermissions] = useState([]);

    // Build permissions array from userAccess
    const buildPermissions = useMemo(() => {
        if (!userAccess || userAccess.controllers.length === 0 && Object.keys(userAccess.actions).length === 0) {
            return [];
        }

        const perms = [];

        // Add controller-level permissions (all actions accessible)
        userAccess.controllers.forEach(controller => {
            // Map controller to page permissions
            const controllerPages = {
                'Home': [PERMISSIONS.HOME_ENTRY, PERMISSIONS.HOME_VOUCHER_ENTRY, PERMISSIONS.HOME_BASE_VOUCHERS,
                PERMISSIONS.HOME_MODIFIED_ENTRIES, PERMISSIONS.HOME_RECIPES, PERMISSIONS.HOME_PENDING_APPROVALS,
                PERMISSIONS.HOME_PAYMENT_AND_RECEIPT],
                'MetaData': [PERMISSIONS.METADATA_PROJECTS, PERMISSIONS.METADATA_CATEGORIES, PERMISSIONS.METADATA_ACCOUNTS,
                PERMISSIONS.METADATA_LOCATIONS, PERMISSIONS.METADATA_BOOKERS],
                'Reporting': [PERMISSIONS.REPORTING_SINGLE_DOC, PERMISSIONS.REPORTING_AGING_REPORT, PERMISSIONS.REPORTING_ACCOUNT_REPORT,
                PERMISSIONS.REPORTING_MASTER_REPORT, PERMISSIONS.REPORTING_USER_REPORT, PERMISSIONS.REPORTING_PRODUCT_REPORT, PERMISSIONS.REPORTING_MISSED_ENTRIES, PERMISSIONS.REPORTING_OLD_MASTER_REPORT,
                PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT],
                'DeliveryChallan': [PERMISSIONS.DELIVERY_PROCESS_DC, PERMISSIONS.DELIVERY_STOCK_TRANSFERS],
            };

            if (controllerPages[controller]) {
                perms.push(...controllerPages[controller]);
            }
        });

        // Add action-level permissions
        Object.keys(userAccess.actions).forEach(controller => {
            const actions = userAccess.actions[controller] || [];
            actions.forEach(action => {
                const permission = `${controller}.${action}`;
                if (Object.values(PERMISSIONS).includes(permission)) {
                    perms.push(permission);
                }
            });
        });

        return [...new Set(perms)]; // Remove duplicates
    }, [userAccess]);

    useEffect(() => {
        setPermissions(buildPermissions);
    }, [buildPermissions]);

    // Permission checking functions
    const hasPermission = (permission) => {
        if (!user) return false;
        if (user.isAdmin) return true;
        return permissions.includes(permission);
    };

    const hasAnyPermission = (permissionList) => {
        if (!user) return false;
        if (user.isAdmin) return true;
        if (!permissionList || permissionList.length === 0) return true;
        return permissionList.some(perm => permissions.includes(perm));
    };

    // Fetch user access and rebuild navigation menu (used on login and refresh)
    const fetchAccessAndMenu = useCallback(async (userData) => {
        try {
            const accessResponse = await apiClient.get('/AccountApi/access');
            if (accessResponse.data.success) {
                const { controllers, actions } = accessResponse.data;
                setUserAccess({ controllers, actions });
                localStorage.setItem('userAccess', JSON.stringify({ controllers, actions }));

                const navMenu = buildNavigationMenu(userData.isAdmin, controllers, actions);
                setNavigationMenu(navMenu);
                localStorage.setItem('navigationMenu', JSON.stringify(navMenu));
                return navMenu;
            }
        } catch (error) {
            console.error('Error loading user access:', error);
        }
        return [];
    }, []);

    useEffect(() => {
        // Check if user is already logged in using cookies
        const savedUser = localStorage.getItem('user');
        const savedAccess = localStorage.getItem('userAccess');
        const savedNavMenu = localStorage.getItem('navigationMenu');

        const restoreSession = async () => {
            // Prevent duplicate session restoration (React StrictMode runs useEffect twice)
            if (isRestoringSession) {
                console.log('Session restoration already in progress, skipping duplicate call');
                return;
            }
            isRestoringSession = true;

            // Try to verify auth using cookies (check-auth endpoint)
            // If access token is expired, the interceptor will automatically refresh it
            try {
                const authCheckResponse = await apiClient.get('/AccountApi/check-auth');
                if (authCheckResponse.data.success && authCheckResponse.data.isAuthenticated) {
                    // User is authenticated via cookies
                    await restoreUserData(savedUser, savedAccess, savedNavMenu);
                } else {
                    // Not authenticated - access token might be expired
                    // If we have saved user data, try to refresh the token
                    if (savedUser) {
                        console.log('Access token expired, attempting to refresh...');
                        try {
                            // Try to refresh the token - this will use the refresh token cookie
                            const refreshResponse = await apiClient.post('/AccountApi/refresh-token', {});
                            if (refreshResponse.data.success) {
                                // Token refreshed successfully, update XSRF token
                                if (refreshResponse.data.xsrfToken) {
                                    setXsrfToken(refreshResponse.data.xsrfToken);
                                }
                                // Now restore the user data
                                await restoreUserData(savedUser, savedAccess, savedNavMenu);
                            } else {
                                // Refresh failed, clear auth state
                                console.log('Token refresh failed, clearing auth state');
                                clearAuthState();
                            }
                        } catch (refreshError) {
                            // Refresh failed (likely refresh token also expired)
                            console.log('Token refresh failed:', refreshError.message);
                            clearAuthState();
                        }
                    } else {
                        // No saved user data, just clear any stale state
                        clearAuthState();
                    }
                }
            } catch (error) {
                // Auth check failed - this could be because:
                // 1. Network error
                // 2. Server error
                
                if (error.isNetworkError) {
                    // Network error - try to restore from saved data if available
                    console.log('Network error during auth check, attempting to restore saved session');
                    if (savedUser) {
                        await restoreUserData(savedUser, savedAccess, savedNavMenu);
                    } else {
                        clearAuthState();
                    }
                } else {
                    // Other error, clear state
                    console.log('Auth check failed:', error.message);
                    clearAuthState();
                }
            }
            setLoading(false);
            isRestoringSession = false;
        };

        const restoreUserData = async (savedUser, savedAccess, savedNavMenu) => {
            if (savedUser) {
                try {
                    const userData = JSON.parse(savedUser);
                    setUser(userData);
                    setIsAuthenticated(true);

                    // Fetch fresh XSRF token
                    try {
                        const xsrfResponse = await apiClient.get('/AccountApi/xsrf-token');
                        if (xsrfResponse.data.success) {
                            setXsrfToken(xsrfResponse.data.xsrfToken);
                        }
                    } catch (xsrfError) {
                        console.error('Error fetching XSRF token:', xsrfError);
                    }

                    // Restore access and navigation menu if available
                    if (savedAccess) {
                        try {
                            setUserAccess(JSON.parse(savedAccess));
                        } catch (e) {
                            console.error('Error parsing saved access:', e);
                        }
                    }

                    if (savedNavMenu) {
                        try {
                            const parsedNav = JSON.parse(savedNavMenu);
                            setNavigationMenu(parsedNav);
                            // If nav is empty, try to refetch access to build it
                            if (!parsedNav || parsedNav.length === 0) {
                                await fetchAccessAndMenu(userData);
                            }
                        } catch (e) {
                            console.error('Error parsing saved navigation menu:', e);
                            await fetchAccessAndMenu(userData);
                        }
                    } else {
                        // No saved menu, fetch it now
                        await fetchAccessAndMenu(userData);
                    }
                } catch (error) {
                    console.error('Error parsing saved user data:', error);
                    // Fetch user from server
                    try {
                        const meResponse = await apiClient.get('/AccountApi/me');
                        if (meResponse.data.success) {
                            const userData = meResponse.data.user;
                            setUser(userData);
                            setIsAuthenticated(true);
                            localStorage.setItem('user', JSON.stringify(userData));
                            await fetchAccessAndMenu(userData);
                        }
                    } catch (meError) {
                        console.error('Error fetching user:', meError);
                        clearAuthState();
                    }
                }
            } else {
                // No saved user but cookies are valid, fetch user from server
                try {
                    const meResponse = await apiClient.get('/AccountApi/me');
                    if (meResponse.data.success) {
                        const userData = meResponse.data.user;
                        setUser(userData);
                        setIsAuthenticated(true);
                        localStorage.setItem('user', JSON.stringify(userData));
                        await fetchAccessAndMenu(userData);

                        // Fetch XSRF token
                        const xsrfResponse = await apiClient.get('/AccountApi/xsrf-token');
                        if (xsrfResponse.data.success) {
                            setXsrfToken(xsrfResponse.data.xsrfToken);
                        }
                    }
                } catch (meError) {
                    console.error('Error fetching user:', meError);
                    clearAuthState();
                }
            }
        };

        const clearAuthState = () => {
            localStorage.removeItem('user');
            localStorage.removeItem('userAccess');
            localStorage.removeItem('navigationMenu');
            clearXsrfToken();
        };

        restoreSession();
    }, [fetchAccessAndMenu]);

    // Build navigation menu structure from user access
    const buildNavigationMenu = (isAdmin, controllers, actions) => {
        const navigationItems = [
            {
                title: 'Home',
                icon: 'fas fa-home',
                controller: 'Home',
                items: [
                    { action: 'Entry', title: 'New Entry', icon: 'fas fa-plus-circle', path: '/home/entry' },
                    { action: 'PaymentAndReceipt', title: 'Payment & Receipt', icon: 'fas fa-money-check-alt', path: '/home/payment-and-receipt' },
                    { action: 'VoucherEntry', title: 'Edit Entry', icon: 'fas fa-edit', path: '/home/voucher-entry' },
                    { action: 'BaseVouchers', title: 'Base Vouchers', icon: 'fas fa-layer-group', path: '/home/base-vouchers' },
                    { action: 'ModifiedEntries', title: 'Modified Entries', icon: 'fas fa-history', path: '/home/modified-entries' },
                    { action: 'Recipes', title: 'Recipes', icon: 'fas fa-book-open', path: '/home/recipes' },
                    { action: 'PendingApprovals', title: 'Pending Approvals', icon: 'fas fa-clipboard-check', path: '/approvals/pending' },
                ],
            },
            {
                title: 'Metadata',
                icon: 'fas fa-database',
                controller: 'MetaData',
                items: [
                    { action: 'Projects', title: 'Projects', icon: 'fas fa-project-diagram', path: '/metadata/projects' },
                    { action: 'Categories', title: 'Categories', icon: 'fas fa-sitemap', path: '/metadata/categories' },
                    { action: 'Accounts', title: 'Accounts', icon: 'fas fa-users', path: '/metadata/accounts' },
                    { action: 'Locations', title: 'Locations', icon: 'fas fa-map-marker-alt', path: '/metadata/locations' },
                    { action: 'Bookers', title: 'Bookers', icon: 'fas fa-user-tie', path: '/metadata/bookers' },
                ],
            },
            {
                title: 'Delivery',
                icon: 'fas fa-truck',
                controller: 'DeliveryChallan',
                items: [
                    { action: 'ProcessDC', title: 'Delivery Challan', icon: 'fas fa-truck', path: '/delivery/process-dc' },
                    { action: 'StockTransfers', title: 'Stock Transfers', icon: 'fas fa-exchange-alt', path: '/stock-transfers' },
                ],
            },
            {
                title: 'Reports',
                icon: 'fas fa-chart-bar',
                controller: 'Reporting',
                items: [
                    { action: 'SingleDoc', title: 'Single Document', icon: 'fas fa-file-alt', path: '/reporting/single-doc' },
                    { action: 'AgingReport', title: 'Aging Report', icon: 'fas fa-calendar-alt', path: '/reporting/aging-report' },
                    { action: 'AccountReport', title: 'Account Report', icon: 'fas fa-user-circle', path: '/reporting/account-report' },
                    { action: 'MasterReport', title: 'Master Report', icon: 'fas fa-file-invoice', path: '/reporting/master-report' },
                    { action: 'UserReport', title: 'User Report', icon: 'fas fa-file-alt', path: '/reporting/user-report' },
                    { action: 'ProductReport', title: 'Product Report', icon: 'fas fa-box', path: '/reporting/product-report' },
                    { action: 'MissedEntriesReport', title: 'Missed Entries', icon: 'fas fa-exclamation-circle', path: '/reporting/missed-entries' },
                    { action: 'OldMasterReport', title: 'Old Master Report', icon: 'fas fa-history', path: '/reporting/old-master-report' },
                    { action: 'PaymentReceiptVoucherReport', title: 'P&R Voucher Report', icon: 'fas fa-receipt', path: '/reporting/payment-receipt-vouchers' },
                ],
            },
        ];

        const hasAccess = (controller, action = null) => {
            if (isAdmin) return true;

            const hasControllerAccess = controllers.includes(controller);

            if (action) {
                const hasSpecificAction = actions[controller]?.includes(action) || false;
                return hasControllerAccess || hasSpecificAction;
            }

            const hasAnyAction = actions[controller] && actions[controller].length > 0;
            return hasControllerAccess || hasAnyAction;
        };

        // Filter navigation items based on access
        return navigationItems
            .map(item => {
                const accessibleItems = item.items.filter(subItem => hasAccess(item.controller, subItem.action));
                if (accessibleItems.length === 0) return null;
                return {
                    ...item,
                    items: accessibleItems,
                };
            })
            .filter(item => item !== null);
    };

    const findFirstAccessibleRoute = (isAdmin, controllers, actions) => {
        if (isAdmin) {
            return '/home/entry';
        }

        // Check Home controller
        if (controllers.includes('Home') || (actions.Home && actions.Home.length > 0)) {
            if (actions.Home?.includes('Entry')) return '/home/entry';
            if (actions.Home?.includes('PaymentAndReceipt')) return '/home/payment-and-receipt';
            if (actions.Home?.includes('VoucherEntry')) return '/home/voucher-entry';
            if (actions.Home?.includes('BaseVouchers')) return '/home/base-vouchers';
            if (actions.Home?.includes('ModifiedEntries')) return '/home/modified-entries';
            if (actions.Home?.includes('Recipes')) return '/home/recipes';
            if (actions.Home?.includes('PendingApprovals')) return '/approvals/pending';
            return '/home/entry';
        }

        // Check MetaData controller
        if (controllers.includes('MetaData') || (actions.MetaData && actions.MetaData.length > 0)) {
            if (actions.MetaData?.includes('Projects')) return '/metadata/projects';
            if (actions.MetaData?.includes('Categories')) return '/metadata/categories';
            if (actions.MetaData?.includes('Accounts')) return '/metadata/accounts';
            if (actions.MetaData?.includes('Locations')) return '/metadata/locations';
            if (actions.MetaData?.includes('Bookers')) return '/metadata/bookers';
            return '/metadata/projects';
        }

        // Check Reporting controller
        if (controllers.includes('Reporting') || (actions.Reporting && actions.Reporting.length > 0)) {
            if (actions.Reporting?.includes('SingleDoc')) return '/reporting/single-doc';
            if (actions.Reporting?.includes('AgingReport')) return '/reporting/aging-report';
            if (actions.Reporting?.includes('AccountReport')) return '/reporting/account-report';
            if (actions.Reporting?.includes('MasterReport')) return '/reporting/master-report';
            if (actions.Reporting?.includes('UserReport')) return '/reporting/user-report';
            if (actions.Reporting?.includes('ProductReport')) return '/reporting/product-report';
            if (actions.Reporting?.includes('MissedEntriesReport')) return '/reporting/missed-entries';
            if (actions.Reporting?.includes('OldMasterReport')) return '/reporting/old-master-report';
            if (actions.Reporting?.includes('PaymentReceiptVoucherReport')) return '/reporting/payment-receipt-vouchers';
            return '/reporting/single-doc';
        }

        // Check DeliveryChallan controller
        if (controllers.includes('DeliveryChallan') || (actions.DeliveryChallan && actions.DeliveryChallan.length > 0)) {
            if (actions.DeliveryChallan?.includes('StockTransfers') &&
                !actions.DeliveryChallan?.includes('ProcessDC')) return '/stock-transfers';
            return '/delivery/process-dc';
        }

        return '/account/access-denied';
    };

    const login = async (username, password) => {
        try {
            const response = await apiClient.post('/AccountApi/login', {
                username,
                password,
            });

            if (response.data.success) {
                const { xsrfToken: newXsrfToken, user: userData } = response.data;

                // Store XSRF token in memory (not localStorage for security)
                if (newXsrfToken) {
                    setXsrfToken(newXsrfToken);
                }

                // Store non-sensitive user info in localStorage
                localStorage.setItem('user', JSON.stringify(userData));
                setUser(userData);
                setIsAuthenticated(true);

                // Show login success toast
                showLoginSuccess(userData.fullName || userData.username);

                // Note: We use the XSRF token from login response directly.
                // DO NOT call /xsrf-token here - it causes a race condition where the
                // cookie from login hasn't been stored yet, leading to token mismatch.

                // Load user access and build navigation menu
                try {
                    const navMenu = await fetchAccessAndMenu(userData);
                    if (navMenu && navMenu.length > 0) {
                        const { controllers = [], actions = {} } = JSON.parse(localStorage.getItem('userAccess') || '{"controllers":[],"actions":{}}');
                        const firstRoute = findFirstAccessibleRoute(userData.isAdmin, controllers, actions);
                        return { success: true, redirectTo: firstRoute };
                    }
                } catch (accessError) {
                    console.error('Error loading user access:', accessError);
                }

                return { success: true, redirectTo: '/home/entry' };
            }
            return { success: false, message: response.data.message || 'Login failed' };
        } catch (error) {
            return {
                success: false,
                message: error.response?.data?.message || 'Login failed. Please try again.',
            };
        }
    };

    const logout = async () => {
        // Always clear local state first (before API call in case it fails)
        clearXsrfToken();
        localStorage.removeItem('user');
        localStorage.removeItem('userAccess');
        localStorage.removeItem('navigationMenu');
        setUser(null);
        setIsAuthenticated(false);
        setUserAccess({ controllers: [], actions: {} });
        setNavigationMenu([]);
        setPermissions([]);

        // Call logout endpoint to revoke refresh token and clear server-side cookies
        try {
            await apiClient.post('/AccountApi/logout', {});
        } catch {
            // Ignore errors - we've already cleared local state
            console.log('Logout API call completed (may have failed, but local state is cleared)');
        }

        // Show logout success toast
        showLogoutSuccess();
    };

    const getCurrentUser = async () => {
        try {
            const response = await apiClient.get('/AccountApi/me');
            if (response.data.success) {
                setUser(response.data.user);
                return response.data.user;
            }
        } catch (error) {
            console.error('Error fetching current user:', error);
            logout();
        }
    };

    const value = {
        user,
        loading,
        isAuthenticated,
        userAccess,
        navigationMenu,
        permissions,
        hasPermission,
        hasAnyPermission,
        login,
        logout,
        getCurrentUser,
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

