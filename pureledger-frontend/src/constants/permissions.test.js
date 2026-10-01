import { describe, expect, it } from 'vitest';
import { PAGE_PERMISSIONS, PERMISSIONS } from './permissions';

describe('Payment & Receipt permissions', () => {
    it('maps creation and report routes to their distinct permissions', () => {
        expect(PAGE_PERMISSIONS['/home/payment-and-receipt']).toEqual([
            PERMISSIONS.HOME_PAYMENT_AND_RECEIPT,
        ]);
        expect(PAGE_PERMISSIONS['/reporting/payment-receipt-vouchers']).toEqual([
            PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT,
        ]);
        expect(PERMISSIONS.HOME_PAYMENT_AND_RECEIPT).not.toBe(
            PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT,
        );
    });
});
