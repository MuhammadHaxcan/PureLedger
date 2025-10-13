document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    setupEventListeners();
    setDefaultDates();
});

// ==================== STATE MANAGEMENT ====================

function initializeReportState() {
    window.reportState = {
        currentPage: 0,
        pageSize: 4,  // Increased page size for better performance, but still reasonable for testing
        isLoading: false,
        hasMoreData: true,
        totalRecords: 0,
        allEntries: [],
        reportParams: null,
        productGroups: {}
    };
}

// ==================== EVENT LISTENERS ====================

function setupEventListeners() {
    // Form submission
    document.getElementById('productReportForm').addEventListener('submit', e => {
        e.preventDefault();
        generateReport();
    });

    // UI controls
    initColumnToggles();
    initSearchFunctionality();

    // Dropdowns with dependencies
    document.getElementById('projectId').addEventListener('change', loadCategoriesForProject);
    document.getElementById('categoryId').addEventListener('change', loadAccountsForCategory);

    // Infinite scroll
    window.addEventListener('scroll', throttle(checkIfShouldLoadMore, 100));
}

function setDefaultDates() {
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('startDate').value = today;
    document.getElementById('endDate').value = today;
}

// ==================== UI CONTROLS ====================

function initColumnToggles() {
    document.querySelectorAll('.column-toggles input[type="checkbox"]').forEach(checkbox => {
        checkbox.addEventListener('change', function () {
            const columnClass = this.id.replace('show', '').toLowerCase() + '-column';
            const isVisible = this.checked;
            document.querySelectorAll('.' + columnClass).forEach(cell => {
                cell.style.display = isVisible ? 'table-cell' : 'none';
            });
        });
    });
}

function initSearchFunctionality() {
    // Global search
    const globalSearchInput = document.getElementById('globalReportSearch');
    if (globalSearchInput) {
        globalSearchInput.addEventListener('keyup', debounce(performSearch, 250));

        // Clear search button
        const clearButton = document.getElementById('clearSearch');
        if (clearButton) {
            clearButton.addEventListener('click', () => {
                globalSearchInput.value = '';
                performSearch();
            });
        }
    }

    // In-table search
    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.addEventListener('keyup', debounce(function () {
            filterReportData(this.value.toLowerCase());
        }, 250));
    }
}

// Performance optimization with debounce
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// Performance optimization with throttle
function throttle(func, limit) {
    let inThrottle;
    return function () {
        if (!inThrottle) {
            func.apply(this, arguments);
            inThrottle = true;
            setTimeout(() => inThrottle = false, limit);
        }
    };
}

// ==================== SEARCH FUNCTIONALITY ====================

function performSearch() {
    const searchText = document.getElementById('globalReportSearch').value.toLowerCase();

    // Filter all tables
    filterReportData(searchText);
    filterSummaryData('productGroupData', searchText);

    // Sync the in-table search box
    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.value = searchText;
    }
}

function filterReportData(searchText) {
    const rows = document.getElementById('reportData').getElementsByTagName('tr');
    let hasVisibleRows = false;

    // Hide/show rows based on search
    for (const row of rows) {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }

    handleNoMatchingRows('reportData', 'noSearchResults', !hasVisibleRows && rows.length > 0,
        searchText, 17);
}

function filterSummaryData(tableId, searchText) {
    const rows = document.getElementById(tableId).getElementsByTagName('tr');
    let hasVisibleRows = false;

    // Hide/show rows based on search
    for (const row of rows) {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }

    handleNoMatchingRows(tableId, 'noResults' + tableId, !hasVisibleRows && rows.length > 0,
        searchText, 10);
}

function handleNoMatchingRows(tableId, noResultsId, shouldShowNoResults, searchText, colSpan) {
    // Remove existing "no results" message
    const previousNoResults = document.getElementById(noResultsId);
    if (previousNoResults) previousNoResults.remove();

    // Add "no results" message if needed
    if (shouldShowNoResults && searchText.length > 0) {
        const noDataRow = document.createElement('tr');
        noDataRow.id = noResultsId;
        noDataRow.innerHTML = `<td colspan="${colSpan}" class="text-center">No matching records found</td>`;
        document.getElementById(tableId).appendChild(noDataRow);
    }
}

