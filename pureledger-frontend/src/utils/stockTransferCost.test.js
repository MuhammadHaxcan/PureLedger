import { describe, expect, it } from 'vitest';
import { transferCarryingValue } from './stockTransferCost';

describe('transfer carrying value preview', () => {
  it('uses exact remaining value on full depletion instead of a rounded unit rate', () => {
    expect(transferCarryingValue({ quantity: 3, availableQuantity: 3, inventoryValue: 100, unitCost: 33.3333, suggestedUnitCost: 33.3333 })).toBe(100);
  });
  it('rounds partial carrying value to the server monetary precision', () => {
    expect(transferCarryingValue({ quantity: 1, availableQuantity: 3, inventoryValue: 100, unitCost: 33.3333, suggestedUnitCost: 33.3333 })).toBe(33.3333);
    expect(transferCarryingValue({ quantity: 2, availableQuantity: 3, inventoryValue: 100, unitCost: 33.3333, suggestedUnitCost: 33.3333 })).toBe(66.6667);
  });
  it('handles blank quantity and availability from older API responses', () => {
    expect(transferCarryingValue({ quantity: '', availableQuantity: 3, inventoryValue: 100 })).toBe(0);
    expect(transferCarryingValue({ quantity: 2, availableQuantity: 3, unitCost: 10 })).toBe(20);
  });
  it('uses the entered price for lower and higher overrides', () => {
    expect(transferCarryingValue({ quantity: 3, availableQuantity: 3, inventoryValue: 100, unitCost: 50, suggestedUnitCost: 33.3333 })).toBe(150);
    expect(transferCarryingValue({ quantity: 3, availableQuantity: 3, inventoryValue: 100, unitCost: 20, suggestedUnitCost: 33.3333 })).toBe(60);
  });
  it('previews oversized transfers and transfers from zero or negative stock', () => {
    expect(transferCarryingValue({ quantity: 4, availableQuantity: 3, inventoryValue: 100, unitCost: 33.3333, suggestedUnitCost: 33.3333 })).toBe(133.3333);
    expect(transferCarryingValue({ quantity: 5, availableQuantity: 0, inventoryValue: 0, unitCost: 20, suggestedUnitCost: 20 })).toBe(100);
    expect(transferCarryingValue({ quantity: 5, availableQuantity: -3, inventoryValue: -60, unitCost: 20, suggestedUnitCost: 20 })).toBe(100);
  });
  it('prices by entered cost when positive stock carries no positive value, like the server', () => {
    expect(transferCarryingValue({ quantity: 2, availableQuantity: 3, inventoryValue: 0, unitCost: 20, suggestedUnitCost: 20 })).toBe(40);
    expect(transferCarryingValue({ quantity: 3, availableQuantity: 3, inventoryValue: -10, unitCost: 20, suggestedUnitCost: 20 })).toBe(60);
  });
});
