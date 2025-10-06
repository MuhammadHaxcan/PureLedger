
$(function () {
    initializeVoucherPage();
});

function initializeVoucherPage() {
    // Get voucher metadata
    const voucherType = document.body.getAttribute('data-voucher-type');
    const voucherId = document.body.getAttribute('data-voucher-id');

    // Set document title
    setDocumentTitle(voucherType, voucherId);

    // Add button event handlers
    setupButtonHandlers();

    // Apply appropriate processing based on voucher type
    if (voucherType === VoucherType.JV) {
        applyJournalVoucherFiltering();
    } else if (voucherType === VoucherType.INV) {
        formatInvoiceAmounts();
    }
}

function setDocumentTitle(voucherType, voucherId) {
    let title = 'Document #' + voucherId;

    switch (voucherType) {
        case VoucherType.JV:
            title = 'Journal Voucher #' + voucherId;
            break;
        case VoucherType.DC:
            title = 'Delivery Challan #' + voucherId;
            break;
        case VoucherType.INV:
            title = 'Invoice #' + voucherId;
            break;
    }

    document.title = title;
}


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
    console.log(`Filtering journal voucher account names by: ${filterType}`);

    rows.forEach((row) => {
        const cells = row.querySelectorAll('td');
        if (cells.length < 6) return; // Updated to match actual column count

        const debitValue = parseFormattedNumber(cells[4].textContent);  // Changed from 5 to 4
        const creditValue = parseFormattedNumber(cells[5].textContent); // Changed from 6 to 5

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

    console.log('Filter applied:', filterType);
}

function formatInvoiceAmounts() {
    // Format the customer running balance
    const balanceElement = document.querySelector('#customerRunningBalance');
    if (balanceElement) {
        const balanceValue = parseFormattedNumber(balanceElement.textContent);
        balanceElement.textContent = formatNumber(balanceValue);
    }
    
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
        return '0.00';
    }

    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(num);
}