import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Bookers from './Bookers';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}));

describe('Bookers dialogs', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        apiClient.get.mockImplementation(async (url) => {
            if (url === '/MetaDataApi/bookers') {
                return { data: { success: true, data: [{ bookerID: 1, bookerName: 'Booker One', isEnabled: true }] } };
            }
            throw new Error(`Unexpected GET ${url}`);
        });
    });

    it('shows a failed save inside the dialog and keeps it open', async () => {
        apiClient.post.mockRejectedValue({ response: { data: { errors: ['A booker with this name already exists.'] } } });
        const user = userEvent.setup();
        render(<Bookers />);
        await user.click(await screen.findByRole('button', { name: /New Booker/i }));
        const dialog = await screen.findByRole('dialog');
        await user.type(within(dialog).getByRole('textbox'), 'Booker One');

        await user.click(within(dialog).getByRole('button', { name: 'Create' }));

        expect(await within(dialog).findByText('A booker with this name already exists.')).toBeInTheDocument();
        expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('clears the error when the dialog is cancelled so it does not linger on the page', async () => {
        apiClient.post.mockRejectedValue({ response: { data: { errors: ['Name is not allowed.'] } } });
        const user = userEvent.setup();
        render(<Bookers />);
        await user.click(await screen.findByRole('button', { name: /New Booker/i }));
        const dialog = await screen.findByRole('dialog');
        await user.type(within(dialog).getByRole('textbox'), 'x');
        await user.click(within(dialog).getByRole('button', { name: 'Create' }));
        await within(dialog).findByText('Name is not allowed.');

        await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

        await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
        expect(screen.queryByText('Name is not allowed.')).not.toBeInTheDocument();
    });

    it('shows a failed delete inside the confirmation dialog', async () => {
        apiClient.delete.mockRejectedValue({ message: 'This booker is linked to accounts and cannot be deleted.' });
        const user = userEvent.setup();
        render(<Bookers />);
        const row = (await screen.findByText('Booker One')).closest('tr');
        const buttons = within(row).getAllByRole('button');
        await user.click(buttons[buttons.length - 1]);
        const dialog = await screen.findByRole('dialog');

        await user.click(within(dialog).getByRole('button', { name: 'Confirm' }));

        expect(await within(dialog).findByText('This booker is linked to accounts and cannot be deleted.')).toBeInTheDocument();
    });
});
