//class MissedEntriesReportManager {
//    constructor() {
//        this.tables = {};
//        this.reportData = {
//            summary: null,
//            byCustomer: [],
//            byProduct: []
//        };
//        this.init();
//    }

//    init() {
//        this.initializeDataTables();
//        this.setupEventHandlers();
//        this.setupTabListeners();

//        // Default to showing 3-month data
//        const endDate = new Date();
//        const startDate = new Date();
//        startDate.setMonth(startDate.getMonth() - 3);

//        $('#startDate').val(this.formatDateForInput(startDate));
//        $('#endDate').val(this.formatDateForInput(endDate));

//        // Automatically load data if project is pre-selected
//        if ($('#projectId').val()) {
//            this.loadReportData();
//        }
//    }

//    formatDateForInput(date) {
//        return date.toISOString().split('T')[0];
//    }

//    initializeDataTables() {
//        // Customer table with action button
//        this.tables.customer = $('#customerTable').DataTable({
//            ordering: true,
//            searching: true,
//            paging: true,
//            pageLength: 10,
//            order: [[2, 'desc']], // Sort by total quantity by default
//            columns: [
//                { data: 'customerName' },
//                { data: 'entryCount', className: 'text-end' },
//                { data: 'totalQuantity', className: 'text-end' },
//                {
//                    data: 'totalValue',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                { data: 'uniqueProductCount', className: 'text-end' },
//                {
//                    data: 'averageQuantityPerEntry',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 1)
//                },
//                {
//                    data: 'percentOfTotal',
//                    className: 'text-end',
//                    render: function (data) {
//                        return data.toFixed(1) + '%';
//                    }
//                },
//                {
//                    data: null,
//                    orderable: false,
//                    className: 'text-center',
//                    render: function (data) {
//                        return `<button class="btn btn-sm btn-primary view-customer-details" data-customer-id="${data.customerAccountId}" data-customer-name="${data.customerName}">
//                            <i class="fas fa-search-plus"></i> Details
//                        </button>`;
//                    }
//                }
//            ]
//        });

//        // Customer details table
//        this.tables.customerDetails = $('#customerDetailsTable').DataTable({
//            ordering: true,
//            searching: true,
//            paging: true,
//            pageLength: 10,
//            order: [[0, 'desc']], // Sort by date by default
//            columns: [
//                {
//                    data: 'missedDate',
//                    render: function (data) {
//                        const date = new Date(data);
//                        return date.toLocaleDateString();
//                    }
//                },
//                { data: 'productName' },
//                { data: 'quantity', className: 'text-end' },
//                {
//                    data: 'rate',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                {
//                    data: 'discount',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                {
//                    data: 'totalValue',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                { data: 'description' }
//            ]
//        });

//        // Product table
//        this.tables.product = $('#productTable').DataTable({
//            ordering: true,
//            searching: true,
//            paging: true,
//            pageLength: 10,
//            order: [[2, 'desc']], // Sort by total quantity by default
//            columns: [
//                { data: 'productName' },
//                { data: 'entryCount', className: 'text-end' },
//                { data: 'totalQuantity', className: 'text-end' },
//                {
//                    data: 'totalValue',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                { data: 'uniqueCustomerCount', className: 'text-end' },
//                {
//                    data: 'averageRate',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                },
//                {
//                    data: 'averageDiscount',
//                    className: 'text-end',
//                    render: $.fn.dataTable.render.number(',', '.', 0)
//                }
//            ]
//        });
//    }

//    setupEventHandlers() {
//        $('#reportFilterForm').on('submit', (e) => {
//            e.preventDefault();
//            this.loadReportData();
//        });

//        $('#exportCsvBtn').on('click', () => {
//            this.exportToCsv();
//        });

//        $('#printReportBtn').on('click', () => {
//            this.printReport();
//        });

//        // Add event handler for customer details button
//        $('#customerTable').on('click', '.view-customer-details', (e) => {
//            const button = $(e.currentTarget);
//            const customerId = button.data('customer-id');
//            const customerName = button.data('customer-name');
//            this.showCustomerDetails(customerId, customerName);
//        });

//        // Add event handler for closing customer details
//        $('#closeCustomerDetails').on('click', () => {
//            this.hideCustomerDetails();
//        });
//    }

//    setupTabListeners() {
//        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', (e) => {
//            const targetId = $(e.target).attr('data-bs-target');

//            // Adjust tables when tab is shown
//            if (targetId === '#by-customer') {
//                this.tables.customer.columns.adjust().draw();
//            } else if (targetId === '#by-product') {
//                this.tables.product.columns.adjust().draw();
//            }
//        });
//    }

