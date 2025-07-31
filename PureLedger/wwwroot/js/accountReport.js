$(function () {
    // Initialize DataTable
    let dataTable = null;

    // Load accounts when category is selected
    $('#categoryId').change(function () {
        const categoryId = $(this).val();
        const projectId = $('#projectId').val();

        if (categoryId) {
            // Enable account dropdown
            $('#accountId').prop('disabled', false);

            // Load accounts for this category
            $.get('/api/common/accounts/' + categoryId, function (accounts) {
                let options = '<option value="">All Accounts</option>';
                accounts.forEach(account => {
                    options += `<option value="${account.id}">${account.name}</option>`;
                });
                $('#accountId').html(options);
            }).fail(function (error) {
                showAlert('danger', 'Failed to load accounts: ' + (error.responseText || 'Unknown error'));
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
            showAlert('warning', 'Please select at least Project or Category');
            $('#reportData').html('<tr><td colspan="6" class="text-center">Please select at least Project or Category</td></tr>');
            updateReportSummary(0);
            return;
        }

        $('#reportData').html('<tr><td colspan="6" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>');

        $.get('/Reporting/GetAccountReportData', { projectId, categoryId, accountId }, function (data) {
            // Destroy existing DataTable if it exists
            if (dataTable) {
                dataTable.destroy();
                dataTable = null;
            }

            if (!data || data.length === 0) {
                $('#reportData').html('<tr><td colspan="6" class="text-center">No data found</td></tr>');
                updateReportSummary(0);
                return;
            }

            let rows = '';
            let totalDebit = 0;
            let totalCredit = 0;
            let totalBalance = 0;

            data.forEach(r => {
                const debit = parseFloat(r.totalDebit) || 0;
                const credit = parseFloat(r.totalCredit) || 0;
                const balance = parseFloat(r.netBalance) || 0;

                totalDebit += debit;
                totalCredit += credit;
                totalBalance += balance;

                // Determine if balance should be highlighted as negative
                const balanceClass = balance < 0 ? 'negative-balance' : '';

                rows += `<tr>
                    <td>${r.accountId}</td>
                    <td>${r.categoryName || '-'}</td>
                    <td>${r.accountName}</td>
                    <td class="text-end">${formatCurrency(debit)}</td>
                    <td class="text-end">${formatCurrency(credit)}</td>
                    <td class="text-end ${balanceClass}">${formatCurrency(balance)}</td>
                </tr>`;
            });

            $('#reportData').html(rows);
            updateReportSummary(data.length);

            // Initialize DataTable with improved configuration
            dataTable = $('#accountReportTable').DataTable({
                "order": [[2, "asc"]],
                "language": {
                    "emptyTable": "No accounts available",
                    "search": "Search accounts:",
                    "info": "Showing _START_ to _END_ of _TOTAL_ accounts",
                    "lengthMenu": "Show _MENU_ accounts"
                },
                "pageLength": 25,
                "lengthMenu": [[10, 25, 50, 100, -1], [10, 25, 50, 100, "All"]],
                "dom": 'lBfrtip',
                "buttons": [
                    {
                        extend: 'excel',
                        text: '<i class="fas fa-file-excel"></i> Excel',
                        className: 'btn btn-sm btn-success',
                        title: 'Account Report ' + new Date().toLocaleDateString(),
                        footer: true,
                        exportOptions: {
                            columns: [0, 1, 2, 3, 4, 5]
                        }
                    },
                    {
                        extend: 'pdf',
                        text: '<i class="fas fa-file-pdf"></i> PDF',
                        className: 'btn btn-sm btn-danger',
                        title: 'Account Report ' + new Date().toLocaleDateString(),
                        footer: true,
                        exportOptions: {
                            columns: [0, 1, 2, 3, 4, 5]
                        }
                    },
                    {
                        extend: 'print',
                        text: '<i class="fas fa-print"></i> Print',
                        className: 'btn btn-sm btn-info',
                        title: 'Account Report ' + new Date().toLocaleDateString(),
                        footer: true,
                        exportOptions: {
                            columns: [0, 1, 2, 3, 4, 5]
                        }
                    }
                ],
                "footerCallback": function (row, data, start, end, display) {
                    const api = this.api();

                    // Calculate column sums for debit, credit, and balance
                    const debitTotal = api.column(3, { page: 'current' }).data()
                        .reduce((acc, val) => acc + parseFloat($(val).text().replace(/[^\d.-]/g, '')), 0);

                    const creditTotal = api.column(4, { page: 'current' }).data()
                        .reduce((acc, val) => acc + parseFloat($(val).text().replace(/[^\d.-]/g, '')), 0);

                    const balanceTotal = api.column(5, { page: 'current' }).data()
                        .reduce((acc, val) => acc + parseFloat($(val).text().replace(/[^\d.-]/g, '')), 0);

                    // Add footer row with totals
                    $(api.column(3).footer()).html(formatCurrency(debitTotal));
                    $(api.column(4).footer()).html(formatCurrency(creditTotal));
                    $(api.column(5).footer()).html(formatCurrency(balanceTotal));
                }
            });

            // Update the summary
            updateReportSummary(data.length, totalDebit, totalCredit, totalBalance);
        })
            .fail(function (error) {
                $('#reportData').html('<tr><td colspan="6" class="text-center text-danger">Error loading data</td></tr>');
                showAlert('danger', 'Failed to load report data: ' + (error.responseText || 'Unknown error'));
                updateReportSummary(0);
            });
    }

    function formatCurrency(value) {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value);
    }

    function updateReportSummary(count, totalDebit = 0, totalCredit = 0, totalBalance = 0) {
        if (count > 0) {
            const balanceClass = totalBalance < 0 ? 'text-danger' : 'text-success';
            $('#reportSummary').html(`
                <span class="badge bg-primary">${count} accounts</span>
                <span class="badge bg-secondary ms-2">Total Balance: <span class="${balanceClass}">${formatCurrency(totalBalance)}</span></span>
            `);
        } else {
            $('#reportSummary').html(`<span class="badge bg-secondary">No data</span>`);
        }
    }

    $('#goBtn').click(function () {
        loadReport();
    });

    $('#allProjectBtn').click(function () {
        $('#projectId').val('');
        // Require category when using "All Project"
        if ($('#categoryId').val()) {
            loadReport();
        } else {
            showAlert("warning", "Please select a Category first");
        }
    });

    $('#searchBox').on('input', function () {
        if (dataTable) {
            dataTable.search($(this).val()).draw();
        } else {
            // Fallback to manual filtering if DataTable isn't initialized
            const val = $(this).val().toLowerCase();
            $('#reportData tr').each(function () {
                $(this).toggle($(this).text().toLowerCase().indexOf(val) > -1);
            });
        }
    });

    // Show alert function (similar to Tags.cshtml)
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

    // Initialize any UI elements
    // Trigger Enter key on inputs to search
    $('#projectId, #categoryId, #accountId, #searchBox').on('keypress', function (e) {
        if (e.which === 13) {
            $('#goBtn').click();
        }
    });

    // Add a footer row to the table if it doesn't exist
    if ($('#accountReportTable tfoot').length === 0) {
        $('#accountReportTable').append(`
            <tfoot>
                <tr class="table-secondary fw-bold">
                    <td colspan="3" class="text-end">TOTAL</td>
                    <td class="text-end"></td>
                    <td class="text-end"></td>
                    <td class="text-end"></td>
                </tr>
            </tfoot>
        `);
    }
});