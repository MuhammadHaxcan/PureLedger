let currentVoucherId = null;

function calculateTotals() {
    let totalDebit = 0;
    let totalCredit = 0;

    $('#voucherSummaryTable tbody tr').each(function () {
        totalDebit += parseInt($(this).find('td').eq(6).text()) || 0;
        totalCredit += parseInt($(this).find('td').eq(7).text()) || 0;
    });

    $('#totalDebit').text(totalDebit);
    $('#totalCredit').text(totalCredit);
}

// Fetch voucher summary
$('#fetchVoucherBtn').on('click', function () {
    var voucherId = $('#voucherIdInput').val();
    if (!voucherId) {
        alert('Please enter a voucher ID.');
        return;
    }
    $.get('/Home/GetVoucherSummary', { voucherId: voucherId }, function (data) {
        var tbody = $('#voucherSummaryTable tbody');
        tbody.empty();
        if (data && data.entries && data.entries.length > 0) {
            currentVoucherId = data.jvId;
            data.entries.forEach(function (entry) {
                var row = `<tr data-entry-id="${entry.entryId}">
                    <td>${entry.accountName ?? ''}</td>
                    <td>${entry.description ?? ''}</td>
                    <td>${entry.quantity ?? 0}</td>
                    <td>${entry.rate ?? 0}</td>
                    <td>${entry.discount ?? 0}</td>
                    <td>${entry.discountPercent ?? 0}</td>
                    <td>${parseInt(entry.debit) || 0}</td>
                    <td>${parseInt(entry.credit) || 0}</td>
                    <td>
                        <button type="button" class="btn btn-primary btn-sm btn-edit-entry" data-entry-id="${entry.entryId}">Edit</button>
                    </td>
                </tr>`;
                tbody.append(row);
            });
            calculateTotals();
            $('#voucherSummaryContainer').show();
            $('#noVoucherMsg').hide();
        } else {
            $('#voucherSummaryContainer').hide();
            $('#noVoucherMsg').show();
        }
    });
});

// Open modal and load entry data
$(document).on('click', '.btn-edit-entry', function () {
    var entryId = $(this).data('entry-id');
    $.get('/Home/GetJournalEntry', { entryId: entryId }, function (data) {
        $('#editEntryId').val(data.entryId);
        $('#editAccountName').val(data.accountName);
        $('#editDescription').val(data.description);
        $('#editQuantity').val(data.quantity);
        $('#editRate').val(data.rate);
        $('#editDiscount').val(data.discount);
        $('#editDiscountPercent').val(data.discountPercent);
        $('#editDebit').val(data.debit);
        $('#editCredit').val(data.credit);
        var modal = new bootstrap.Modal(document.getElementById('editEntryModal'));
        modal.show();
    });
});

// Save changes
$('#editEntryForm').on('submit', function (e) {
    e.preventDefault();
    var formData = $(this).serialize();
    $.post('/Home/UpdateJournalEntry', formData, function (result) {
        if (result.success) {
            $('#fetchVoucherBtn').click();
            bootstrap.Modal.getInstance(document.getElementById('editEntryModal')).hide();
        } else {
            alert('Error saving entry');
        }
    });
});