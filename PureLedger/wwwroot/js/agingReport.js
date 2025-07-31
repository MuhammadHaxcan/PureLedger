$(function () {
    // Initialize variables for report data
    let dataTable = null;

    // Set default reference date to today if not set
    const refDateInput = document.getElementById('referenceDate');
    if (refDateInput && !refDateInput.value) {
        refDateInput.valueAsDate = new Date();
    }

    // Initialize form
    initializeForm();

    async function initializeForm() {
        try {
            // Setup form event
            const form = document.getElementById('agingReportForm');
            if (form) {
                form.addEventListener('submit', async function (e) {
                    e.preventDefault();
                    await generateReport();
                });
            } else {
                console.error('Form with id "agingReportForm" not found.');
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

        // Clear previous results
        $('#reportData').html('<tr><td colspan="9" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>');
        $('#reportResults').show();

        try {
            const queryParams = new URLSearchParams({
                referenceDate: referenceDate,
                projectId: projectId || '',
                categoryId: categoryId
            });

            const response = await fetch(`/Reporting/GetAgingReportData?${queryParams}`);

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Error: ${response.status} ${errorText}`);
            }

            const data = await response.json();
            displayReport(data);
            showAlert('success', 'Report generated successfully');
        } catch (error) {
            console.error('Error generating report:', error);
            $('#reportData').html(`<tr><td colspan="9" class="text-center text-danger">Error generating report: ${error.message}</td></tr>`);
            showAlert('danger', 'Failed to generate report: ' + error.message);
        }
    }

    function displayReport(data) {
        // Destroy existing DataTable if it exists
        if (dataTable) {
            dataTable.destroy();
            dataTable = null;
        }

        const tbody = document.getElementById('reportData');
        if (!tbody) return;
        tbody.innerHTML = '';

        if (!data || data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="9" class="text-center">No data found</td></tr>';
            updateReportSummary(0);
            return;
        }

        let totalDebit = 0;
        let totalCredit = 0;
        let totalNet = 0;
        let totalCurrent = 0;
        let total30Days = 0;
        let total60Days = 0;
        let total90Days = 0;
        let totalOver90 = 0;

        data.forEach(item => {
            const debit = parseFloat(item.totalDebit) || 0;
            const credit = parseFloat(item.totalCredit) || 0;
            const net = parseFloat(item.netBalance) || 0;

            // Extract aging buckets (assuming they're in the data)
            const current = parseFloat(item.current || 0);
            const days30 = parseFloat(item.days30 || 0);
            const days60 = parseFloat(item.days60 || 0);
            const days90 = parseFloat(item.days90 || 0);
            const over90 = parseFloat(item.over90 || 0);

            // Add to totals
            totalDebit += debit;
            totalCredit += credit;
            totalNet += net;
            totalCurrent += current;
            total30Days += days30;
            total60Days += days60;
            total90Days += days90;
            totalOver90 += over90;

            // Determine if balance should be highlighted as negative
            const netClass = net < 0 ? 'negative-balance' : '';
            const currentClass = current < 0 ? 'negative-balance' : '';
            const days30Class = days30 < 0 ? 'negative-balance' : '';
            const days60Class = days60 < 0 ? 'negative-balance' : '';
            const days90Class = days90 < 0 ? 'negative-balance' : '';
            const over90Class = over90 < 0 ? 'negative-balance' : '';

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${item.accountName}</td>
                <td class="text-end">${formatCurrency(debit)}</td>
                <td class="text-end">${formatCurrency(credit)}</td>
                <td class="text-end ${netClass}">${formatCurrency(net)}</td>
                <td class="text-end ${currentClass}">${formatCurrency(current)}</td>
                <td class="text-end ${days30Class}">${formatCurrency(days30)}</td>
                <td class="text-end ${days60Class}">${formatCurrency(days60)}</td>
                <td class="text-end ${days90Class}">${formatCurrency(days90)}</td>
                <td class="text-end ${over90Class}">${formatCurrency(over90)}</td>
            `;
            tbody.appendChild(row);
        });

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
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5, 6, 7, 8]
                    }
                },
                {
                    extend: 'pdf',
                    text: '<i class="fas fa-file-pdf"></i> PDF',
                    className: 'btn btn-sm btn-danger',
                    title: 'Aging Report ' + $('#referenceDate').val(),
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5, 6, 7, 8]
                    }
                },
                {
                    extend: 'print',
                    text: '<i class="fas fa-print"></i> Print',
                    className: 'btn btn-sm btn-info',
                    title: 'Aging Report ' + $('#referenceDate').val(),
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5, 6, 7, 8]
                    }
                }
            ],
            "footerCallback": function (row, data, start, end, display) {
                const api = this.api();

                // Update footer with totals
                $(api.column(1).footer()).html(formatCurrency(totalDebit));
                $(api.column(2).footer()).html(formatCurrency(totalCredit));
                $(api.column(3).footer()).html(formatCurrency(totalNet));
                $(api.column(4).footer()).html(formatCurrency(totalCurrent));
                $(api.column(5).footer()).html(formatCurrency(total30Days));
                $(api.column(6).footer()).html(formatCurrency(total60Days));
                $(api.column(7).footer()).html(formatCurrency(total90Days));
                $(api.column(8).footer()).html(formatCurrency(totalOver90));
            }
        });

        updateReportSummary(data.length, totalNet);
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
});