// Site-wide utility functions for PureLedger
const PureLedger = {
    // Get the current base path (simplified since we no longer use route prefixes)
    getCurrentBasePath: function () {
        // With simplified routing, we don't need to extract route prefixes
        return '';
    },

    // Create an AJAX request that automatically includes the correct base path
    ajax: function (url, options) {
        options = options || {};

        // Create the full URL (now without base path)
        const fullUrl = url.startsWith('/') ? url : '/' + url;

        console.log(`PureLedger.ajax - URL: ${fullUrl}, Method: ${options.method || 'GET'}`);

        // Return the jQuery ajax promise
        return $.ajax($.extend({
            url: fullUrl,
            dataType: 'json', // Ensure JSON response parsing
            cache: false // Prevent caching issues
        }, options));
    },

    // Shorthand methods for common AJAX operations
    get: function (url, data) {
        console.log(`PureLedger.get - URL: ${url}, Data:`, data);
        
        return this.ajax(url, {
            method: 'GET',
            data: data,
            dataType: 'json', // Explicitly set dataType for GET requests
            cache: false, // Prevent caching issues with pagination
            traditional: true // Use traditional parameter serialization
        });
    },

    post: function (url, data) {
        const fullUrl = url.startsWith('/') ? url : '/' + url;

        console.log(`PureLedger.post - Full URL: ${fullUrl}`);

        return $.ajax({
            url: fullUrl,
            method: 'POST',
            data: JSON.stringify(data), // Serialize to JSON
            contentType: 'application/json',
            dataType: 'json',
            cache: false
        });
    }
};

// Add console logging for debugging
console.log('PureLedger utilities loaded. Using simplified routing without prefixes.');