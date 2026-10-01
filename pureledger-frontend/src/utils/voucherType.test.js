import { describe, expect, it } from 'vitest';
import { isPaymentReceiptVoucherType, resolvePrintVoucherType } from './voucherType';

describe('voucher type resolution', () => {
    it('trusts the backend resolved type over a JV request', () => {
        const resolved = resolvePrintVoucherType({ resolvedVoucherType: 'PaymentReceipt' }, 'JV');
        expect(resolved).toBe('PaymentReceipt');
        expect(isPaymentReceiptVoucherType(resolved)).toBe(true);
    });

    it('preserves the requested type for legacy responses', () => {
        expect(resolvePrintVoucherType({ data: {} }, 'DC')).toBe('DC');
    });
});
