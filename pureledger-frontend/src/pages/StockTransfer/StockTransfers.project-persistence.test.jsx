import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import StockTransfers from './StockTransfers';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('../../contexts/AuthContext', () => ({
  useAuth: () => ({ user: { isAdmin: false } }),
}));

describe('Stock Transfer project persistence', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    apiClient.get.mockImplementation(async (url) => {
      if (url === '/StockTransferApi/init') {
        return {
          data: {
            projects: [
              { id: 1, name: 'Main Project' },
              { id: 2, name: 'Remembered Project' },
            ],
            locations: [
              { id: 10, name: 'Source Warehouse' },
              { id: 20, name: 'Destination Warehouse' },
            ],
          },
        };
      }
      if (url === '/StockTransferApi/categories') return { data: [] };
      if (url === '/StockTransferApi/settings') return { data: {} };
      if (url === '/StockTransferApi') return { data: { items: [], totalCount: 0, hasMoreData: false } };
      throw new Error(`Unexpected GET ${url}`);
    });
  });

  it('restores the same last accessible project used by Journal Entry', async () => {
    localStorage.setItem('pl_last_selected_project', '2');

    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByPlaceholderText('Search project')).toHaveValue('Remembered Project');
      expect(apiClient.get).toHaveBeenCalledWith('/StockTransferApi/categories', {
        params: { projectId: '2', accountGroupId: 2 },
      });
      expect(apiClient.get).toHaveBeenCalledWith('/StockTransferApi/categories', {
        params: { projectId: '2', accountGroupId: 1 },
      });
    });

    fireEvent.click(screen.getByRole('tab', { name: /In Transit/ }));
    const filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    expect(within(filterCard).getByPlaceholderText('All projects')).toHaveValue('Remembered Project');
    await waitFor(() => {
      const activeFilterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
      expect(within(activeFilterCard).getByRole('button', { name: 'Search' })).toBeEnabled();
    });
  });

  it('locks product category selection until both warehouses are selected', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    const categoryInput = screen.getByPlaceholderText('Search category');
    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    expect(categoryInput).toBeDisabled();

    const sourceInput = screen.getByPlaceholderText('Search source');
    fireEvent.change(sourceInput.parentElement.querySelector('select'), { target: { value: '10' } });
    expect(categoryInput).toBeDisabled();

    const destinationInput = screen.getByPlaceholderText('Search destination');
    fireEvent.change(destinationInput.parentElement.querySelector('select'), { target: { value: '20' } });
    await waitFor(() => expect(categoryInput).toBeEnabled());
  });

  it('places the separate history dates before the other filters', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole('tab', { name: /History/ }));
    const filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    const labels = [...filterCard.querySelectorAll('.form-label')].map(label => label.textContent.trim());

    expect(labels).toEqual(['Since', 'To', 'Project', 'Warehouse']);
    expect(within(filterCard).getByLabelText('Dispatch date from').closest('.input-group')).not.toHaveClass('input-group-sm');
    expect(within(filterCard).getByLabelText('Dispatch date to').closest('.input-group')).not.toHaveClass('input-group-sm');
    expect(within(filterCard).queryByText('Dispatch date range')).not.toBeInTheDocument();
  });

  it('keeps the in-transit search action on the filter row after Warehouse', () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole('tab', { name: /In Transit/ }));
    const filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    const warehouseField = within(filterCard).getByText('Warehouse').closest('[class*="col-"]');
    const searchColumn = within(filterCard).getByRole('button', { name: 'Search' }).closest('[class*="col-"]');

    expect(searchColumn).toHaveClass('col-xl-2');
    expect(warehouseField.nextElementSibling).toBe(searchColumn);
  });

  it('disables search without a project in both result tabs', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    fireEvent.click(screen.getByRole('tab', { name: /In Transit/ }));
    let filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    const projectInput = within(filterCard).getByPlaceholderText('All projects');
    fireEvent.change(projectInput.parentElement.querySelector('select'), { target: { value: '' } });
    expect(within(filterCard).getByRole('button', { name: 'Search' })).toBeDisabled();

    fireEvent.click(screen.getByRole('tab', { name: /History/ }));
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    expect(within(filterCard).getByRole('button', { name: 'Search' })).toBeDisabled();
  });
});
