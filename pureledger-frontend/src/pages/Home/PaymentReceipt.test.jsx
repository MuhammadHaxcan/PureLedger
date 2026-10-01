import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import PaymentReceipt from './PaymentReceipt';
import NavbarTitleContext from '../../contexts/NavbarTitleContext';

vi.mock('./PaymentReceiptForm', () => ({
    default: ({ mode, journalVoucherId }) => (
        <div data-testid="payment-receipt-form">{mode}:{journalVoucherId ?? 'new'}</div>
    ),
}));

describe('PaymentReceipt route modes', () => {
    it('opens a posted voucher as a full revision form from the Voucher Editor redirect', () => {
        const setNavbarTitle = vi.fn();
        render(
            <NavbarTitleContext.Provider value={setNavbarTitle}>
                <MemoryRouter initialEntries={['/home/payment-and-receipt?voucherId=41']}>
                    <PaymentReceipt />
                </MemoryRouter>
            </NavbarTitleContext.Provider>
        );

        expect(setNavbarTitle).toHaveBeenCalledWith('Edit Payment & Receipt - JV-41');
        expect(screen.queryByText('Edit Payment & Receipt - JV-41')).not.toBeInTheDocument();
        expect(screen.getByTestId('payment-receipt-form')).toHaveTextContent('revision:41');
    });

    it('keeps the same route as a create form when no voucher ID is supplied', () => {
        render(
            <MemoryRouter initialEntries={['/home/payment-and-receipt']}>
                <PaymentReceipt />
            </MemoryRouter>
        );

        expect(screen.getByTestId('payment-receipt-form')).toHaveTextContent('create:new');
    });

    it('does not silently fall back to create mode for an invalid voucher ID', () => {
        render(
            <MemoryRouter initialEntries={['/home/payment-and-receipt?voucherId=bad']}>
                <PaymentReceipt />
            </MemoryRouter>
        );

        expect(screen.getByText(/Invalid Payment & Receipt voucher ID/i)).toBeInTheDocument();
        expect(screen.queryByTestId('payment-receipt-form')).not.toBeInTheDocument();
    });
});