//    async loadReportData() {
//        const startDate = $('#startDate').val();
//        const endDate = $('#endDate').val();
//        const projectId = $('#projectId').val();

//        if (!this.validateDates(startDate, endDate)) {
//            return;
//        }

//        try {
//            this.showLoading();

//            // Hide any open customer details
//            this.hideCustomerDetails();

//            // Load data in parallel
//            const [summaryData, customerData, productData] = await Promise.all([
//                this.fetchSummaryData(startDate, endDate, projectId),
//                this.fetchCustomerData(startDate, endDate, projectId),
//                this.fetchProductData(startDate, endDate, projectId)
//            ]);

//            // Store data
//            this.reportData.summary = summaryData;
//            this.reportData.byCustomer = this.processCustomerData(customerData, summaryData);
//            this.reportData.byProduct = productData;

//            // Update UI
//            this.updateSummaryView();
//            this.updateCustomerTable();
//            this.updateProductTable();

//        } catch (error) {
//            console.error('Error loading report data:', error);
//            this.showError('Failed to load report data. Please try again.');
//        } finally {
//            this.hideLoading();
//        }
//    }

//    validateDates(startDate, endDate) {
//        if (!startDate || !endDate) {
//            this.showError('Please select both start and end dates');
//            return false;
//        }

//        if (new Date(startDate) > new Date(endDate)) {
//            this.showError('Start date must be before end date');
//            return false;
//        }

//        return true;
//    }

//    async fetchSummaryData(startDate, endDate, projectId) {
//        const response = await $.get('/Reporting/GetMissedEntriesSummary', {
//            startDate,
//            endDate,
//            projectId
//        });

//        return response;
//    }

//    async fetchCustomerData(startDate, endDate, projectId) {
//        const response = await $.get('/Reporting/GetMissedEntriesByCustomer', {
//            startDate,
//            endDate,
//            projectId
//        });

//        return response;
//    }

//    async fetchProductData(startDate, endDate, projectId) {
//        const response = await $.get('/Reporting/GetMissedEntriesByProduct', {
//            startDate,
//            endDate,
//            projectId
//        });

//        return response;
//    }

//    processCustomerData(customerData, summaryData) {
//        const totalQuantity = summaryData.totalMissedQuantity;

//        return customerData.map(customer => ({
//            ...customer,
//            percentOfTotal: totalQuantity > 0 ? (customer.totalQuantity / totalQuantity) * 100 : 0
//        }));
//    }

//    showCustomerDetails(customerId, customerName) {
//        // Find the customer in the data
//        const customer = this.reportData.byCustomer.find(c => c.customerAccountId === customerId);

//        if (customer && customer.customerEntries) {
//            // Update the details section title
//            $('#selectedCustomerName').text(`${customerName} - Details`);

//            // Update the details table
//            this.tables.customerDetails.clear();
//            this.tables.customerDetails.rows.add(customer.customerEntries);
//            this.tables.customerDetails.draw();

//            // Show the section
//            $('#customerDetailSection').removeClass('d-none');
//        } else {
//            this.showError('No details available for this customer');
//        }
//    }

//    hideCustomerDetails() {
//        $('#customerDetailSection').addClass('d-none');
//    }

//    updateSummaryView() {
//        const summary = this.reportData.summary;
//        if (!summary) return;

//        // Update summary metrics
//        $('#summaryTotalEntries').text(summary.totalMissedEntries.toLocaleString());
//        $('#summaryTotalQuantity').text(summary.totalMissedQuantity.toLocaleString());
//        $('#summaryTotalValue').text(summary.totalMissedValue.toLocaleString());
//        $('#summaryAvgValue').text(summary.averageValuePerEntry.toLocaleString(undefined, { maximumFractionDigits: 0 }));

//        // Update top products list
//        const topProducts = this.reportData.byProduct
//            .sort((a, b) => b.totalQuantity - a.totalQuantity)
//            .slice(0, 3);

//        const $productsList = $('#topProductsList');
//        $productsList.empty();

//        if (topProducts.length === 0) {
//            $productsList.append('<div class="list-group-item">No data available</div>');
//        } else {
//            topProducts.forEach((product, index) => {
//                $productsList.append(`
//                    <div class="list-group-item d-flex justify-content-between align-items-center">
//                        <div>
//                            <span class="badge bg-primary rounded-pill me-2">${index + 1}</span>
//                            ${product.productName}
//                        </div>
//                        <div>
//                            <span class="badge bg-secondary rounded-pill">${product.totalQuantity.toLocaleString()} items</span>
//                            <span class="badge bg-info rounded-pill ms-2">$${product.totalValue.toLocaleString()}</span>
//                        </div>
//                    </div>
//                `);
//            });
//        }

