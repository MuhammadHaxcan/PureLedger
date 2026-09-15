import axios from 'axios';
import { toast } from 'react-toastify';
import { normalizeApiError } from '../utils/errorHandler';

/**
 * API Base URL Configuration
 *
 * Development: Uses VITE_API_BASE_URL from .env.development or falls back to http://localhost:5221/api
 * Production: Should be set to /api in .env.production for Nginx reverse proxy
 *
 * The environment variable VITE_API_BASE_URL is injected at build time by Vite.
 * For production builds, ensure .env.production has: VITE_API_BASE_URL=/api
 */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5221/api';
const CORRELATION_HEADER = 'X-Correlation-ID';

let lastCorrelationId = null;

export const getLastCorrelationId = () => lastCorrelationId;

const createCorrelationId = () => {
    if (window.crypto?.randomUUID) {
        return `web_${window.crypto.randomUUID()}`;
    }

    return `web_${Date.now()}_${Math.random().toString(16).slice(2)}`;
};

/**
 * Read XSRF token directly from cookie.
 * This ensures the header always matches what the browser sends as the cookie.
 */
const getXsrfTokenFromCookie = () => {
    const match = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]*)/);
    return match ? decodeURIComponent(match[1]) : null;
};

/**
 * Set XSRF token (for backwards compatibility - now also sets cookie)
 */
export const setXsrfToken = (token) => {
    // Set cookie directly to ensure consistency
    if (token) {
        // Add secure flag if on HTTPS
        const secureFlag = window.location.protocol === 'https:' ? '; secure' : '';
        document.cookie = `XSRF-TOKEN=${encodeURIComponent(token)}; path=/; samesite=lax${secureFlag}`;
    }
};

/**
 * Get current XSRF token (reads from cookie)
 */
export const getXsrfToken = () => getXsrfTokenFromCookie();

/**
 * Clear XSRF token (called on logout)
 */
export const clearXsrfToken = () => {
    // Delete the cookie by setting it to expire in the past
    // Add secure flag if on HTTPS to properly delete secure cookies
    const secureFlag = window.location.protocol === 'https:' ? '; secure' : '';
    document.cookie = `XSRF-TOKEN=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT${secureFlag}`;
};

// Create axios instance with default configuration
const apiClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
    withCredentials: true, // Send cookies with requests (HttpOnly cookies for auth)
    timeout: 180000, // 180 seconds timeout
});

// Flag to prevent multiple simultaneous refresh requests
let isRefreshing = false;
let refreshSubscribers = [];

// Helper to add requests to queue during token refresh
const subscribeTokenRefresh = (callback) => {
    refreshSubscribers.push(callback);
};

// Helper to notify all queued requests when refresh completes
const onTokenRefreshed = () => {
    refreshSubscribers.forEach((callback) => callback());
    refreshSubscribers = [];
};

