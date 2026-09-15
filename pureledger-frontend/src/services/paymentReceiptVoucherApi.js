import apiClient from './api';

const BASE_PATH = '/PaymentReceiptVoucherApi';

export const paymentReceiptVoucherApi = {
    getInitialization: () => apiClient.get(`${BASE_PATH}/init`),
    getCategories: (projectId) => apiClient.get(`${BASE_PATH}/categories/${projectId}`),
    getAccounts: (categoryId) => apiClient.get(`${BASE_PATH}/accounts/${categoryId}`),
    getDuplicateWarnings: (payload) => apiClient.post(`${BASE_PATH}/duplicate-warnings`, payload),
    getPostedVoucher: (journalVoucherId) => apiClient.get(`${BASE_PATH}/${journalVoucherId}`),
    submitRevision: (journalVoucherId, payload) =>
        apiClient.post(`${BASE_PATH}/${journalVoucherId}/revision`, payload),
};

export default paymentReceiptVoucherApi;
