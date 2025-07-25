$(function () {
    // Load accounts when category is selected
    $('#categoryId').change(function() {
        const categoryId = $(this).val();
        const projectId = $('#projectId').val();
        
        if (categoryId) {
            // Enable account dropdown
            $('#accountId').prop('disabled', false);
            
            // Load accounts for this category
            $.get('/api/common/accounts/' + categoryId, function(accounts) {
                let options = '<option value="">All Accounts</option>';
                accounts.forEach(account => {
                    options += `<option value="${account.id}">${account.name}</option>`;
                });
                $('#accountId').html(options);
            });
        } else {
            $('#accountId').prop('disabled', true);
            $('#accountId').html('<option value="">All Accounts</option>');
        }
    });

    function loadReport() {
        const projectId = $('#projectId').val();
        const categoryId = $('#categoryId').val();
        const accountId = $('#accountId').val();
        
        // Must have either project or category
        if (!projectId && !categoryId) {
            $('#reportData').html('<tr><td colspan="6" class="text-center">Please select at least Project or Category</td></tr>');
            return;
        }
        
        $('#reportData').html('<tr><td colspan="6" class="text-center">Loading...</td></tr>');
        
        $.get('/Reporting/GetAccountReportData', { projectId, categoryId, accountId }, function (data) {
            if (data.length === 0) {
                $('#reportData').html('<tr><td colspan="6" class="text-center">No data found</td></tr>');
                return;
            }
            
            let rows = '';
            data.forEach(r => {
                rows += `<tr>
                    <td>${r.accountId}</td>
                    <td>${r.categoryName}</td>
                    <td>${r.accountName}</td>
                    <td class="text-end">${parseFloat(r.totalDebit).toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
                    <td class="text-end">${parseFloat(r.totalCredit).toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
                    <td class="text-end">${parseFloat(r.netBalance).toLocaleString('en-US', {minimumFractionDigits: 2, maximumFractionDigits: 2})}</td>
                </tr>`;
            });
            $('#reportData').html(rows);
        });
    }
    
    $('#goBtn').click(function() {
        loadReport();
    });
    
    $('#allProjectBtn').click(function () {
        $('#projectId').val('');
        // Require category when using "All Project"
        if ($('#categoryId').val()) {
            loadReport();
        } else {
            alert("Please select a Category first");
        }
    });
    
    $('#searchBox').on('input', function () {
        const val = $(this).val().toLowerCase();
        $('#reportData tr').each(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(val) > -1);
        });
    });
});