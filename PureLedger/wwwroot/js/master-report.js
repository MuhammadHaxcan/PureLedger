document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    setupEventListeners();
    setDefaultDates();
});

// ==================== STATE MANAGEMENT ====================

function initializeReportState() {
    window.reportState = {
        currentPage: 0,
        pageSize: 5,  // Small for testing, adjust as needed
        isLoading: false,
        hasMoreData: true,
        totalRecords: 0,
        allEntries: [],
        reportParams: null,
        previousBalance: 0
    };
}

// ==================== EVENT LISTENERS ====================

function setupEventListeners() {
    // Form submission
    document.getElementById('masterReportForm').addEventListener('submit', e => {
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
    filterSummaryData('accountGroupData', searchText);
    filterSummaryData('categoryGroupData', searchText);

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
        searchText, 7);
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
        searchText, 5);
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

    // Use routes object instead of hardcoded URL
    PureLedger.get(routes.getCategoriesByProject, { projectId: projectId })
        .done(categories => {
            resetSelect(categorySelect, 'All Categories');
            
            if (categories?.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            }
        })
        .fail(() => {
            resetSelect(categorySelect, 'All Categories');
            showError('Failed to load categories. Please try again.');
        });
}

function loadAccountsForCategory() {
    const categoryId = document.getElementById('categoryId').value;
    const accountSelect = document.getElementById('accountId');

    resetSelect(accountSelect, 'All Accounts', true);

    if (!categoryId) return;

    // Show loading state
    accountSelect.innerHTML = '<option value="" disabled selected>Loading accounts...</option>';

    // Use routes object instead of hardcoded URL
    PureLedger.get(routes.getAccountsByCategory, { categoryId: categoryId })
        .done(accounts => {
            resetSelect(accountSelect, 'All Accounts');
            updateAccountDropdown(accounts);
        })
        .fail(() => {
            resetSelect(accountSelect, 'All Accounts');
            showError('Failed to load accounts. Please try again.');
        });
}

function updateAccountDropdown(accounts) {
    const accountSelect = document.getElementById('accountId');
    resetSelect(accountSelect, 'All Accounts');

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
    resetSelect(accountSelect, 'All Accounts', true);
}

