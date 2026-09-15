import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProductReport from './ProductReport';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    default: {
        get: vi.fn()
    }
}));

vi.mock('../../utils/exportUtils', () => ({
    exportToCsvAsync: vi.fn(),
    generatePdfAsync: vi.fn()
}));

const reportData = {
    productSummaries: [{
        productId: 100,
        productName: 'Product A',
        totalDebitQty: 50,
        totalCreditQty: 0,
        qtyBalance: 50,
        averageRate: 10,
        totalDebit: 500,
        totalCredit: 0,
        balance: 500,
        cogs: 0,
        margin: 0,
        marginPercentage: 0
    }],
    totals: {
        totalDebitQty: 50,
        totalCreditQty: 0,
        netQuantity: 50
    },
    totalRecords: 1,
    hasMoreData: false
};

const chooseOption = async (user, ariaLabel, optionText) => {
    const field = await screen.findByLabelText(ariaLabel);
    await user.click(field);
    await user.clear(field);
    await user.type(field, optionText);
    const dropdown = field.closest('.searchable-select-container').querySelector('.select-dropdown.show');
    fireEvent.click(within(dropdown).getByText(optionText, { exact: true }));
};

describe('ProductReport stock breakdown', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        globalThis.IntersectionObserver = class {
            observe() {}
            disconnect() {}
        };
        apiClient.get.mockImplementation((url) => {
            if (url === '/ReportingApi/product-report-init') {
                return Promise.resolve({
                    data: {
                        success: true,
                        projects: [{ id: 1, name: 'Project One' }],
                        locations: [{ id: 10, name: 'Warehouse X' }]
                    }
                });
            }
            if (url === '/ReportingApi/categories-by-project/1') {
                return Promise.resolve({ data: { success: true, data: [] } });
            }
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: reportData } });
            }
            if (url === '/ReportingApi/product-report-stock-breakdown') {
                return Promise.resolve({
                    data: {
                        success: true,
                        data: {
                            productId: 100,
                            productName: 'Product A',
                            totalQuantity: 50,
                            unassignedQuantity: 0,
                            ambiguousQuantity: 0,
                            reconciliationDifference: 0,
                            assignmentIssueCount: 0,
                            locations: [
                                {
                                    locationId: 10,
                                    locationName: 'Warehouse X',
                                    quantity: 30
                                },
                                {
                                    locationId: 20,
                                    locationName: 'Warehouse Y',
                                    quantity: 20
                                }
                            ]
                        }
                    }
                });
            }
            return Promise.reject(new Error(`Unexpected URL: ${url}`));
        });
    });

    it('loads the per-product modal lazily when the generated report used all locations', async () => {
        const user = userEvent.setup();
        render(<ProductReport />);

        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));

        const breakdownButton = await screen.findByRole('button', {
            name: 'View stock locations for Product A'
        });
        await user.click(breakdownButton);

        await waitFor(() => expect(apiClient.get).toHaveBeenCalledWith(
            '/ReportingApi/product-report-stock-breakdown',
            {
                params: { projectId: 1, productId: 100 },
                signal: expect.any(AbortSignal)
            }
        ));
        const dialog = await screen.findByRole('dialog');
        expect(within(dialog).getByText('Warehouse X')).toBeInTheDocument();
        expect(within(dialog).getByText('Warehouse Y')).toBeInTheDocument();
        expect(within(dialog).getByText('30.00')).toBeInTheDocument();
        expect(within(dialog).getByText('20.00')).toBeInTheDocument();
        expect(within(dialog).getByText('50.00')).toBeInTheDocument();
        expect(within(dialog).queryByRole('columnheader', { name: 'Average rate' })).not.toBeInTheDocument();
        expect(within(dialog).queryByRole('columnheader', { name: 'Stock value' })).not.toBeInTheDocument();
    });

    it('does not offer a breakdown when the generated report already targets one location', async () => {
        const user = userEvent.setup();
        render(<ProductReport />);

        await chooseOption(user, 'Project', 'Project One');
        await chooseOption(user, 'Location', 'Warehouse X');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product A');

        expect(screen.queryByRole('button', {
            name: 'View stock locations for Product A'
        })).not.toBeInTheDocument();
    });
});
