$(function () {
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
});