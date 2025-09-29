class MissedEntriesManager {
    constructor() {
        this.table = null;
        this.init();
    }

    init() {
        this.initializeDataTable();
        this.setupEventHandlers();
        // Removed initial data loading
    }

    initializeDataTable() {
        this.table = $('#missedEntriesTable').DataTable({
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
                { "data": "description" }
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

            this.loadEntries();
        });

        // Date validation
        $('#endDate').on('change', () => this.validateDates());
        $('#startDate').on('change', () => this.validateDates());
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

    async loadEntries() {
        const startDate = $('#startDate').val();
        const endDate = $('#endDate').val();
        const projectId = $('#projectId').val();

        try {
            this.showLoading();

            const response = await $.get('/Home/GetMissedEntries', {
                startDate,
                endDate,
                projectId
            });

            this.table.clear();
            if (response && response.length > 0) {
                this.table.rows.add(response);
            }
            this.table.draw();

        } catch (error) {
            console.error('Error loading missed entries:', error);
            alert('Error loading missed entries. Please try again.');
        } finally {
            this.hideLoading();
        }
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