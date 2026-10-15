import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PendingApprovals from './PendingApprovals';
import approvalApi from '../../services/approvalApi';

vi.mock('../../services/approvalApi', () => ({
    default: {
        getPending: vi.fn(),
        getHistory: vi.fn(),
        getById: vi.fn(),
        approve: vi.fn(),
        reject: vi.fn(),
    },
}));

vi.mock('../../hooks/usePermissions', () => ({
    usePermissions: () => ({ canAccessReporting: true }),
}));

const pendingRow = (overrides = {}) => ({
    approvalVoucherId: 11,
    voucherType: 'ManualExpense',
    requestKind: 'New',
    journalVoucherId: null,
    reservedJournalVoucherId: 123,
    displayVoucherId: 123,
    projectName: 'Alpha',
    submittedDate: '2026-10-15',
    postedByUsername: 'maker',
    totalDebit: 100,
    ...overrides,
});

const historyRow = (overrides = {}) => ({
    approvalVoucherId: 12,
    voucherType: 'ManualExpense',
    requestKind: 'New',
    status: 'Rejected',
    journalVoucherId: null,
    reservedJournalVoucherId: 124,
    displayVoucherId: 124,
    projectName: 'Alpha',
    submittedDate: '2026-10-15',
    decisionDate: '2026-10-15',
    postedByUsername: 'maker',
    decisionByUsername: 'admin',
    totalDebit: 50,
    ...overrides,
});

const renderPage = () => render(<MemoryRouter><PendingApprovals /></MemoryRouter>);

describe('PendingApprovals JV numbers', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        approvalApi.getPending.mockResolvedValue({ data: { items: [pendingRow()] } });
        approvalApi.getHistory.mockResolvedValue({ data: { items: [historyRow()] } });
    });

    it('shows the reserved JV number of a pending request on the Pending tab', async () => {
        renderPage();

        expect(await screen.findByRole('columnheader', { name: 'JV ID' })).toBeInTheDocument();
        expect(screen.getByText('JV-123')).toBeInTheDocument();
    });

    it('shows the existing voucher number for a pending revision', async () => {
        approvalApi.getPending.mockResolvedValue({
            data: { items: [pendingRow({ requestKind: 'Revision', journalVoucherId: 77, reservedJournalVoucherId: null, displayVoucherId: 77 })] },
        });

        renderPage();

        expect(await screen.findByText('JV-77')).toBeInTheDocument();
    });

    it('falls back to a dash for a legacy pending request that has no number', async () => {
        approvalApi.getPending.mockResolvedValue({
            data: { items: [pendingRow({ reservedJournalVoucherId: null, displayVoucherId: null })] },
        });

        renderPage();

        const row = (await screen.findByText('maker')).closest('tr');
        expect(within(row).getByText('—')).toBeInTheDocument();
        expect(within(row).queryByText(/JV-/)).not.toBeInTheDocument();
    });

    it('keeps the number of a rejected request visible in History', async () => {
        renderPage();
        await screen.findByText('JV-123');

        await userEvent.click(screen.getByRole('button', { name: /History/i }));

        expect(await screen.findByText('JV-124')).toBeInTheDocument();
        expect(screen.getByText('Rejected')).toBeInTheDocument();
    });

    it('prefers the posted voucher number over the reserved one in History', async () => {
        approvalApi.getHistory.mockResolvedValue({
            data: { items: [historyRow({ status: 'Approved', journalVoucherId: 124, displayVoucherId: 124 })] },
        });
        renderPage();
        await screen.findByText('JV-123');

        await userEvent.click(screen.getByRole('button', { name: /History/i }));

        expect(await screen.findByText('JV-124')).toBeInTheDocument();
    });

    it('tells the approver which number the voucher will be posted as', async () => {
        renderPage();
        const row = (await screen.findByText('JV-123')).closest('tr');

        await userEvent.click(within(row).getByRole('button', { name: /^Approve/i }));

        expect(await screen.findByText(/post the voucher as JV-123/i)).toBeInTheDocument();
    });

    it('keeps the generic wording for a legacy request without a reserved number', async () => {
        approvalApi.getPending.mockResolvedValue({
            data: { items: [pendingRow({ reservedJournalVoucherId: null, displayVoucherId: null })] },
        });
        renderPage();
        const row = (await screen.findByText('maker')).closest('tr');

        await userEvent.click(within(row).getByRole('button', { name: /^Approve/i }));

        expect(await screen.findByText(/assign the voucher its JV number/i)).toBeInTheDocument();
    });
});
