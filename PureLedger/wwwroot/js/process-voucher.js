$(function () {
    const form = $('#processingForm');
    const submitBtn = $('#submitBtn');
    const selectAllBtn = $('#selectAllBtn');
    const deselectAllBtn = $('#deselectAllBtn');
    const maxQuantityBtn = $('#maxQuantityBtn');
    const entryCheckboxes = $('.entry-select');
    const processQtyInputs = $('.process-qty');

    // Initialize DataTable for entries
    $('#entriesTable').DataTable({
        "order": [[1, "asc"]], // Sort by Entry ID ascending
        "columnDefs": [
            { "orderable": false, "targets": [0, 5] } // Disable sorting on select and process qty columns
        ],
        "pageLength": 50
    });

    // Update selected count
    function updateSelectedCount() {
        const count = entryCheckboxes.filter(':checked').length;
        $('#selectedCount').text(count);

        // Enable/disable submit button
        if (count > 0) {
            submitBtn.prop('disabled', false);
        } else {
            submitBtn.prop('disabled', true);
        }
    }

    // Initialize the form state
    updateSelectedCount();
    submitBtn.prop('disabled', true);

    // Handle checkbox selection
    entryCheckboxes.on('change', function () {
        const $checkbox = $(this);
        const $row = $checkbox.closest('tr');
        const $quantityInput = $row.find('.process-qty');

        if ($checkbox.is(':checked')) {
            $row.addClass('table-active');
            // If quantity is 0, set it to max
            if (parseInt($quantityInput.val()) === 0) {
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
        const value = parseInt($input.val());
        const max = parseInt($input.attr('max'));

        // Ensure value is within bounds
        if (value < 0) {
            $input.val(0);
        } else if (value > max) {
            $input.val(max);
        }

        // If quantity is > 0, check the checkbox
        if (value > 0) {
            $checkbox.prop('checked', true);
            $row.addClass('table-active');
        } else {
            $checkbox.prop('checked', false);
            $row.removeClass('table-active');
        }

        updateSelectedCount();
    });

    // Select All button
    selectAllBtn.on('click', function () {
        entryCheckboxes.not(':disabled').prop('checked', true).trigger('change');
    });

    // Deselect All button
    deselectAllBtn.on('click', function () {
        entryCheckboxes.prop('checked', false).trigger('change');
    });

    // Max Quantities button
    maxQuantityBtn.on('click', function () {
        entryCheckboxes.not(':disabled').each(function () {
            const $checkbox = $(this);
            if ($checkbox.is(':checked')) {
                const $row = $checkbox.closest('tr');
                const $quantityInput = $row.find('.process-qty');
                $quantityInput.val($quantityInput.attr('max'));
            }
        });
    });

    // Form submission
    form.on('submit', function (e) {
        // Validate that at least one entry is selected
        if (entryCheckboxes.filter(':checked').length === 0) {
            e.preventDefault();
            alert('Please select at least one entry to process.');
            return false;
        }

        // Validate that all selected entries have quantities > 0
        let valid = true;
        entryCheckboxes.filter(':checked').each(function () {
            const $row = $(this).closest('tr');
            const $quantityInput = $row.find('.process-qty');
            if (parseInt($quantityInput.val()) <= 0) {
                valid = false;
                $quantityInput.addClass('is-invalid');
            } else {
                $quantityInput.removeClass('is-invalid');
            }
        });

        if (!valid) {
            e.preventDefault();
            alert('All selected entries must have a process quantity greater than zero.');
            return false;
        }

        // Disable submit button to prevent double submission
        submitBtn.prop('disabled', true);
        submitBtn.html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');
    });
});