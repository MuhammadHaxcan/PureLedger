import axios from 'axios';
import { API_BASE_URL, getLastCorrelationId, getXsrfToken } from './api';

const MAX_TEXT_LENGTH = 8000;
const SENSITIVE_KEYS = [
  'password',
  'confirmPassword',
  'token',
  'refreshToken',
  'accessToken',
  'secret',
  'authorization',
  'cookie',
  'xsrfToken',
];

let recentErrorSignature = '';
let recentErrorTime = 0;

const truncate = (value, maxLength = MAX_TEXT_LENGTH) => {
  if (!value) return value;
  const text = String(value);
  return text.length <= maxLength ? text : text.slice(0, maxLength);
};

const redact = (value) => {
  if (Array.isArray(value)) {
    return value.slice(0, 25).map(redact);
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value)
        .slice(0, 50)
        .map(([key, item]) => [
          key,
          SENSITIVE_KEYS.some((sensitiveKey) => sensitiveKey.toLowerCase() === key.toLowerCase())
            ? '[REDACTED]'
            : redact(item),
        ])
    );
  }

  return typeof value === 'string' ? truncate(value) : value;
};

export const logClientError = async (error, context = {}) => {
  const message = error?.message || context.message || String(error || 'Unknown frontend error');
  const stackTrace = error?.stack || context.stackTrace || '';
  const signature = `${message}|${window.location.pathname}`;
  const now = Date.now();

  if (signature === recentErrorSignature && now - recentErrorTime < 5000) {
    return;
  }

  recentErrorSignature = signature;
  recentErrorTime = now;

  try {
    const xsrfToken = getXsrfToken();
    await axios.post(`${API_BASE_URL}/AdminApi/logs/client-error`, {
      message: truncate(message),
      stackTrace: truncate(stackTrace),
      path: window.location.pathname + window.location.search,
      source: 'Frontend',
      correlationId: getLastCorrelationId(),
      extraPropertiesJson: JSON.stringify(redact({
        ...context,
        userAgent: navigator.userAgent,
        url: window.location.href,
      })),
    }, {
      withCredentials: true,
      headers: {
        'Content-Type': 'application/json',
        ...(xsrfToken ? { 'X-XSRF-TOKEN': xsrfToken } : {}),
      },
    });
  } catch (loggingError) {
    console.error('Failed to submit frontend error log:', loggingError);
  }
};

export const registerGlobalClientErrorHandlers = () => {
  window.addEventListener('error', (event) => {
    logClientError(event.error || new Error(event.message), {
      type: 'window.error',
      filename: event.filename,
      line: event.lineno,
      column: event.colno,
    });
  });

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason instanceof Error
      ? event.reason
      : new Error(typeof event.reason === 'string' ? event.reason : 'Unhandled promise rejection');

    logClientError(reason, {
      type: 'unhandledrejection',
      reason: typeof event.reason === 'object' ? redact(event.reason) : event.reason,
    });
  });
};
