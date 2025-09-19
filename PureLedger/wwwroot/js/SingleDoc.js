$(function () {
    // Initialize any stored recent documents
    initializeRecentDocs();

    // Show/hide journal voucher filter based on voucher type selection
    $('#voucherType').on('change', function () {
        const selectedType = $(this).val();

        // Show JV filter only when Journal Voucher type is selected
        if (selectedType === 'JV') {
            $('#jvFilterContainer').fadeIn(300);
        } else {
            $('#jvFilterContainer').fadeOut(300);
        }
    });

    // Handle Enter key press in inputs
    $('#voucherId').keypress(function (e) {
        if (e.which === 13) {
            $('#btnGo').click();
        }
    });

    // Handle Go button click
    $('#btnGo').on('click', function () {
        const id = $('#voucherId').val().trim();
        const type = $('#voucherType').val();
        const filter = type === 'JV' ? $('#jvFilter').val() : 'all'; // Get the filter value only for JV type

        if (!id || !Number.isInteger(Number(id)) || Number(id) < 1) {
            showAlert('warning', 'Please enter a valid Voucher ID.');
            return;
        }

        if (!type) {
            showAlert('warning', 'Please select a Voucher Type.');
            return;
        }

        // Show loading state
        const $btn = $(this);
        $btn.html('<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Validating...');
        $btn.prop('disabled', true);

        // Use PureLedger utility for AJAX
        PureLedger.get('/Reporting/ValidateVoucher', {
            journalVoucherId: id,
            type: type
        })
            .done(function (result) {
                if (result.exists) {
                    // If valid, open print page in new tab
                    // Use the base path from PureLedger utility to construct the URL
                    const basePath = PureLedger.getCurrentBasePath();

                    // Include the filter parameter for journal vouchers
                    let printUrl = `${basePath}/Reporting/PrintVoucher?journalVoucherId=${id}&type=${type}`;

                    // Add filter parameter only for Journal Voucher type
                    if (type === 'JV') {
                        printUrl += `&filter=${filter}`;
                    }

                    window.open(printUrl, '_blank');

                    // Add to recent docs history
                    addToRecentDocs(id, type, filter);

                    // Reset the form
                    $('#voucherId').val('');

                    // Show success message
                    showAlert('success', 'Document found and opened for printing.');
                } else {
                    // Show error message on the same page
                    const errorMessage = type === "DC"
                        ? "No Delivery Challan exists for this Voucher ID."
                        : (type === "INV"
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

    // Recent documents functionality
    function initializeRecentDocs() {
        // Get recent docs from localStorage
        const recentDocs = getRecentDocs();

        if (recentDocs.length > 0) {
            // Show the recent docs section
            $('#recentDocuments').removeClass('d-none');

            // Populate the table
            updateRecentDocsTable(recentDocs);
        }
    }

    function getRecentDocs() {
        try {
            const docs = localStorage.getItem('recentPrintDocs');
            return docs ? JSON.parse(docs) : [];
        } catch (e) {
            console.error('Error loading recent docs:', e);
            return [];
        }
    }

    function addToRecentDocs(id, type, filter) {
        try {
            const docs = getRecentDocs();

            // Check if already exists
            const existingIndex = docs.findIndex(d => d.id === id && d.type === type);
            if (existingIndex !== -1) {
                // Remove the existing entry (will be re-added at the top)
                docs.splice(existingIndex, 1);
            }

            // Add new entry at the beginning
            docs.unshift({
                id: id,
                type: type,
                filter: type === 'JV' ? filter : null, // Store filter only for JV
                date: new Date().toISOString()
            });

            // Keep only the most recent 10
            const recentDocs = docs.slice(0, 10);

            // Save back to localStorage
            localStorage.setItem('recentPrintDocs', JSON.stringify(recentDocs));

            // Show the recent docs section if it was hidden
            $('#recentDocuments').removeClass('d-none');

            // Update the table
            updateRecentDocsTable(recentDocs);
        } catch (e) {
            console.error('Error adding to recent docs:', e);
        }
    }

    function updateRecentDocsTable(docs) {
        const tbody = $('#recentDocsData');
        tbody.empty();

        docs.forEach(doc => {
            const date = new Date(doc.date);
            const formattedDate = date.toLocaleDateString() + ' ' + date.toLocaleTimeString();

            const typeLabel = doc.type === 'JV' ? 'Journal Voucher' :
                doc.type === 'DC' ? 'Delivery Challan' :
                    'Invoice';

            // Include filter info for journal vouchers
            let filterInfo = '';
            if (doc.type === 'JV' && doc.filter) {
                const filterLabel = doc.filter === 'debit' ? 'Debit Only' :
                    doc.filter === 'credit' ? 'Credit Only' : 'All Entries';
                filterInfo = ` (${filterLabel})`;
            }

            const row = $(`
                <tr>
                    <td>${doc.id}</td>
                    <td>${typeLabel}${filterInfo}</td>
                    <td>${formattedDate}</td>
                    <td class="text-center">
                        <button class="btn btn-sm btn-primary print-again-btn" 
                                data-id="${doc.id}" 
                                data-type="${doc.type}"
                                data-filter="${doc.filter || 'all'}">
                            <i class="fas fa-print me-1"></i> Print
                        </button>
                    </td>
                </tr>
            `);

            tbody.append(row);
        });

        // Attach event handlers
        $('.print-again-btn').on('click', function () {
            const id = $(this).data('id');
            const type = $(this).data('type');
            const filter = $(this).data('filter') || 'all';

            // Use the base path from PureLedger utility to construct the URL
            const basePath = PureLedger.getCurrentBasePath();

            // Include the filter parameter for journal vouchers
            let printUrl = `${basePath}/Reporting/PrintVoucher?journalVoucherId=${id}&type=${type}`;

            // Add filter parameter only for Journal Voucher type
            if (type === 'JV') {
                printUrl += `&filter=${filter}`;
            }

            window.open(printUrl, '_blank');
        });
    }
});