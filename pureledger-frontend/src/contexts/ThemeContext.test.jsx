import { act, cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from './ThemeContext';
import { THEME_STORAGE_KEY, useTheme } from './theme';

const ThemeProbe = () => {
  const { theme, toggleTheme } = useTheme();
  return <button onClick={toggleTheme}>{theme}</button>;
};

describe('ThemeProvider', () => {
  let systemThemeListeners;
  let systemPrefersDark;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-bs-theme');
    document.documentElement.style.colorScheme = '';
    systemThemeListeners = new Set();
    systemPrefersDark = false;

    vi.stubGlobal('matchMedia', vi.fn().mockImplementation((query) => ({
      matches: systemPrefersDark,
      media: query,
      addEventListener: (_event, listener) => systemThemeListeners.add(listener),
      removeEventListener: (_event, listener) => systemThemeListeners.delete(listener),
    })));
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('restores a saved dark preference and synchronizes Bootstrap color mode', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    render(<ThemeProvider><ThemeProbe /></ThemeProvider>);

    expect(screen.getByRole('button')).toHaveTextContent('dark');
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
    expect(document.documentElement).toHaveAttribute('data-bs-theme', 'dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('persists an explicit theme toggle', async () => {
    const user = userEvent.setup();
    render(<ThemeProvider><ThemeProbe /></ThemeProvider>);

    expect(screen.getByRole('button')).toHaveTextContent('light');
    await user.click(screen.getByRole('button'));

    expect(screen.getByRole('button')).toHaveTextContent('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
    expect(document.documentElement).toHaveAttribute('data-bs-theme', 'dark');
  });

  it('tracks system color-scheme changes until the user chooses a preference', () => {
    render(<ThemeProvider><ThemeProbe /></ThemeProvider>);

    act(() => {
      systemPrefersDark = true;
      systemThemeListeners.forEach((listener) => listener({ matches: systemPrefersDark }));
    });

    expect(screen.getByRole('button')).toHaveTextContent('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
    expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
  });
});
