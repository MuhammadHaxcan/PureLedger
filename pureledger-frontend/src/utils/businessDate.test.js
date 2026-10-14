import { afterEach, describe, expect, it, vi } from 'vitest';
import { addDays, addMonths, todayString } from './businessDate';
import { getDateRange } from './dateFilters';

describe('business date helpers', () => {
  afterEach(() => vi.useRealTimers());

  it('uses the Pakistan calendar day, not the UTC day, after local midnight', () => {
    // 20:30 UTC on 13 Oct is 01:30 on 14 Oct in Pakistan.
    expect(todayString(new Date('2026-10-13T20:30:00Z'))).toBe('2026-10-14');
    expect(new Date('2026-10-13T20:30:00Z').toISOString().slice(0, 10)).toBe('2026-10-13');
    // 18:59 UTC is still the same Pakistan day; 19:00 UTC rolls over.
    expect(todayString(new Date('2026-10-13T18:59:00Z'))).toBe('2026-10-13');
    expect(todayString(new Date('2026-10-13T19:00:00Z'))).toBe('2026-10-14');
  });

  it('adds days across month and year ends without drift', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(addDays('2026-10-13', 0)).toBe('2026-10-13');
  });

  it('adds months and clamps to the end of a shorter month', () => {
    expect(addMonths('2026-03-31', -1)).toBe('2026-02-28');
    expect(addMonths('2026-01-15', -1)).toBe('2025-12-15');
    expect(addMonths('2026-10-13', 3)).toBe('2027-01-13');
  });

  it('builds date presets from the business date', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-10-13T20:30:00Z')); // already 14 Oct in Pakistan
    expect(getDateRange('today')).toEqual({ startDate: '2026-10-14', endDate: '2026-10-14' });
    expect(getDateRange('thisMonth').startDate).toBe('2026-10-01');
  });
});
