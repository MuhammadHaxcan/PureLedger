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
                $('#editDescription').val(data.description || '');
                $('#editQuantity').val(data.quantity || 0);
                $('#editRate').val(data.rate || 0);
                $('#editDiscount').val(data.discount || 0);
                $('#editDiscountPercent').val(data.discountPercent || 0);
                $('#editDebit').val(data.debit || 0);
                $('#editCredit').val(data.credit || 0);

                loadDropdowns(data.categoryId, data.accountId);
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
        debugger
        e.preventDefault();

        // Get form data
        const formData = $(this).serialize();

        // Submit data
        $.post('/Home/UpdateJournalEntry', formData)
            .done(function (result) {
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
            })
            .fail(function () {
                alert('An error occurred while saving changes.');
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
    function loadDropdowns(selectedCategoryId, selectedAccountId) {
        $.get('/api/common/categories', function (categories) {
            const $category = $('#editCategory');
            $category.empty().append('<option value="">Select Category</option>');
            categories.forEach(c => {
                const selected = c.id === selectedCategoryId ? 'selected' : '';
                $category.append(`<option value="${c.id}" ${selected}>${c.name}</option>`);
            });
        });

        $.get('/api/common/accounts', function (accounts) {
            const $account = $('#editAccount');
            $account.empty().append('<option value="">Select Account</option>');
            accounts.forEach(a => {
                const selected = a.id === selectedAccountId ? 'selected' : '';
                $account.append(`<option value="${a.id}" ${selected}>${a.name}</option>`);
            });
        });
    }



});