//        // Update top customers list
//        const topCustomers = this.reportData.byCustomer
//            .sort((a, b) => b.totalQuantity - a.totalQuantity)
//            .slice(0, 3);

//        const $customersList = $('#topCustomersList');
//        $customersList.empty();

//        if (topCustomers.length === 0) {
//            $customersList.append('<div class="list-group-item">No data available</div>');
//        } else {
//            topCustomers.forEach((customer, index) => {
//                $customersList.append(`
//                    <div class="list-group-item d-flex justify-content-between align-items-center">
//                        <div>
//                            <span class="badge bg-warning rounded-pill me-2">${index + 1}</span>
//                            ${customer.customerName}
//                        </div>
//                        <div>
//                            <span class="badge bg-secondary rounded-pill">${customer.totalQuantity.toLocaleString()} items</span>
//                            <span class="badge bg-success rounded-pill ms-2">${customer.uniqueProductCount} products</span>
//                        </div>
//                    </div>
//                `);
//            });
//        }
//    }

//    updateCustomerTable() {
//        this.tables.customer.clear();
//        if (this.reportData.byCustomer.length > 0) {
//            this.tables.customer.rows.add(this.reportData.byCustomer);
//        }
//        this.tables.customer.draw();
//    }

//    updateProductTable() {
//        this.tables.product.clear();
//        if (this.reportData.byProduct.length > 0) {
//            this.tables.product.rows.add(this.reportData.byProduct);
//        }
//        this.tables.product.draw();
//    }

//    exportToCsv() {
//        const activeTab = $('.nav-link.active').attr('id');
//        let data, filename;

//        switch (activeTab) {
//            case 'summary-tab':
//                this.exportSummaryToCsv();
//                break;
//            case 'by-customer-tab':
//                data = this.reportData.byCustomer;
//                filename = 'missed_entries_by_customer.csv';
//                this.exportDataToCsv(data, filename);
//                break;
//            case 'by-product-tab':
//                data = this.reportData.byProduct;
//                filename = 'missed_entries_by_product.csv';
//                this.exportDataToCsv(data, filename);
//                break;
//        }
//    }

//    exportSummaryToCsv() {
//        const summary = this.reportData.summary;
//        if (!summary) return;

//        const data = [{
//            'Total Missed Entries': summary.totalMissedEntries,
//            'Total Missed Quantity': summary.totalMissedQuantity,
//            'Total Missed Value': summary.totalMissedValue,
//            'Unique Customers': summary.uniqueCustomers,
//            'Unique Products': summary.uniqueProducts,
//            'Average Quantity Per Entry': summary.averageQuantityPerEntry,
//            'Average Value Per Entry': summary.averageValuePerEntry,
//            'Start Date': summary.startDate,
//            'End Date': summary.endDate,
//            'Top Product': summary.topMissedProduct,
//            'Top Product Quantity': summary.topMissedProductQuantity,
//            'Top Customer': summary.topCustomer,
//            'Top Customer Missed Quantity': summary.topCustomerMissedQuantity
//        }];

//        this.exportDataToCsv(data, 'missed_entries_summary.csv');
//    }

//    exportDataToCsv(data, filename) {
//        if (!data || data.length === 0) {
//            this.showError('No data available for export');
//            return;
//        }

//        // Get headers from first object
//        const headers = Object.keys(data[0]);

//        // Create CSV content
//        let csvContent = headers.join(',') + '\n';

//        data.forEach(item => {
//            const row = headers.map(header => {
//                const value = item[header];
//                // Wrap strings containing commas in quotes
//                if (typeof value === 'string' && value.includes(',')) {
//                    return `"${value}"`;
//                }
//                return value;
//            });
//            csvContent += row.join(',') + '\n';
//        });

//        // Create download link
//        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
//        const url = URL.createObjectURL(blob);
//        const link = document.createElement('a');
//        link.setAttribute('href', url);
//        link.setAttribute('download', filename);
//        link.style.display = 'none';
//        document.body.appendChild(link);
//        link.click();
//        document.body.removeChild(link);
//    }

//    printReport() {
//        window.print();
//    }

//    showLoading() {
//        $('button[type="submit"]')
//            .prop('disabled', true)
//            .html('<span class="spinner-border spinner-border-sm me-2"></span>Loading...');
//    }

//    hideLoading() {
//        $('button[type="submit"]')
//            .prop('disabled', false)
//            .html('<i class="fas fa-search me-1"></i> Generate Report');
//    }

//    showError(message) {
//        // You could use a toast or alert component here
//        alert(message);
//    }
//}

//// Initialize when document is ready
//$(function () {
//    window.missedEntriesReportManager = new MissedEntriesReportManager();
//});