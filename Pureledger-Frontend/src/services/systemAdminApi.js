import apiClient, { setXsrfToken, clearXsrfToken } from './api';

/**
 * System Admin API service for tenant management operations.
 */

// System Admin Authentication
export const systemAdminLogin = async (username, password) => {
    const response = await apiClient.post('/SystemAdminApi/login', { username, password });
    if (response.data.success && response.data.xsrfToken) {
        setXsrfToken(response.data.xsrfToken);
    }
    return response.data;
};

export const systemAdminLogout = async () => {
    try {
        await apiClient.post('/SystemAdminApi/logout');
    } finally {
        clearXsrfToken();
    }
};

export const getSystemAdminMe = async () => {
    const response = await apiClient.get('/SystemAdminApi/me');
    return response.data;
};

// Tenant Management
export const getAllTenants = async () => {
    const response = await apiClient.get('/system/tenants');
    return response.data;
};

export const getTenantById = async (id) => {
    const response = await apiClient.get(`/system/tenants/${id}`);
    return response.data;
};

export const createTenant = async (name, slug) => {
    const response = await apiClient.post('/system/tenants', { name, slug });
    return response.data;
};

export const updateTenant = async (id, data) => {
    const response = await apiClient.put(`/system/tenants/${id}`, data);
    return response.data;
};

export const activateTenant = async (id) => {
    const response = await apiClient.patch(`/system/tenants/${id}/activate`);
    return response.data;
};

export const deactivateTenant = async (id) => {
    const response = await apiClient.patch(`/system/tenants/${id}/deactivate`);
    return response.data;
};

export const deleteTenant = async (id) => {
    const response = await apiClient.delete(`/system/tenants/${id}?confirm=true`);
    return response.data;
};

// Migration Management
export const getPendingMigrations = async () => {
    const response = await apiClient.get('/system/tenants/migrations/pending');
    return response.data;
};

export const applyTenantMigrations = async (id) => {
    const response = await apiClient.post(`/system/tenants/${id}/migrations/apply`);
    return response.data;
};

export const applyAllMigrations = async () => {
    const response = await apiClient.post('/system/tenants/migrations/apply-all');
    return response.data;
};

// Audit Logs
export const getTenantAuditLogs = async (id, page = 1, pageSize = 20) => {
    const response = await apiClient.get(`/system/tenants/${id}/audit-logs?page=${page}&pageSize=${pageSize}`);
    return response.data;
};

export default {
    systemAdminLogin,
    systemAdminLogout,
    getSystemAdminMe,
    getAllTenants,
    getTenantById,
    createTenant,
    updateTenant,
    activateTenant,
    deactivateTenant,
    deleteTenant,
    getPendingMigrations,
    applyTenantMigrations,
    applyAllMigrations,
    getTenantAuditLogs,
};
