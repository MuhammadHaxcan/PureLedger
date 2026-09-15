import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PrintVoucher from './PrintVoucher';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
  API_BASE_URL: 'https://localhost:7062/api',
  default: { get: vi.fn() },
}));

describe('PrintVoucher', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiClient.get.mockResolvedValue({ data: { success: true, data: null } });
  });

  it('can transition from no voucher data to a rendered journal voucher', () => {
    const { container, rerender } = render(
      <PrintVoucher voucherData={null} voucherType="JV" voucherId="17" />,
    );

    expect(container).toBeEmptyDOMElement();

    rerender(
      <PrintVoucher
        voucherType="JV"
        voucherId="17"
        voucherData={{
          createdDate: '2026-08-02',
          entries: [{ accountName: 'Cash Account', debit: 100, credit: 0 }],
          totalDebit: 100,
          totalCredit: 0,
        }}
      />,
    );

    expect(screen.getByText('Journal Voucher #17')).toBeInTheDocument();
    expect(screen.getByText('Cash Account')).toBeInTheDocument();
  });
});
