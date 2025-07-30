$(document).ready(function () {
    let currentVoucherId = null;
    let editModal = null;

    // Initialize Bootstrap modal
    document.addEventListener('DOMContentLoaded', function () {
        editModal = new bootstrap.Modal(document.getElementById('editEntryModal'));
    });

    // Fetch voucher by ID
    $('#fetchVoucherBtn').on('click', function () {
        const voucherId = $('#voucherIdInput').val();
        if (!voucherId) {
            alert('Please enter a voucher ID.');
            return;
        }

        fetchVoucherSummary(voucherId);
    });

    // Function to fetch voucher summary
    function fetchVoucherSummary(voucherId) {
        $.get('/Home/GetVoucherSummary', { voucherId: voucherId })
            .done(function (data) {
                if (data && data.entries && data.entries.length > 0) {
                    currentVoucherId = data.jvId;
                    displayVoucherEntries(data.entries);
                } else {
                    $('#voucherSummaryContainer').hide();
                    $('#noVoucherMsg').show();
                }
            })
            .fail(function () {
                $('#voucherSummaryContainer').hide();
                $('#noVoucherMsg').show();
            });
    }

    // Display voucher entries in the table
    function displayVoucherEntries(entries) {
        const tbody = $('#voucherSummaryTable tbody');
        tbody.empty();

        let totalDebit = 0;
        let totalCredit = 0;

        entries.forEach(function (entry) {
            totalDebit += parseFloat(entry.debit || 0);
            totalCredit += parseFloat(entry.credit || 0);

            const row = $('<tr>').append(
                $('<td>').text(entry.accountName || ''),
                $('<td>').text(entry.description || ''),
                $('<td>').text(entry.quantity || '0'),
                $('<td>').text(formatCurrency(entry.rate || 0)),
                $('<td>').text(formatCurrency(entry.discount || 0)),
                $('<td>').text(formatPercent(entry.discountPercent || 0)),
                $('<td>').text(formatCurrency(entry.debit || 0)),
                $('<td>').text(formatCurrency(entry.credit || 0)),
                $('<td>').append(
                    $('<button>')
                        .addClass('btn btn-primary btn-sm btn-edit-entry')
                        .text('Edit')
                        .data('entry-id', entry.entryId),
                    $('<button>')
                        .addClass('btn btn-danger btn-sm btn-delete-entry')
                        .text('Delete')
                        .data('entry-id', entry.entryId)
                )
            );
            tbody.append(row);
        });

        // Update totals
        $('#totalDebit').text(formatCurrency(totalDebit));
        $('#totalCredit').text(formatCurrency(totalCredit));

        $('#voucherSummaryContainer').show();
        $('#noVoucherMsg').hide();
    }

    // Handle edit button click
    $(document).on('click', '.btn-edit-entry', function () {
        const entryId = $(this).data('entry-id');

        // Fetch entry details
        $.get('/Home/GetJournalEntry', { entryId: entryId })
            .done(function (data) {
                // Populate modal
                $('#editEntryId').val(data.entryId);
                $('#editProjectId').val(data.projectId);
                $('#editDescription').val(data.description || '');
                $('#editQuantity').val(data.quantity || 0);
                $('#editRate').val(data.rate || 0);
                $('#editDiscount').val(data.discount || 0);
                $('#editDiscountPercent').val(data.discountPercent || 0);
                $('#editDebit').val(data.debit || 0);
                $('#editCredit').val(data.credit || 0);
                $('#editAccountGroupId').val(data.accountGroupId || 0); // <-- Add this line

                loadDropdowns(data.projectId, data.categoryId, data.accountId);

                // Show DeliveryChallanEntry/location fields if AccountGroupId == 2
                if (data.accountGroupId === 2) {
                    // Show location dropdown and set value
                    $('#editLocationGroup').show();
                    const $loc = $('#editLocation');
                    $loc.empty();
                    if (data.locations && data.locations.length) {
                        data.locations.forEach(function (loc) {
                            $loc.append(
                                $('<option>')
                                    .val(loc.locationId)
                                    .text(loc.locationName)
                                    .prop('selected', loc.locationId === data.locationId)
                            );
                        });
                    }
                    $('#editDeliveryChallanEntryId').val(data.deliveryChallanEntryId || '');
                } else {
                    $('#editLocationGroup').hide();
                    $('#editDeliveryChallanEntryId').val('');
                }

                // Show modal
                if (editModal) {
                    editModal.show();
                } else {
                    new bootstrap.Modal(document.getElementById('editEntryModal')).show();
                }
            })
            .fail(function () {
                alert('Error loading entry details.');
            });
    });

    // Handle form submission
    $('#editEntryForm').on('submit', function (e) {
        e.preventDefault();
        
        // Get form data
        const formData = new FormData(this);
        
        // Submit data using AJAX with FormData
        $.ajax({
            url: '/Home/UpdateJournalEntry',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function(result) {
                if (result.success) {
                    // Hide modal
                    bootstrap.Modal.getInstance(document.getElementById('editEntryModal')).hide();

                    // Refresh voucher summary
                    fetchVoucherSummary(currentVoucherId);
                } else {
                    // Display errors
                    if (result.errors && result.errors.length) {
                        alert('Error: ' + result.errors.join('\n'));
                    } else if (result.message) {
                        alert('Error: ' + result.message);
                    } else {
                        alert('An error occurred while saving changes.');
                    }
                }
            },
            error: function() {
                alert('An error occurred while saving changes.');
            }
        });
    });

    // Format currency
    function formatCurrency(value) {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value);
    }

    // Format percentage
    function formatPercent(value) {
        return value + '%';
    }

    //Load Drop Downs for the combo boxes
    function loadDropdowns(projectId, selectedCategoryId, selectedAccountId) {
        // Load Categories by ProjectId
        $.get(`/api/common/categories/${projectId}`, function (categories) {
            const $category = $('#editCategory');
            $category.empty().append('<option value="">Select Category</option>');
            categories.forEach(c => {
                const selected = c.id == selectedCategoryId ? 'selected' : '';
                $category.append(`<option value="${c.id}" ${selected}>${c.name}</option>`);
            });

            // Now load accounts based on selected category
            if (selectedCategoryId) {
                $.get(`/api/common/accounts/${selectedCategoryId}`, function (accounts) {
                    const $account = $('#editAccount');
                    $account.empty().append('<option value="">Select Account</option>');
                    accounts.forEach(a => {
                        const selected = a.id == selectedAccountId ? 'selected' : '';
                        $account.append(`<option value="${a.id}" ${selected}>${a.name}</option>`);
                    });
                });
            }
        });
    }

    $('#editCategory').on('change', function () {
        const categoryId = $(this).val();
        const $account = $('#editAccount');
        $account.empty().append('<option value="">Select Account</option>');

        if (categoryId) {
            $.get(`/api/common/accounts/${categoryId}`, function (accounts) {
                accounts.forEach(a => {
                    $account.append(`<option value="${a.id}">${a.name}</option>`);
                });
            });
        }
    });

});

$(document).on('click', '.btn-delete-entry', function () {
    const entryId = $(this).data('entry-id');

    Swal.fire({
        title: 'Are you sure?',
        text: "Do you really want to delete this entry?",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Yes, delete it!',
        cancelButtonText: 'Cancel'
    }).then((result) => {
        if (result.isConfirmed) {
            $.post('/Home/DeleteJournalEntry', { entryId })
                .done(function (response) {
                    if (response.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Deleted!',
                            text: 'The entry has been deleted.',
                            timer: 1500,
                            showConfirmButton: false
                        });
                        fetchVoucherSummary(currentVoucherId);
                    } else {
                        Swal.fire('Error', response.message || 'Could not delete the entry.', 'error');
                    }
                })
                .fail(function () {
                    Swal.fire('Error', 'An error occurred while deleting the entry.', 'error');
                });
        }
    });
});
