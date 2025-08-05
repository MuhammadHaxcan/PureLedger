$(function () {
    let dataTable = null;

    // Set default reference date to today if not set
    const refDateInput = document.getElementById('referenceDate');
    if (refDateInput && !refDateInput.value) {
        refDateInput.valueAsDate = new Date();
    }

    initializeForm();

    async function initializeForm() {
        try {
            const form = document.getElementById('agingReportForm');
            if (form) {
                form.addEventListener('submit', async function (e) {
                    e.preventDefault();
                    await generateReport();
                });
            }

            // Press Enter to submit
            $('#projectId, #categoryId').on('keypress', function (e) {
                if (e.which === 13) {
                    $('#agingReportForm').submit();
                }
            });
        } catch (err) {
            console.error('Error initializing form:', err);
            showAlert('danger', 'Error initializing form: ' + err.message);
        }
    }

    async function generateReport() {
        const referenceDate = $('#referenceDate').val();
        const projectId = $('#projectId').val();
        const categoryId = $('#categoryId').val();

        if (!referenceDate || !categoryId) {
            showAlert('warning', 'Please select a reference date and category.');
            return;
        }

        // Clear previous results and show loading
        $('#reportData').html('<tr><td colspan="4" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>');
        $('#reportResults').show();

        const params = {
            referenceDate: referenceDate,
            projectId: projectId || '',
            categoryId: categoryId
        };

        PureLedger.get('/Reporting/GetAgingReportData', params)
            .done(function(data) {
                displayReport(data);
                showAlert('success', 'Report generated successfully');
            })
            .fail(function(xhr, status, error) {
                console.error('Error generating report:', error);
                let errorMessage = 'Error generating report';
                try {
                    errorMessage += ': ' + xhr.responseText;
                } catch (e) {
                    errorMessage += ': ' + error;
                }
                $('#reportData').html(`<tr><td colspan="4" class="text-center text-danger">${errorMessage}</td></tr>`);
                showAlert('danger', 'Failed to generate report: ' + error);
            });
    }

    function displayReport(data) {
        // Destroy existing DataTable if it exists
        if (dataTable) {
            dataTable.destroy();
            dataTable = null;
        }

        const tbody = document.getElementById('reportData');
        tbody.innerHTML = '';

        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" class="text-center">No data found</td></tr>';
            updateTotals(0, 0, 0);
            return;
        }

        let totalDebit = 0;
        let totalCredit = 0;
        let totalNet = 0;

        data.forEach(item => {
            const debit = parseFloat(item.totalDebit) || 0;
            const credit = parseFloat(item.totalCredit) || 0;
            const net = parseFloat(item.netBalance) || 0;

            totalDebit += debit;
            totalCredit += credit;
            totalNet += net;

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${item.accountName}</td>
                <td class="text-end">${formatCurrency(debit)}</td>
                <td class="text-end">${formatCurrency(credit)}</td>
                <td class="text-end ${net < 0 ? 'negative-balance' : ''}">${formatCurrency(net)}</td>
            `;
            tbody.appendChild(row);
        });

        // Update totals in footer
        updateTotals(totalDebit, totalCredit, totalNet);

        // Initialize DataTable
        dataTable = $('#agingReportTable').DataTable({
            "order": [[0, "asc"]],
            "language": {
                "emptyTable": "No aging data available",
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
                    title: 'Aging Report ' + $('#referenceDate').val(),
                    footer: true
                },
                {
                    extend: 'pdf',
                    text: '<i class="fas fa-file-pdf"></i> PDF',
                    className: 'btn btn-sm btn-danger',
                    title: 'Aging Report ' + $('#referenceDate').val(),
                    footer: true
                },
                {
                    extend: 'print',
                    text: '<i class="fas fa-print"></i> Print',
                    className: 'btn btn-sm btn-info',
                    title: 'Aging Report ' + $('#referenceDate').val(),
                    footer: true
                }
            ]
        });

        updateReportSummary(data.length, totalNet);
    }

    function updateTotals(totalDebit, totalCredit, totalNet) {
        document.getElementById('totalDebit').textContent = formatCurrency(totalDebit);
        document.getElementById('totalCredit').textContent = formatCurrency(totalCredit);
        document.getElementById('totalNet').textContent = formatCurrency(totalNet);
    }

    function updateReportSummary(count, totalBalance = 0) {
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

    function formatCurrency(value) {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value);
    }

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

        $('#alertArea').html(alert);

        setTimeout(() => alert.alert('close'), 5000);
    }
});