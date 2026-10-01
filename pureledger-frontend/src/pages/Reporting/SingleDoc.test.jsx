import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SingleDoc from './SingleDoc';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    default: { get: vi.fn() },
}));

describe('SingleDoc', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        vi.spyOn(window, 'open').mockImplementation(() => null);
    });

    it('preserves the debit-only filter when a JV ID resolves to P&R', async () => {
        apiClient.get.mockResolvedValue({
            data: {
                success: true,
                exists: true,
                resolvedVoucherType: 'PaymentReceipt',
            },
        });

        render(<SingleDoc />);
        fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '41' } });
        fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'debit' } });
        fireEvent.click(screen.getByRole('button', { name: /open in new tab/i }));

        await waitFor(() => expect(window.open).toHaveBeenCalledWith(
            '/reporting/print-voucher?voucherId=41&type=PaymentReceipt&filter=debit',
            '_blank',
        ));
    });
});
