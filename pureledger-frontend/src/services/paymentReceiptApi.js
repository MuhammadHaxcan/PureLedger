import apiClient from './api';

const BASE_PATH = '/PaymentReceiptApi';

export const paymentReceiptApi = {
    getInitialization: () => apiClient.get(`${BASE_PATH}/init`),
    getCategories: (projectId) => apiClient.get(`${BASE_PATH}/categories/${projectId}`),
    getAccounts: (categoryId) => apiClient.get(`${BASE_PATH}/accounts/${categoryId}`),
    getDuplicateWarnings: (payload) => apiClient.post(`${BASE_PATH}/duplicate-warnings`, payload),
    submit: (payload) => apiClient.post(BASE_PATH, payload),
};

export default paymentReceiptApi;
