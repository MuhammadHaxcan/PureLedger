import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PaymentReceiptForm from './PaymentReceiptForm';
import paymentReceiptApi from '../../services/paymentReceiptApi';
import paymentReceiptVoucherApi from '../../services/paymentReceiptVoucherApi';
import approvalApi from '../../services/approvalApi';

vi.mock('../../services/paymentReceiptApi', () => ({
    default: {
        getInitialization: vi.fn(),
        getCategories: vi.fn(),
        getAccounts: vi.fn(),
        getDuplicateWarnings: vi.fn(),
        submit: vi.fn(),
        getPostedVoucher: vi.fn(),
        submitRevision: vi.fn(),
    },
}));

vi.mock('../../services/approvalApi', () => ({
    default: {
        getById: vi.fn(),
        updatePaymentReceipt: vi.fn(),
        getPaymentReceiptInitialization: vi.fn(),
        getPaymentReceiptCategories: vi.fn(),
        getPaymentReceiptAccounts: vi.fn(),
        getPaymentReceiptDuplicateWarnings: vi.fn(),
    },
}));

vi.mock('../../services/paymentReceiptVoucherApi', () => ({
    default: {
        getInitialization: vi.fn(),
        getCategories: vi.fn(),
        getAccounts: vi.fn(),
        getDuplicateWarnings: vi.fn(),
        getPostedVoucher: vi.fn(),
        submitRevision: vi.fn(),
    },
}));

