import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import Layout from './Layout';
import { ThemeProvider } from '../contexts/ThemeContext';

vi.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { fullName: 'Test User', isAdmin: false },
    logout: vi.fn(),
  }),
}));

vi.mock('./NavigationMenu', () => ({
  default: () => <div>Navigation</div>,
}));

describe('Layout theme control', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }));
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('exposes an accessible navbar toggle and updates its action label', async () => {
    const user = userEvent.setup();

    render(
      <ThemeProvider>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<div>Page content</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </ThemeProvider>,
    );

    const toggle = screen.getByRole('button', { name: 'Switch to dark mode' });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    expect(toggle).toHaveTextContent('Dark');

    await user.click(toggle);

    expect(screen.getByRole('button', { name: 'Switch to light mode' })).toHaveAttribute('aria-pressed', 'true');
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
  });
});
