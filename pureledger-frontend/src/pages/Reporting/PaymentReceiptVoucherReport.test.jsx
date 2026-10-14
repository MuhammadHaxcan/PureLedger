import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PaymentReceiptVoucherReport from './PaymentReceiptVoucherReport';
import paymentReceiptReportApi from '../../services/paymentReceiptReportApi';

vi.mock('../../services/paymentReceiptReportApi', () => ({
    default: {
        getInitialization: vi.fn(),
        getReport: vi.fn(),
    },
}));

const reportResponse = {
    data: {
        page: 1,
        pageSize: 50,
        totalVouchers: 1,
        totalDebit: 100,
        totalCredit: 100,
        vouchers: [{
            journalVoucherId: 15,
            entryDate: '2026-08-01',
            projectName: 'Project One',
            entries: [
                {
                    journalEntryId: 1,
                    categoryName: 'Category One',
                    accountName: 'Account One',
                    description: 'Cheque payment',
                    instrumentType: 'Cheque',
                    referenceNumber: '00017',
                    instrumentDate: '2026-08-01',
                    debit: 100,
                    credit: 0,
                    isFilterMatch: false,
                },
                {
                    journalEntryId: 2,
                    categoryName: 'Category One',
                    accountName: 'Account Two',
                    description: 'Balancing entry',
                    instrumentType: 'Cash',
                    debit: 0,
                    credit: 100,
                    isFilterMatch: false,
                },
            ],
        }],
    },
};

const chooseSearchableOption = async (user, field, optionText) => {
    await user.click(field);
    await user.clear(field);
    await user.type(field, optionText);
    const dropdown = field.closest('.searchable-select-container').querySelector('.select-dropdown.show');
    fireEvent.click(within(dropdown).getByText(optionText, { exact: true }));
};

const chooseProject = async user => {
    const project = await screen.findByLabelText('Project filter');
    await chooseSearchableOption(user, project, 'Project One');
    return project;
};

describe('PaymentReceiptVoucherReport', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        paymentReceiptReportApi.getInitialization.mockResolvedValue({
            data: {
                projects: [
                    { id: 1, name: 'Project One' },
                    { id: 2, name: 'Project Two' },
                ],
                categories: [
                    { id: 10, name: 'Category One', projectId: 1 },
                    { id: 20, name: 'Category Two', projectId: 2 },
                ],
                accounts: [
                    { id: 100, name: 'Account One', categoryId: 10, projectId: 1 },
                    { id: 200, name: 'Account Two', categoryId: 20, projectId: 2 },
                ],
            },
        });
        paymentReceiptReportApi.getReport.mockResolvedValue(reportResponse);
    });

    it('requires a project and keeps category and account as dependent searchable filters', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptVoucherReport />);

        const project = await screen.findByLabelText('Project filter');
        const category = screen.getByLabelText('Category filter');
        const account = screen.getByLabelText('Account filter');
        const generate = screen.getByRole('button', { name: 'Generate Report' });

        expect(generate).toBeDisabled();
        expect(account).toBeDisabled();
        expect(project.closest('.searchable-select-container')).not.toHaveTextContent('All Projects');

        await chooseSearchableOption(user, project, 'Project One');
        expect(generate).toBeEnabled();
        await chooseSearchableOption(user, category, 'Category One');
        expect(account).toBeEnabled();
        await chooseSearchableOption(user, account, 'Account One');

        await chooseSearchableOption(user, project, 'Project Two');
        expect(category).toHaveValue('All Categories');
        expect(account).toHaveValue('All Accounts');
        expect(account).toBeDisabled();
    });

    it('submits dates and the mandatory project while rendering the simplified output table', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptVoucherReport />);

        await chooseProject(user);
        await user.selectOptions(screen.getByLabelText('Quick date filter'), 'today');
        const now = new Date();
        const expectedDate = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));

        await waitFor(() => expect(paymentReceiptReportApi.getReport).toHaveBeenCalledWith(
            expect.objectContaining({
                startDate: expectedDate,
                endDate: expectedDate,
                projectId: '1',
                search: '',
            }),
            1,
            50,
            expect.any(AbortSignal),
        ));

        const table = await screen.findByRole('table');
        expect(within(table).queryByRole('columnheader', { name: 'Project' })).not.toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Category' })).not.toBeInTheDocument();
        expect(within(table).getByRole('columnheader', { name: 'Account' })).toBeInTheDocument();
        expect(within(table).getByRole('columnheader', { name: 'Reference/Cheque No.' })).toBeInTheDocument();
        expect(within(table).getAllByText('08/01/2026')).toHaveLength(2);
        expect(within(table).queryByText('2026-08-01')).not.toBeInTheDocument();
        expect(screen.queryByText('Matching Vouchers')).not.toBeInTheDocument();
        expect(screen.queryByLabelText('Instrument type filter')).not.toBeInTheDocument();
        expect(screen.queryByLabelText('Reference or cheque number filter')).not.toBeInTheDocument();
        expect(screen.queryByLabelText('General search filter')).not.toBeInTheDocument();
    });

    it('searches the complete server-side result set by reference from above the output table', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptVoucherReport />);

        await chooseProject(user);
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        await screen.findByText('JV-15');
        paymentReceiptReportApi.getReport.mockClear();

        const resultSearch = screen.getByLabelText('Search results');
        expect(resultSearch).toHaveAttribute('placeholder', 'Filter results...');
        expect(resultSearch.closest('.input-group')).not.toHaveClass('input-group-sm');
        await user.type(resultSearch, '00017');
        expect(screen.queryByLabelText('Clear result search')).not.toBeInTheDocument();

        await waitFor(() => expect(paymentReceiptReportApi.getReport).toHaveBeenCalledWith(
            expect.objectContaining({ projectId: '1', search: '00017' }),
            1,
            50,
            expect.any(AbortSignal),
        ), { timeout: 1500 });
    });

    it('resets filters, output search, and generated results together', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptVoucherReport />);

        const project = await chooseProject(user);
        await user.click(screen.getByRole('button', { name: 'Generate Report' }));
        expect(await screen.findByText('JV-15')).toBeInTheDocument();

        await user.type(screen.getByLabelText('Search results'), 'cash');
        await user.click(screen.getByRole('button', { name: 'Reset' }));

        expect(project).toHaveValue('');
        expect(screen.queryByLabelText('Search results')).not.toBeInTheDocument();
        expect(screen.queryByText('JV-15')).not.toBeInTheDocument();
    });
});
