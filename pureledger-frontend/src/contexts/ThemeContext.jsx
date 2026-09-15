import { useCallback, useEffect, useMemo, useState } from 'react';
import { THEME_STORAGE_KEY, ThemeContext } from './theme';

const getSystemTheme = () => {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return 'light';
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
};

const getStoredTheme = () => {
  if (typeof window === 'undefined') return null;

  try {
    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
    return storedTheme === 'light' || storedTheme === 'dark' ? storedTheme : null;
  } catch {
    return null;
  }
};

export const ThemeProvider = ({ children }) => {
  const [preferredTheme, setPreferredTheme] = useState(getStoredTheme);
  const [systemTheme, setSystemTheme] = useState(getSystemTheme);
  const theme = preferredTheme ?? systemTheme;

  useEffect(() => {
    const root = document.documentElement;
    root.setAttribute('data-theme', theme);
    root.setAttribute('data-bs-theme', theme);
    root.style.colorScheme = theme;
  }, [theme]);

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return undefined;

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handleSystemThemeChange = (event) => {
      setSystemTheme(event.matches ? 'dark' : 'light');
    };

    mediaQuery.addEventListener?.('change', handleSystemThemeChange);
    return () => mediaQuery.removeEventListener?.('change', handleSystemThemeChange);
  }, []);

  const setTheme = useCallback((nextTheme) => {
    if (nextTheme !== 'light' && nextTheme !== 'dark') return;

    setPreferredTheme(nextTheme);
    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, nextTheme);
    } catch {
      // The in-memory preference still works when storage is unavailable.
    }
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme(theme === 'dark' ? 'light' : 'dark');
  }, [setTheme, theme]);

  const value = useMemo(() => ({ theme, setTheme, toggleTheme }), [setTheme, theme, toggleTheme]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
};
