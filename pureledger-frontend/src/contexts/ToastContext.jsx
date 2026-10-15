import { createContext, useContext } from 'react';
import { toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

const ToastContext = createContext(null);

// The hook is deliberately colocated with its provider; splitting it would touch every importer.
// eslint-disable-next-line react-refresh/only-export-components
export const useToast = () => {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return context;
};

export const ToastProvider = ({ children }) => {
  const showSuccess = (message, options = {}) => {
    toast.success(message, {
      position: "top-right",
      autoClose: 5000,
      hideProgressBar: true,
      closeOnClick: true,
      pauseOnHover: true,
      draggable: true,
      ...options
    });
  };

  const showError = (message, options = {}) => {
    toast.error(message, {
      position: "top-right",
      autoClose: 10000,
      hideProgressBar: true,
      closeOnClick: true,
      pauseOnHover: true,
      draggable: true,
      ...options
    });
  };

  const showWarning = (message, options = {}) => {
    toast.warning(message, {
      position: "top-right",
      autoClose: 5000,
      hideProgressBar: true,
      closeOnClick: true,
      pauseOnHover: true,
      draggable: true,
      ...options
    });
  };

  const showInfo = (message, options = {}) => {
    toast.info(message, {
      position: "top-right",
      autoClose: 5000,
      hideProgressBar: true,
      closeOnClick: true,
      pauseOnHover: true,
      draggable: true,
      ...options
    });
  };

  const showAuthError = (message = "Your session has expired. Please log in again.") => {
    showError(message, {
      autoClose: 8000, // Longer for auth errors
      toastId: 'auth-error', // Prevent duplicate toasts
    });
  };

  const showSessionExpired = () => {
    showWarning("Your session has expired due to inactivity. Please log in again.", {
      autoClose: 8000,
      toastId: 'session-expired', //session has been expired.
    });
  };

  const showLoginSuccess = (username) => {
    showSuccess(`Welcome back, ${username}!`, {
      toastId: 'login-success',
    });
  };

  const showLogoutSuccess = () => {
    showInfo("You have been logged out successfully.", {
      toastId: 'logout-success',
    });
  };

  const value = {
    showSuccess,
    showError,
    showWarning,
    showInfo,
    showAuthError,
    showSessionExpired,
    showLoginSuccess,
    showLogoutSuccess,
  };

  return (
    <ToastContext.Provider value={value}>
      {children}
    </ToastContext.Provider>
  );
};
