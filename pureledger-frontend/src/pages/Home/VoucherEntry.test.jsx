import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import VoucherEntry from './VoucherEntry';
import apiClient from '../../services/api';
import paymentReceiptVoucherApi from '../../services/paymentReceiptVoucherApi';

vi.mock('../../services/api', () => ({
    default: {
        get: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

vi.mock('../../services/paymentReceiptVoucherApi', () => ({
    default: {
        getPostedVoucher: vi.fn(),
    },
}));

vi.mock('../../hooks/usePermissions', () => ({
    usePermissions: () => ({ canAccessReporting: true }),
}));

const voucherResponse = (overrides = {}) => ({
    data: {
        success: true,
        voucher: {
            journalVoucherId: 41,
            projectName: 'Main Project',
            entryDate: '2026-07-31',
            creditableDate: '2026-08-01',
            totalDebit: 100,
            totalCredit: 100,
            hasPaymentReceiptDetails: false,
            isPaymentReceipt: false,
            isManualExpenseVoucher: false,
            isStockTransfer: false,
            isStockTransferGenerated: false,
            entries: [
                { entryId: 1, categoryName: 'General', accountName: 'Cash', description: 'Debit row', debit: 100, credit: 0 },
                { entryId: 2, categoryName: 'General', accountName: 'Bank', description: 'Credit row', debit: 0, credit: 100 },
            ],
            ...overrides,
        },
    },
});

const postedPaymentReceipt = (overrides = {}) => ({
    data: {
        journalVoucherId: 41,
        projectId: 1,
        projectName: 'Main Project',
        entryDate: '2026-07-31',
        isCompleted: true,
        pendingApprovalId: null,
        totalDebit: 100,
        totalCredit: 100,
        entries: [
            {
                originalJournalEntryId: 1,
                categoryName: 'Customers',
                accountName: 'Customer A',
                description: 'Cheque received',
                instrumentType: 'Cheque',
                referenceNumber: '00017',
                instrumentDate: '2026-07-31',
                debit: 100,
                credit: 0,
            },
            {
                originalJournalEntryId: 2,
                categoryName: 'Cash and Bank',
                accountName: 'Main Bank',
                description: 'Receipt balancing entry',
                instrumentType: 'Cash',
                referenceNumber: null,
                instrumentDate: null,
                debit: 0,
                credit: 100,
            },
        ],
        ...overrides,
    },
});

const LocationProbe = () => {
    const location = useLocation();
    return <div data-testid="location">{location.pathname}{location.search}</div>;
};

const renderEditor = () => render(
    <MemoryRouter initialEntries={['/home/voucher-entry?jvId=41']}>
        <Routes>
            <Route path="/home/voucher-entry" element={<><VoucherEntry /><LocationProbe /></>} />
            <Route path="*" element={<LocationProbe />} />
        </Routes>
    </MemoryRouter>
);

describe('VoucherEntry aggregate editing boundaries', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        paymentReceiptVoucherApi.getPostedVoucher.mockResolvedValue(postedPaymentReceipt());
    });

    it('loads an Expense voucher as a monetary table without individual line actions', async () => {
        apiClient.get.mockResolvedValue(voucherResponse({ isManualExpenseVoucher: true }));

        renderEditor();

        expect(await screen.findByText('Expense Voucher')).toBeInTheDocument();
        const table = screen.getByRole('table');
        expect(within(table).getByRole('columnheader', { name: 'Debit' })).toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Quantity' })).not.toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Rate' })).not.toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Discount' })).not.toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Edit Entire Voucher/i })).toBeInTheDocument();
        expect(screen.queryByTitle('Edit Entry')).not.toBeInTheDocument();
        expect(screen.queryByTitle('Delete Entry')).not.toBeInTheDocument();
        expect(screen.queryByText('Actions')).not.toBeInTheDocument();
    });

    it('redirects Expense whole-voucher editing to the main Journal Entry page', async () => {
        const user = userEvent.setup();
        apiClient.get.mockResolvedValue(voucherResponse({ isManualExpenseVoucher: true }));

        renderEditor();
        await user.click(await screen.findByRole('button', { name: /Edit Entire Voucher/i }));

        expect(screen.getByTestId('location')).toHaveTextContent('/home/entry?voucherId=41');
    });

    it('preserves individual line actions and product columns for an ordinary journal voucher', async () => {
        apiClient.get.mockResolvedValue(voucherResponse());

        renderEditor();

        const table = await screen.findByRole('table');
        expect(within(table).getByRole('columnheader', { name: 'Quantity' })).toBeInTheDocument();
        expect(await screen.findByText('Actions')).toBeInTheDocument();
        expect(screen.getAllByTitle('Edit Entry')).toHaveLength(2);
        expect(screen.getAllByTitle('Delete Entry')).toHaveLength(2);
    });

    it.each([
        [2080, 1880, 200],
        [1880, 2080, -200],
        [100, 100, 0],
    ])('shows debit minus credit after the summary totals (%s, %s)', async (totalDebit, totalCredit, balance) => {
        apiClient.get.mockResolvedValue(voucherResponse({ totalDebit, totalCredit }));

        renderEditor();

        const summary = (await screen.findByText('Balance:')).parentElement;
        const expected = new Intl.NumberFormat('en-PK', {
            style: 'currency', currency: 'PKR', minimumFractionDigits: 2,
        }).format(balance);
        expect(summary.textContent.replace(/\s+/g, ' ')).toContain(expected.replace(/\s+/g, ' '));
        expect(summary.textContent.indexOf('Balance:')).toBeGreaterThan(summary.textContent.indexOf('Total Credit:'));
    });

    it('keeps Stock Transfer navigation and its existing read-only summary behavior', async () => {
        const user = userEvent.setup();
        apiClient.get.mockResolvedValue(voucherResponse({
            isStockTransfer: true,
            isStockTransferGenerated: true,
        }));

        renderEditor();

        const openTransfer = await screen.findByRole('button', { name: /Open Stock Transfer/i });
        expect(screen.queryByTitle('Edit Entry')).not.toBeInTheDocument();
        expect(screen.queryByTitle('Delete Entry')).not.toBeInTheDocument();
        await user.click(openTransfer);
        expect(screen.getByTestId('location')).toHaveTextContent('/stock-transfers?transferId=41');
    });

    it('renders P&R as an instrument-aware summary and redirects whole-voucher editing', async () => {
        const user = userEvent.setup();
        apiClient.get.mockResolvedValue(voucherResponse({
            hasPaymentReceiptDetails: true,
            isPaymentReceipt: true,
        }));

        renderEditor();

        expect(await screen.findByText('P&R Voucher')).toBeInTheDocument();
        expect(paymentReceiptVoucherApi.getPostedVoucher).toHaveBeenCalledWith('41');
        const table = screen.getByRole('table');
        expect(within(table).getByRole('columnheader', { name: 'Instrument' })).toBeInTheDocument();
        expect(within(table).getByRole('columnheader', { name: 'Reference/Cheque No.' })).toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Actions' })).not.toBeInTheDocument();
        expect(within(table).getByText('00017')).toBeInTheDocument();
        expect(screen.queryByTitle('Edit Entry')).not.toBeInTheDocument();
        expect(screen.queryByTitle('Delete Entry')).not.toBeInTheDocument();

        await user.click(screen.getByRole('button', { name: /Edit Entire Voucher/i }));
        expect(screen.getByTestId('location')).toHaveTextContent('/home/payment-and-receipt?voucherId=41');
    });

    it('keeps Expense and P&R whole-voucher editing disabled while revisions are pending', async () => {
        apiClient.get.mockResolvedValue(voucherResponse({
            hasPaymentReceiptDetails: true,
            isPaymentReceipt: true,
        }));
        paymentReceiptVoucherApi.getPostedVoucher.mockResolvedValue(postedPaymentReceipt({
            isCompleted: false,
            pendingApprovalId: 72,
        }));

        renderEditor();

        expect(await screen.findByText(/pending as approval #72/i)).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Edit Entire Voucher/i })).toBeDisabled();
        expect(screen.queryByRole('link', { name: /Print P&R/i })).not.toBeInTheDocument();
    });

    it('blocks partially linked P&R data instead of falling back to ordinary JV actions', async () => {
        apiClient.get.mockResolvedValue(voucherResponse({
            hasPaymentReceiptDetails: true,
            isPaymentReceipt: false,
            paymentReceiptIntegrityError: 'Partial instrument linkage detected.',
        }));

        renderEditor();

        expect(await screen.findByText(/Partial instrument linkage detected/i)).toBeInTheDocument();
        expect(screen.queryByTitle('Edit Entry')).not.toBeInTheDocument();
        expect(screen.queryByTitle('Delete Entry')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Edit Entire Voucher/i })).not.toBeInTheDocument();
    });

    it('explains that a looked-up number is pending approval instead of a bare not-found warning', async () => {
        apiClient.get.mockRejectedValue({
            response: { status: 404, data: { success: false, message: 'JV-41 is pending approval. It becomes available once it is approved.' } },
        });

        renderEditor();

        expect(await screen.findByText(/JV-41 is pending approval/i)).toBeInTheDocument();
        expect(screen.queryByText(/No voucher found for the entered ID/i)).not.toBeInTheDocument();
    });

    it('explains that a looked-up number was rejected', async () => {
        apiClient.get.mockRejectedValue({
            response: { status: 404, data: { success: false, message: 'JV-41 was rejected and was never posted.' } },
        });

        renderEditor();

        expect(await screen.findByText(/JV-41 was rejected and was never posted/i)).toBeInTheDocument();
    });

    it('shows why a line could not be deleted inside the confirmation dialog instead of behind it', async () => {
        apiClient.get.mockResolvedValue(voucherResponse());
        apiClient.delete.mockRejectedValue({
            response: { data: { success: false, message: 'This voucher is pending approval and cannot be changed or printed.' } },
        });
        const user = userEvent.setup();

        renderEditor();
        await user.click((await screen.findAllByTitle('Delete Entry'))[0]);
        const dialog = await screen.findByRole('dialog');
        await user.click(within(dialog).getByRole('button', { name: 'Confirm' }));

        expect(await within(dialog).findByText(/pending approval and cannot be changed/i)).toBeInTheDocument();
        expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('shows a failed line edit inside the edit dialog instead of behind it', async () => {
        apiClient.get.mockImplementation(async (url) => {
            if (url.startsWith('/HomeApi/voucher/')) return voucherResponse();
            if (url.startsWith('/HomeApi/journal-entry/')) {
                return { data: { success: true, data: {
                    entryId: 1, categoryId: 1, accountId: 1, projectId: 1, description: 'Debit row',
                    quantity: 0, rate: 0, discount: 0, discountPercent: 0, debit: 100, credit: 0,
                } } };
            }
            return { data: { success: true, data: [] } };
        });
        apiClient.put.mockRejectedValue({
            response: { data: { success: false, message: 'Entry could not be updated.' } },
        });
        const user = userEvent.setup();

        renderEditor();
        await user.click((await screen.findAllByTitle('Edit Entry'))[0]);
        const dialog = await screen.findByRole('dialog');
        await user.click(await within(dialog).findByRole('button', { name: /Save Changes/i }));

        expect(await within(dialog).findByText('Entry could not be updated.')).toBeInTheDocument();
    });
});
