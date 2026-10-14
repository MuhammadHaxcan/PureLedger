import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import Login from './Login';

const mocks = vi.hoisted(() => ({ login: vi.fn(), showError: vi.fn(), navigate: vi.fn() }));
vi.mock('../contexts/AuthContext', () => ({ useAuth: () => ({ login: mocks.login }) }));
vi.mock('../contexts/ToastContext', () => ({ useToast: () => ({ showError: mocks.showError }) }));
vi.mock('../hooks/usePageTitle', () => ({ usePageTitle: vi.fn() }));
vi.mock('react-router-dom', async (importOriginal) => ({
  ...await importOriginal(), useNavigate: () => mocks.navigate,
}));

describe('username or email login', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mocks.login.mockResolvedValue({ success: true, redirectTo: '/home' });
  });

  it.each(['bookkeeper', 'Bookkeeper@Example.com'])('submits %s and remembers the trimmed identifier', async (identifier) => {
    const user = userEvent.setup();
    render(<MemoryRouter><Login /></MemoryRouter>);
    const input = screen.getByLabelText('Username or email');
    expect(input).toHaveAttribute('type', 'text');
    expect(input).toHaveAttribute('autocomplete', 'username');
    await user.type(input, ` ${identifier} `);
    await user.type(screen.getByLabelText('Password'), 'password with trailing space ');
    await user.click(screen.getByLabelText('Remember me'));
    await user.click(screen.getByRole('button', { name: /Login/ }));

    await waitFor(() => expect(mocks.login).toHaveBeenCalledWith(identifier, 'password with trailing space '));
    expect(localStorage.getItem('rememberedUsername')).toBe(identifier);
    expect(mocks.navigate).toHaveBeenCalledWith('/home');
  });

  it('restores a previously remembered email', () => {
    localStorage.setItem('rememberedUsername', 'bookkeeper@example.com');
    render(<MemoryRouter><Login /></MemoryRouter>);
    expect(screen.getByLabelText('Username or email')).toHaveValue('bookkeeper@example.com');
    expect(screen.getByLabelText('Remember me')).toBeChecked();
  });

  it('shows a failed login without navigating or remembering it', async () => {
    mocks.login.mockResolvedValue({ success: false });
    const user = userEvent.setup();
    render(<MemoryRouter><Login /></MemoryRouter>);
    await user.type(screen.getByLabelText('Username or email'), 'bookkeeper@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong-password');
    await user.click(screen.getByLabelText('Remember me'));
    await user.click(screen.getByRole('button', { name: /Login/ }));

    await waitFor(() => expect(mocks.showError).toHaveBeenCalledWith('Invalid username/email or password'));
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(localStorage.getItem('rememberedUsername')).toBeNull();
  });
});
