import apiClient from './api';

const BASE_PATH = '/ApprovalApi';

export const approvalApi = {
    getPending: () => apiClient.get(`${BASE_PATH}/pending`),
    getHistory: () => apiClient.get(`${BASE_PATH}/history`),
    getById: (approvalVoucherId) => apiClient.get(`${BASE_PATH}/${approvalVoucherId}`),
    update: (approvalVoucherId, payload) =>
        apiClient.put(`${BASE_PATH}/${approvalVoucherId}`, payload),
    updatePaymentReceipt: (approvalVoucherId, payload) =>
        apiClient.put(`${BASE_PATH}/${approvalVoucherId}/payment-receipt`, payload),
    getPaymentReceiptInitialization: () => apiClient.get(`${BASE_PATH}/payment-receipt/init`),
    getPaymentReceiptCategories: (projectId) =>
        apiClient.get(`${BASE_PATH}/payment-receipt/categories/${projectId}`),
    getPaymentReceiptAccounts: (categoryId) =>
        apiClient.get(`${BASE_PATH}/payment-receipt/accounts/${categoryId}`),
    getPaymentReceiptDuplicateWarnings: (payload) =>
        apiClient.post(`${BASE_PATH}/payment-receipt/duplicate-warnings`, payload),
    approve: (approvalVoucherId, snapshotVersion) =>
        apiClient.post(`${BASE_PATH}/${approvalVoucherId}/approve`, snapshotVersion ? { snapshotVersion } : {}),
    reject: (approvalVoucherId, rejectionReason, snapshotVersion) =>
        apiClient.post(`${BASE_PATH}/${approvalVoucherId}/reject`, {
            reason: rejectionReason,
            ...(snapshotVersion ? { snapshotVersion } : {}),
        }),
};

export default approvalApi;
