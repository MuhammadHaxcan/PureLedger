/**
 * PrintVoucher.js - Handles client-side functionality for voucher printing
 */
$(function () {
    initializeVoucherPage();
});

/**
 * Initialize the voucher page with all required functionality
 */
function initializeVoucherPage() {
    // Get voucher metadata
    const voucherType = document.body.getAttribute('data-voucher-type');
    const voucherId = document.body.getAttribute('data-voucher-id');

    // Set document title
    setDocumentTitle(voucherType, voucherId);

    // Add button event handlers
    setupButtonHandlers();

    // Apply appropriate processing based on voucher type
    if (voucherType === 'JV') {
        applyJournalVoucherFiltering();
    } else if (voucherType === 'INV') {
        formatInvoiceAmounts();
    }
}

/**
 * Set the document title based on voucher type and ID
 */
function setDocumentTitle(voucherType, voucherId) {
    let title = 'Document #' + voucherId;

    switch (voucherType) {
        case 'JV':
            title = 'Journal Voucher #' + voucherId;
            break;
        case 'DC':
            title = 'Delivery Challan #' + voucherId;
            break;
        case 'INV':
            title = 'Invoice #' + voucherId;
            break;
    }

    document.title = title;
}

/**
 * Setup event handlers for print and close buttons
 */
function setupButtonHandlers() {
    // Print button
    $('.btn-success').on('click', function () {
        window.print();
    });

    // Close button
    $('.btn-secondary').on('click', function () {
        window.close();
    });

    // After print handler
    window.addEventListener('afterprint', function () {
        console.log('Printing completed');
    });
}

/**
 * Apply filtering to journal voucher entries based on selected filter
 */
function applyJournalVoucherFiltering() {
    // Get filter from URL parameters or use default
    const urlParams = new URLSearchParams(window.location.search);
    const filter = urlParams.get('filter') || currentFilter || 'all';

    // Apply filter to entries
    hideAccountNamesBasedOnFilter(filter);

    // Update document title if filtered
    if (filter !== 'all') {
        const filterText = filter === 'debit' ? 'Debit Entries' : 'Credit Entries';
        document.title += ' - ' + filterText;
    }
}

/**
 * Hide account names in journal voucher entries based on selected filter
 * Shows all rows but only displays account names for entries matching the filter
 */
function hideAccountNamesBasedOnFilter(filterType) {
    if (filterType === 'all') {
        return; // No filtering needed
    }

    const rows = document.querySelectorAll('.table-striped tbody tr');
    console.log(`Filtering journal voucher account names by: ${filterType}`);

    rows.forEach((row) => {
        const cells = row.querySelectorAll('td');
        if (cells.length < 7) return; // Skip rows with insufficient columns

        // Get debit and credit values
        const debitValue = parseFormattedNumber(cells[5].textContent); // Debit column
        const creditValue = parseFormattedNumber(cells[6].textContent); // Credit column

        // Store original account name
        const accountCell = cells[0];
        if (!accountCell.hasAttribute('data-original-name')) {
            accountCell.setAttribute('data-original-name', accountCell.textContent);
        }

        // Determine if this account name should be visible based on filter
        const shouldShowName = filterType === 'debit' ? debitValue > 0 : creditValue > 0;

        // Show or hide the account name without changing row opacity
        accountCell.textContent = shouldShowName ? accountCell.getAttribute('data-original-name') : '';
    });
}

/**
 * Format invoice amounts for better display
 */
function formatInvoiceAmounts() {
    // Format the customer running balance
    const balanceElement = document.querySelector('#customerRunningBalance');
    if (balanceElement) {
        const balanceValue = parseFormattedNumber(balanceElement.textContent);
        balanceElement.textContent = formatNumber(balanceValue);
    }
    
    // Format the builty value if it contains cartage amount
    const builtyElement = document.querySelector('.invoice-info .info-table tr:nth-child(3) td:nth-child(2)');
    if (builtyElement && builtyElement.textContent.includes('(')) {
        const builtyText = builtyElement.textContent;
        const match = builtyText.match(/(\d+)\s*\(([0-9,.]+)\)/);
        if (match) {
            const builtyNumber = match[1];
            const cartageAmount = parseFormattedNumber(match[2]);
            builtyElement.textContent = `${formatNumber(cartageAmount)}`;
        }
    }
    
    // Format all amount cells in the table
    const amountCells = document.querySelectorAll('.table-striped tbody tr td:last-child, .table-striped tfoot tr td:last-child, .table-striped tfoot tr th:last-child');
    amountCells.forEach(cell => {
        const value = parseFormattedNumber(cell.textContent);
        cell.textContent = formatNumber(value);
    });
}

/**
 * Parse a number from a formatted string (removes commas, % signs, handles parentheses)
 */
function parseFormattedNumber(str) {
    if (!str || typeof str !== 'string') return 0;

    // Remove commas, percentage signs
    let cleanedStr = str.replace(/,/g, '').replace(/%/g, '');

    // Handle parentheses for negative numbers like (123.45)
    if (cleanedStr.startsWith('(') && cleanedStr.endsWith(')')) {
        cleanedStr = '-' + cleanedStr.substring(1, cleanedStr.length - 1);
    }

    const result = parseFloat(cleanedStr);
    return isNaN(result) ? 0 : result;
}

/**
 * Format a number with commas and decimal places
 */
function formatNumber(num) {
    if (isNaN(num) || num === null || num === undefined) {
        return '0.00';
    }

    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(num);
}