import { useContext, useEffect } from 'react';
import NavbarTitleContext from '../contexts/NavbarTitleContext';

/**
 * Custom hook to set the page title in the format "PureLedger - PageName"
 * @param {string} pageName - The name used in the browser title
 * @param {string} navbarTitle - Optional title shown in the shared navbar
 */
export const usePageTitle = (pageName, navbarTitle = pageName) => {
  const setNavbarTitle = useContext(NavbarTitleContext);

  useEffect(() => {
    const previousTitle = document.title;
    document.title = `PureLedger - ${pageName}`;
    setNavbarTitle?.(navbarTitle);

    // Cleanup: restore previous title when component unmounts
    return () => {
      document.title = previousTitle;
      setNavbarTitle?.(null);
    };
  }, [navbarTitle, pageName, setNavbarTitle]);
};

