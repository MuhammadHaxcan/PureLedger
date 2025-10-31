class MissedEntriesManager {
    constructor() {
        this.tables = {};
        this.reportState = {
            currentPage: 0,
            pageSize: 100, // Increased batch size to reduce round-trips
            isLoading: false,
            hasMoreData: true,
            totalRecords: 0,
            loadedCount: 0 // track count only to reduce memory usage
        };
        this.currentFilters = null;
        this.isGlobalLoading = false;
        this.init();
    }

    init() {
        this.initializeTables();
        this.setupEventHandlers();
        this.setupTabListeners();
    }

    initializeTables() {
        // NOTE: By-customer and by-product are rendered as plain tables (no DataTables paging).
        // Keep DataTable for customerDetails but disable paging controls and search box.
        this.tables.customerDetails = $('#customerDetailsTable').DataTable({
            ordering: true,
            searching: false,   // remove search input
            paging: false,      // disable Prev/Next and paging controls
            info: false,        // remove "Showing X of Y entries"
            lengthChange: false,// remove "Show N entries" select
            order: [[0, 'desc']],
            dom: 't',           // only render the table element (no filter/length/info/pagination)
            columns: [
                {
                    data: 'missedDate',
                    render: (data) => this.formatDate(data)
                },
                { data: 'productName' },
                { data: 'quantity', className: 'text-end' },
                {
                    data: 'rate',
                    className: 'text-end',
                    render: $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    data: 'discount',
                    className: 'text-end',
                    render: $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    data: 'totalValue',
                    className: 'text-end',
                    render: $.fn.dataTable.render.number(',', '.', 0)
                },
                { data: 'description' }
            ]
        });
    }

    setupEventHandlers() {
        $('#filterForm').on('submit', (e) => {
            e.preventDefault();

            const projectId = $('#projectId').val();
            if (!projectId) {
                this.showAlert('Please select a project first', 'warning');
                return;
            }

            this.currentFilters = this.getFilterParams();
            this.resetReportState();
            this.loadAllData();
        });

        $('#endDate').on('change', () => this.validateDates());
        $('#startDate').on('change', () => this.validateDates());

        // Delegated handler works when rows are rendered into DOM
        $('#byCustomerTable').on('click', '.view-customer-details', (e) => {
            const button = $(e.currentTarget);
            const customerId = button.data('customer-id');
            const customerName = button.data('customer-name');
            this.showCustomerDetails(customerId, customerName);
        });

        $('#closeCustomerDetails').on('click', () => {
            this.hideCustomerDetails();
        });
    }

    setupTabListeners() {
        const $tabButtons = $('button[data-bs-toggle="tab"]');

        // Prevent tab switching while loading
        $tabButtons.on('show.bs.tab', (e) => {
            if (this.isGlobalLoading || this.reportState.isLoading) {
                e.preventDefault();
                this.showAlert('Please wait for data to finish loading', 'info');
                return false;
            }
        });

        // When shown, nothing to adjust for plain tables, only redraw customerDetails if visible
        $tabButtons.on('shown.bs.tab', (e) => {
            const targetId = $(e.target).attr('data-bs-target');

            if (targetId === '#by-customer') {
                // nothing — plain table
                this.hideCustomerDetails();
            } else if (targetId === '#by-product') {
                // nothing — plain table
                this.hideCustomerDetails();
            }
        });
    }

    resetReportState() {
        this.reportState = {
            currentPage: 0,
            pageSize: this.reportState.pageSize || 100,
            isLoading: false,
            hasMoreData: true,
            totalRecords: 0,
            loadedCount: 0
        };
        $('#entriesTableBody').html('');
        this.removeLoadingIndicators();
        this.hideCustomerDetails();

        // Clear grouped lists (no showing counters)
        $('#byCustomerTableBody').html('');
        $('#byProductTableBody').html('');
        // detailsCount removed from UI — safe to skip updates if present
        $('#detailsCount').text && $('#detailsCount').text('Showing 0 entries');
    }

    removeLoadingIndicators() {
        $('#loadingStatus').remove();
        $('#autoLoadIndicator').remove();
    }

    validateDates() {
        const startDate = $('#startDate').val();
        const endDate = $('#endDate').val();

        if (startDate && endDate && startDate > endDate) {
            this.showAlert('Start date must be before end date', 'warning');
            $('#startDate').val(this.getDefaultStartDate());
            $('#endDate').val(this.getDefaultEndDate());
        }
    }

    getDefaultStartDate() {
        const date = new Date();
        date.setMonth(date.getMonth() - 1);
        return date.toISOString().split('T')[0];
    }

    getDefaultEndDate() {
        return new Date().toISOString().split('T')[0];
    }

    getFilterParams() {
        return {
            startDate: $('#startDate').val(),
            endDate: $('#endDate').val(),
            projectId: $('#projectId').val()
        };
    }

    async loadAllData() {
        try {
            this.isGlobalLoading = true;
            this.showLoading();
            this.disableTabs();

            // Load summary and grouped data in parallel
            await Promise.all([
                this.loadSummaryStats(),
                this.loadByCustomerData(),
                this.loadByProductData()
            ]);

            // Start loading detailed entries with auto-pagination
            await this.loadDetailedEntriesChunk(true);

        } catch (error) {
            console.error('Error loading missed entries data:', error);
            this.showAlert('Error loading missed entries data. Please try again.', 'danger');
        } finally {
            this.isGlobalLoading = false;
            this.hideLoading();
            this.enableTabs();
        }
    }

    async loadDetailedEntriesChunk(autoLoad) {
        if (this.reportState.isLoading || !this.reportState.hasMoreData) {
            return;
        }

        this.reportState.isLoading = true;
        this.disableTabs();
        this.updateLoadingIndicator();

        const params = {
            ...this.currentFilters,
            page: this.reportState.currentPage,
            pageSize: this.reportState.pageSize
        };

        try {
            const response = await $.ajax({
                url: '/Reporting/GetMissedEntries',
                method: 'GET',
                data: params,
                timeout: 30000,
                cache: false
            });

            if (this.reportState.currentPage === 0) {
                this.initializeDetailedDisplay(response.totalRecords || 0);
            }

            if (response?.entries?.length > 0) {
                // track count only to reduce memory pressure
                this.reportState.loadedCount += response.entries.length;
                this.appendEntriesToTable(response.entries);
            }

            this.reportState.currentPage++;
            this.reportState.hasMoreData = response.hasMoreData;
            this.reportState.totalRecords = response.totalRecords || 0;

            // Update simple details count indicator if present
            this.updateDetailsCount && this.updateDetailsCount();

            // Auto-load next chunk if more data exists
            if (autoLoad && this.reportState.hasMoreData) {
                // small delay to keep UI responsive, but avoid too many rapid requests
                setTimeout(() => this.loadDetailedEntriesChunk(true), 150);
            } else if (!this.reportState.hasMoreData) {
                this.enableTabs();
                // finalize details count to total
                this.updateDetailsCount && this.updateDetailsCount(true);
            }

        } catch (error) {
            console.error('Error loading detailed entries:', error);
            $('#entriesTableBody').html(
                '<tr><td colspan="9" class="text-center text-danger">' +
                '<i class="fas fa-exclamation-triangle me-2"></i>' +
                'Error loading entries. Please try again.' +
                '</td></tr>'
            );
            this.enableTabs();
        } finally {
            this.reportState.isLoading = false;
            this.updateLoadingIndicator();
        }
    }

    initializeDetailedDisplay(totalRecords) {
        $('#entriesTableBody').html('');
        this.removeLoadingIndicators();
        this.reportState.totalRecords = totalRecords;
        this.reportState.loadedCount = 0;

        // detailsCount removed from UI in the view — only update if element exists
        $('#detailsCount').text && $('#detailsCount').text(`Showing 0 of ${this.reportState.totalRecords.toLocaleString()} entries`);
    }

    // Update the details count indicator. If finalize=true set to total.
    updateDetailsCount(finalize = false) {
        if (!$('#detailsCount').text) return;
        const loaded = this.reportState.loadedCount;
        const total = this.reportState.totalRecords || 0;
        const text = finalize
            ? `Showing ${total.toLocaleString()} entries`
            : `Showing ${loaded.toLocaleString()} of ${total.toLocaleString()} entries`;
        $('#detailsCount').text(text);
    }

    updateLoadingIndicator() {
        // spinner element may not exist now — safe no-op if missing
        const spinner = $('#loadMoreSpinner');
        if (this.reportState.isLoading) {
            spinner.removeClass('d-none');
        } else {
            spinner.addClass('d-none');
        }
    }

    appendEntriesToTable(entries) {
        const tbody = $('#entriesTableBody');
        const fragment = document.createDocumentFragment();

        // Batch DOM updates for better performance
        entries.forEach(entry => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${this.formatDate(entry.missedDate)}</td>
                <td>${this.escapeHtml(entry.projectName || '')}</td>
                <td>${this.escapeHtml(entry.customerName || '')}</td>
                <td>${this.escapeHtml(entry.productName || '')}</td>
                <td class="text-end">${(entry.quantity || 0).toLocaleString()}</td>
                <td class="text-end">${this.formatNumber(entry.rate)}</td>
                <td class="text-end">${this.formatNumber(entry.discount)}</td>
                <td class="text-end">${this.formatNumber(entry.totalValue)}</td>
                <td>${this.escapeHtml(entry.description || '')}</td>
            `;
            fragment.appendChild(row);
        });

        tbody.append(fragment);
    }

    formatDate(dateString) {
        if (!dateString) return '';
        try {
            const date = new Date(dateString);
            return date.toLocaleDateString();
        } catch {
            return dateString;
        }
    }

    formatNumber(value) {
        return new Intl.NumberFormat('en-US', {
            style: 'decimal',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value || 0);
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    async loadSummaryStats() {
        const params = this.currentFilters;

        try {
            const response = await $.ajax({
                url: '/Reporting/GetMissedEntriesSummary',
                method: 'GET',
                data: params,
                timeout: 15000
            });

            if (response) {
                $('#totalEntries').text(response.totalMissedEntries.toLocaleString());
                $('#totalQuantity').text(response.totalMissedQuantity.toLocaleString());
                $('#uniqueCustomers').text(response.uniqueCustomers.toLocaleString());
                $('#uniqueProducts').text(response.uniqueProducts.toLocaleString());

                if (response.topCustomer) {
                    $('#topCustomer').html(`
                        <strong>${this.escapeHtml(response.topCustomer)}</strong>
                        <div class="text-muted small">Missed Quantity: ${response.topCustomerMissedQuantity.toLocaleString()}</div>
                    `);
                } else {
                    $('#topCustomer').html('<span class="text-muted">No data available</span>');
                }

                if (response.topMissedProduct) {
                    $('#topProduct').html(`
                        <strong>${this.escapeHtml(response.topMissedProduct)}</strong>
                        <div class="text-muted small">Missed Quantity: ${response.topMissedProductQuantity.toLocaleString()}</div>
                    `);
                } else {
                    $('#topProduct').html('<span class="text-muted">No data available</span>');
                }
            }
        } catch (error) {
            console.error('Error loading summary stats:', error);
            this.showAlert('Error loading summary statistics', 'warning');
        }
    }

    // Render grouped customer rows into plain table body (no showing counter)
    async loadByCustomerData() {
        const params = this.currentFilters;

        try {
            const response = await $.ajax({
                url: '/Reporting/GetMissedEntriesByCustomer',
                method: 'GET',
                data: params,
                timeout: 15000
            });

            const tbody = $('#byCustomerTableBody');
            tbody.html('');

            if (response && response.length > 0) {
                const fragment = document.createDocumentFragment();
                response.forEach(item => {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `
                        <td>${this.escapeHtml(item.customerName || 'Unknown')}</td>
                        <td class="text-end">${(item.entryCount || 0).toLocaleString()}</td>
                        <td class="text-end">${(item.totalQuantity || 0).toLocaleString()}</td>
                        <td class="text-end">${this.formatNumber(item.totalValue || 0)}</td>
                        <td class="text-end">${(item.uniqueProductCount || 0).toLocaleString()}</td>
                        <td class="text-end">${this.formatNumber(item.averageQuantityPerEntry || 0)}</td>
                        <td class="text-center">
                            <button class="btn btn-sm btn-primary view-customer-details" 
                                    data-customer-id="${item.customerAccountId}" 
                                    data-customer-name="${this.escapeHtml(item.customerName || '')}">
                                <i class="fas fa-search-plus"></i> Details
                            </button>
                        </td>
                    `;
                    fragment.appendChild(tr);
                });
                tbody.append(fragment);
            } else {
                tbody.html('<tr><td colspan="7" class="text-center text-muted">No customer data available.</td></tr>');
            }

        } catch (error) {
            console.error('Error loading by-customer data:', error);
            this.showAlert('Error loading customer data', 'warning');
            $('#byCustomerTableBody').html('<tr><td colspan="7" class="text-center text-danger">Error loading customer data</td></tr>');
        }
    }

    // Render grouped product rows into plain table body (no showing counter)
    async loadByProductData() {
        const params = this.currentFilters;

        try {
            const response = await $.ajax({
                url: '/Reporting/GetMissedEntriesByProduct',
                method: 'GET',
                data: params,
                timeout: 15000
            });

            const tbody = $('#byProductTableBody');
            tbody.html('');

            if (response && response.length > 0) {
                const fragment = document.createDocumentFragment();
                response.forEach(item => {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `
                        <td>${this.escapeHtml(item.productName || 'Unknown')}</td>
                        <td class="text-end">${(item.entryCount || 0).toLocaleString()}</td>
                        <td class="text-end">${(item.totalQuantity || 0).toLocaleString()}</td>
                        <td class="text-end">${this.formatNumber(item.totalValue || 0)}</td>
                        <td class="text-end">${(item.uniqueCustomerCount || 0).toLocaleString()}</td>
                        <td class="text-end">${this.formatNumber(item.averageRate || 0)}</td>
                        <td class="text-end">${this.formatNumber(item.averageDiscount || 0)}</td>
                    `;
                    fragment.appendChild(tr);
                });
                tbody.append(fragment);
            } else {
                tbody.html('<tr><td colspan="7" class="text-center text-muted">No product data available.</td></tr>');
            }

        } catch (error) {
            console.error('Error loading by-product data:', error);
            this.showAlert('Error loading product data', 'warning');
            $('#byProductTableBody').html('<tr><td colspan="7" class="text-center text-danger">Error loading product data</td></tr>');
        }
    }

    async showCustomerDetails(customerId, customerName) {
        const params = {
            ...this.currentFilters,
            customerAccountId: customerId
        };

        $('#selectedCustomerName').text(`${customerName} - Loading...`);
        $('#customerDetailSection').removeClass('d-none');

        try {
            const response = await $.ajax({
                url: '/Reporting/GetMissedEntriesData',
                method: 'GET',
                data: params,
                timeout: 15000
            });

            $('#selectedCustomerName').text(`${customerName} - Details`);

            this.tables.customerDetails.clear();

            if (response?.entries?.length > 0) {
                this.tables.customerDetails.rows.add(response.entries);
            }

            this.tables.customerDetails.draw();
        } catch (error) {
            console.error('Error loading customer details:', error);
            $('#selectedCustomerName').text(`${customerName} - Error loading data`);
            this.showAlert('Error loading customer details', 'danger');
        }
    }

    hideCustomerDetails() {
        $('#customerDetailSection').addClass('d-none');
        this.tables.customerDetails.clear().draw(false);
    }

    disableTabs() {
        $('button[data-bs-toggle="tab"]')
            .prop('disabled', true)
            .addClass('disabled')
            .css('cursor', 'not-allowed');
    }

    enableTabs() {
        $('button[data-bs-toggle="tab"]')
            .prop('disabled', false)
            .removeClass('disabled')
            .css('cursor', 'pointer');
    }

    showLoading() {
        $('button[type="submit"]')
            .prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Loading...');

        // Disable form inputs
        $('#filterForm input, #filterForm select').prop('disabled', true);
    }

    hideLoading() {
        $('button[type="submit"]')
            .prop('disabled', false)
            .html('<i class="fas fa-search me-1"></i> Search');

        // Re-enable form inputs
        $('#filterForm input, #filterForm select').prop('disabled', false);
    }

    showAlert(message, type = 'info') {
        // Remove existing alerts
        $('.dynamic-alert').remove();

        const alert = `
            <div class="alert alert-${type} alert-dismissible fade show dynamic-alert" role="alert">
                <i class="fas fa-info-circle me-2"></i>${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;

        $('.container-fluid').prepend(alert);

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            $('.dynamic-alert').fadeOut(300, function () {
                $(this).remove();
            });
        }, 5000);
    }
}

// Initialize on page load
$(function () {
    window.missedEntriesManager = new MissedEntriesManager();
});