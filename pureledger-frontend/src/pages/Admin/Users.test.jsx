import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Users from './Users';
import apiClient from '../../services/api';

vi.mock('../../services/api', () => ({
    default: {
        get: vi.fn(),
        post: vi.fn(),
        put: vi.fn(),
        delete: vi.fn(),
    },
}));

vi.mock('../../contexts/AuthContext', () => ({
    useAuth: () => ({ user: { userId: 1, isAdmin: true } }),
}));

const openNewUserDialog = async (user) => {
    await user.click(await screen.findByRole('button', { name: /New User/i }));
    return screen.findByRole('dialog');
};

const typeInto = async (user, dialog, index, text) => {
    const inputs = dialog.querySelectorAll('input[type="text"], input[type="email"], input[type="password"]');
    await user.type(inputs[index], text);
};

// Order of the text-like inputs in the dialog: username, full name, email, password, confirm password.
const FIELD = { username: 0, fullName: 1, email: 2, password: 3, confirm: 4 };

describe('Users create dialog', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        apiClient.get.mockImplementation(async (url) => {
            if (url === '/AdminApi/users') return { data: { success: true, data: [] } };
            if (url === '/AdminApi/controller-actions') return { data: { success: true, data: { Home: ['Entry'] } } };
            if (url === '/AdminApi/projects') return { data: { success: true, data: [] } };
            if (url === '/AdminApi/categories') return { data: { success: true, data: [] } };
            if (url === '/AdminApi/settings/legacyDatabase') return { data: { success: true, data: false } };
            throw new Error(`Unexpected GET ${url}`);
        });
    });

    it('shows a missing-username error inside the dialog instead of silently doing nothing', async () => {
        const user = userEvent.setup();
        render(<Users />);
        const dialog = await openNewUserDialog(user);

        await user.click(within(dialog).getByRole('button', { name: 'Create' }));

        expect(await within(dialog).findByText('Username is required.')).toBeInTheDocument();
        expect(apiClient.post).not.toHaveBeenCalled();
    });

    it.each([
        ['a too-short username', { username: 'ab', fullName: 'Some Person' }, 'Username must be at least 3 characters.'],
        ['a missing full name', { username: 'newuser' }, 'Full name is required.'],
        ['a missing password', { username: 'newuser', fullName: 'Some Person' }, 'Password is required for new users.'],
        ['mismatched passwords', { username: 'newuser', fullName: 'Some Person', password: 'Secret123!', confirm: 'Different1!' }, 'Passwords do not match.'],
    ])('reports %s inside the dialog without calling the server', async (_label, values, message) => {
        const user = userEvent.setup();
        render(<Users />);
        const dialog = await openNewUserDialog(user);
        for (const [field, text] of Object.entries(values)) await typeInto(user, dialog, FIELD[field], text);

        await user.click(within(dialog).getByRole('button', { name: 'Create' }));

        expect(await within(dialog).findByText(message)).toBeInTheDocument();
        expect(apiClient.post).not.toHaveBeenCalled();
    });

    it('shows a server rejection (for example a duplicate user) inside the dialog and keeps it open', async () => {
        apiClient.post.mockRejectedValue({
            response: { data: { success: false, message: 'Error creating user. Username or email may already be in use.' } },
        });
        const user = userEvent.setup();
        render(<Users />);
        const dialog = await openNewUserDialog(user);
        for (const [field, text] of Object.entries({ username: 'taken', fullName: 'Some Person', password: 'Secret123!', confirm: 'Secret123!' })) {
            await typeInto(user, dialog, FIELD[field], text);
        }

        await user.click(within(dialog).getByRole('button', { name: 'Create' }));

        expect(await within(dialog).findByText(/Username or email may already be in use/)).toBeInTheDocument();
        expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('clears the error when the dialog is cancelled so it does not linger on the page', async () => {
        const user = userEvent.setup();
        render(<Users />);
        const dialog = await openNewUserDialog(user);
        await user.click(within(dialog).getByRole('button', { name: 'Create' }));
        await within(dialog).findByText('Username is required.');

        await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

        await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
        expect(screen.queryByText('Username is required.')).not.toBeInTheDocument();
    });

    it('creates the user and closes the dialog when the form is valid', async () => {
        apiClient.post.mockResolvedValue({ data: { success: true, message: 'User created successfully' } });
        const user = userEvent.setup();
        render(<Users />);
        const dialog = await openNewUserDialog(user);
        for (const [field, text] of Object.entries({ username: 'newuser', fullName: 'Some Person', password: 'Secret123!', confirm: 'Secret123!' })) {
            await typeInto(user, dialog, FIELD[field], text);
        }

        await user.click(within(dialog).getByRole('button', { name: 'Create' }));

        await waitFor(() => expect(apiClient.post).toHaveBeenCalledWith(
            '/AdminApi/users',
            expect.objectContaining({ username: 'newuser', fullName: 'Some Person', password: 'Secret123!', confirmPassword: 'Secret123!' })
        ));
        await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
        expect(await screen.findByText('User created successfully')).toBeInTheDocument();
    });
});
