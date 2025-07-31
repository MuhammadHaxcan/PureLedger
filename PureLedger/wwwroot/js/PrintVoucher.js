$(function() {
    // Auto-print when the document is loaded (optional)
    // Uncomment this if you want auto-printing
    /*
    $(window).on('load', function() {
        // Give the browser a moment to render everything
        setTimeout(function() {
            window.print();
        }, 500);
    });
    */

    // When printing is complete
    window.addEventListener('afterprint', function() {
        console.log('Printing completed');
    });
    
    // Document title based on voucher type and ID
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
});