// Request interceptor to add XSRF token for CSRF protection
apiClient.interceptors.request.use(
    (config) => {
        const correlationId = createCorrelationId();
        config.headers[CORRELATION_HEADER] = correlationId;
        config.metadata = {
            ...(config.metadata || {}),
            correlationId,
        };

        // Add XSRF token for state-changing requests (POST, PUT, DELETE, PATCH)
        const stateMutatingMethods = ['post', 'put', 'delete', 'patch'];
        if (stateMutatingMethods.includes(config.method?.toLowerCase())) {
            // Always read fresh from cookie to ensure consistency
            const token = getXsrfTokenFromCookie();
            if (token) {
                config.headers['X-XSRF-TOKEN'] = token;
            }
        }

        // Don't override Content-Type for FormData (file uploads)
        if (config.data instanceof FormData) {
            delete config.headers['Content-Type'];
        }

        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

// Response interceptor to handle errors globally
apiClient.interceptors.response.use(
    (response) => {
        const responseCorrelationId = response.headers?.[CORRELATION_HEADER.toLowerCase()] || response.headers?.[CORRELATION_HEADER];
        if (responseCorrelationId) {
            lastCorrelationId = responseCorrelationId;
        }

        return response;
    },
    async (error) => {
        const originalRequest = error.config;

        // Extract error information from response
        const errorData = error.response?.data || {};
        const statusCode = error.response?.status;
        const responseCorrelationId = error.response?.headers?.[CORRELATION_HEADER.toLowerCase()] || errorData.correlationId || originalRequest?.metadata?.correlationId;
        if (responseCorrelationId) {
            lastCorrelationId = responseCorrelationId;
        }
        const standardizedError = normalizeApiError({
            ...error,
            statusCode,
            errorId: errorData.errorId,
            correlationId: responseCorrelationId,
            errorCode: errorData.errorCode,
            needsRefresh: errorData.needsRefresh,
            xsrfToken: errorData.xsrfToken,
        });

        // Handle XSRF token refresh scenario (400 with needsRefresh flag)
        if (statusCode === 400 && standardizedError.needsRefresh && originalRequest && !originalRequest._xsrfRetry) {
            originalRequest._xsrfRetry = true;

            // Update XSRF token if provided in error response
            if (standardizedError.xsrfToken) {
                // Set the cookie with the new token
                setXsrfToken(standardizedError.xsrfToken);
                // Also update the header on the original request for the retry
                originalRequest.headers['X-XSRF-TOKEN'] = standardizedError.xsrfToken;
                console.log('XSRF token refreshed from error response, retrying request...');
            }

            // Small delay to ensure cookie is set before retry
            await new Promise(resolve => setTimeout(resolve, 50));

            // Retry the original request with the new token
            return apiClient(originalRequest);
        }

        // Handle 401 Unauthorized - Token expired or invalid
        if (statusCode === 401 && originalRequest && !originalRequest._retry) {
            // Don't retry refresh for auth-related endpoints to prevent loops
            const noRefreshEndpoints = [
                '/AccountApi/login',
                '/AccountApi/refresh-token',
                '/AccountApi/logout',
                '/AccountApi/check-auth',
            ];
            const isAuthEndpoint = noRefreshEndpoints.some((endpoint) =>
                originalRequest.url?.includes(endpoint)
            );

            if (isAuthEndpoint) {
                return Promise.reject(standardizedError);
            }

            originalRequest._retry = true;

            // Try to refresh token using cookies (tokens are in HttpOnly cookies)
            if (isRefreshing) {
                // If already refreshing, queue this request
                return new Promise((resolve) => {
                    subscribeTokenRefresh(() => {
                        resolve(apiClient(originalRequest));
                    });
                });
            }

            isRefreshing = true;

            try {
                // Refresh endpoint reads tokens from cookies, no body needed
                const response = await axios.post(
                    `${API_BASE_URL}/AccountApi/refresh-token`,
                    {},
                    {
                        withCredentials: true, // Send cookies
                        headers: {
                            'Content-Type': 'application/json',
                        },
                    }
                );

                if (response.data.success) {
                    // Update XSRF token
                    if (response.data.xsrfToken) {
                        setXsrfToken(response.data.xsrfToken);
                    }

                    // Notify queued requests
                    onTokenRefreshed();
                    isRefreshing = false;

                    // Retry original request (cookies are automatically included)
                    return apiClient(originalRequest);
                } else {
                    // Refresh returned success=false, treat as failure
                    throw new Error(response.data.message || 'Token refresh failed');
                }
            } catch (refreshError) {
                console.error('Token refresh failed:', refreshError);
                isRefreshing = false;
                refreshSubscribers = [];

                // Clear XSRF token
                clearXsrfToken();

                // Show auth error toast
                toast.error('Your session has expired. Please log in again.', {
                    position: "top-right",
                    autoClose: 8000,
                    hideProgressBar: false,
                    closeOnClick: true,
                    pauseOnHover: true,
                    draggable: true,
                    toastId: 'session-expired',
                });

                // Clear non-sensitive auth data from localStorage
                localStorage.removeItem('user');
                localStorage.removeItem('userAccess');
                localStorage.removeItem('navigationMenu');

                // Redirect to login after a short delay to show the toast
                setTimeout(() => {
                    if (window.location.pathname !== '/login') {
                        window.location.href = '/login';
                    }
                }, 2000);

                return Promise.reject(standardizedError);
            }
        }

        // Handle 403 Forbidden - Access denied
        if (statusCode === 403) {
            const accessMessage = standardizedError.message || 'You do not have permission to access this resource.';
            standardizedError.message = accessMessage;

            // Show access denied toast
            toast.warning(accessMessage, {
                position: "top-right",
                autoClose: 5000,
                hideProgressBar: false,
                closeOnClick: true,
                pauseOnHover: true,
                draggable: true,
                toastId: 'access-denied',
            });

            return Promise.reject(standardizedError);
        }

        // Handle network errors
        if (!error.response) {
            console.error('Network Error:', error.message);
            standardizedError.message = 'Network error. Please check your connection and try again.';
            standardizedError.errors = ['Unable to connect to the server. Please check your internet connection.'];
            return Promise.reject(standardizedError);
        }

        // Handle timeout errors
        if (error.code === 'ECONNABORTED' || error.message?.includes('timeout')) {
            standardizedError.message = 'Request timed out. Please try again.';
            standardizedError.errors = ['The request took too long to complete. Please try again.'];
            return Promise.reject(standardizedError);
        }

        // Handle server errors (5xx)
        if (statusCode >= 500) {
            console.error('Server Error:', errorData);
            if (!standardizedError.errors || standardizedError.errors.length === 0) {
                standardizedError.errors = [standardizedError.message];
            }
            return Promise.reject(standardizedError);
        }

        // Handle client errors (4xx) - already set above
        if (statusCode >= 400 && statusCode < 500) {
            // If no errors array, create one from message
            if (!standardizedError.errors || standardizedError.errors.length === 0) {
                standardizedError.errors = [standardizedError.message];
            }
            return Promise.reject(standardizedError);
        }

        return Promise.reject(standardizedError);
    }
);

// Helper function to convert file to base64
export const fileToBase64 = (file) => {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.readAsDataURL(file);
        reader.onload = () => resolve(reader.result);
        reader.onerror = (error) => reject(error);
    });
};

// Helper function to validate image file
export const validateImageFile = (file, maxSizeMB = 5) => {
    const maxSize = maxSizeMB * 1024 * 1024; // Convert to bytes
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];

    if (!file) {
        return { valid: false, error: 'No file selected' };
    }

    if (!allowedTypes.includes(file.type)) {
        return { valid: false, error: 'Invalid file type. Please select an image file (JPEG, PNG, GIF, or WebP)' };
    }

    if (file.size > maxSize) {
        return { valid: false, error: `File size exceeds ${maxSizeMB}MB limit` };
    }

    return { valid: true };
};

export default apiClient;
