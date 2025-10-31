class MissedEntriesManager {
    constructor() {
        this.tables = {};
        this.init();
    }

    init() {
        this.initializeTables();
        this.setupEventHandlers();
        this.setupTabListeners();
    }

    initializeTables() {
        // Main entries table
        this.tables.details = $('#missedEntriesTable').DataTable({
            "order": [[0, "desc"]], // Sort by date descending
            "pageLength": 25,
            "language": {
                "search": "Search entries:",
                "emptyTable": "No entries found. Select a project and click Search to load data."
            },
            "columns": [
                { "data": "missedDate" },
                { "data": "projectName" },
                { "data": "customerName" },
                { "data": "productName" },
                {
                    "data": "quantity",
                    "className": "text-end"
                },
                {
                    "data": "rate",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    "data": "discount",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    "data": "totalValue",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                { "data": "description" }
            ]
        });

        // By Customer table
        this.tables.byCustomer = $('#byCustomerTable').DataTable({
            "order": [[2, "desc"]], // Sort by quantity descending
            "pageLength": 10,
            "language": {
                "search": "Search customers:",
                "emptyTable": "No customer data available."
            },
            "columns": [
                { "data": "customerName" },
                {
                    "data": "entryCount",
                    "className": "text-end"
                },
                {
                    "data": "totalQuantity",
                    "className": "text-end"
                },
                {
                    "data": "totalValue",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    "data": "uniqueProductCount",
                    "className": "text-end"
                },
                {
                    "data": "averageQuantityPerEntry",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 1)
                },
                {
                    "data": null,
                    "orderable": false,
                    "className": "text-center",
                    "render": function (data) {
                        return `<button class="btn btn-sm btn-primary view-customer-details" data-customer-id="${data.customerAccountId}" data-customer-name="${data.customerName}">
                            <i class="fas fa-search-plus"></i> Details
                        </button>`;
                    }
                }
            ]
        });

        // Customer details table
        this.tables.customerDetails = $('#customerDetailsTable').DataTable({
            ordering: true,
            searching: true,
            paging: true,
            pageLength: 10,
            order: [[0, 'desc']], // Sort by date by default
            columns: [
                {
                    data: 'missedDate',
                    render: function (data) {
                        const date = new Date(data);
                        return date.toLocaleDateString();
                    }
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

        // By Product table
        this.tables.byProduct = $('#byProductTable').DataTable({
            "order": [[2, "desc"]], // Sort by quantity descending
            "pageLength": 10,
            "language": {
                "search": "Search products:",
                "emptyTable": "No product data available."
            },
            "columns": [
                { "data": "productName" },
                {
                    "data": "entryCount",
                    "className": "text-end"
                },
                {
                    "data": "totalQuantity",
                    "className": "text-end"
                },
                {
                    "data": "totalValue",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    "data": "uniqueCustomerCount",
                    "className": "text-end"
                },
                {
                    "data": "averageRate",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                },
                {
                    "data": "averageDiscount",
                    "className": "text-end",
                    "render": $.fn.dataTable.render.number(',', '.', 0)
                }
            ]
        });
    }

    setupEventHandlers() {
        $('#filterForm').on('submit', (e) => {
            e.preventDefault();

            // Validate project selection
            const projectId = $('#projectId').val();
            if (!projectId) {
                alert('Please select a project first');
                return;
            }

            this.loadAllData();
        });

        // Date validation
        $('#endDate').on('change', () => this.validateDates());
        $('#startDate').on('change', () => this.validateDates());

        // Add event handler for customer details button
        $('#byCustomerTable').on('click', '.view-customer-details', (e) => {
            const button = $(e.currentTarget);
            const customerId = button.data('customer-id');
            const customerName = button.data('customer-name');
            this.showCustomerDetails(customerId, customerName);
        });

        // Add event handler for closing customer details
        $('#closeCustomerDetails').on('click', () => {
            this.hideCustomerDetails();
        });
    }

    setupTabListeners() {
        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', (e) => {
            const targetId = $(e.target).attr('data-bs-target');

            // Redraw tables on tab switch to fix column width issues
            if (targetId === '#by-customer') {
                this.tables.byCustomer.columns.adjust().draw();
                // Hide any open customer details
                this.hideCustomerDetails();
            } else if (targetId === '#by-product') {
                this.tables.byProduct.columns.adjust().draw();
                // Hide any open customer details
                this.hideCustomerDetails();
            }
        });
    }

    validateDates() {
        const startDate = $('#startDate').val();
        const endDate = $('#endDate').val();

        if (startDate && endDate && startDate > endDate) {
            alert('Start date must be before end date');
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
            this.showLoading();

            // Load all data in parallel
            await Promise.all([
                this.loadDetailedEntries(),
                this.loadSummaryStats(),
                this.loadByCustomerData(),
                this.loadByProductData()
            ]);

        } catch (error) {
            console.error('Error loading missed entries data:', error);
            alert('Error loading missed entries data. Please try again.');
        } finally {
            this.hideLoading();
        }
    }

    async loadDetailedEntries() {
        const params = this.getFilterParams();

        try {
            const response = await $.get('/Reporting/GetMissedEntries', params);

            this.tables.details.clear();
            if (response && response.length > 0) {
                // Add total value to each entry
                const enhancedData = response.map(item => ({
                    ...item,
                    totalValue: (item.quantity * item.rate) - item.discount
                }));
                this.tables.details.rows.add(enhancedData);
            }
            this.tables.details.draw();
        } catch (error) {
            console.error('Error loading detailed entries:', error);
            throw error;
        }
    }

    async loadSummaryStats() {
        const params = this.getFilterParams();

        try {
            const response = await $.get('/Reporting/GetMissedEntriesSummary', params);

            if (response) {
                // Update summary cards
                $('#totalEntries').text(response.totalMissedEntries.toLocaleString());
                $('#totalQuantity').text(response.totalMissedQuantity.toLocaleString());
                $('#uniqueCustomers').text(response.uniqueCustomers.toLocaleString());
                $('#uniqueProducts').text(response.uniqueProducts.toLocaleString());

                // Update top customer and product
                if (response.topCustomer) {
                    $('#topCustomer').html(`
                        <strong>${response.topCustomer}</strong>
                        <div>Missed Quantity: ${response.topCustomerMissedQuantity.toLocaleString()}</div>
                    `);
                }

                if (response.topMissedProduct) {
                    $('#topProduct').html(`
                        <strong>${response.topMissedProduct}</strong>
                        <div>Missed Quantity: ${response.topMissedProductQuantity.toLocaleString()}</div>
                    `);
                }
            }
        } catch (error) {
            console.error('Error loading summary stats:', error);
            throw error;
        }
    }

    async loadByCustomerData() {
        const params = this.getFilterParams();

        try {
            const response = await $.get('/Reporting/GetMissedEntriesByCustomer', params);

            this.tables.byCustomer.clear();
            if (response && response.length > 0) {
                this.tables.byCustomer.rows.add(response);
            }
            this.tables.byCustomer.draw();
        } catch (error) {
            console.error('Error loading by-customer data:', error);
            throw error;
        }
    }

    async loadByProductData() {
        const params = this.getFilterParams();

        try {
            const response = await $.get('/Reporting/GetMissedEntriesByProduct', params);

            this.tables.byProduct.clear();
            if (response && response.length > 0) {
                this.tables.byProduct.rows.add(response);
            }
            this.tables.byProduct.draw();
        } catch (error) {
            console.error('Error loading by-product data:', error);
            throw error;
        }
    }

    showCustomerDetails(customerId, customerName) {
        const params = {
            ...this.getFilterParams(),
            customerAccountId: customerId
        };

        try {
            // Show loading state
            $('#selectedCustomerName').text(`${customerName} - Loading...`);
            $('#customerDetailSection').removeClass('d-none');

            // Fetch customer-specific entries
            $.get('/Reporting/GetMissedEntriesData', params)
                .then(response => {
                    // Update the title
                    $('#selectedCustomerName').text(`${customerName} - Details`);

                    // Clear and populate the table
                    this.tables.customerDetails.clear();

                    if (response && response.entries && response.entries.length > 0) {
                        // Add total value to each entry if it doesn't exist
                        const enhancedData = response.entries.map(item => ({
                            ...item,
                            totalValue: item.totalValue || (item.quantity * item.rate) - item.discount
                        }));
                        this.tables.customerDetails.rows.add(enhancedData);
                    }

                    this.tables.customerDetails.draw();
                })
                .catch(error => {
                    console.error('Error loading customer details:', error);
                    $('#selectedCustomerName').text(`${customerName} - Error loading data`);
                });
        } catch (error) {
            console.error('Error showing customer details:', error);
        }
    }

    hideCustomerDetails() {
        $('#customerDetailSection').addClass('d-none');
        this.tables.customerDetails.clear().draw();
    }

    showLoading() {
        $('button[type="submit"]')
            .prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Loading...');
    }

    hideLoading() {
        $('button[type="submit"]')
            .prop('disabled', false)
            .html('<i class="fas fa-search me-1"></i> Search');
    }
}

// Initialize when document is ready
$(function () {
    window.missedEntriesManager = new MissedEntriesManager();
});