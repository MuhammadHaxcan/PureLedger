import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import HomeEntry from './Entry';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    default: {
        get: vi.fn(),
        post: vi.fn(),
    },
}));

describe('Expense full-voucher revision', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        localStorage.clear();
        apiClient.get.mockImplementation(async (url) => {
            if (url === '/HomeApi/entry-data') {
                return {
                    data: {
                        success: true,
                        projects: [
                            { id: 1, name: 'Main Project' },
                            { id: 2, name: 'Remembered Project' },
                        ],
                        bookers: [{ id: 1, name: 'Default Booker' }],
                        selectedProjectId: null,
                    },
                };
            }
            if (url === '/HomeApi/edit-voucher/41') {
                return {
                    data: {
                        success: true,
                        isManualExpenseVoucher: true,
                        selectedProjectId: 1,
                        projects: [{ id: 1, name: 'Main Project' }],
                        bookers: [{ id: 1, name: 'Default Booker' }],
                        model: {
                            projectId: 1,
                            entryDate: '2026-07-31',
                            dueDate: '2026-08-01',
                            creditDays: 1,
                            bookerId: 1,
                            voucherVersion: 'AQID',
                            requireQuantity: false,
                            isBaseVoucher: false,
                            entries: [
                                { entryId: 1, categoryId: 1, accountId: 1, des: 'Debit', debit: 100, credit: 0 },
                                { entryId: 2, categoryId: 1, accountId: 2, des: 'Credit', debit: 0, credit: 100 },
                            ],
                        },
                    },
                };
            }
            if (url === '/HomeApi/categories-by-project/1' || url === '/HomeApi/categories-by-project/2') {
                return { data: { success: true, data: [{ id: 1, name: 'General' }] } };
            }
            if (url === '/HomeApi/accounts-by-category/1') {
                return {
                    data: {
                        success: true,
                        data: [
                            { id: 1, name: 'Cash' },
                            { id: 2, name: 'Bank' },
                        ],
                    },
                };
            }
            if (url.startsWith('/HomeApi/product-details/')) {
                return {
                    data: {
                        success: true,
                        data: { accountGroupId: 1, accountTypeName: 'Account', pkg: null },
                    },
                };
            }
            throw new Error(`Unexpected GET ${url}`);
        });
    });

    it('prompts and does not submit when the final Expense account has been removed', async () => {
        const user = userEvent.setup();
        render(
            <MemoryRouter initialEntries={['/home/entry?voucherId=41']}>
                <Routes>
                    <Route path="/home/entry" element={<HomeEntry />} />
                </Routes>
            </MemoryRouter>
        );

        expect(await screen.findByText(/This is an Expense voucher/i)).toBeInTheDocument();
        await user.click(screen.getByRole('button', { name: /Update Journal Voucher/i }));

        expect(await screen.findByText('Expense account required')).toBeInTheDocument();
        expect(screen.getByText(/must retain at least one Expense account/i)).toBeInTheDocument();
        expect(apiClient.post).not.toHaveBeenCalled();
    });

    it.each(['Approved', 'PendingApproval'])('handles %s with an audit ID and preserves the editor version', async (status) => {
        const user = userEvent.setup();
        const getMetadata = apiClient.get.getMockImplementation();
        apiClient.get.mockImplementation(async (url) => url === '/HomeApi/product-details/1'
            ? { data: { success: true, data: { accountGroupId: 3, accountTypeName: 'Expense', pkg: null } } }
            : getMetadata(url));
        apiClient.post.mockResolvedValue({ data: {
            success: true, status, approvalVoucherId: 7, voucherId: 41,
            shouldPrint: status === 'Approved', message: status === 'Approved' ? 'Expense posted and automatically approved.' : 'Voucher submitted for approval.',
        } });
        const print = vi.spyOn(window, 'open').mockImplementation(() => null);
        render(<MemoryRouter initialEntries={['/home/entry?voucherId=41']}><Routes>
            <Route path="/home/entry" element={<HomeEntry />} />
        </Routes></MemoryRouter>);
        const submit = await screen.findByRole('button', { name: 'Update and Print' });
        await waitFor(() => expect(submit).not.toBeDisabled());
        await user.click(submit);
        await waitFor(() => expect(apiClient.post).toHaveBeenCalledTimes(1));
        expect(apiClient.post.mock.calls[0][1].voucherVersion).toBe('AQID');
        if (status === 'Approved')
            expect(print).toHaveBeenCalledWith('/reporting/print-voucher?voucherId=41&type=JV', '_blank');
        else expect(print).not.toHaveBeenCalled();
        print.mockRestore();
    });

    it('restores the last project on the shared Journal Entry and Expense creation page', async () => {
        localStorage.setItem('pl_last_selected_project', '2');

        render(
            <MemoryRouter initialEntries={['/home/entry']}>
                <Routes>
                    <Route path="/home/entry" element={<HomeEntry />} />
                </Routes>
            </MemoryRouter>
        );

        expect(await screen.findByPlaceholderText('Select Project')).toHaveValue('Remembered Project');
    });

    it('formats debit input and journal totals with thousands separators', async () => {
        const user = userEvent.setup();
        render(
            <MemoryRouter initialEntries={['/home/entry?voucherId=41']}>
                <Routes>
                    <Route path="/home/entry" element={<HomeEntry />} />
                </Routes>
            </MemoryRouter>
        );

        const debitInput = await screen.findByLabelText('Entry 1 debit');
        await user.click(debitInput);
        await user.clear(debitInput);
        await user.type(debitInput, '10000.5');

        expect(debitInput).toHaveValue('10,000.5');
        await user.tab();
        expect(debitInput).toHaveValue('10,000.50');

        const totalRow = screen.getByText('TOTAL').closest('tr');
        expect(within(totalRow).getByText('10,000.50')).toBeInTheDocument();
    });
});
