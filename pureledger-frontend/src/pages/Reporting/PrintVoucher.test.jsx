import { render, screen, within } from '@testing-library/react';
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

  it.each([
    ['DC', 'Karachi', 1],
    ['DC', '', 0],
    ['DC', '  ', 0],
    ['DC', null, 0],
    ['INV', 'Karachi', 0],
    ['JV', 'Karachi', 0],
  ])('prints Station only once on a nonblank DC (%s, %s)', (type, station, count) => {
    render(<PrintVoucher voucherType={type} voucherId="17" voucherData={{
      createdDate: '2026-10-10', entries: [], station,
    }} />);
    expect(screen.queryAllByText('Station:')).toHaveLength(count);
    if (count) expect(screen.getByText('Karachi')).toBeInTheDocument();
    else expect(screen.queryByText('Karachi')).not.toBeInTheDocument();
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

    expect(screen.getByText('Journal Voucher', { selector: 'h1' })).toBeInTheDocument();
    expect(screen.getByText('#17')).toBeInTheDocument();
    expect(screen.getByText('Cash Account')).toBeInTheDocument();
  });

  it('uses the selected project logo in a journal voucher print', () => {
    const imageDataUrl = 'data:image/png;base64,iVBORw0KGgo=';
    render(<PrintVoucher voucherType="JV" voucherId="17" voucherData={{
      createdDate: '2026-08-02', entries: [], projectImage: imageDataUrl,
    }} />);
    expect(screen.getByAltText('Project Logo')).toHaveAttribute('src', imageDataUrl);
  });

  it.each([
    ['debit', ['Debit A', 'Debit B', 'Credit A', 'Credit B']],
    ['credit', ['Credit A', 'Credit B', 'Debit A', 'Debit B']],
    ['all', ['Credit A', 'Debit A', 'Credit B', 'Debit B']],
  ])('prints visible %s accounts first while keeping whole JV rows together', (filter, expectedDescriptions) => {
    const entries = [
      { accountName: 'Credit Account A', description: 'Credit A', debit: 0, credit: 70 },
      { accountName: 'Debit Account A', description: 'Debit A', debit: 100, credit: 0 },
      { accountName: 'Credit Account B', description: 'Credit B', debit: 0, credit: 230 },
      { accountName: 'Debit Account B', description: 'Debit B', debit: 200, credit: 0 },
    ];
    render(<PrintVoucher voucherType="JV" voucherId="17" filter={filter} voucherData={{
      createdDate: '2026-08-02', entries, totalDebit: 300, totalCredit: 300,
    }} />);

    const table = screen.getAllByRole('table').find(candidate =>
      within(candidate).queryByRole('columnheader', { name: 'Account' }));
    const rows = [...table.querySelectorAll('tbody tr')];
    expect(rows.map(row => row.cells[1].textContent)).toEqual(expectedDescriptions);
    rows.forEach(row => {
      const description = row.cells[1].textContent;
      const side = description.startsWith('Debit') ? 'debit' : 'credit';
      expect(row.cells[0].textContent).toBe(filter === 'all' || filter === side
        ? `${side === 'debit' ? 'Debit' : 'Credit'} Account ${description.at(-1)}`
        : '');
      expect(row.cells[side === 'debit' ? 4 : 5].textContent).toMatch(/\d+\.00/);
    });
    const totalCells = table.querySelector('tfoot tr').cells;
    expect(totalCells[totalCells.length - 2]).toHaveTextContent('300.00');
    expect(totalCells[totalCells.length - 1]).toHaveTextContent('300.00');
    expect(entries.map(entry => entry.description)).toEqual(['Credit A', 'Debit A', 'Credit B', 'Debit B']);
  });
});
