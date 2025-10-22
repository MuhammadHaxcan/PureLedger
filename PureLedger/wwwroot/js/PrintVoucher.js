/**
 * PrintVoucher.js - Handles voucher document rendering and printing
 */
document.addEventListener('DOMContentLoaded', function() {
    initializeVoucherPage();
    handleProjectImage();
});

function initializeVoucherPage() {
    // Get voucher metadata from data attributes on the body
    const voucherType = document.body.getAttribute('data-voucher-type');
    const voucherId = document.body.getAttribute('data-voucher-id');

    // Set up event handlers
    setupEventHandlers();

    // Apply appropriate processing based on voucher type
    if (voucherType === VoucherType.JV) {
        applyJournalVoucherFiltering();
    } else if (voucherType === VoucherType.INV) {
        formatInvoiceAmounts();
    }
}

function setupEventHandlers() {
    // Print button
    document.getElementById('btnPrint').addEventListener('click', function() {
        window.print();
    });

    // Close button - this works better for a standalone window
    document.getElementById('btnClose').addEventListener('click', function() {
        window.close();
    });

    // After print handler
    window.addEventListener('afterprint', function() {
        console.log('Printing completed');
    });
}

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

function hideAccountNamesBasedOnFilter(filterType) {
    const rows = document.querySelectorAll('.table-striped tbody tr');

    rows.forEach((row) => {
        const cells = row.querySelectorAll('td');
        if (cells.length < 6) return;

        const debitValue = parseFormattedNumber(cells[4].textContent);
        const creditValue = parseFormattedNumber(cells[5].textContent);

        // Store original account name
        const accountCell = cells[0];
        if (!accountCell.hasAttribute('data-original-name')) {
            accountCell.setAttribute('data-original-name', accountCell.textContent);
        }

        if (filterType === 'all') {
            // Show all account names
            accountCell.textContent = accountCell.getAttribute('data-original-name');
        } else {
            const shouldShowName = filterType === 'debit' ? debitValue > 0 : creditValue > 0;
            accountCell.textContent = shouldShowName ? accountCell.getAttribute('data-original-name') : '';
        }
    });
}

function formatInvoiceAmounts() {
    formatBalanceElements();
    formatBuiltyElement();
    formatTableAmounts();
}

function formatBalanceElements() {
    // Format the previous balance
    const previousBalanceElement = document.querySelector('#previousBalance');
    if (previousBalanceElement) {
        const previousBalanceValue = parseFormattedNumber(previousBalanceElement.textContent);
        previousBalanceElement.textContent = formatNumber(previousBalanceValue);
    }

    // Format the customer running balance
    const balanceElement = document.querySelector('#customerRunningBalance');
    if (balanceElement) {
        const balanceValue = parseFormattedNumber(balanceElement.textContent);
        balanceElement.textContent = formatNumber(balanceValue);
    }
}

function formatBuiltyElement() {
    const builtyElement = document.querySelector('.invoice-info .info-table tr:nth-child(3) td:nth-child(2)');
    if (builtyElement && builtyElement.textContent.includes('(')) {
        const builtyText = builtyElement.textContent;
        const match = builtyText.match(/(\d+)\s*\(([0-9,.]+)\)/);
        if (match) {
            const builtyNumber = match[1];
            const cartageAmount = parseFormattedNumber(match[2]);
            builtyElement.textContent = formatNumber(cartageAmount);
        }
    }
}

function formatTableAmounts() {
    // Format all amount cells in the table - using more specific selectors for better performance
    const amountCells = document.querySelectorAll('.table-striped tbody tr td:last-child, .table-striped tfoot tr td:last-child, .table-striped tfoot tr th:last-child');
    amountCells.forEach(cell => {
        const value = parseFormattedNumber(cell.textContent);
        cell.textContent = formatNumber(value);
    });
}

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

function formatNumber(num) {
    if (isNaN(num) || num === null || num === undefined) {
        return '0';
    }

    // Format as integer (no decimal places)
    return new Intl.NumberFormat('en-US', {
        style: 'decimal',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(Math.round(num));
}

function handleProjectImage() {
    // Ensure project image is properly sized and displayed
    const logoImage = document.querySelector('.logo-container img');
    if (logoImage) {
        logoImage.addEventListener('error', function() {
            // If image fails to load, fall back to default logo
            this.src = '/images/logo.png';
        });
        
        // Ensure the image is properly sized
        logoImage.style.maxHeight = '100px';
        logoImage.style.maxWidth = '200px';
    }
}