import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProductReport from './ProductReport';
import apiClient from '../../services/api';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';

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
        netQuantity: 50,
        balanceQuantity: 50
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

    it('shows available source stock without treating an in-transit route as destination stock', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData,
                    productSummaries: [{
                        ...reportData.productSummaries[0],
                        transferOutwardQuantity: 4,
                        qtyBalance: 6
                    }],
                    totals: { ...reportData.totals, balanceQuantity: 6 }
                } } });
            }
            if (url === '/ReportingApi/product-report-stock-breakdown') {
                return Promise.resolve({ data: { success: true, data: {
                    productId: 100,
                    productName: 'Product A',
                    totalQuantity: 6,
                    unassignedQuantity: 0,
                    ambiguousQuantity: 0,
                    reconciliationDifference: 0,
                    assignmentIssueCount: 0,
                    locations: [{ locationId: 10, locationName: 'Warehouse X', quantity: 6 }]
                } } });
            }
            return defaultGet(url, config);
        });
        const user = userEvent.setup();
        render(<ProductReport />);

        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await user.click(await screen.findByRole('button', { name: 'View stock locations for Product A' }));

        const dialog = await screen.findByRole('dialog');
        expect(within(dialog).getByText('Warehouse X')).toBeInTheDocument();
        expect(within(dialog).getAllByText('6.00')).toHaveLength(2);
        expect(within(dialog).queryByText('Destination')).not.toBeInTheDocument();
        expect(within(dialog).queryByText(/differs from the generated report row/)).not.toBeInTheDocument();
        expect(within(dialog).queryByText(/do not have exactly one warehouse assignment/)).not.toBeInTheDocument();
    });

    it('separates incoming transit from available stock and refetches after a transfer change', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        let breakdownCalls = 0;
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData,
                    productSummaries: [{ ...reportData.productSummaries[0], qtyBalance: 6, inTransitQuantity: 4 }]
                } } });
            }
            if (url === '/ReportingApi/product-report-stock-breakdown') {
                breakdownCalls += 1;
                const outstanding = breakdownCalls === 1;
                return Promise.resolve({ data: { success: true, data: {
                    productId: 100,
                    productName: 'Product A',
                    totalQuantity: outstanding ? 6 : 10,
                    inTransitQuantity: outstanding ? 4 : 0,
                    totalStockPosition: 10,
                    unassignedQuantity: 0,
                    ambiguousQuantity: 0,
                    reconciliationDifference: 0,
                    assignmentIssueCount: 0,
                    transitRouteIssueCount: 0,
                    locations: [{ locationId: 10, locationName: 'Source', quantity: outstanding ? 6 : 10 }],
                    inTransitRoutes: outstanding ? [{
                        dispatchJournalVoucherId: 123,
                        dispatchDate: '2026-01-02',
                        sourceLocationId: 10,
                        sourceLocationName: 'Source',
                        destinationLocationId: 20,
                        destinationLocationName: 'Destination',
                        quantity: 4,
                        hasRouteIssue: false
                    }] : []
                } } });
            }
            return defaultGet(url, config);
        });
        const user = userEvent.setup();
        render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        const viewButton = await screen.findByRole('button', { name: 'View stock locations for Product A' });
        await user.click(viewButton);

        let dialog = await screen.findByRole('dialog');
        expect(within(dialog).getByText('Destination')).toBeInTheDocument();
        expect(within(dialog).getByText('Source → Destination')).toBeInTheDocument();
        expect(within(dialog).getByText('JV #123')).toBeInTheDocument();
        expect(within(dialog).getByText('Total stock position: 10.00')).toBeInTheDocument();
        expect(within(dialog).queryByText(/current stock position differs/)).not.toBeInTheDocument();
        await user.click(within(dialog).getAllByRole('button', { name: 'Close' }).at(-1));
        await user.click(viewButton);

        dialog = await screen.findByRole('dialog');
        await waitFor(() => expect(breakdownCalls).toBe(2));
        expect(within(dialog).queryByText('Destination')).not.toBeInTheDocument();
        expect(within(dialog).getByText(/current stock position differs/)).toBeInTheDocument();
    });

    it('keeps a fully in-transit product in the report and exports its available quantity unchanged', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData,
                    productSummaries: [{ ...reportData.productSummaries[0],
                        totalCreditQty: 50, qtyBalance: 0, inTransitQuantity: 10 }],
                    totals: { ...reportData.totals, totalCreditQty: 50,
                        netQuantity: 0, balanceQuantity: 0 }
                } } });
            }
            return defaultGet(url, config);
        });
        const user = userEvent.setup();
        render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByLabelText('Nil Product'));
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product A');
        await user.click(screen.getByRole('button', { name: 'Export CSV' }));
        await user.click(screen.getByRole('button', { name: 'Export PDF' }));

        expect(exportToCsvAsync).toHaveBeenCalledWith(
            [expect.objectContaining({ Product: 'Product A', 'Debit Qty': '50.00', 'Credit Qty': '50.00', 'Balance Qty': '0.00' })],
            expect.stringContaining('ProductReport_')
        );
        expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({
            rows: expect.arrayContaining([
                expect.objectContaining({ product: 'Product A', debitQty: '50.00', creditQty: '50.00', balanceQty: '0.00' }),
                expect.objectContaining({ product: 'TOTAL', debitQty: '50.00', creditQty: '50.00', balanceQty: '0.00' })
            ])
        }));
    });

    it('shows and exports transfer quantities with the physical balance after a receipt', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData,
                    productSummaries: [{
                        ...reportData.productSummaries[0],
                        totalDebitQty: 5, totalCreditQty: 2, qtyBalance: 3,
                        transferOutwardQuantity: 2, transferInwardQuantity: 2,
                        totalDebit: 860, totalCredit: 0
                    }],
                    totals: {
                        ...reportData.totals,
                        totalDebitQty: 5, totalCreditQty: 2,
                        netQuantity: 3, balanceQuantity: 3,
                        totalDebit: 860, totalCredit: 0
                    }
                } } });
            }
            return defaultGet(url, config);
        });
        const user = userEvent.setup();
        render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product A');

        const cells = [...document.querySelectorAll('#productReportTable tbody tr:first-child td')]
            .map(cell => cell.textContent.trim());
        expect(cells.slice(0, 4)).toEqual(['Product A', '5.00', '2.00', '3.00']);
        const footerCells = [...document.querySelectorAll('#productReportTable tfoot tr td')]
            .map(cell => cell.textContent.trim());
        expect(footerCells.slice(0, 4)).toEqual(['TOTAL', '5.00', '2.00', '3.00']);

        await user.click(screen.getByRole('button', { name: 'Export CSV' }));
        await user.click(screen.getByRole('button', { name: 'Export PDF' }));
        expect(exportToCsvAsync).toHaveBeenCalledWith(
            [expect.objectContaining({ Product: 'Product A', 'Debit Qty': '5.00', 'Credit Qty': '2.00', 'Balance Qty': '3.00' })],
            expect.stringContaining('ProductReport_')
        );
        expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({
            rows: expect.arrayContaining([
                expect.objectContaining({ product: 'Product A', debitQty: '5.00', creditQty: '2.00', balanceQty: '3.00' }),
                expect.objectContaining({ product: 'TOTAL', debitQty: '5.00', creditQty: '2.00', balanceQty: '3.00' })
            ])
        }));
    });

    it('does not substitute period net quantity for the total stock balance', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData,
                    productSummaries: [{ ...reportData.productSummaries[0],
                        totalDebitQty: 2, totalCreditQty: 0, qtyBalance: 2 }],
                    totals: { totalDebitQty: 2, totalCreditQty: 0, netQuantity: 0 }
                } } });
            }
            return defaultGet(url, config);
        });
        const user = userEvent.setup();
        render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product A');

        const footerCells = [...document.querySelectorAll('#productReportTable tfoot tr td')]
            .map(cell => cell.textContent.trim());
        expect(footerCells.slice(0, 4)).toEqual(['TOTAL', '2.00', '0.00', '2.00']);
        await user.click(screen.getByRole('button', { name: 'Export PDF' }));
        expect(generatePdfAsync).toHaveBeenCalledWith(expect.objectContaining({
            rows: expect.arrayContaining([
                expect.objectContaining({ product: 'TOTAL', balanceQty: '2.00' })
            ])
        }));
    });

    it('loads each page once even when the last row becomes visible during automatic paging', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        const requestedPages = [];
        globalThis.IntersectionObserver = class {
            constructor(callback) { this.callback = callback; }
            observe() { this.callback([{ isIntersecting: true }]); }
            disconnect() {}
        };
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                const page = config.params.page;
                requestedPages.push(page);
                return Promise.resolve({ data: { success: true, data: {
                    productSummaries: [{
                        ...reportData.productSummaries[0],
                        productId: page === 0 ? 100 : 101,
                        productName: page === 0 ? 'Product A' : 'Product B'
                    }],
                    totals: { ...reportData.totals, balanceQuantity: 100 },
                    totalRecords: 2,
                    hasMoreData: page === 0
                } } });
            }
            return defaultGet(url, config);
        });

        const user = userEvent.setup();
        render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product B');
        await act(async () => { await new Promise(resolve => setTimeout(resolve, 150)); });

        expect(requestedPages).toEqual([0, 1]);
        expect(document.querySelectorAll('#productReportTable tbody tr')).toHaveLength(2);
        await user.click(screen.getByRole('button', { name: 'Export CSV' }));
        expect(exportToCsvAsync).toHaveBeenCalledWith(
            [expect.objectContaining({ Product: 'Product A' }),
                expect.objectContaining({ Product: 'Product B' })],
            expect.stringContaining('ProductReport_')
        );
    });

    it('cancels a scheduled next-page request when the report is closed', async () => {
        const defaultGet = apiClient.get.getMockImplementation();
        const requestedPages = [];
        apiClient.get.mockImplementation((url, config) => {
            if (url === '/ReportingApi/product-report') {
                requestedPages.push(config.params.page);
                return Promise.resolve({ data: { success: true, data: {
                    ...reportData, totalRecords: 2, hasMoreData: true
                } } });
            }
            return defaultGet(url, config);
        });

        const user = userEvent.setup();
        const { unmount } = render(<ProductReport />);
        await chooseOption(user, 'Project', 'Project One');
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('Product A');
        unmount();
        await new Promise(resolve => setTimeout(resolve, 150));

        expect(requestedPages).toEqual([0]);
    });
});