// ==================== DROPDOWNS & DATA LOADING ====================

function loadCategoriesForProject() {
    const projectId = document.getElementById('projectId').value;
    const categorySelect = document.getElementById('categoryId');

    // Reset dropdowns
    resetSelect(categorySelect, 'All Categories', true);
    resetAccountDropdown();

    if (!projectId) return;

    // Show loading state
    categorySelect.innerHTML = '<option value="" disabled selected>Loading categories...</option>';

    $.ajax({
        url: routes.getCategoriesByProject,
        data: { projectId: projectId },
        success: function (categories) {
            resetSelect(categorySelect, 'All Categories');

            if (categories?.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            }
        },
        error: function () {
            resetSelect(categorySelect, 'All Categories');
            showError('Failed to load categories. Please try again.');
        }
    });
}

function loadAccountsForCategory() {
    const categoryId = document.getElementById('categoryId').value;
    const accountSelect = document.getElementById('accountId');

    resetSelect(accountSelect, 'All Products', true);

    if (!categoryId) return;

    // Show loading state
    accountSelect.innerHTML = '<option value="" disabled selected>Loading products...</option>';

    $.ajax({
        url: routes.getAccountsByCategory,
        data: { categoryId: categoryId },
        success: function (accounts) {
            resetSelect(accountSelect, 'All Products');
            updateAccountDropdown(accounts);
        },
        error: function () {
            resetSelect(accountSelect, 'All Products');
            showError('Failed to load products. Please try again.');
        }
    });
}

function updateAccountDropdown(accounts) {
    const accountSelect = document.getElementById('accountId');
    resetSelect(accountSelect, 'All Products');

    if (accounts?.length > 0) {
        accounts.forEach(account => {
            const option = document.createElement('option');
            option.value = account.id;
            option.textContent = account.name;
            accountSelect.appendChild(option);
        });
    }
}

function resetAccountDropdown() {
    const accountSelect = document.getElementById('accountId');
    resetSelect(accountSelect, 'All Products', true);
}

