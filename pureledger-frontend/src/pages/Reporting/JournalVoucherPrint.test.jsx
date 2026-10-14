import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PrintVoucher from './PrintVoucher';
import PaymentReceiptPrint from './PaymentReceiptPrint';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    API_BASE_URL: 'https://localhost:7062/api', default: { get: vi.fn() },
}));

const voucher = {
    projectName: 'Ghazi Plastics', createdDate: '2026-10-06', totalAmount2: -600488,
    entries: [
        { accountId: 10, accountName: 'Nayab Agencies', description: 'On account', debit: 600488, credit: 0 },
        { accountId: 20, accountName: 'Private bank A', description: 'First deposit', debit: 0, credit: 94988 },
        { accountId: 21, accountName: 'Private bank B', description: 'Second deposit', debit: 0, credit: 250000 },
        { accountId: 20, accountName: 'Private bank A', description: 'Third deposit', debit: 0, credit: 180500 },
        { accountId: 22, accountName: 'Private cash', description: 'Fourth deposit', debit: 0, credit: 75000 },
    ],
};

describe('Journal vendor layout', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        apiClient.get.mockResolvedValue({ data: { success: true, data: null } });
    });

    it('condenses one debit account into a vendor summary and itemizes credit slips exactly once', () => {
        render(<PrintVoucher voucherType="JV" voucherId="14153" filter="debit" voucherData={voucher} />);
        expect(screen.getByText('Vendor copy')).toBeInTheDocument();
        expect(screen.getByText('Paid to')).toBeInTheDocument();
        expect(screen.getByText('Nayab Agencies')).toBeInTheDocument();
        expect(screen.getByRole('heading', { name: 'Payment Voucher' })).toBeInTheDocument();
        expect(screen.getByText('4 slips')).toBeInTheDocument();
        expect(screen.getAllByText('600,488.00')).toHaveLength(2);
        expect(screen.queryByText('Private bank A')).not.toBeInTheDocument();
        expect(screen.queryByText('Private bank B')).not.toBeInTheDocument();
        const table = screen.getByRole('table');
        expect(table.querySelectorAll('tbody tr')).toHaveLength(4);
        expect([...table.querySelectorAll('tbody tr')].map(row => row.cells[2].textContent))
            .toEqual(['94,988.00', '250,000.00', '180,500.00', '75,000.00']);
        for (const column of ['Instrument date', 'Payment mode', 'Bank', 'Qty', 'Rate']) {
            expect(within(table).queryByRole('columnheader', { name: column })).not.toBeInTheDocument();
        }
        expect(screen.getByText('On account')).toBeInTheDocument();
        expect(screen.getByText('06/10/2026')).toBeInTheDocument();
        expect(screen.getByText('Ghazi Plastics')).toHaveClass('journal-project-name');
        expect(screen.queryByText('Project:', { exact: false })).not.toBeInTheDocument();
    });

    it('recognizes repeated debit rows for the same account ID and preserves their descriptions', () => {
        const entries = [
            { accountId: 10, accountName: 'Nayab Agencies', description: 'Invoice A', debit: 300000 },
            { accountId: 10, accountName: 'Nayab Agencies', description: 'Invoice B', debit: 300488 },
            ...voucher.entries.slice(1),
        ];
        render(<PrintVoucher voucherType="JV" voucherId="17" filter="DEBIT" voucherData={{ ...voucher, entries }} />);
        expect(screen.getByText('Paid to')).toBeInTheDocument();
        expect(screen.getByText('4 slips')).toBeInTheDocument();
        expect(screen.getByText('Invoice A | Invoice B')).toBeInTheDocument();
    });

    it('uses distinct IDs even when two debit accounts have the same name', () => {
        const entries = [
            { accountId: 10, accountName: 'Same vendor name', debit: 300000 },
            { accountId: 11, accountName: 'Same vendor name', debit: 300488 },
            ...voucher.entries.slice(1),
        ];
        render(<PrintVoucher voucherType="JV" voucherId="17" filter="debit" voucherData={{ ...voucher, entries }} />);
        expect(screen.queryByText('Paid to')).not.toBeInTheDocument();
        expect(screen.queryByText('Vendor copy')).not.toBeInTheDocument();
        expect(screen.getByText('Debit copy')).toBeInTheDocument();
        expect(screen.getAllByText('Same vendor name')).toHaveLength(2);
        expect(screen.getByRole('table').querySelectorAll('tbody tr')).toHaveLength(6);
        expect(screen.queryByText('Private bank A')).not.toBeInTheDocument();
    });

    it.each(['all', 'credit'])('keeps the full journal table for %s copies', filter => {
        render(<PrintVoucher voucherType="JV" voucherId="17" filter={filter} voucherData={voucher} />);
        expect(screen.queryByText('Paid to')).not.toBeInTheDocument();
        expect(screen.getByRole('table').querySelectorAll('tbody tr')).toHaveLength(5);
        const totals = screen.getByRole('table').querySelector('tfoot tr');
        expect(totals.cells[totals.cells.length - 2]).toHaveTextContent('600,488.00');
        expect(totals.cells[totals.cells.length - 1]).toHaveTextContent('600,488.00');
        expect(screen.queryByText('-600,488.00')).not.toBeInTheDocument();
    });

    it.each([
        ['unbalanced', [{ accountId: 10, accountName: 'Vendor', debit: 100 }, { accountId: 20, accountName: 'Bank', credit: 90 }]],
        ['unknown account', [{ accountName: '', debit: 100 }, { accountId: 20, accountName: 'Bank', credit: 100 }]],
        ['mixed sides', [{ accountId: 10, accountName: 'Vendor', debit: 100, credit: 10 }, { accountId: 20, accountName: 'Bank', credit: 90 }]],
        ['no credit', [{ accountId: 10, accountName: 'Vendor', debit: 100 }]],
    ])('preserves all rows for %s data', (_case, entries) => {
        render(<PrintVoucher voucherType="JV" voucherId="17" filter="debit" voucherData={{ ...voucher, entries }} />);
        expect(screen.queryByText('Paid to')).not.toBeInTheDocument();
        expect(screen.getByRole('table').querySelectorAll('tbody tr')).toHaveLength(entries.length);
    });

    it('shows actual instrument metadata without inventing a bank or deposit date', () => {
        const instruments = [
            { instrumentType: 'BankTransfer', referenceNumber: '0012016267', instrumentDate: '2026-10-06' },
            { instrumentType: 'Cheque', referenceNumber: '00458712', instrumentDate: '2026-10-05' },
            { instrumentType: 'WireTransfer', referenceNumber: 'PO-112233', instrumentDate: '2026-10-04' },
            { instrumentType: 'Cash', referenceNumber: 'stale cash reference', instrumentDate: '2026-10-03' },
        ];
        const entries = voucher.entries.map((entry, index) => ({ ...entry, ...(instruments[index - 1] || {}) }));
        render(<PaymentReceiptPrint voucherId="17" filter="debit" voucherData={{ ...voucher, entries }} />);
        const table = screen.getByRole('table');
        expect(within(table).getByRole('columnheader', { name: 'Instrument date' })).toBeInTheDocument();
        expect(screen.getByText('Bank Transfer')).toBeInTheDocument();
        expect(screen.getByText('0012016267')).toBeInTheDocument();
        expect(screen.getByText('00458712')).toBeInTheDocument();
        expect(screen.getByText('05/10/2026')).toBeInTheDocument();
        expect(screen.queryByText('stale cash reference')).not.toBeInTheDocument();
        expect(screen.queryByText('03/10/2026')).not.toBeInTheDocument();
        expect(within(table).queryByRole('columnheader', { name: 'Bank' })).not.toBeInTheDocument();
        expect([...table.querySelectorAll('thead th')]).toHaveLength(6);
        expect(table.querySelector('tfoot th').colSpan).toBe(5);
    });

    it('preserves slip metadata when it is stored only on the debit rows', () => {
        const entries = [
            { accountId: 10, accountName: 'Vendor', debit: 60, instrumentType: 'Cheque', referenceNumber: '001', instrumentDate: '2026-10-05' },
            { accountId: 10, accountName: 'Vendor', debit: 40, instrumentType: 'BankTransfer', referenceNumber: '002', instrumentDate: '2026-10-06' },
            { accountId: 20, accountName: 'Private bank', credit: 100 },
        ];
        render(<PaymentReceiptPrint voucherId="17" filter="debit" voucherData={{ ...voucher, entries }} />);
        expect(screen.getByText('2 slips')).toBeInTheDocument();
        expect(screen.getByText('001')).toBeInTheDocument();
        expect(screen.getByText('002')).toBeInTheDocument();
        expect(screen.getAllByText('100.00')).toHaveLength(2);
    });

    it('retains existing quantity and rate fields on vendor slips when used', () => {
        const entries = voucher.entries.map((entry, index) => index === 1 ? { ...entry, quantity: 2, rate: 47494 } : entry);
        render(<PrintVoucher voucherType="JV" voucherId="17" filter="debit" voucherData={{ ...voucher, entries }} />);
        expect(screen.getByRole('columnheader', { name: 'Qty' })).toBeInTheDocument();
        expect(screen.getByRole('columnheader', { name: 'Rate' })).toBeInTheDocument();
        expect(screen.getByText('47,494.00')).toBeInTheDocument();
    });

    it.each(['stock-transfer flag', 'product account'])('uses the journal layout for a %s even with one debit account', reason => {
        const entries = voucher.entries.map((entry, index) => ({ ...entry,
            accountGroupId: reason === 'product account' && index === 1 ? 2 : 1,
        }));
        render(<PrintVoucher voucherType="JV" voucherId="17" filter="debit" voucherData={{ ...voucher,
            entries, isStockTransfer: reason === 'stock-transfer flag',
        }} />);
        expect(screen.queryByText('Paid to')).not.toBeInTheDocument();
        expect(screen.getByRole('table').querySelectorAll('tbody tr')).toHaveLength(5);
    });

    it('replaces an unavailable project logo with a clean placeholder', () => {
        render(<PrintVoucher voucherType="JV" voucherId="17" voucherData={{ ...voucher, projectImage: 'missing.png' }} />);
        fireEvent.error(screen.getByAltText('Project Logo'));
        expect(screen.queryByAltText('Project Logo')).not.toBeInTheDocument();
        expect(screen.getByText('Logo')).toHaveClass('journal-logo-placeholder');
    });

    it('supports printing and closing, and loads the existing authorized signature', async () => {
        apiClient.get.mockResolvedValue({ data: { success: true, data: { name: 'Manager', signatureImage: 'abc123' } } });
        const print = vi.spyOn(window, 'print').mockImplementation(() => {});
        const back = vi.spyOn(window.history, 'back').mockImplementation(() => {});
        const { unmount } = render(<PrintVoucher voucherType="JV" voucherId="17" filter="debit" voucherData={voucher} />);
        await waitFor(() => expect(screen.getByAltText('Manager')).toHaveAttribute('src', 'data:image/png;base64,abc123'));
        fireEvent.click(screen.getByRole('button', { name: 'Print' }));
        fireEvent.click(screen.getByRole('button', { name: 'Close' }));
        expect(print).toHaveBeenCalledOnce();
        expect(back).toHaveBeenCalledOnce();
        const pageStyles = [...document.head.querySelectorAll('style')].filter(style => style.textContent.includes('A4 portrait'));
        expect(pageStyles).toHaveLength(1);
        unmount();
        expect(pageStyles[0].isConnected).toBe(false);
    });

    it.each(['INV', 'DC'])('retains the existing %s layout', voucherType => {
        render(<PrintVoucher voucherType={voucherType} voucherId="17" voucherData={{ createdDate: '2026-10-06', entries: [] }} />);
        expect(screen.queryByText('Paid to')).not.toBeInTheDocument();
        expect(screen.getByText(voucherType === 'INV' ? 'Invoice #17' : 'Delivery Challan #17')).toBeInTheDocument();
    });
});
