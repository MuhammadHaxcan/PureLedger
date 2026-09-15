import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PaymentReceiptPrint from './PaymentReceiptPrint';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    API_BASE_URL: 'https://localhost:7062/api',
    default: { get: vi.fn() },
}));

const voucherData = {
    projectName: 'Main Project',
    createdDate: '2026-08-01',
    totalDebit: 100,
    totalCredit: 100,
    approvalAudit: {
        submittedByUsername: 'submitter_user',
        decisionByUsername: 'approver_user',
        submittedDate: '2026-07-31',
        decisionDate: '2026-08-01',
    },
    entries: [
        {
            journalEntryId: 1,
            categoryName: 'Hidden Category Name',
            accountName: 'Customer A',
            description: 'Cheque received',
            instrumentType: 'Cheque',
            referenceNumber: '00017',
            instrumentDate: '2026-08-01',
            debit: 100,
            credit: 0,
        },
        {
            journalEntryId: 2,
            categoryName: 'Hidden Category Name',
            accountName: 'Main Bank',
            description: 'Balancing row',
            instrumentType: 'Cash',
            referenceNumber: null,
            instrumentDate: null,
            debit: 0,
            credit: 100,
        },
    ],
};

describe('PaymentReceiptPrint', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        apiClient.get.mockResolvedValue({ data: { success: true, data: null } });
    });

    it('uses the Journal Voucher print structure without category or approval audit metadata', () => {
        render(<PaymentReceiptPrint voucherData={voucherData} voucherId="41" />);

        expect(screen.getByText('Journal Voucher #41')).toBeInTheDocument();
        const voucherTable = screen.getAllByRole('table').find(table => (
            within(table).queryByRole('columnheader', { name: 'Account' })
        ));
        expect(voucherTable).toBeTruthy();
        expect(within(voucherTable).queryByRole('columnheader', { name: 'Category' })).not.toBeInTheDocument();
        expect(within(voucherTable).getByRole('columnheader', { name: 'Instrument' })).toBeInTheDocument();
        expect(within(voucherTable).getByRole('columnheader', { name: 'Reference/Cheque No.' })).toBeInTheDocument();
        expect(screen.queryByText('Approval audit', { exact: false })).not.toBeInTheDocument();
        expect(screen.queryByText('submitter_user')).not.toBeInTheDocument();
        expect(screen.queryByText('approver_user')).not.toBeInTheDocument();
        expect(screen.queryByText('Hidden Category Name')).not.toBeInTheDocument();
        expect(screen.getByText('Accountant')).toBeInTheDocument();
        expect(screen.getByText('Authorized By')).toBeInTheDocument();
        expect(screen.getByText('Received/Paid By')).toBeInTheDocument();
    });

    it('keeps cash reference and instrument date cells blank', () => {
        render(<PaymentReceiptPrint voucherData={voucherData} voucherId="41" />);

        const cashCell = screen.getByText('Cash');
        const cashRow = cashCell.closest('tr');
        expect(cashRow.children[3]).toHaveTextContent('');
        expect(cashRow.children[4]).toHaveTextContent('');
    });

    it('hides the credit-side account name when printing debit only', () => {
        render(<PaymentReceiptPrint voucherData={voucherData} voucherId="41" filter="debit" />);

        expect(screen.getByText('Customer A')).toBeInTheDocument();
        expect(screen.queryByText('Main Bank')).not.toBeInTheDocument();
    });

    it('hides the debit-side account name when printing credit only', () => {
        render(<PaymentReceiptPrint voucherData={voucherData} voucherId="41" filter="credit" />);

        expect(screen.queryByText('Customer A')).not.toBeInTheDocument();
        expect(screen.getByText('Main Bank')).toBeInTheDocument();
    });
});
