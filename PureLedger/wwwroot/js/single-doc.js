$(function () {
    // Show/hide journal voucher filter based on voucher type selection
    $('#voucherType').on('change', function () {
        const selectedType = $(this).val();

        // Show JV filter only when Journal Voucher type is selected
        if (selectedType === VoucherType.JV) {
            $('#jvFilterContainer').fadeIn(300);
        } else {
            $('#jvFilterContainer').fadeOut(300);
        }
    });

    // Handle Enter key press in inputs
    $('#voucherId').on('keypress', function (e) {
        if (e.which === 13) {
            $('#btnGo').trigger('click');
        }
    });

    // Handle Go button click
    $('#btnGo').on('click', function () {
        const id = $('#voucherId').val().trim();
        const type = $('#voucherType').val();
        const filter = type === VoucherType.JV ? $('#jvFilter').val() : 'all';

        if (!id || !Number.isInteger(Number(id)) || Number(id) < 1) {
            showAlert('warning', 'Please enter a valid Voucher ID.');
            return;
        }

        if (!type || !Object.values(VoucherType).includes(type)) {
            showAlert('warning', 'Please select a valid Voucher Type.');
            return;
        }

        // Show loading state
        const $btn = $(this);
        $btn.html('<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Validating...');
        $btn.prop('disabled', true);

        // Use routes object instead of hardcoded URL
        PureLedger.get(routes.validateVoucher, {
            journalVoucherId: id,
            type: type
        })
            .done(function (result) {
                if (result.exists) {
                    // If valid, open print page in new tab using routes object
                    // Construct the full URL with query parameters
                    let printUrl = `${routes.printVoucher}?journalVoucherId=${id}&type=${type}`;

                    // Add filter parameter only for Journal Voucher type
                    if (type === VoucherType.JV) {
                        printUrl += `&filter=${filter}`;
                    }

                    window.open(printUrl, '_blank');

                    // Reset the form
                    $('#voucherId').val('');

                    // Show success message
                    showAlert('success', 'Document found and opened for printing.');
                } else {
                    // Show error message on the same page
                    const errorMessage = type === VoucherType.DC
                        ? "No Delivery Challan exists for this Voucher ID."
                        : (type === VoucherType.INV
                            ? "No Invoice exists for this Voucher ID."
                            : "No Journal Voucher found for this ID.");

                    showAlert('danger', errorMessage);
                }
            })
            .fail(function (xhr, status, error) {
                console.error('Error validating voucher:', error);
                showAlert('danger', 'An error occurred while validating the voucher.');
            })
            .always(function () {
                // Restore button state
                $btn.html('<i class="fas fa-search me-1"></i> Find Document');
                $btn.prop('disabled', false);
            });
    });

    // Function to show alerts
    function showAlert(type, message) {
        const icon = type === 'success' ? 'check-circle' :
            type === 'warning' ? 'exclamation-triangle' :
                'exclamation-circle';

        const alert = $(`
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                <i class="fas fa-${icon} me-2"></i>${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `);

        $('#alertArea').append(alert);

        // Auto dismiss after 5 seconds
        setTimeout(function () {
            alert.alert('close');
        }, 5000);
    }
});