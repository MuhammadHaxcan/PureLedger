// Site-wide utility functions for PureLedger
const PureLedger = {
    // Get the current base path based on route prefix
    getCurrentBasePath: function () {
        const pathParts = window.location.pathname.split('/');
        if (pathParts[1] === 'route1' || pathParts[1] === 'route2') {
            return '/' + pathParts[1];
        }
        return '';
    },

    // Create an AJAX request that automatically includes the correct base path
    ajax: function (url, options) {
        const basePath = this.getCurrentBasePath();
        options = options || {};

        // Create the full URL with the base path
        const fullUrl = url.startsWith('/') ?
            `${basePath}${url}` :
            `${basePath}/${url}`;

        // Return the jQuery ajax promise
        return $.ajax($.extend({
            url: fullUrl
        }, options));
    },

    // Shorthand methods for common AJAX operations
    get: function (url, data) {
        return this.ajax(url, {
            method: 'GET',
            data: data
        });
    },

    post: function (url, data) {
        const basePath = this.getCurrentBasePath();
        const fullUrl = url.startsWith('/') ?
            `${basePath}${url}` :
            `${basePath}/${url}`;

        console.log(`PureLedger.post - Full URL: ${fullUrl}`);

        return $.ajax({
            url: fullUrl,
            method: 'POST',
            data: JSON.stringify(data), // Serialize to JSON
            contentType: 'application/json',
            dataType: 'json'
        });
    }
};

// Add console logging for debugging
console.log('PureLedger utilities loaded. Base path:', PureLedger.getCurrentBasePath());