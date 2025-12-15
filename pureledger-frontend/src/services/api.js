import axios from 'axios';
import { toast } from 'react-toastify';

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

// Create axios instance with default configuration
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000, // 30 seconds timeout
});

// Flag to prevent multiple simultaneous refresh requests
let isRefreshing = false;
let refreshSubscribers = [];

// Helper to add requests to queue during token refresh
const subscribeTokenRefresh = (callback) => {
  refreshSubscribers.push(callback);
};

// Helper to notify all queued requests when refresh completes
const onTokenRefreshed = (newToken) => {
  refreshSubscribers.forEach((callback) => callback(newToken));
  refreshSubscribers = [];
};

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
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
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // Extract error information from response
    const errorData = error.response?.data || {};
    const statusCode = error.response?.status;

    // Build standardized error object
    const standardizedError = {
      ...error,
      message: errorData.message || error.message || 'An error occurred. Please try again.',
      errors: errorData.errors || (errorData.message ? [errorData.message] : []),
      statusCode: statusCode,
      isNetworkError: !error.response,
      isServerError: statusCode >= 500,
      isClientError: statusCode >= 400 && statusCode < 500,
    };

    // Handle 401 Unauthorized - Token expired or invalid
    if (statusCode === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      // Try to refresh token
      const refreshToken = localStorage.getItem('refreshToken');
      const accessToken = localStorage.getItem('authToken');

      if (refreshToken && accessToken) {
        if (isRefreshing) {
          // If already refreshing, queue this request
          return new Promise((resolve) => {
            subscribeTokenRefresh((newToken) => {
              originalRequest.headers.Authorization = `Bearer ${newToken}`;
              resolve(apiClient(originalRequest));
            });
          });
        }

        isRefreshing = true;

        try {
          const response = await axios.post(
            `${API_BASE_URL}/AccountApi/refresh-token`,
            {
              accessToken: accessToken,
              refreshToken: refreshToken,
            },
            {
              headers: {
                'Content-Type': 'application/json',
              },
            }
          );

          if (response.data.success) {
            const { token: newAccessToken, refreshToken: newRefreshToken } = response.data;

            // Update stored tokens
            localStorage.setItem('authToken', newAccessToken);
            localStorage.setItem('refreshToken', newRefreshToken);

            // Update authorization header for original request
            originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;

            // Notify queued requests
            onTokenRefreshed(newAccessToken);
            isRefreshing = false;

            // Retry original request
            return apiClient(originalRequest);
          }
        } catch (refreshError) {
          console.error('Token refresh failed:', refreshError);
          isRefreshing = false;
          refreshSubscribers = [];

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

          // Clear auth data
          localStorage.removeItem('authToken');
          localStorage.removeItem('refreshToken');
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
      } else {
        // No refresh token available, clear auth and redirect
        localStorage.removeItem('authToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('user');
        localStorage.removeItem('userAccess');
        localStorage.removeItem('navigationMenu');

        // Redirect to login if not already there
        if (window.location.pathname !== '/login') {
          window.location.href = '/login';
        }

        return Promise.reject(standardizedError);
      }
    }

    // Handle 403 Forbidden - Access denied
    if (statusCode === 403) {
      const accessMessage = errorData.message || 'You do not have permission to access this resource.';
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
      standardizedError.message = errorData.message || 'Server error. Please try again later.';
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

// Helper function to convert base64 to blob URL for display
export const base64ToBlobUrl = (base64String) => {
  if (!base64String) return null;
  
  // If it's already a data URL, return it
  if (base64String.startsWith('data:')) {
    return base64String;
  }
  
  // If it's base64 without data URL prefix, add it
  if (!base64String.startsWith('data:image')) {
    return `data:image/png;base64,${base64String}`;
  }
  
  return base64String;
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
