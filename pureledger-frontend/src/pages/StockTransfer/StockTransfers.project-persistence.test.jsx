import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
      if (url === '/StockTransferApi/categories') {
        return { data: [
          { id: 100, name: 'Products A' },
          { id: 200, name: 'Products B' },
        ] };
      }
      if (url === '/StockTransferApi/accounts') {
        return { data: [{ id: 500, name: 'Product A' }] };
      }
      if (url === '/StockTransferApi/availability') {
        return { data: { availableQuantity: 5, suggestedUnitCost: 42 } };
      }
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

  it('suggests source cost and permits a lower or higher transfer price', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    fireEvent.change(screen.getByPlaceholderText('Search source').parentElement.querySelector('select'), { target: { value: '10' } });
    fireEvent.change(screen.getByPlaceholderText('Search destination').parentElement.querySelector('select'), { target: { value: '20' } });

    const categoryInput = screen.getByPlaceholderText('Search category');
    await waitFor(() => expect(categoryInput.parentElement.querySelector('select option[value="100"]')).toBeInTheDocument());
    fireEvent.change(categoryInput.parentElement.querySelector('select'), { target: { value: '100' } });
    const productInput = screen.getByPlaceholderText('Select product');
    await waitFor(() => expect(productInput.parentElement.querySelector('select option[value="500"]')).toBeInTheDocument());
    fireEvent.change(productInput.parentElement.querySelector('select'), { target: { value: '500' } });

    const transferCostHeader = screen.getByRole('columnheader', { name: 'Transfer cost' });
    expect(screen.queryByRole('columnheader', { name: 'Source avg cost' })).not.toBeInTheDocument();
    const costInput = transferCostHeader.closest('table').querySelector('tbody tr td:nth-child(5) input');
    await waitFor(() => expect(costInput).toHaveValue(42));
    expect(costInput).not.toHaveAttribute('readonly');
    expect(costInput).toHaveAccessibleName('Transfer cost');
    fireEvent.change(costInput, { target: { value: '50' } });
    expect(costInput).toHaveValue(50);
    fireEvent.change(costInput, { target: { value: '30' } });
    expect(costInput).toHaveValue(30);
  });

  it.each([[null, 2, 5], [50, 2, 5], [30, 2, 5], [null, 12, 5], [50, 12, 5], [50, 12, 0], [null, 12, -4]])('submits price %s and quantity %s with availability %s', async (price, quantity, availableQuantity) => {
    const defaultGet = apiClient.get.getMockImplementation();
    apiClient.get.mockImplementation((url, config) => {
      if (url === '/StockTransferApi/settings') return Promise.resolve({ data: { isConfigured: true } });
      if (url === '/StockTransferApi/availability') return Promise.resolve({ data: { availableQuantity, suggestedUnitCost: 42 } });
      return defaultGet(url, config);
    });
    apiClient.post.mockResolvedValue({ data: { transferNumber: 'ST-test' } });
    render(<MemoryRouter><StockTransfers /></MemoryRouter>);
    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    fireEvent.change(screen.getByPlaceholderText('Search source').parentElement.querySelector('select'), { target: { value: '10' } });
    fireEvent.change(screen.getByPlaceholderText('Search destination').parentElement.querySelector('select'), { target: { value: '20' } });
    const category = screen.getByPlaceholderText('Search category');
    await waitFor(() => expect(category.parentElement.querySelector('select option[value="100"]')).toBeInTheDocument());
    fireEvent.change(category.parentElement.querySelector('select'), { target: { value: '100' } });
    const product = screen.getByPlaceholderText('Select product');
    await waitFor(() => expect(product.parentElement.querySelector('select option[value="500"]')).toBeInTheDocument());
    fireEvent.change(product.parentElement.querySelector('select'), { target: { value: '500' } });
    const cost = screen.getByRole('spinbutton', { name: 'Transfer cost' });
    await waitFor(() => expect(cost).toHaveValue(42));
    const row = cost.closest('tr');
    const quantityInput = row.querySelector('td:nth-child(4) input');
    expect(quantityInput).not.toHaveAttribute('max');
    fireEvent.change(quantityInput, { target: { value: String(quantity) } });
    if (price != null) fireEvent.change(cost, { target: { value: String(price) } });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Dispatch transfer' })).toBeEnabled());
    fireEvent.submit(cost.closest('form'));
    await waitFor(() => expect(apiClient.post).toHaveBeenCalledWith('/StockTransferApi/dispatch', expect.objectContaining({
      items: [{ productAccountId: 500, quantity, unitCost: price }]
    })));
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

    expect(labels).toEqual(['Since', 'To', 'Project', 'Warehouse', 'Category']);
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
    expect(warehouseField.nextElementSibling).toHaveTextContent('Category');
    expect(warehouseField.nextElementSibling.nextElementSibling).toBe(searchColumn);
  });

  it('filters both transfer lists by category and clears it when the project changes', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );

    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    fireEvent.click(screen.getByRole('tab', { name: /In Transit/ }));
    await waitFor(() => expect(within(document.querySelector('.tab-pane.active .transfer-filter-card')).getByRole('button', { name: 'Search' })).toBeEnabled());
    let filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    await waitFor(() => expect(within(document.querySelector('.tab-pane.active .transfer-filter-card')).getByPlaceholderText('All categories').parentElement.querySelector('select option[value="100"]')).toBeInTheDocument());
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    let categoryInput = within(filterCard).getByPlaceholderText('All categories');
    fireEvent.focus(categoryInput);
    fireEvent.click(categoryInput.parentElement.querySelector('.searchable-select-option[data-value="100"]'));
    await waitFor(() => expect(within(document.querySelector('.tab-pane.active .transfer-filter-card')).getByPlaceholderText('All categories')).toHaveValue('Products A'));
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    await waitFor(() => expect(within(document.querySelector('.tab-pane.active .transfer-filter-card')).getByRole('button', { name: 'Search' })).toBeEnabled());
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    fireEvent.click(within(filterCard).getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith('/StockTransferApi', {
      params: expect.objectContaining({ projectId: '1', categoryId: 100, statusGroup: 'Open' }),
    }));

    fireEvent.click(screen.getByRole('tab', { name: /History/ }));
    await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith('/StockTransferApi', {
      params: expect.objectContaining({ projectId: '1', categoryId: 100, statusGroup: 'Final' }),
    }));

    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    const projectInput = within(filterCard).getByPlaceholderText('All projects');
    fireEvent.change(projectInput.parentElement.querySelector('select'), { target: { value: '2' } });
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    categoryInput = within(filterCard).getByPlaceholderText('All categories');
    expect(categoryInput).toHaveValue('');
    await waitFor(() => expect(within(document.querySelector('.tab-pane.active .transfer-filter-card')).getByRole('button', { name: 'Search' })).toBeEnabled());
    filterCard = document.querySelector('.tab-pane.active .transfer-filter-card');
    fireEvent.click(within(filterCard).getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith('/StockTransferApi', {
      params: expect.objectContaining({ projectId: 2, statusGroup: 'Final' }),
    }));
    const latestListCall = apiClient.get.mock.calls.filter(([url]) => url === '/StockTransferApi').at(-1);
    expect(latestListCall[1].params).not.toHaveProperty('categoryId');
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

  it('keeps keyboard focus on the product field after Enter so Tab continues to the next column', async () => {
    render(
      <MemoryRouter>
        <StockTransfers />
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByPlaceholderText('Search project')).toHaveValue('Main Project'));
    fireEvent.change(screen.getByPlaceholderText('Search source').parentElement.querySelector('select'), { target: { value: '10' } });
    fireEvent.change(screen.getByPlaceholderText('Search destination').parentElement.querySelector('select'), { target: { value: '20' } });
    const categoryInput = screen.getByPlaceholderText('Search category');
    await waitFor(() => expect(categoryInput.parentElement.querySelector('select option[value="100"]')).toBeInTheDocument());
    fireEvent.change(categoryInput.parentElement.querySelector('select'), { target: { value: '100' } });
    const productInput = screen.getByPlaceholderText('Select product');
    await waitFor(() => expect(productInput).toBeEnabled());
    await waitFor(() => expect(productInput.parentElement.querySelector('select option[value="500"]')).toBeInTheDocument());
    const row = productInput.closest('tr');

    const user = userEvent.setup();
    await user.click(productInput);
    await user.keyboard('{Enter}');

    await waitFor(() => expect(productInput).toHaveValue('Product A'));
    await waitFor(() => expect(row.querySelector('td:nth-child(3)')).toHaveTextContent('5'));
    // The row must not be recreated: a recreated row loses focus and the next Tab restarts from the top of the page.
    expect(screen.getByPlaceholderText('Select product')).toBe(productInput);
    expect(productInput.closest('tr')).toBe(row);
    expect(productInput).toHaveFocus();

    await user.tab();
    expect(row.querySelector('input[type="number"]')).toHaveFocus();
  });
});
