import { describe, expect, it } from 'vitest';
import { PERMISSIONS } from '../constants/permissions';
import { getFirstAccessibleRoute } from './firstAccessibleRoute';

describe('first accessible route', () => {
    it('keeps Home.Entry first when available', () => {
        const allowed = new Set([PERMISSIONS.HOME_ENTRY, PERMISSIONS.HOME_PAYMENT_AND_RECEIPT]);
        expect(getFirstAccessibleRoute(permission => allowed.has(permission))).toBe('/home/entry');
    });

    it('falls back to Payment & Receipt before other Home pages', () => {
        const allowed = new Set([PERMISSIONS.HOME_PAYMENT_AND_RECEIPT, PERMISSIONS.HOME_VOUCHER_ENTRY]);
        expect(getFirstAccessibleRoute(permission => allowed.has(permission))).toBe('/home/payment-and-receipt');
    });

    it('uses the P&R report for report-only access', () => {
        expect(getFirstAccessibleRoute(
            permission => permission === PERMISSIONS.REPORTING_PAYMENT_RECEIPT_VOUCHER_REPORT,
        )).toBe('/reporting/payment-receipt-vouchers');
    });
});
