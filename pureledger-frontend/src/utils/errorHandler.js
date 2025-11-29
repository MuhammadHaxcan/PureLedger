/**
 * Utility functions for handling and displaying errors consistently
 */

/**
 * Extract error message from error object
 * @param {Error|Object} error - The error object from API call
 * @returns {string} - User-friendly error message
 */
export const getErrorMessage = (error) => {
  if (!error) return 'An unexpected error occurred.';
  
  // If error has a message property
  if (error.message) {
    return error.message;
  }
  
  // If error has errors array
  if (Array.isArray(error.errors) && error.errors.length > 0) {
    return error.errors[0];
  }
  
  // If error is a string
  if (typeof error === 'string') {
    return error;
  }
  
  // If error.response exists (axios error)
  if (error.response?.data) {
    const errorData = error.response.data;
    if (errorData.message) return errorData.message;
    if (Array.isArray(errorData.errors) && errorData.errors.length > 0) {
      return errorData.errors[0];
    }
  }
  
  return 'An unexpected error occurred. Please try again.';
};

/**
 * Extract all error messages from error object
 * @param {Error|Object} error - The error object from API call
 * @returns {string[]} - Array of error messages
 */
export const getAllErrorMessages = (error) => {
  if (!error) return ['An unexpected error occurred.'];
  
  // If error has errors array
  if (Array.isArray(error.errors) && error.errors.length > 0) {
    return error.errors;
  }
  
  // If error.response exists (axios error)
  if (error.response?.data) {
    const errorData = error.response.data;
    if (Array.isArray(errorData.errors) && errorData.errors.length > 0) {
      return errorData.errors;
    }
    if (errorData.message) {
      return [errorData.message];
    }
  }
  
  // If error has a message property
  if (error.message) {
    return [error.message];
  }
  
  // If error is a string
  if (typeof error === 'string') {
    return [error];
  }
  
  return ['An unexpected error occurred. Please try again.'];
};

/**
 * Check if error is a specific status code
 * @param {Error|Object} error - The error object
 * @param {number} statusCode - The status code to check
 * @returns {boolean}
 */
export const isErrorStatus = (error, statusCode) => {
  return error?.statusCode === statusCode || 
         error?.response?.status === statusCode;
};

/**
 * Check if error is a network error
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isNetworkError = (error) => {
  return error?.isNetworkError === true || !error?.response;
};

/**
 * Check if error is a server error (5xx)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isServerError = (error) => {
  return error?.isServerError === true || 
         (error?.response?.status >= 500 && error?.response?.status < 600);
};

/**
 * Check if error is a client error (4xx)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isClientError = (error) => {
  return error?.isClientError === true || 
         (error?.response?.status >= 400 && error?.response?.status < 500);
};

/**
 * Check if error is access denied (403)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isAccessDenied = (error) => {
  return isErrorStatus(error, 403);
};

/**
 * Check if error is unauthorized (401)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isUnauthorized = (error) => {
  return isErrorStatus(error, 401);
};

/**
 * Get user-friendly error message based on error type
 * @param {Error|Object} error - The error object
 * @returns {string} - User-friendly error message
 */
export const getUserFriendlyErrorMessage = (error) => {
  if (isNetworkError(error)) {
    return 'Network error. Please check your connection and try again.';
  }
  
  if (isServerError(error)) {
    return 'Server error. Please try again later or contact support if the problem persists.';
  }
  
  if (isAccessDenied(error)) {
    return 'You do not have permission to access this resource. Please contact your administrator.';
  }
  
  if (isUnauthorized(error)) {
    return 'Your session has expired. Please log in again.';
  }
  
  if (isErrorStatus(error, 404)) {
    return 'The requested resource was not found.';
  }
  
  if (isErrorStatus(error, 400)) {
    return getErrorMessage(error) || 'Invalid request. Please check your input and try again.';
  }
  
  return getErrorMessage(error);
};


