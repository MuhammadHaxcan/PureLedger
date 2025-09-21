$(function () {
    // Set document title based on voucher type and ID
    const voucherType = document.body.getAttribute('data-voucher-type');
    const voucherId = document.body.getAttribute('data-voucher-id');

    let title = 'Document #' + voucherId;

    if (voucherType === 'JV') {
        title = 'Journal Voucher #' + voucherId;
    } else if (voucherType === 'DC') {
        title = 'Delivery Challan #' + voucherId;
    } else if (voucherType === 'INV') {
        title = 'Invoice #' + voucherId;
    }

    document.title = title;

    // Add print button functionality
    $('.btn-success').on('click', function () {
        window.print();
    });

    // Add close button functionality
    $('.btn-secondary').on('click', function () {
        window.close();
    });

    // When printing is complete
    window.addEventListener('afterprint', function () {
        console.log('Printing completed');
    });

    // For Journal Vouchers, apply filtering
    if (voucherType === 'JV') {
        // Apply the filter from URL if it exists
        const urlParams = new URLSearchParams(window.location.search);
        const filter = urlParams.get('filter') || 'all';

        // Apply filtering to only hide account names, not entire rows
        hideAccountNamesBasedOnFilter(filter);

        // Update the document title with filter info
        if (filter !== 'all') {
            const filterText = filter === 'debit' ? 'Debit Entries' : 'Credit Entries';
            document.title += ' - ' + filterText;

            // No badge added to indicate filtering
        }
    }

    // For Invoice documents, validate calculations
    if (voucherType === 'INV') {
        fixInvoiceDiscountAndNetCalculations();
    }
});

/**
 * Hides account names in journal voucher entries based on selected filter
 * Shows all rows but only displays account names for entries matching the filter
 */
function hideAccountNamesBasedOnFilter(filterType) {
    if (filterType === 'all') {
        // No filtering needed when showing all
        return;
    }

    // Parse number from formatted string
    function parseFormattedNumber(str) {
        if (!str) return 0;
        return parseFloat(str.replace(/,/g, '').replace(/%/g, ''));
    }

    const rows = document.querySelectorAll('.table-striped tbody tr');

    console.log(`Filtering journal voucher account names by: ${filterType}`);

    // Process each row
    rows.forEach((row, index) => {
        const cells = row.querySelectorAll('td');
        if (cells.length >= 7) { // Journal voucher table has at least 7 columns
            // Get debit and credit values from their respective columns
            const debitValue = parseFormattedNumber(cells[5].textContent); // Debit column
            const creditValue = parseFormattedNumber(cells[6].textContent); // Credit column

            // Store original account name
            const accountCell = cells[0];
            if (!accountCell.hasAttribute('data-original-name')) {
                accountCell.setAttribute('data-original-name', accountCell.textContent);
            }

            let shouldShowName = false;

            // Determine if this account name should be visible based on filter
            if (filterType === 'debit') {
                shouldShowName = debitValue > 0;
            } else if (filterType === 'credit') {
                shouldShowName = creditValue > 0;
            }

            // Show or hide the account name without changing row opacity
            if (shouldShowName) {
                accountCell.textContent = accountCell.getAttribute('data-original-name');
            } else {
                // Only hide the account name, keep row normal
                accountCell.textContent = '';
            }
        }
    });

    console.log('Account name filtering applied');
}

/**
 * Fixes the invoice discount percentage display and net calculations
 */
function fixInvoiceDiscountAndNetCalculations() {
    // Format numbers with commas and decimal places
    function formatNumber(num) {
        if (isNaN(num) || num === null || num === undefined) {
            return '0.00';
        }
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(num);
    }

    // Parse number from formatted string
    function parseFormattedNumber(str) {
        if (!str || typeof str !== 'string') return 0;
        // Remove commas, percentage signs, and handle parentheses (negative numbers)
        let cleanedStr = str.replace(/,/g, '').replace(/%/g, '');
        if (cleanedStr.startsWith('(') && cleanedStr.endsWith(')')) {
            // Handle parentheses for negative numbers like (123.45)
            cleanedStr = '-' + cleanedStr.substring(1, cleanedStr.length - 1);
        }
        const result = parseFloat(cleanedStr);
        return isNaN(result) ? 0 : result;
    }

    // Calculate row values and update totals
    const rows = document.querySelectorAll('.table-striped tbody tr');
    let totalQuantity = 0;
    let totalAmount = 0;

    rows.forEach((row, index) => {
        // Process each row and update totals...
        const cells = row.querySelectorAll('td');
        if (cells.length >= 8) {
            try {
                const quantity = parseFormattedNumber(cells[2]?.textContent || '0');
                const rate = parseFormattedNumber(cells[4]?.textContent || '0');
                const discountPercent = parseFormattedNumber(cells[5]?.textContent || '0');
                
                // Calculate net rate after discount
                const netRate = rate * (1 - (discountPercent / 100));
                
                // Calculate amount
                const amount = quantity * netRate;
                
                // Update displayed values
                cells[6].textContent = formatNumber(netRate);
                cells[7].textContent = formatNumber(amount);
                
                // Update totals
                totalQuantity += quantity;
                totalAmount += amount;
            } catch (error) {
                console.error(`Error processing row ${index}:`, error);
            }
        }
    });

    // Update main total row
    const totalRow = document.querySelector('.table-striped tfoot tr.table-active');
    if (totalRow) {
        const totalCells = totalRow.querySelectorAll('th');
        if (totalCells.length >= 8) {
            totalCells[2].textContent = formatNumber(totalQuantity);
            totalCells[7].textContent = formatNumber(totalAmount);
        }
    }   

    // Get deduction amounts
    let forChargesAmount = 0;
    let cartageAmount = 0;
    
    // Find FOR charges row - look for the first table-warning row
    const forChargesRow = document.querySelector('.table-warning td:last-child');
    if (forChargesRow) {
        forChargesAmount = parseFormattedNumber(forChargesRow.textContent);
    }
    
    // Find cartage row - look for the second table-warning row
    const cartageRow = document.querySelector('.table-warning + .table-warning td:last-child');
    if (cartageRow) {
        cartageAmount = parseFormattedNumber(cartageRow.textContent);
    }

    // Update the final amount row
    const netAmountRow = document.querySelector('.table-info td:last-child');
    if (netAmountRow) {
        const netAmount = totalAmount - forChargesAmount - cartageAmount;
        netAmountRow.textContent = formatNumber(netAmount);
        console.log(`Updated final amount: ${totalAmount} - ${forChargesAmount} - ${cartageAmount} = ${netAmount}`);
    }
    
    // Update customer running balance
    const balanceElement = document.querySelector('#customerRunningBalance');
    if (balanceElement) {
        const balanceValue = parseFormattedNumber(balanceElement.textContent);
        balanceElement.textContent = formatNumber(balanceValue);
    }
}