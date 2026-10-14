import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AccountReport from './AccountReport';
import apiClient from '../../services/api';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';

vi.mock('../../services/api', () => ({ default: { get: vi.fn() } }));
vi.mock('../../services/projectImgApi', () => ({ getProjectImageDataUrl: vi.fn().mockResolvedValue(null) }));
vi.mock('../../utils/exportUtils', () => ({
  exportToCsvAsync: vi.fn().mockResolvedValue(undefined),
  generatePdfAsync: vi.fn().mockResolvedValue(undefined),
}));

describe('Account Report booker filter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('IntersectionObserver', class {
      observe() {}
      disconnect() {}
    });
    apiClient.get.mockImplementation(async (url) => {
      if (url === '/ReportingApi/account-report-init') {
        return { data: { success: true, projects: [{ id: 1, name: 'Main' }] } };
      }
      if (url === '/ReportingApi/bookers') {
        return { data: { success: true, data: [{ id: 11, name: 'Salesperson A' }] } };
      }
      if (url === '/ReportingApi/categories-by-project/1') {
        return { data: { success: true, data: [] } };
      }
      if (url === '/ReportingApi/account-report') {
        return { data: { success: true, data: {
          entries: [{ accountId: '100', projectId: '1', categoryId: '10',
            categoryName: 'Customers', accountName: 'Customer A',
            totalDebit: 100, totalCredit: 0, netBalance: 100 }],
          totalRecords: 1, hasMoreData: false,
        } } };
      }
      throw new Error(`Unexpected GET ${url}`);
    });
  });

  it('sends the selected booker and exports the filtered rows with a PDF label', async () => {
    render(<AccountReport />);
    const projectInput = await screen.findByPlaceholderText('Search project...');
    fireEvent.change(projectInput.parentElement.querySelector('select'), { target: { value: '1' } });
    const bookerInput = screen.getByPlaceholderText('Search booker...');
    await waitFor(() => expect(bookerInput.parentElement.querySelector('select option[value="11"]')).toBeInTheDocument());
    fireEvent.change(bookerInput.parentElement.querySelector('select'), { target: { value: '11' } });
    fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));

    await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith('/ReportingApi/account-report', {
      params: expect.objectContaining({ projectId: '1', bookerId: '11' }),
    }));
    fireEvent.click(await screen.findByRole('button', { name: /Export CSV/ }));
    await waitFor(() => expect(exportToCsvAsync).toHaveBeenCalledWith(
      [expect.objectContaining({ 'Account Name': 'Customer A', Debit: '100.00' })],
      expect.stringContaining('AccountReport_'),
    ));
    fireEvent.click(screen.getByRole('button', { name: /Export PDF/ }));
    await waitFor(() => expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({
      subtitle: 'Booker: Salesperson A',
      rows: expect.arrayContaining([expect.objectContaining({ account: 'Customer A' })]),
    })));
  });

  it('loads pages sequentially with frozen filters and exports the applied labels after selectors change', async () => {
    const original = apiClient.get.getMockImplementation();
    let release;
    const pending = new Promise(resolve => { release = resolve; });
    const row = id => ({ accountId: String(id), projectId: '1', categoryId: '10', accountName: `Customer ${id}`,
      categoryName: 'Customers', totalDebit: 130, totalCredit: 40, netBalance: 90 });
    apiClient.get.mockImplementation(async (url, config) => {
      if (url === '/ReportingApi/bookers') return { data: { success: true, data: [{ id: 11, name: 'Salesperson A' }, { id: 12, name: 'Salesperson B' }] } };
      if (url !== '/ReportingApi/account-report') return original(url, config);
      if (config.params.page === 1) await pending;
      return { data: { success: true, data: { entries: [row(config.params.page + 1)], totalRecords: 3,
        hasMoreData: config.params.page < 2 } } };
    });
    render(<AccountReport />);
    const project = await screen.findByPlaceholderText('Search project...');
    fireEvent.change(project.parentElement.querySelector('select'), { target: { value: '1' } });
    const booker = screen.getByPlaceholderText('Search booker...');
    await waitFor(() => expect(booker.parentElement.querySelector('option[value="11"]')).toBeInTheDocument());
    fireEvent.change(booker.parentElement.querySelector('select'), { target: { value: '11' } });
    fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));
    await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith('/ReportingApi/account-report', { params: expect.objectContaining({ page: 1, bookerId: '11' }) }));
    expect(screen.getByPlaceholderText('Search booker...')).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Export CSV/ })).not.toBeInTheDocument();
    await act(async () => release());
    await screen.findByRole('button', { name: /Export CSV/ });
    const calls = apiClient.get.mock.calls.filter(([url]) => url === '/ReportingApi/account-report');
    expect(calls.map(([, options]) => options.params.page)).toEqual([0, 1, 2]);
    expect(calls.every(([, options]) => options.params.bookerId === '11')).toBe(true);
    fireEvent.change(booker.parentElement.querySelector('select'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: /Export PDF/ }));
    await waitFor(() => expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({ subtitle: 'Booker: Salesperson A', projectName: 'Main' })));
    fireEvent.click(screen.getByRole('button', { name: /Export CSV/ }));
    await waitFor(() => expect(exportToCsvAsync).toHaveBeenCalledWith(expect.arrayContaining([
      expect.objectContaining({ 'Account Name': 'Customer 1', Debit: '130.00', Credit: '40.00', Balance: '90.00' }),
      expect.objectContaining({ 'Account Name': 'Customer 3' }),
    ]), expect.any(String)));
    expect(exportToCsvAsync.mock.calls[0][0]).toHaveLength(3);
  });

  it('removes partial results if a later page fails and permits retry', async () => {
    const original = apiClient.get.getMockImplementation();
    let fail = true;
    apiClient.get.mockImplementation(async (url, config) => {
      if (url !== '/ReportingApi/account-report') return original(url, config);
      if (config.params.page === 1 && fail) throw new Error('Page failed');
      return { data: { success: true, data: { entries: [{ accountId: '100', accountName: 'Customer A', totalDebit: 130, totalCredit: 40 }],
        totalRecords: fail ? 2 : 1, hasMoreData: fail } } };
    });
    render(<AccountReport />);
    const project = await screen.findByPlaceholderText('Search project...');
    fireEvent.change(project.parentElement.querySelector('select'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));
    await screen.findByText('Page failed');
    expect(screen.queryByRole('button', { name: /Export CSV/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Generate Report/ })).not.toBeDisabled();
    fail = false; fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));
    await screen.findByRole('button', { name: /Export CSV/ });
    expect(screen.queryByText('Page failed')).not.toBeInTheDocument();
  });

  it('shows an empty result and ignores a response after the report has unmounted', async () => {
    const original = apiClient.get.getMockImplementation();
    let release;
    const pending = new Promise(resolve => { release = resolve; });
    apiClient.get.mockImplementation(async (url, config) => url === '/ReportingApi/account-report' ? pending : original(url, config));
    const first = render(<AccountReport />);
    const project = await screen.findByPlaceholderText('Search project...');
    fireEvent.change(project.parentElement.querySelector('select'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));
    first.unmount();
    apiClient.get.mockImplementation(async (url, config) => url === '/ReportingApi/account-report'
      ? { data: { success: true, data: { entries: [], totalRecords: 0, hasMoreData: false } } } : original(url, config));
    render(<AccountReport />);
    const next = await screen.findByPlaceholderText('Search project...');
    fireEvent.change(next.parentElement.querySelector('select'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: /Generate Report/ }));
    await screen.findByText('No accounts match the selected report filters.');
    await act(async () => release({ data: { success: true, data: { entries: [{ accountName: 'Old result' }], hasMoreData: false } } }));
    expect(screen.queryByText('Old result')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Export CSV/ })).not.toBeInTheDocument();
  });
});
