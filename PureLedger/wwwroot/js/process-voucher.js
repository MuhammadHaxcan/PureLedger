$(function () {
    const form = $('#processingForm');
    const submitBtn = $('#submitBtn');
    const selectAllBtn = $('#selectAllBtn');
    const deselectAllBtn = $('#deselectAllBtn');
    const maxQuantityBtn = $('#maxQuantityBtn');
    const entryCheckboxes = $('.entry-select');
    const processQtyInputs = $('.process-qty');

    // Track the account entry rows
    const accountEntryRows = $('.entry-row[data-is-account="true"]');
    let updatingAccounts = false;

    // Calculate and update entry amounts (debit and credit)
    function calculateEntryAmounts(row) {
        const $row = $(row);
        const $checkbox = $row.find('.entry-select');
        const $quantityInput = $row.find('.process-qty');
        const $debitDisplay = $row.find('.entry-debit');
        const $creditDisplay = $row.find('.entry-credit');

        const isChecked = $checkbox.is(':checked');
        const quantity = parseInt($quantityInput.val()) || 0;
        const originalDebit = parseInt($row.find('input[name$=".Debit"]').val()) || 0;
        const originalCredit = parseInt($row.find('input[name$=".Credit"]').val()) || 0;
        const availableQuantity = parseInt($row.find('input[name$=".AvailableQuantity"]').val()) || 0;
        const isAccount = $row.data('isAccount') === true;

        let debitAmount = 0;
        let creditAmount = 0;

        if (isChecked && quantity > 0 && availableQuantity > 0) {
            // For regular entries, calculate proportional values based on quantity
            const ratio = quantity / availableQuantity;

            if (originalDebit > 0) {
                debitAmount = Math.round(originalDebit * ratio);
            } else {
                creditAmount = Math.round(originalCredit * ratio);
            }
        }

        // Update the display
        $debitDisplay.text(debitAmount.toLocaleString());
        $creditDisplay.text(creditAmount.toLocaleString());

        return {
            debit: debitAmount,
            credit: creditAmount,
            isAccount: isAccount
        };
    }

    // Calculate and update account entries based on other entries
    function updateAccountEntries() {
        // Prevent recursion when updating account entries
        if (updatingAccounts) return;
        updatingAccounts = true;

        // Calculate totals from non-account entries first
        let totalDebits = 0;
        let totalCredits = 0;

        $('.entry-row').each(function () {
            const $row = $(this);
            const isAccount = $row.data('isAccount') === true;

            // Skip account entries for this calculation
            if (!isAccount && $row.find('.entry-select').is(':checked')) {
                const debit = parseInt($row.find('.entry-debit').text().replace(/,/g, '')) || 0;
                const credit = parseInt($row.find('.entry-credit').text().replace(/,/g, '')) || 0;
                totalDebits += debit;
                totalCredits += credit;
            }
        });

        // Calculate the imbalance
        const difference = totalDebits - totalCredits;

        // Update each account entry
        accountEntryRows.each(function () {
            const $row = $(this);
            const $checkbox = $row.find('.entry-select');
            const $debitDisplay = $row.find('.entry-debit');
            const $creditDisplay = $row.find('.entry-credit');
            const $hiddenAmountField = $row.find('.account-entry-amount');
            const $hiddenIsDebitField = $row.find('.account-entry-is-debit');

            // NEVER disable account entries - they should always be available
            $checkbox.prop('disabled', false);

            // Always check the account entry checkbox if any regular entry is selected
            if (totalDebits > 0 || totalCredits > 0) {
                $checkbox.prop('checked', true);
                $row.addClass('table-active');
            }

            // Update the debit/credit display based on the imbalance
            if (difference > 0) {
                // More debits than credits, so account entry needs to be credit
                $debitDisplay.text('0');
                $creditDisplay.text(difference.toLocaleString());

                // Also update the hidden fields
                if ($hiddenAmountField.length) {
                    $hiddenAmountField.val(difference);
                }
                if ($hiddenIsDebitField.length) {
                    $hiddenIsDebitField.val(false);
                }

                // Set the AccountEntryAmount and AccountEntryIsDebit fields in the form
                $('#AccountEntryAmount').val(difference);
                $('#AccountEntryIsDebit').val(false);

            } else if (difference < 0) {
                // More credits than debits, so account entry needs to be debit
                $debitDisplay.text(Math.abs(difference).toLocaleString());
                $creditDisplay.text('0');

                // Also update the hidden fields
                if ($hiddenAmountField.length) {
                    $hiddenAmountField.val(Math.abs(difference));
                }
                if ($hiddenIsDebitField.length) {
                    $hiddenIsDebitField.val(true);
                }

                // Set the AccountEntryAmount and AccountEntryIsDebit fields in the form
                $('#AccountEntryAmount').val(Math.abs(difference));
                $('#AccountEntryIsDebit').val(true);

            } else {
                // Balanced
                $debitDisplay.text('0');
                $creditDisplay.text('0');

                if ($hiddenAmountField.length) {
                    $hiddenAmountField.val(0);
                }

                // Set the AccountEntryAmount field in the form
                $('#AccountEntryAmount').val(0);
            }
        });

        updatingAccounts = false;
    }

    // Calculate and update total amounts
    function calculateTotals() {
        let totalDebit = 0;
        let totalCredit = 0;

        $('.entry-row').each(function () {
            const $row = $(this);
            const $checkbox = $row.find('.entry-select');

            if ($checkbox.is(':checked')) {
                const debit = parseInt($row.find('.entry-debit').text().replace(/,/g, '')) || 0;
                const credit = parseInt($row.find('.entry-credit').text().replace(/,/g, '')) || 0;
                totalDebit += debit;
                totalCredit += credit;
            }
        });

        $('#totalDebit').text(totalDebit.toLocaleString());
        $('#totalCredit').text(totalCredit.toLocaleString());

        return {
            totalDebit,
            totalCredit
        };
    }

    // Update selected count and all calculated values
    function updateSelectedCount() {
        const count = entryCheckboxes.filter(':checked').length;
        $('#selectedCount').text(count);

        // Enable/disable submit button
        submitBtn.prop('disabled', count === 0);

        // Calculate amounts for all non-account rows first
        $('.entry-row').not('[data-is-account="true"]').each(function () {
            calculateEntryAmounts(this);
        });

        // Update account entries based on other entries
        updateAccountEntries();

        // Calculate totals after all entries are updated
        calculateTotals();
    }

    // Handle checkbox selection
    entryCheckboxes.on('change', function () {
        const $checkbox = $(this);
        const $row = $checkbox.closest('tr');
        const $quantityInput = $row.find('.process-qty');
        const isAccount = $row.data('isAccount') === true;

        if ($checkbox.is(':checked')) {
            $row.addClass('table-active');
            // If quantity is 0, set it to max (for non-account entries only)
            if (parseInt($quantityInput.val()) === 0 && !isAccount) {
                $quantityInput.val($quantityInput.attr('max'));
            }
        } else {
            $row.removeClass('table-active');
            // Reset quantity to 0 when unchecked
            $quantityInput.val(0);
        }

        updateSelectedCount();
    });

    // Handle quantity changes
    processQtyInputs.on('change', function () {
        const $input = $(this);
        const $row = $input.closest('tr');
        const $checkbox = $row.find('.entry-select');
        const value = parseInt($input.val()) || 0;
        const max = parseInt($input.attr('max'));
        const isAccount = $row.data('isAccount') === true;

        // Ensure value is within bounds
        if (value < 0) {
            $input.val(0);
        } else if (value > max) {
            $input.val(max);
        }

        // For account entries, allow 0 quantity but still keep checked
        if (isAccount) {
            $checkbox.prop('checked', true);
            $row.addClass('table-active');
        } else {
            // For regular entries, check/uncheck based on quantity
            if (value > 0) {
                $checkbox.prop('checked', true);
                $row.addClass('table-active');
            } else {
                $checkbox.prop('checked', false);
                $row.removeClass('table-active');
            }
        }

        updateSelectedCount();
    });

    // Select All button
    selectAllBtn.on('click', function () {
        entryCheckboxes.not(':disabled').prop('checked', true).trigger('change');
    });

    // Deselect All button - keep account entries selected if any other entries are selected
    deselectAllBtn.on('click', function () {
        entryCheckboxes.filter(function () {
            return !$(this).closest('tr').data('isAccount');
        }).prop('checked', false).trigger('change');
    });

    // Max Quantities button
    maxQuantityBtn.on('click', function () {
        entryCheckboxes.filter(':checked').each(function () {
            const $checkbox = $(this);
            const $row = $checkbox.closest('tr');
            const isAccount = $row.data('isAccount') === true;

            // Skip account entries when setting max quantities
            if (!isAccount) {
                const $quantityInput = $row.find('.process-qty');
                $quantityInput.val($quantityInput.attr('max')).trigger('change');
            }
        });
    });

    // Form submission
    form.on('submit', function (e) {
        // Force update account entries one last time
        updateAccountEntries();

        // Validate that at least one entry is selected
        if (entryCheckboxes.filter(':checked').length === 0) {
            e.preventDefault();
            alert('Please select at least one entry to process.');
            return false;
        }

        // Validate that all selected non-account entries have quantities > 0
        let valid = true;
        entryCheckboxes.filter(':checked').each(function () {
            const $row = $(this).closest('tr');
            const isAccount = $row.data('isAccount') === true;

            // Skip validation for account entries - allow them to have 0 quantity
            if (!isAccount) {
                const $quantityInput = $row.find('.process-qty');
                if (parseInt($quantityInput.val()) <= 0) {
                    valid = false;
                    $quantityInput.addClass('is-invalid');
                } else {
                    $quantityInput.removeClass('is-invalid');
                }
            }
        });

        if (!valid) {
            e.preventDefault();
            alert('All selected regular entries must have a process quantity greater than zero.');
            return false;
        }

        // Disable submit button to prevent double submission
        submitBtn.prop('disabled', true);
        submitBtn.html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');
    });

    // Initialize datepicker for Creditable Date field
    $('#CreditableDate').datepicker({
        dateFormat: 'yy-mm-dd',
        changeMonth: true,
        changeYear: true,
        minDate: 0
    });

    // Initial update
    updateSelectedCount();
});