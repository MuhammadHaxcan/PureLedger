$(function () {
    const missedEntriesModal = new bootstrap.Modal(document.getElementById('missedEntriesModal'));
    
    // Initialize DataTable for the base vouchers list
    $('#baseVouchersTable').DataTable({
        "order": [[0, "desc"]], // Sort by Voucher ID descending
        "language": {
            "search": "Search vouchers:",
            "emptyTable": "No base vouchers available"
        },
        "columnDefs": [
            { "orderable": false, "targets": 6 } // Disable sorting on actions column
        ],
        "pageLength": 25
    });

    // Handle Mark Missed button click
    $(document).on('click', '.mark-missed-btn', async function(e) {
        e.preventDefault();
        const $btn = $(this);
        const voucherId = $btn.data('voucher-id');

        if (confirm('Are you sure you want to mark these entries as missed? This cannot be undone.')) {
            $btn.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-1"></span>Processing...');

            $.post('/Home/MarkVoucherAsMissed', { voucherId: voucherId })
                .done(function(response) {
                    if (response.success) {
                        // Refresh the page to show updated status
                        location.reload();
                    } else {
                        alert('Error marking entries as missed: ' + (response.message || 'Unknown error'));
                        $btn.prop('disabled', false)
                            .html('<i class="fas fa-times-circle me-1"></i> Mark Missed');
                    }
                })
                .fail(function() {
                    alert('Error communicating with server');
                    $btn.prop('disabled', false)
                        .html('<i class="fas fa-times-circle me-1"></i> Mark Missed');
                });
        }
    });
});