import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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
});
