import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AgingFifoModal from './AgingFifoModal';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({ default: { get: vi.fn() } }));
const target = {
  accountId: 100, accountName: 'Customer A', scope: 'full', totalDue: 50, dueTillDate: 25,
  filters: { projectId: 1, categoryId: 10, referenceDate: '2026-10-20', projectName: 'Main', categoryName: 'Customers' }
};
const row = {
  journalEntryId: 1, journalVoucherId: 11, entryDate: '2026-10-01', creditableDate: '2026-11-01',
  description: 'Purchase A', debit: 100, credit: 0, settledDebit: 50, remainingDebit: 50,
  appliedCredit: 0, unappliedCredit: 0, runningBalance: 100, allocationCount: 1
};
const response = (changes = {}) => ({ data: { success: true, data: {
  ledgerVersion: 'version-1', rows: [row], allocations: [], page: 0, pageSize: 100, totalRecords: 1, hasMoreData: false,
  summary: { totalDebits: 100, totalCredits: 50, outstandingDebits: 50, unappliedCredits: 0, netBalance: 50, mixedEntryCount: 0 },
  ...changes
} } });

describe('AgingFifoModal', () => {
  beforeEach(() => { vi.clearAllMocks(); apiClient.get.mockResolvedValue(response()); });

  it('loads lazily and requests the applied report scope; displays outstanding and allocated payments', async () => {
    const { rerender } = render(<AgingFifoModal target={null} onHide={vi.fn()} />);
    expect(apiClient.get).not.toHaveBeenCalled();
    rerender(<AgingFifoModal target={target} onHide={vi.fn()} />);
    await screen.findByText('Purchase A');
    expect(apiClient.get).toHaveBeenCalledWith('/ReportingApi/aging-report-fifo', {
      params: { projectId: 1, categoryId: 10, accountId: 100, referenceDate: '2026-10-20', scope: 'full', view: 'outstanding', page: 0, pageSize: 100 },
      signal: expect.any(AbortSignal)
    });
    apiClient.get.mockResolvedValueOnce(response({ rows: [], allocations: [{
      debitEntryId: 1, creditEntryId: 2, debitVoucherId: 11, creditVoucherId: 12,
      debitEntryDate: '2026-10-01', creditEntryDate: '2026-10-02', amount: 50,
      debitDescription: 'Purchase A', creditDescription: 'Payment by bank'
    }] }));
    fireEvent.click(screen.getByRole('button', { name: 'View allocations for entry 1' }));
    await screen.findByText('Payment by bank');
    expect(apiClient.get.mock.calls.at(-1)[1].params).toMatchObject({ view: 'allocations', entryId: 1, page: 0 });
  });

  it('switches scope and views with page reset', async () => {
    render(<AgingFifoModal target={target} onHide={vi.fn()} />);
    await screen.findByText('Purchase A');
    fireEvent.change(screen.getByLabelText('FIFO scope'), { target: { value: 'due' } });
    await waitFor(() => expect(apiClient.get.mock.calls.at(-1)[1].params.scope).toBe('due'));
    fireEvent.click(screen.getByRole('button', { name: 'Payments & Credits' }));
    await waitFor(() => expect(apiClient.get.mock.calls.at(-1)[1].params).toMatchObject({ scope: 'due', view: 'credits', page: 0 }));
  });

  it('cancels stale account requests and ignores their responses', async () => {
    let finishOld;
    apiClient.get.mockImplementationOnce(() => new Promise(resolve => { finishOld = resolve; }));
    const { rerender } = render(<AgingFifoModal target={target} onHide={vi.fn()} />);
    const oldSignal = apiClient.get.mock.calls[0][1].signal;
    apiClient.get.mockResolvedValueOnce(response({ rows: [{ ...row, description: 'Customer B purchase' }] }));
    rerender(<AgingFifoModal target={{ ...target, accountId: 200, accountName: 'Customer B' }} onHide={vi.fn()} />);
    await screen.findByText('Customer B purchase');
    expect(oldSignal.aborted).toBe(true);
    await act(async () => finishOld(response()));
    expect(screen.queryByText('Purchase A')).not.toBeInTheDocument();
  });

  it('aborts requests on close', () => {
    apiClient.get.mockReturnValue(new Promise(() => {}));
    const { rerender } = render(<AgingFifoModal target={target} onHide={vi.fn()} />);
    const signal = apiClient.get.mock.calls[0][1].signal;
    rerender(<AgingFifoModal target={null} onHide={vi.fn()} />);
    expect(signal.aborted).toBe(true);
  });

  it('keeps errors inside the modal and retries', async () => {
    apiClient.get.mockRejectedValueOnce(new Error('Connection failed'));
    render(<AgingFifoModal target={target} onHide={vi.fn()} />);
    await screen.findByText('Connection failed');
    fireEvent.click(screen.getByRole('button', { name: 'Retry breakdown' }));
    await screen.findByText('Purchase A');
  });

  it('pages server results without recalculating balances locally and warns when ledger changes', async () => {
    apiClient.get.mockResolvedValueOnce(response({ totalRecords: 101, hasMoreData: true }));
    render(<AgingFifoModal target={target} onHide={vi.fn()} />);
    await screen.findByText('Purchase A');
    apiClient.get.mockResolvedValueOnce(response({ page: 1, totalRecords: 101, rows: [{ ...row, description: 'Last debit' }], ledgerVersion: 'version-2' }));
    fireEvent.click(screen.getByRole('button', { name: 'Next entries' }));
    await screen.findByText('Last debit');
    expect(apiClient.get.mock.calls.at(-1)[1].params.page).toBe(1);
    expect(screen.getByText(/ledger changed while/i)).toBeInTheDocument();
  });

  it('shows report reconciliation differences and net credit balances without hiding unapplied credits', async () => {
    apiClient.get.mockResolvedValueOnce(response({ rows: [], summary: {
      totalDebits: 100, totalCredits: 120, outstandingDebits: 0, unappliedCredits: 20, netBalance: -20, mixedEntryCount: 0
    } }));
    const refresh = vi.fn();
    render(<AgingFifoModal target={target} onHide={vi.fn()} onRefreshReport={refresh} />);
    await screen.findByText('No outstanding debit entries.');
    expect(screen.getByText(/differs from the generated report/i)).toBeInTheDocument();
    expect(screen.getByText('Net credit balance')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Refresh report' }));
    expect(refresh).toHaveBeenCalledOnce();
  });
});
