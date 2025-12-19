import DOMPurify from 'dompurify';

/**
 * Sanitization utilities for XSS protection
 * Uses DOMPurify for HTML sanitization
 */

/**
 * Sanitize HTML content to prevent XSS attacks
 * @param {string} html - The HTML content to sanitize
 * @param {object} options - DOMPurify configuration options
 * @returns {string} - Sanitized HTML
 */
export const sanitizeHtml = (html, options = {}) => {
  if (!html || typeof html !== 'string') {
    return '';
  }

  const defaultOptions = {
    ALLOWED_TAGS: ['b', 'i', 'em', 'strong', 'a', 'p', 'br', 'ul', 'ol', 'li', 'span'],
    ALLOWED_ATTR: ['href', 'target', 'rel', 'class'],
    ALLOW_DATA_ATTR: false,
    ADD_ATTR: ['target'], // Add target="_blank" to links
  };

  return DOMPurify.sanitize(html, { ...defaultOptions, ...options });
};

/**
 * Sanitize and strip all HTML, returning plain text
 * @param {string} html - The HTML content to strip
 * @returns {string} - Plain text without HTML
 */
export const stripHtml = (html) => {
  if (!html || typeof html !== 'string') {
    return '';
  }

  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: [],
    ALLOWED_ATTR: [],
  });
};

/**
 * Sanitize user input for safe display (escape HTML entities)
 * @param {string} input - The user input to sanitize
 * @returns {string} - Escaped string safe for display
 */
export const escapeHtml = (input) => {
  if (!input || typeof input !== 'string') {
    return '';
  }

  const htmlEntities = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;',
    '/': '&#x2F;',
    '`': '&#x60;',
    '=': '&#x3D;',
  };

  return input.replace(/[&<>"'`=/]/g, (char) => htmlEntities[char]);
};

/**
 * Sanitize URL to prevent javascript: and data: URL injection
 * @param {string} url - The URL to sanitize
 * @returns {string} - Safe URL or empty string if invalid
 */
export const sanitizeUrl = (url) => {
  if (!url || typeof url !== 'string') {
    return '';
  }

  const trimmedUrl = url.trim().toLowerCase();

  // Block dangerous protocols
  const dangerousProtocols = ['javascript:', 'data:', 'vbscript:', 'file:'];
  if (dangerousProtocols.some((protocol) => trimmedUrl.startsWith(protocol))) {
    console.warn('Blocked dangerous URL:', url);
    return '';
  }

  // Allow relative URLs, http, https, mailto
  const allowedProtocols = ['http:', 'https:', 'mailto:', 'tel:'];
  const hasProtocol = /^[a-z][a-z0-9+.-]*:/i.test(url);

  if (hasProtocol && !allowedProtocols.some((protocol) => trimmedUrl.startsWith(protocol))) {
    console.warn('Blocked URL with unknown protocol:', url);
    return '';
  }

  return url;
};

/**
 * Sanitize object values recursively
 * @param {object} obj - Object with potential unsafe values
 * @returns {object} - Object with sanitized string values
 */
export const sanitizeObject = (obj) => {
  if (!obj || typeof obj !== 'object') {
    return obj;
  }

  if (Array.isArray(obj)) {
    return obj.map((item) => sanitizeObject(item));
  }

  const sanitized = {};
  for (const [key, value] of Object.entries(obj)) {
    if (typeof value === 'string') {
      sanitized[key] = escapeHtml(value);
    } else if (typeof value === 'object' && value !== null) {
      sanitized[key] = sanitizeObject(value);
    } else {
      sanitized[key] = value;
    }
  }

  return sanitized;
};

/**
 * Check if a string contains potentially dangerous HTML
 * @param {string} input - String to check
 * @returns {boolean} - True if potentially dangerous
 */
export const containsDangerousHtml = (input) => {
  if (!input || typeof input !== 'string') {
    return false;
  }

  // Check for script tags, event handlers, and javascript: URLs
  const dangerousPatterns = [
    /<script\b/i,
    /\bon\w+\s*=/i, // Event handlers like onclick, onerror
    /javascript:/i,
    /data:\s*text\/html/i,
    /<iframe\b/i,
    /<object\b/i,
    /<embed\b/i,
    /<svg\b.*\bonload\b/i,
  ];

  return dangerousPatterns.some((pattern) => pattern.test(input));
};

export default {
  sanitizeHtml,
  stripHtml,
  escapeHtml,
  sanitizeUrl,
  sanitizeObject,
  containsDangerousHtml,
};
