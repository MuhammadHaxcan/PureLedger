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

    it('uses the selected project logo in a payment receipt print', () => {
        const imageDataUrl = 'data:image/png;base64,iVBORw0KGgo=';
        render(<PaymentReceiptPrint voucherData={{ ...voucherData, projectImage: imageDataUrl }} voucherId="41" />);
        expect(screen.getByAltText('Project Logo')).toHaveAttribute('src', imageDataUrl);
    });

    it('uses the Journal Voucher print structure without category or approval audit metadata', () => {
        render(<PaymentReceiptPrint voucherData={voucherData} voucherId="41" />);

        expect(screen.getByRole('heading', { name: 'Journal Voucher' })).toBeInTheDocument();
        expect(screen.getByText('#41')).toBeInTheDocument();
        const voucherTable = screen.getAllByRole('table').find(table => (
            within(table).queryByRole('columnheader', { name: 'Account' })
        ));
        expect(voucherTable).toBeTruthy();
        expect(within(voucherTable).queryByRole('columnheader', { name: 'Category' })).not.toBeInTheDocument();
        expect(within(voucherTable).getByRole('columnheader', { name: 'Payment mode' })).toBeInTheDocument();
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
        expect(cashRow.children[2]).toBeEmptyDOMElement();
        expect(cashRow.children[4]).toBeEmptyDOMElement();
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

    it.each([
        ['debit', ['Debit A', 'Debit B', 'Credit A', 'Credit B']],
        ['credit', ['Credit A', 'Credit B', 'Debit A', 'Debit B']],
        ['all', ['Credit A', 'Debit A', 'Credit B', 'Debit B']],
    ])('prints visible %s accounts first while preserving P&R row details', (filter, expectedDescriptions) => {
        const entries = [
            { journalEntryId: 1, accountName: 'Credit Account A', description: 'Credit A', instrumentType: 'Cash', debit: 0, credit: 70 },
            { journalEntryId: 2, accountName: 'Debit Account A', description: 'Debit A', instrumentType: 'Cheque', referenceNumber: 'A1', debit: 100, credit: 0 },
            { journalEntryId: 3, accountName: 'Credit Account B', description: 'Credit B', instrumentType: 'Cash', debit: 0, credit: 230 },
            { journalEntryId: 4, accountName: 'Debit Account B', description: 'Debit B', instrumentType: 'Cheque', referenceNumber: 'B1', debit: 200, credit: 0 },
        ];
        render(<PaymentReceiptPrint voucherData={{ ...voucherData, entries, totalDebit: 300, totalCredit: 300 }}
            voucherId="41" filter={filter} />);

        const table = screen.getAllByRole('table').find(candidate =>
            within(candidate).queryByRole('columnheader', { name: 'Account' }));
        const rows = [...table.querySelectorAll('tbody tr')];
        expect(rows.map(row => row.cells[1].textContent)).toEqual(expectedDescriptions);
        rows.forEach(row => {
            const description = row.cells[1].textContent;
            const side = description.startsWith('Debit') ? 'debit' : 'credit';
            expect(row.cells[0].textContent).toBe(filter === 'all' || filter === side
                ? `${side === 'debit' ? 'Debit' : 'Credit'} Account ${description.at(-1)}`
                : '');
            expect(row.cells[side === 'debit' ? 5 : 6]).toHaveTextContent(/\d+\.00/);
            if (description === 'Debit A') expect(row.cells[4]).toHaveTextContent('A1');
            if (description === 'Debit B') expect(row.cells[4]).toHaveTextContent('B1');
        });
        const totalCells = table.querySelector('tfoot tr').cells;
        expect(totalCells[totalCells.length - 2]).toHaveTextContent('300.00');
        expect(totalCells[totalCells.length - 1]).toHaveTextContent('300.00');
        expect(entries.map(entry => entry.description)).toEqual(['Credit A', 'Debit A', 'Credit B', 'Debit B']);
    });
});
