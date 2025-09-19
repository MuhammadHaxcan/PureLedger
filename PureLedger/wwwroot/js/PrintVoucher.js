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
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(num);
    }

    // Parse number from formatted string
    function parseFormattedNumber(str) {
        if (!str) return 0;
        return parseFloat(str.replace(/,/g, '').replace(/%/g, ''));
    }

    // Get all rows in the invoice table
    const rows = document.querySelectorAll('.table-striped tbody tr');
    let totalQuantity = 0;
    let totalAmount = 0;

    console.log('Starting invoice calculations fix');

    // Process each row
    rows.forEach((row, index) => {
        const cells = row.querySelectorAll('td');
        if (cells.length >= 8) {
            // Extract values from cells
            const quantity = parseFormattedNumber(cells[2].textContent);
            const rate = parseFormattedNumber(cells[4].textContent);

            // Check if discount percent is showing as 0% but should be non-zero
            let discountPercent = parseFormattedNumber(cells[5].textContent);
            let originalAmount = quantity * rate;

            // If discount percent is 0 but we have Credit different from original amount,
            // recalculate the discount percent based on the difference
            const creditAmount = parseFormattedNumber(cells[7].textContent);

            if (discountPercent === 0 && creditAmount < originalAmount && originalAmount > 0) {
                // Calculate what the discount percent should be
                discountPercent = ((originalAmount - creditAmount) / originalAmount) * 100;
                console.log(`Row ${index + 1}: Calculated discount percent ${discountPercent.toFixed(2)}%`);

                // Update the display
                cells[5].textContent = discountPercent.toFixed(2) + '%';
            }

            // Calculate net rate (rate after discount)
            const netRate = rate * (1 - (discountPercent / 100));

            // Calculate final amount (this should match the Credit value)
            const amount = quantity * netRate;

            console.log(`Row ${index + 1}: Quantity=${quantity}, Rate=${rate}, Discount=${discountPercent}%, NetRate=${netRate.toFixed(2)}, Amount=${amount.toFixed(2)}`);

            // Update the displayed values with proper formatting
            cells[6].textContent = formatNumber(netRate);
            cells[7].textContent = formatNumber(amount);

            // Add to totals
            totalQuantity += quantity;
            totalAmount += amount;
        }
    });

    console.log(`Total Quantity: ${totalQuantity}, Total Amount: ${totalAmount.toFixed(2)}`);

    // Update the total row
    const totalRow = document.querySelector('.table-striped tfoot tr');
    if (totalRow) {
        const totalCells = totalRow.querySelectorAll('th');
        if (totalCells.length >= 8) {
            // Update totals with proper formatting
            totalCells[2].textContent = formatNumber(totalQuantity);
            totalCells[7].textContent = formatNumber(totalAmount);
        }
    }

    // Format customer running balance if present
    const balanceElement = document.querySelector('.alert-secondary strong + *');
    if (balanceElement && balanceElement.textContent !== 'N/A') {
        const balance = parseFormattedNumber(balanceElement.textContent);
        if (!isNaN(balance)) {
            balanceElement.textContent = formatNumber(balance);
        }
    }

    console.log('Invoice calculations fixed successfully');
}