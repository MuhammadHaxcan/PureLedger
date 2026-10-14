import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AgingReport from './AgingReport';
import apiClient from '../../services/api';
import { generatePdfAsync } from '../../utils/exportUtils';

vi.mock('../../services/api', () => ({ default: { get: vi.fn() } }));
vi.mock('../../utils/exportUtils', () => ({ exportToCsvAsync: vi.fn(), generatePdfAsync: vi.fn() }));
vi.mock('../../components/SearchableSelect', () => ({ default: ({ value, onChange, options, ariaLabel, disabled }) => (
  <select aria-label={ariaLabel} value={value ?? ''} onChange={e => onChange(e.target.value)} disabled={disabled}>
    <option value="">Select</option>{options.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
  </select>
) }));
vi.mock('./AgingFifoModal', () => ({ default: ({ target }) => target && <div data-testid="fifo-target">{JSON.stringify(target)}</div> }));

const account = { accountId: '100', accountName: 'Customer A', dueTillDate: 50, totalDue: 100 };
const page = (entries = [account], hasMoreData = false, totalRecords = 1) => ({ data: { success: true, data: { entries, hasMoreData, totalRecords } } });
const selectFilters = async () => {
  await screen.findByRole('option', { name: 'Main' });
  fireEvent.change(screen.getByLabelText('Project'), { target: { value: '1' } });
  await screen.findByRole('option', { name: 'Customers' });
  fireEvent.change(screen.getByLabelText('Category'), { target: { value: '10' } });
  fireEvent.change(screen.getByLabelText('Reference Date'), { target: { value: '2026-10-20' } });
};

describe('AgingReport FIFO integration and paging', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiClient.get.mockImplementation(url => {
      if (url === '/ReportingApi/report-data') return Promise.resolve({ data: { success: true, projects: [{ id: 1, name: 'Main' }] } });
      if (url === '/ReportingApi/categories-by-project/1') return Promise.resolve({ data: { success: true, data: [{ id: 10, name: 'Customers' }] } });
      return Promise.resolve(page());
    });
  });

  it('uses the generated filters for the popup and exports after the form is edited', async () => {
    render(<AgingReport />); await selectFilters();
    fireEvent.click(screen.getByRole('button', { name: 'Generate Report' }));
    await screen.findByRole('button', { name: 'View full ledger FIFO for Customer A' });
    fireEvent.change(screen.getByLabelText('Reference Date'), { target: { value: '2026-12-01' } });
    fireEvent.click(screen.getByRole('button', { name: 'View due till date breakdown for Customer A' }));
    const target = JSON.parse(screen.getByTestId('fifo-target').textContent);
    expect(target.scope).toBe('due'); expect(target.filters.referenceDate).toBe('2026-10-20');
    expect(target.totalDue).toBe(100);
    fireEvent.click(screen.getByRole('button', { name: 'Export PDF' }));
    await waitFor(() => expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({ subtitle: 'Reference Date: 2026-10-20\nCategory: Customers' })));
  });

  it('requests pages 0, 1 and 2 once and appends every account', async () => {
    const pages = [];
    const original = apiClient.get.getMockImplementation();
    apiClient.get.mockImplementation((url, config) => {
      if (url !== '/ReportingApi/aging-report') return original(url, config);
      pages.push(config.params.page);
      return Promise.resolve(page([{ ...account, accountId: `${100 + config.params.page}`, accountName: `Account ${config.params.page}` }], config.params.page < 2, 3));
    });
    render(<AgingReport />); await selectFilters();
    fireEvent.click(screen.getByRole('button', { name: 'Generate Report' }));
    await screen.findByText('3 of 3 accounts');
    expect(pages).toEqual([0, 1, 2]);
    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeEnabled();
  });

  it('marks a later page failure incomplete and blocks export until regenerated', async () => {
    const original = apiClient.get.getMockImplementation();
    apiClient.get.mockImplementation((url, config) => {
      if (url !== '/ReportingApi/aging-report') return original(url, config);
      return config.params.page === 0 ? Promise.resolve(page([account], true, 2)) : Promise.reject(new Error('Second page failed'));
    });
    render(<AgingReport />); await selectFilters();
    fireEvent.click(screen.getByRole('button', { name: 'Generate Report' }));
    await screen.findByText(/Report is incomplete/i);
    expect(screen.queryByRole('button', { name: 'Export CSV' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Generate Report' })).toBeEnabled();
  });

  it('freezes the reference date for later pages while the next report filters are edited', async () => {
    let finishSecond;
    const requests = [];
    const original = apiClient.get.getMockImplementation();
    apiClient.get.mockImplementation((url, config) => {
      if (url !== '/ReportingApi/aging-report') return original(url, config);
      requests.push(config.params);
      if (config.params.page === 1) return new Promise(resolve => { finishSecond = resolve; });
      return Promise.resolve(page([{ ...account, accountId: `${100 + config.params.page}` }], config.params.page === 0, 3));
    });
    render(<AgingReport />); await selectFilters();
    fireEvent.click(screen.getByRole('button', { name: 'Generate Report' }));
    await waitFor(() => expect(requests).toHaveLength(2));
    fireEvent.change(screen.getByLabelText('Reference Date'), { target: { value: '2026-12-01' } });
    await act(async () => finishSecond(page([{ ...account, accountId: '101' }], true, 3)));
    await screen.findByText('3 of 3 accounts');
    expect(requests.map(x => x.referenceDate)).toEqual(['2026-10-20', '2026-10-20', '2026-10-20']);
  });

  it('cancels paging on unmount and does not launch subsequent pages', async () => {
    let finish;
    const original = apiClient.get.getMockImplementation();
    apiClient.get.mockImplementation((url, config) => url === '/ReportingApi/aging-report'
      ? new Promise(resolve => { finish = resolve; }) : original(url, config));
    const { unmount } = render(<AgingReport />); await selectFilters();
    fireEvent.click(screen.getByRole('button', { name: 'Generate Report' }));
    const signal = apiClient.get.mock.calls.find(([url]) => url === '/ReportingApi/aging-report')[1].signal;
    unmount(); expect(signal.aborted).toBe(true);
    await act(async () => finish(page([account], true, 2)));
    expect(apiClient.get.mock.calls.filter(([url]) => url === '/ReportingApi/aging-report')).toHaveLength(1);
  });
});
