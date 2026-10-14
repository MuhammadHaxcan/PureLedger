import { describe, expect, it } from 'vitest';
import { calculateAllocations } from './chargeAllocation';

const items = (n) => Array.from({ length: n }, (_, i) => ({
  productAccountId: i + 1, quantity: 1, unitCost: 1, baseValue: 1,
}));

describe('calculateAllocations', () => {
  it('never produces a negative share and sums exactly to the charge', () => {
    const result = calculateAllocations({ amount: 0.0003, allocationMethod: 'Equal', allocations: {} }, items(5));
    const values = Object.values(result);
    expect(values.every((v) => v >= 0)).toBe(true);
    expect(Math.round(values.reduce((s, v) => s + v, 0) * 10000)).toBe(3);
  });

  it('breaks ties by highest id', () => {
    const result = calculateAllocations({ amount: 0.0001, allocationMethod: 'Equal', allocations: {} }, items(3));
    expect(result).toEqual({ 1: 0, 2: 0, 3: 0.0001 });
  });

  it('handles skewed weights', () => {
    const skewed = [1, 1, 1, 1, 996].map((q, i) => ({ productAccountId: i + 1, quantity: q, unitCost: 1, baseValue: q }));
    const result = calculateAllocations({ amount: 100, allocationMethod: 'ByQuantity', allocations: {} }, skewed);
    expect(Math.round(Object.values(result).reduce((s, v) => s + v, 0) * 10000)).toBe(1000000);
  });
});