function resetSelect(select, defaultText, disable = false) {
    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

function showError(message) {
    // You could implement a more sophisticated error handling here
    alert(message);
}

// ==================== REPORT GENERATION ====================

function generateReport() {
    // Reset report state
    initializeReportState();

    // Get form values
    const params = {
        StartDate: document.getElementById('startDate').value,
        EndDate: document.getElementById('endDate').value,
        Page: 0,
        PageSize: window.reportState.pageSize
    };

    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;

    if (projectId) params.ProjectId = projectId;
    if (categoryId) params.CategoryId = categoryId;
    if (accountId) params.AccountId = accountId;

    // Store params for subsequent requests
    window.reportState.reportParams = params;

    // Clear existing data
    clearReportData();

    // Show loading indicators
    document.getElementById('reportResults').style.display = 'block';
    document.getElementById('reportData').innerHTML = createLoadingRow(7);

    // Fetch first data chunk
    fetchReportChunk();
}

function clearReportData() {
    document.getElementById('reportData').innerHTML = '';
    document.getElementById('accountGroupData').innerHTML = '';
    document.getElementById('categoryGroupData').innerHTML = '';
    document.getElementById('totalDebit').textContent = '0.00';
    document.getElementById('totalCredit').textContent = '0.00';
    document.getElementById('netBalance').textContent = '0.00';

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
    updateLoadMoreButton();

    // Create request parameters
    const params = { ...window.reportState.reportParams };
    params.Page = window.reportState.currentPage;

    // Prevent caching
    const timestamp = new Date().getTime();
    // Use routes object instead of hardcoded URL
    const url = `${routes.getMasterReportData}?_=${timestamp}`;

    $.ajax({
        url: url,
        method: 'GET',
        data: params,
        dataType: 'json',
        cache: false
    })
        .done(data => {
            // First page initialization
            if (window.reportState.currentPage === 0) {
                initializeDisplay();
            }

            // Process data
            processAndDisplayEntries(data.entries);

            // Update report state
            window.reportState.currentPage++;
            window.reportState.hasMoreData = data.hasMoreData;
            window.reportState.totalRecords = data.totalRecords;
            window.reportState.previousBalance = data.previousBalance || 0;

            // Update UI
            updateLoadingStatus();
        })
        .fail(() => {
            document.getElementById('reportData').innerHTML =   
                '<tr><td colspan="7" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
        })
        .always(() => {
            window.reportState.isLoading = false;
            updateLoadMoreButton();
        });
}

// ==================== DATA PROCESSING ====================

function processAndDisplayEntries(entries) {
    if (!entries?.length) return;

    // Accumulate entries for infinite scroll
    window.reportState.allEntries = [...window.reportState.allEntries, ...entries];

    // Re-render tables
    renderAllEntriesWithBalance();
    generateAccountGroups(window.reportState.allEntries);
    generateCategoryGroups(window.reportState.allEntries);

    // Update totals
    updateTotals();
}

function renderAllEntriesWithBalance() {
    const tbody = document.getElementById('reportData');
    tbody.innerHTML = '';

    const fragment = document.createDocumentFragment();

    window.reportState.allEntries.forEach((entry) => {
        const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                ${isOpening ? '<span class="fw-bold text-primary">OPENING</span>' : `
                <div class="id-container">
                    <a href="/Reporting/PrintVoucher?journalVoucherId=${entry.journalVoucherId}&type=JV" 
                       class="voucher-link" 
                       target="_blank" 
                       title="Open voucher in new window">
                        JV-${entry.journalVoucherId}
                    </a>
                    <span class="entry-id">Entry: ${entry.id}</span>
                </div>
                `}
            </td>
            <td class="date-column">${formatDate(entry.transactionDate)}</td>
            <td class="description-column">${entry.description || ''}</td>
            <td class="account-column">${entry.accountName || ''}</td>
            <td class="debit-column text-right">${formatCurrency(entry.debit)}</td>
            <td class="credit-column text-right">${formatCurrency(entry.credit)}</td>
            <td class="balance-column text-right">${formatCurrency(entry.balance)}</td>
        `;
        if (isOpening) {
            row.classList.add('table-info');
        }
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

function generateAccountGroups(entries) {
    const groupedByAccount = {};

    // Group by account
    entries.forEach(entry => {
        if (!entry.accountId) return;

        const accountKey = entry.accountId.toString();
        if (!groupedByAccount[accountKey]) {
            const formattedId = `${entry.projectId}-${entry.categoryId}-${entry.accountId}`;
            groupedByAccount[accountKey] = {
                debit: 0,
                credit: 0,
                id: formattedId,
                name: entry.accountName
            };
        }

        groupedByAccount[accountKey].debit += entry.debit;
        groupedByAccount[accountKey].credit += entry.credit;
    });

    // Generate rows
    renderSummaryTable('accountGroupData', groupedByAccount);
}

function generateCategoryGroups(entries) {
    const groupedByCategory = {};

    // Group by category
    entries.forEach(entry => {
        if (!entry.categoryId) return;

        const categoryKey = entry.categoryId.toString();
        if (!groupedByCategory[categoryKey]) {
            const formattedId = `${entry.projectId}-${entry.categoryId}`;
            groupedByCategory[categoryKey] = {
                debit: 0,
                credit: 0,
                id: formattedId,
                name: entry.categoryName
            };
        }

        groupedByCategory[categoryKey].debit += entry.debit;
        groupedByCategory[categoryKey].credit += entry.credit;
    });

    // Generate rows
    renderSummaryTable('categoryGroupData', groupedByCategory);
}

function renderSummaryTable(tableId, groupedData) {
    const tbody = document.getElementById(tableId);
    tbody.innerHTML = '';

    if (Object.keys(groupedData).length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">No data available</td></tr>';
        return;
    }

    // Use DocumentFragment for better performance
    const fragment = document.createDocumentFragment();

    Object.values(groupedData).forEach(data => {
        const balance = data.debit - data.credit;
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${data.id}</td>
            <td>${data.name}</td>
            <td class="text-right">${formatCurrency(data.debit)}</td>
            <td class="text-right">${formatCurrency(data.credit)}</td>
            <td class="text-right">${formatCurrency(balance)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    // Reset data and balance
    document.getElementById('reportData').innerHTML = '';
    window.reportState.runningBalance = 0;

    // Clear summary tables
    document.getElementById('accountGroupData').innerHTML = '';
    document.getElementById('categoryGroupData').innerHTML = '';

    // Remove any existing status indicators
    removeElements(['loadingStatus', 'autoLoadIndicator']);

    // Add loading status indicator
    addLoadingStatus();
    addAutoLoadIndicator();
}

function addLoadingStatus() {
    const tableContainer = document.querySelector('#masterReportTable').closest('.table-responsive');
    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded 0 of ${window.reportState.totalRecords} records`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function addAutoLoadIndicator() {
    const tableContainer = document.querySelector('#masterReportTable').closest('.table-responsive');
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
    // Remove existing status
    const existingStatus = document.getElementById('loadingStatus');
    if (existingStatus) {
        existingStatus.remove();
    }

    // Create new status
    const tableContainer = document.querySelector('#masterReportTable').closest('.table-responsive');
    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded ${window.reportState.allEntries.length} of ${window.reportState.totalRecords} records`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function updateLoadMoreButton() {
    const loadMoreSpinner = document.getElementById('loadMoreSpinner');
    if (!loadMoreSpinner) return;

    if (window.reportState.isLoading) {
        loadMoreSpinner.classList.remove('d-none');
    } else {
        loadMoreSpinner.classList.add('d-none');
    }

    // Update loading status
    updateLoadingStatus();
}

function updateTotals() {
    // Calculate totals from all loaded entries
    const totalDebit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.debit, 0);
    const totalCredit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.credit, 0);
    const netBalance = totalDebit - totalCredit;

    // Update the display
    document.getElementById('totalDebit').textContent = formatCurrency(totalDebit);
    document.getElementById('totalCredit').textContent = formatCurrency(totalCredit);
    document.getElementById('netBalance').textContent = formatCurrency(netBalance);
}

// ==================== UTILITY FUNCTIONS ====================

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value);
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit'
    });
}