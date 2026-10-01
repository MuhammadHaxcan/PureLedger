import apiClient from './api';

const BASE_PATH = '/PaymentReceiptReportApi';

export const serializePaymentReceiptReportFilters = (filters, page, pageSize) => ({
    startDate: filters.startDate || undefined,
    endDate: filters.endDate || undefined,
    projectId: filters.projectId || undefined,
    categoryId: filters.categoryId || undefined,
    accountId: filters.accountId || undefined,
    search: String(filters.search || '').trim() || undefined,
    page,
    pageSize,
});

export const paymentReceiptReportApi = {
    getInitialization: () => apiClient.get(`${BASE_PATH}/init`),
    getReport: (filters, page, pageSize, signal) => apiClient.get(BASE_PATH, {
        params: serializePaymentReceiptReportFilters(filters, page, pageSize),
        signal,
    }),
};

export default paymentReceiptReportApi;