function resetSelect(select, defaultText, disable = false) {
    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

function showError(message) {
    $("#alertArea").html(`
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <i class="fas fa-exclamation-circle me-2"></i> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `);
}

// ==================== REPORT GENERATION ====================

function generateReport() {
    // Reset report state
    initializeReportState();

    // Get form values
    const params = {
        startDate: document.getElementById('startDate').value,
        endDate: document.getElementById('endDate').value,
        page: 0, // lowercase to match backend expectation
        pageSize: window.reportState.pageSize // lowercase to match backend expectation
    };

    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;
    const locationId = document.getElementById('locationId').value;

    if (projectId) params.projectId = projectId;
    if (categoryId) params.categoryId = categoryId;
    if (accountId) params.accountId = accountId;
    if (locationId) params.locationId = locationId;

    // Store params for subsequent requests
    window.reportState.reportParams = params;

    // Clear existing data
    clearReportData();

    // Show loading indicators
    document.getElementById('reportResults').style.display = 'block';
    document.getElementById('reportData').innerHTML = createLoadingRow(17);

    // Fetch first data chunk
    fetchReportChunk();
}

function clearReportData() {
    document.getElementById('reportData').innerHTML = '';
    document.getElementById('productGroupData').innerHTML = '';

    // Reset state
    window.reportState.allEntries = [];
    window.reportState.productGroups = {};
    window.reportState.currentPage = 0; // Make sure we start at page 0

    // Reset totals
    document.getElementById('totalQty').textContent = '0';
    document.getElementById('totalQtyBalance').textContent = '0';
    document.getElementById('totalDebitQty').textContent = '0';
    document.getElementById('totalCreditQty').textContent = '0';
    document.getElementById('netQty').textContent = '0';
    document.getElementById('totalDebit').textContent = '0.00';
    document.getElementById('totalCredit').textContent = '0.00';
    document.getElementById('netBalance').textContent = '0.00';
    document.getElementById('totalCOGS').textContent = '0.00';
    document.getElementById('totalMargin').textContent = '0.00';

    // Remove any existing UI elements
    removeElements(['loadMoreContainer', 'loadingStatus', 'autoLoadIndicator']);
}

function removeElements(ids) {
    ids.forEach(id => {
        const element = document.getElementById(id);
        if (element) element.remove();
    });
}

function createLoadingRow(colSpan) {
    return `<tr><td colspan="${colSpan}" class="text-center">
        <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </td></tr>`;
}

// ==================== INFINITE SCROLL ====================

function checkIfShouldLoadMore() {
    if (!window.reportState?.hasMoreData || window.reportState?.isLoading) {
        return;
    }

    const reportResults = document.getElementById('reportResults');
    if (!reportResults || reportResults.style.display === 'none') {
        return;
    }

    const scrollPosition = window.innerHeight + window.pageYOffset;
    const bodyHeight = document.body.offsetHeight;
    const scrollThreshold = bodyHeight - 500; // Load more when 500px from bottom

    if (scrollPosition >= scrollThreshold) {
        fetchReportChunk();
    }
}

// ==================== DATA FETCHING ====================

function fetchReportChunk() {
    if (window.reportState.isLoading || !window.reportState.hasMoreData) {
        return;
    }

    window.reportState.isLoading = true;
    updateLoadingStatus();

    // Create request parameters
    const params = { ...window.reportState.reportParams };
    params.page = window.reportState.currentPage; // Make sure we're using lowercase 'page'

    // Log the request to debug
    console.log(`Fetching page ${params.page} with size ${params.pageSize}`);

    // Prevent caching
    const timestamp = new Date().getTime();
    const url = `${routes.getProductReportData}?_=${timestamp}`;

    $.ajax({
        url: url,
        method: 'GET',
        data: params,
        dataType: 'json',
        cache: false
    })
        .done(data => {
            console.log('Received data:', data);

            // First page initialization
            if (window.reportState.currentPage === 0) {
                initializeDisplay();
            }

            // Check if we received any new entries
            if (data.entries && data.entries.length > 0) {
                // Process data
                processAndDisplayEntries(data.entries);

                // Update report state
                window.reportState.currentPage++;
                window.reportState.hasMoreData = data.hasMoreData;
                window.reportState.totalRecords = data.totalRecords;
            } else {
                // No more data
                window.reportState.hasMoreData = false;
            }

            // Update UI
            updateLoadingStatus();
        })
        .fail(error => {
            console.error('Error fetching data:', error);
            document.getElementById('reportData').innerHTML =
                '<tr><td colspan="17" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
        })
        .always(() => {
            window.reportState.isLoading = false;
            updateLoadingStatus();
        });
}

// ==================== DATA PROCESSING ====================

function processAndDisplayEntries(entries) {
    if (!entries?.length) return;

    // Calculate starting entry index for proper rendering
    const startIdx = window.reportState.allEntries.length;

    // Add entries to allEntries array without duplicates
    // Use a Map to track IDs we've already seen to avoid duplicates
    const existingIdsMap = new Map(window.reportState.allEntries.map(e => [e.id, true]));

    const newEntries = entries.filter(entry => !existingIdsMap.has(entry.id));
    console.log(`Found ${newEntries.length} new entries out of ${entries.length} total`);

    window.reportState.allEntries = [...window.reportState.allEntries, ...newEntries];

    // Only render the new entries (not re-render everything)
    renderNewEntries(newEntries);

    // Update product groups with the new entries
    updateProductGroups(newEntries);
    renderProductSummary();

    // Update totals
    updateTotals();
}

// Renders only newly added entries
function renderNewEntries(entries) {
    if (!entries.length) return;

    const tbody = document.getElementById('reportData');
    const fragment = document.createDocumentFragment();

    entries.forEach(entry => {
        const row = document.createElement('tr');
        row.dataset.id = entry.entryId; // Use entry ID as data attribute
        row.innerHTML = `
            <td class="id-column">
                <div class="id-container">
                    <a href="/Reporting/PrintVoucher?journalVoucherId=${entry.id}&type=JV" 
                       class="voucher-link" 
                       target="_blank" 
                       title="Open voucher in new window">
                        JV-${entry.id}
                    </a>
                    <span class="entry-id">Entry: ${entry.entryId}</span>
                </div>
            </td>
            <td class="date-column">${entry.entryDate}</td>
            <td class="description-column">${entry.description}</td>
            <td class="account-column">${entry.accountName}</td>
            <td class="qty-column text-end">${formatNumber(entry.quantity)}</td>
            <td class="qty-balance-column text-end">${formatNumber(entry.qtyBalance)}</td>
            <td class="rate-column text-end">${formatDecimal(entry.rate)}</td>
            <td class="debit-qty-column text-end">${formatNumber(entry.debitQty)}</td>
            <td class="credit-qty-column text-end">${formatNumber(entry.creditQty)}</td>
            <td class="qty-balance-column text-end">${formatNumberWithSign(entry.quantityBalance)}</td>
            <td class="avg-rate-column text-end">${formatDecimal(entry.avgRate)}</td>
            <td class="debit-column text-end">${formatDecimal(entry.debit)}</td>
            <td class="credit-column text-end">${formatDecimal(entry.credit)}</td>
            <td class="balance-column text-end ${entry.balance < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(entry.balance)}</td>
            <td class="cogs-column text-end">${formatDecimal(entry.cogs)}</td>
            <td class="margin-column text-end ${entry.margin < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(entry.margin)}</td>
            <td class="location-column">${entry.locationName || '-'}</td>
        `;
        fragment.appendChild(row);
    });

    // If this is the first page and there's a loading indicator, clear it
    if (tbody.querySelector('.spinner-border')) {
        tbody.innerHTML = '';
    }

    tbody.appendChild(fragment);
}

function updateProductGroups(entries) {
    entries.forEach(entry => {
        const productName = entry.accountName;

        if (!window.reportState.productGroups[productName]) {
            window.reportState.productGroups[productName] = {
                totalQty: 0, debitQty: 0, creditQty: 0, balanceQty: 0,
                debit: 0, credit: 0, balance: 0, cogs: 0, margin: 0
            };
        }

        const group = window.reportState.productGroups[productName];
        group.totalQty += entry.quantity;
        group.debitQty += entry.debitQty;
        group.creditQty += entry.creditQty;
        group.balanceQty = group.debitQty - group.creditQty;
        group.debit += entry.debit;
        group.credit += entry.credit;
        group.balance = group.debit - group.credit;
        group.cogs += entry.cogs;
        group.margin += entry.margin;
    });
}

function renderProductSummary() {
    const tbody = document.getElementById('productGroupData');
    tbody.innerHTML = '';

    // Use DocumentFragment for better performance
    const fragment = document.createDocumentFragment();

    Object.entries(window.reportState.productGroups).forEach(([productName, group]) => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${productName}</td>
            <td class="text-end">${formatNumber(group.totalQty)}</td>
            <td class="text-end">${formatNumber(group.debitQty)}</td>
            <td class="text-end">${formatNumber(group.creditQty)}</td>
            <td class="text-end ${group.balanceQty < 0 ? 'negative-balance' : ''}">${formatNumberWithSign(group.balanceQty)}</td>
            <td class="text-end">${formatDecimal(group.debit)}</td>
            <td class="text-end">${formatDecimal(group.credit)}</td>
            <td class="text-end ${group.balance < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(group.balance)}</td>
            <td class="text-end">${formatDecimal(group.cogs)}</td>
            <td class="text-end ${group.margin < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(group.margin)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    // Reset data
    document.getElementById('reportData').innerHTML = '';

    // Clear summary tables
    document.getElementById('productGroupData').innerHTML = '';

    // Remove any existing status indicators
    removeElements(['loadingStatus', 'autoLoadIndicator', 'pagination']);

    // Add loading status indicator
    addLoadingStatus();
    addAutoLoadIndicator();
}

function addLoadingStatus() {
    const tableContainer = document.querySelector('#productReportTable').closest('.table-responsive');
    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded 0 of ${window.reportState.totalRecords} records`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function addAutoLoadIndicator() {
    const tableContainer = document.querySelector('#productReportTable').closest('.table-responsive');
    const loadingStatus = document.getElementById('loadingStatus');

    const autoLoadIndicator = document.createElement('div');
    autoLoadIndicator.id = 'autoLoadIndicator';
    autoLoadIndicator.className = 'text-center mt-1 mb-3';
    autoLoadIndicator.innerHTML = `
        <div class="spinner-border spinner-border-sm text-primary d-none" id="loadMoreSpinner" role="status">
            <span class="visually-hidden">Loading more records...</span>
        </div>
    `;
    tableContainer.parentNode.insertBefore(autoLoadIndicator, loadingStatus.nextSibling);
}

function updateLoadingStatus() {
    const loadingStatus = document.getElementById('loadingStatus');
    if (loadingStatus) {
        loadingStatus.textContent = `Loaded ${window.reportState.allEntries.length} of ${window.reportState.totalRecords} records`;

        // Add end of records message if we've reached the end
        if (!window.reportState.hasMoreData && window.reportState.allEntries.length > 0) {
            loadingStatus.textContent += " (End of records)";
        }
    } else {
        addLoadingStatus();
    }

    const loadMoreSpinner = document.getElementById('loadMoreSpinner');
    if (!loadMoreSpinner) return;

    if (window.reportState.isLoading) {
        loadMoreSpinner.classList.remove('d-none');
    } else {
        loadMoreSpinner.classList.add('d-none');
    }
}

function updateTotals() {
    // Calculate totals from all loaded entries
    const totalQty = window.reportState.allEntries.reduce((sum, entry) => sum + entry.quantity, 0);
    const totalDebitQty = window.reportState.allEntries.reduce((sum, entry) => sum + entry.debitQty, 0);
    const totalCreditQty = window.reportState.allEntries.reduce((sum, entry) => sum + entry.creditQty, 0);
    const totalDebit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.debit, 0);
    const totalCredit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.credit, 0);
    const totalCOGS = window.reportState.allEntries.reduce((sum, entry) => sum + entry.cogs, 0);
    const totalMargin = window.reportState.allEntries.reduce((sum, entry) => sum + entry.margin, 0);

    // Update the display
    document.getElementById('totalQty').textContent = formatNumber(totalQty);
    document.getElementById('totalDebitQty').textContent = formatNumber(totalDebitQty);
    document.getElementById('totalCreditQty').textContent = formatNumber(totalCreditQty);
    document.getElementById('netQty').textContent = formatNumberWithSign(totalDebitQty - totalCreditQty);
    document.getElementById('totalDebit').textContent = formatDecimal(totalDebit);
    document.getElementById('totalCredit').textContent = formatDecimal(totalCredit);
    document.getElementById('netBalance').textContent = formatDecimalWithSign(totalDebit - totalCredit);
    document.getElementById('totalCOGS').textContent = formatDecimal(totalCOGS);
    document.getElementById('totalMargin').textContent = formatDecimalWithSign(totalMargin);
}

// ==================== UTILITY FUNCTIONS ====================

function formatNumber(value) {
    return value?.toLocaleString() || '0';
}

function formatNumberWithSign(value) {
    return value >= 0 ? formatNumber(value) : `(${formatNumber(Math.abs(value))})`;
}

function formatDecimal(value) {
    return value?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || '0.00';
}

function formatDecimalWithSign(value) {
    return value >= 0 ? formatDecimal(value) : `(${formatDecimal(Math.abs(value))})`;
}