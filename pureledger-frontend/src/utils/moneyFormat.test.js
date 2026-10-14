import { describe, expect, it } from 'vitest';
import { formatMoney, formatMoneyInput, parseMoneyInput } from './moneyFormat';

describe('money formatting', () => {
    it('formats totals with thousands separators and two decimals', () => {
        expect(formatMoney(10000)).toBe('10,000.00');
        expect(formatMoney(1234567.8)).toBe('1,234,567.80');
        expect(formatMoney(-1000)).toBe('-1,000.00');
    });

    it('formats editable input while preserving a decimal in progress', () => {
        expect(formatMoneyInput('10000')).toBe('10,000');
        expect(formatMoneyInput('10000.')).toBe('10,000.');
        expect(formatMoneyInput('10000.5')).toBe('10,000.5');
        expect(formatMoneyInput('1,234.567')).toBe('1,234.56');
    });

    it('parses formatted input back to a number', () => {
        expect(parseMoneyInput('1,234,567.89')).toBe(1234567.89);
        expect(parseMoneyInput('')).toBeNull();
    });
});
