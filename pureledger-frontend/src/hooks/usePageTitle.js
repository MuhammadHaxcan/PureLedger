import { useEffect } from 'react';

/**
 * Custom hook to set the page title in the format "PureLedger - PageName"
 * @param {string} pageName - The name of the page to display
 */
export const usePageTitle = (pageName) => {
  useEffect(() => {
    const previousTitle = document.title;
    document.title = `PureLedger - ${pageName}`;
    
    // Cleanup: restore previous title when component unmounts
    return () => {
      document.title = previousTitle;
    };
  }, [pageName]);
};

