import { describe, expect, it } from 'vitest';
import { serializePaymentReceiptReportFilters } from './paymentReceiptReportApi';

describe('P&R report filter serialization', () => {
    it('uses the same trimmed filters for page, export, and register requests', () => {
        expect(serializePaymentReceiptReportFilters({
            startDate: '2026-07-01', endDate: '2026-07-31', projectId: '4', categoryId: '',
            accountId: '9', search: ' supplier ',
        }, 2, 500)).toEqual({
            startDate: '2026-07-01', endDate: '2026-07-31', projectId: '4', categoryId: undefined,
            accountId: '9', search: 'supplier',
            page: 2, pageSize: 500,
        });
    });
});
