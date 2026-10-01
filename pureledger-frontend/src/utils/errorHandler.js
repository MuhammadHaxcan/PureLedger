/**
 * Utility functions for handling and displaying errors consistently.
 * The API now uses a shared error envelope:
 * success, message, errors, statusCode, errorCode, errorId, correlationId.
 */

const DEFAULT_ERROR_MESSAGE = 'An unexpected error occurred. Please try again.';

const flattenErrors = (errors) => {
  if (!errors) return [];

  if (Array.isArray(errors)) {
    return errors
      .flatMap((error) => flattenErrors(error))
      .filter(Boolean);
  }

  if (typeof errors === 'object') {
    return Object.entries(errors).flatMap(([field, value]) => {
      const values = flattenErrors(value);
      return values.length > 0 ? values.map((msg) => `${field}: ${msg}`) : [`${field}: ${String(value)}`];
    });
  }

  const message = String(errors).trim();
  return message ? [message] : [];
};

const appendReference = (message, errorId) => {
  if (!errorId || !message || message.includes(errorId)) return message;
  return `${message} Reference: ${errorId}`;
};

export const normalizeApiError = (error, fallbackMessage = DEFAULT_ERROR_MESSAGE) => {
  if (!error) {
    return {
      message: fallbackMessage,
      errors: [fallbackMessage],
      statusCode: null,
      errorCode: null,
      errorId: null,
      correlationId: null,
      isNetworkError: false,
      isServerError: false,
      isClientError: false,
      raw: error,
    };
  }

  if (typeof error === 'string') {
    return {
      message: error,
      errors: [error],
      statusCode: null,
      errorCode: null,
      errorId: null,
      correlationId: null,
      isNetworkError: false,
      isServerError: false,
      isClientError: false,
      raw: error,
    };
  }

  const data = error.response?.data || error.data || {};
  const statusCode = error.statusCode || error.response?.status || data.statusCode || null;
  const errorId = error.errorId || data.errorId || null;
  const correlationId = error.correlationId || data.correlationId || error.config?.metadata?.correlationId || null;
  const errorCode = error.errorCode || data.errorCode || null;
  const errors = flattenErrors(error.errors || data.errors);
  const responseMessage = data.message || error.message || fallbackMessage;
  const isNetworkError = error.isNetworkError === true || (!error.response && !statusCode && error.name !== 'Error');
  const isServerError = error.isServerError === true || (statusCode >= 500 && statusCode < 600);
  const isClientError = error.isClientError === true || (statusCode >= 400 && statusCode < 500);

  let message = responseMessage || fallbackMessage;
  if (isNetworkError) {
    message = 'Network error. Please check your connection and try again.';
  } else if (error.code === 'ECONNABORTED' || error.message?.includes('timeout')) {
    message = 'Request timed out. Please try again.';
  } else if (isServerError) {
    message = appendReference(message || 'Server error. Please try again later.', errorId);
  }

  return {
    ...error,
    message,
    errors: errors.length > 0 ? errors : [message],
    statusCode,
    errorCode,
    errorId,
    correlationId,
    isNetworkError,
    isServerError,
    isClientError,
    needsRefresh: error.needsRefresh ?? data.needsRefresh,
    xsrfToken: error.xsrfToken ?? data.xsrfToken,
    raw: error,
  };
};

/**
 * Extract error message from error object
 * @param {Error|Object} error - The error object from API call
 * @returns {string} - User-friendly error message
 */
export const getErrorMessage = (error) => {
  return normalizeApiError(error).message;
};

/**
 * Extract all error messages from error object
 * @param {Error|Object} error - The error object from API call
 * @returns {string[]} - Array of error messages
 */
export const getAllErrorMessages = (error) => {
  return normalizeApiError(error).errors;
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
  return normalizeApiError(error).isNetworkError === true;
};

/**
 * Check if error is a server error (5xx)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isServerError = (error) => {
  return normalizeApiError(error).isServerError === true;
};

/**
 * Check if error is a client error (4xx)
 * @param {Error|Object} error - The error object
 * @returns {boolean}
 */
export const isClientError = (error) => {
  return normalizeApiError(error).isClientError === true;
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
  const normalized = normalizeApiError(error);

  if (normalized.isNetworkError) {
    return 'Network error. Please check your connection and try again.';
  }
  
  if (normalized.isServerError) {
    return appendReference(
      'Server error. Please try again later or contact support if the problem persists.',
      normalized.errorId
    );
  }
  
  if (normalized.statusCode === 403) {
    return normalized.message || 'You do not have permission to access this resource. Please contact your administrator.';
  }
  
  if (normalized.statusCode === 401) {
    return normalized.message || 'Your session has expired. Please log in again.';
  }
  
  if (normalized.statusCode === 404) {
    return normalized.message || 'The requested resource was not found.';
  }
  
  if (normalized.statusCode === 400) {
    return normalized.message || 'Invalid request. Please check your input and try again.';
  }
  
  return normalized.message;
};

export const getErrorReference = (error) => {
  const normalized = normalizeApiError(error);
  return {
    errorId: normalized.errorId,
    correlationId: normalized.correlationId,
    statusCode: normalized.statusCode,
    errorCode: normalized.errorCode,
  };
};