describe('PaymentReceiptForm', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        localStorage.clear();
        paymentReceiptApi.getInitialization.mockResolvedValue({
            data: { projects: [{ id: 1, name: 'Main Project' }], defaultEntryDate: '2026-07-31' },
        });
        paymentReceiptApi.getCategories.mockResolvedValue({ data: [] });
        paymentReceiptApi.getAccounts.mockResolvedValue({ data: [] });
        paymentReceiptApi.getDuplicateWarnings.mockResolvedValue({ data: { warnings: [] } });
        paymentReceiptVoucherApi.getInitialization.mockResolvedValue({ data: { projects: [{ id: 1, name: 'Main Project' }] } });
        paymentReceiptVoucherApi.getCategories.mockResolvedValue({ data: [] });
        paymentReceiptVoucherApi.getAccounts.mockResolvedValue({ data: [] });
        paymentReceiptVoucherApi.getDuplicateWarnings.mockResolvedValue({ data: { warnings: [] } });
        approvalApi.getPaymentReceiptInitialization.mockResolvedValue({ data: { projects: [{ id: 1, name: 'Main Project' }] } });
        approvalApi.getPaymentReceiptCategories.mockResolvedValue({ data: [] });
        approvalApi.getPaymentReceiptAccounts.mockResolvedValue({ data: [] });
        approvalApi.getPaymentReceiptDuplicateWarnings.mockResolvedValue({ data: { warnings: [] } });
        approvalApi.getById.mockResolvedValue({ data: {
            approvalVoucherId: 7,
            voucherType: 'PaymentReceipt',
            status: 'Pending',
            projectId: 1,
            projectName: 'Main Project',
            requestedEntryDate: '2026-07-31',
            snapshotVersion: '66ea1753-067a-4fd3-a7a9-16ef5fcf86e1',
            entries: [
                { categoryId: 2, accountId: 3, instrumentType: 'Cash', debit: 100, credit: 0 },
                { categoryId: 2, accountId: 4, instrumentType: 'Cash', debit: 0, credit: 100 },
            ],
        } });
    });

    it('clears and disables cheque fields when the instrument changes to Cash', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptForm />);

        const instrument = await screen.findByLabelText('Entry 1 instrument type');
        const reference = screen.getByLabelText('Entry 1 Reference No.');
        const instrumentDate = screen.getByLabelText('Entry 1 instrument date');

        expect(reference).toBeDisabled();
        expect(instrumentDate).toBeDisabled();

        await user.selectOptions(instrument, 'Cheque');
        await user.type(reference, '000123');
        await user.type(instrumentDate, '2026-07-20');
        expect(reference).toHaveValue('000123');
        expect(instrumentDate).toHaveValue('2026-07-20');

        await user.selectOptions(instrument, 'Cash');
        await waitFor(() => {
            expect(reference).toBeDisabled();
            expect(reference).toHaveValue('');
            expect(instrumentDate).toBeDisabled();
            expect(instrumentDate).toHaveValue('');
        });
    });

    it('defaults transfer dates to today but leaves Cheque dates for deliberate selection', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptForm />);

        const instrument = await screen.findByLabelText('Entry 1 instrument type');
        const instrumentDate = screen.getByLabelText('Entry 1 instrument date');
        const now = new Date();
        const offset = now.getTimezoneOffset();
        const localToday = new Date(now.getTime() - offset * 60000).toISOString().slice(0, 10);

        await user.selectOptions(instrument, 'BankTransfer');
        expect(instrumentDate).toHaveValue(localToday);

        await user.selectOptions(instrument, 'Cheque');
        expect(instrumentDate).toHaveValue('');

        await user.selectOptions(instrument, 'WireTransfer');
        expect(instrumentDate).toHaveValue(localToday);
    });

    it('keeps the minimum two rows and disables their delete buttons', async () => {
        render(<PaymentReceiptForm />);
        await screen.findByLabelText('Entry 1 instrument type');
        expect(screen.getAllByTitle('Delete entry from proposed voucher')).toHaveLength(2);
        screen.getAllByTitle('Delete entry from proposed voucher').forEach(button => expect(button).toBeDisabled());
    });

    it('uses Journal Entry searchable selectors without a line-number column', async () => {
        render(<PaymentReceiptForm />);

        expect(await screen.findByPlaceholderText('Select Project')).toHaveClass('select-search');
        expect(screen.queryByRole('columnheader', { name: '#' })).not.toBeInTheDocument();
    });

    it('restores the last accessible Journal Entry project for a new request', async () => {
        localStorage.setItem('pl_last_selected_project', '2');
        paymentReceiptApi.getInitialization.mockResolvedValue({
            data: {
                projects: [
                    { id: 1, name: 'Main Project' },
                    { id: 2, name: 'Remembered Project' },
                ],
            },
        });

        render(<PaymentReceiptForm />);

        expect(await screen.findByLabelText('Project')).toHaveValue('Remembered Project');
        await waitFor(() => {
            expect(paymentReceiptApi.getCategories).toHaveBeenCalledWith('2');
        });
    });

    it('adds a row with Alt+N using the same shortcut as Journal Entry', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptForm />);

        await screen.findByLabelText('Entry 1 instrument type');
        expect(screen.getAllByTitle('Delete entry from proposed voucher')).toHaveLength(2);

        await user.keyboard('{Alt>}n{/Alt}');

        expect(screen.getAllByTitle('Delete entry from proposed voucher')).toHaveLength(3);
        expect(screen.getByRole('button', { name: /Add Row \(Alt\+N\)/i })).toBeInTheDocument();
    });

    it('does not let Alt+N change a read-only submitted request', async () => {
        const user = userEvent.setup();
        render(<PaymentReceiptForm mode="revision" journalVoucherId="20" initialData={{
            journalVoucherId: 20,
            projectId: 1,
            projectName: 'Main Project',
            entryDate: '2026-07-31',
            voucherVersion: 'AQID',
            pendingApprovalId: 12,
            entries: [
                { originalJournalEntryId: 10, categoryId: 2, accountId: 3, instrumentType: 'Cash', debit: 100, credit: 0 },
                { originalJournalEntryId: 11, categoryId: 2, accountId: 4, instrumentType: 'Cash', debit: 0, credit: 100 },
            ],
        }} />);

        await screen.findByText(/already pending as approval #12/i);
        await user.keyboard('{Alt>}n{/Alt}');

        expect(screen.getAllByTitle('Delete entry from proposed voucher')).toHaveLength(2);
    });

    it('shows duplicate warnings only on the matching rows', async () => {
        render(<PaymentReceiptForm mode="revision" journalVoucherId="20" initialData={{
            journalVoucherId: 20,
            projectId: 1,
            projectName: 'Main Project',
            entryDate: '2026-07-31',
            voucherVersion: 'AQID',
            duplicateWarnings: [
                { instrumentType: 'BankTransfer', referenceNumber: '1001', message: 'Duplicate reference.' },
            ],
            entries: [
                { originalJournalEntryId: 10, categoryId: 2, accountId: 3, instrumentType: 'BankTransfer', referenceNumber: '1001', instrumentDate: '2026-07-31', debit: 100, credit: 0 },
                { originalJournalEntryId: 11, categoryId: 2, accountId: 4, instrumentType: 'Cash', debit: 0, credit: 100 },
            ],
        }} />);

        expect(await screen.findByText('Match')).toBeInTheDocument();
        expect(screen.queryByText(/Duplicate-reference warning/i)).not.toBeInTheDocument();
        expect(screen.getByText('Match').closest('tr')).toHaveClass('duplicate-warning');
    });

    it('uses ApprovalApi metadata for pending-only editing', async () => {
        render(<PaymentReceiptForm mode="pending" approvalId="7" />);
        await screen.findByText('Edit Pending Payment & Receipt — #7');
        expect(approvalApi.getPaymentReceiptInitialization).toHaveBeenCalledTimes(1);
        expect(paymentReceiptApi.getInitialization).not.toHaveBeenCalled();
        expect(paymentReceiptVoucherApi.getInitialization).not.toHaveBeenCalled();
        expect(screen.getByLabelText('Project')).toBeDisabled();
        expect(screen.getByLabelText(/Entry Date/)).not.toBeDisabled();
    });

    it('uses VoucherEntry-authorized metadata for posted revisions', async () => {
        render(<PaymentReceiptForm mode="revision" journalVoucherId="20" initialData={{
            journalVoucherId: 20,
            projectId: 1,
            projectName: 'Main Project',
            entryDate: '2026-07-31',
            voucherVersion: 'AQID',
            entries: [
                { originalJournalEntryId: 10, categoryId: 2, accountId: 3, instrumentType: 'Cash', debit: 100, credit: 0 },
                { originalJournalEntryId: 11, categoryId: 2, accountId: 4, instrumentType: 'Cash', debit: 0, credit: 100 },
            ],
        }} />);
        await screen.findByText('Edit Payment & Receipt — JV-20');
        expect(paymentReceiptVoucherApi.getInitialization).toHaveBeenCalledTimes(1);
        expect(paymentReceiptApi.getInitialization).not.toHaveBeenCalled();
        expect(approvalApi.getPaymentReceiptInitialization).not.toHaveBeenCalled();
        expect(screen.getByLabelText(/Entry Date/)).not.toBeDisabled();
        expect(screen.getByLabelText('Project')).toBeDisabled();
    });
});
