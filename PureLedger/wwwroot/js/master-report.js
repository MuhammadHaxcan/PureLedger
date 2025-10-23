document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    setupEventListeners();
    setDefaultDates();
    initializeDropdownStates();
    loadAllBookers();
});

// ==================== DROPDOWN LOCKING LOGIC ====================

function initializeDropdownStates() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    resetSelect(categoryId, 'All Categories', true);
    resetSelect(accountId, 'All Accounts', true);

    if (projectId.value) {
        categoryId.disabled = false;
    }
}

function handleProjectChange() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    if (projectId.value) {
        resetSelect(categoryId, 'All Categories', false);
        resetSelect(accountId, 'All Accounts', true);
        loadCategoriesForProject();
    } else {
        resetSelect(categoryId, 'All Categories', true);
        resetSelect(accountId, 'All Accounts', true);
    }
}

function handleCategoryChange() {
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    if (categoryId.value) {
        resetSelect(accountId, 'All Accounts', false);
        loadAccountsForCategory();
    } else {
        resetSelect(accountId, 'All Accounts', true);
    }
}

// ==================== STATE MANAGEMENT ====================

function initializeReportState() {
    window.reportState = {
        currentPage: 0,
        pageSize: 5,
        isLoading: false,
        hasMoreData: true,
        totalRecords: 0,
        allEntries: [],
        reportParams: null,
        previousBalance: 0,
        visibleColumns: {
            date: true,
            creditableDate: true,
            description: true,
            account: true,
            booker: true,
            quantity: true,
            debit: true,
            credit: true,
            balance: true
        }
    };
}

// ==================== EVENT LISTENERS ====================

function setupEventListeners() {
    document.getElementById('masterReportForm').addEventListener('submit', e => {
        e.preventDefault();
        generateReport();
    });

    initColumnToggles();
    initSearchFunctionality();

    document.getElementById('projectId').addEventListener('change', handleProjectChange);
    document.getElementById('categoryId').addEventListener('change', handleCategoryChange);

    window.addEventListener('scroll', throttle(checkIfShouldLoadMore, 100));
}

function setDefaultDates() {
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
}

// ==================== UI CONTROLS ====================

function initColumnToggles() {
    const columnMap = {
        'showDate': 'date',
        'showCreditableDate': 'creditableDate',
        'showDescription': 'description',
        'showAccount': 'account',
        'showBooker': 'booker',
        'showQuantity': 'quantity',
        'showDebit': 'debit',
        'showCredit': 'credit',
        'showBalance': 'balance'
    };

    document.querySelectorAll('.column-toggles input[type="checkbox"]').forEach(checkbox => {
        checkbox.addEventListener('change', function () {
            const columnKey = columnMap[this.id];
            const isVisible = this.checked;

            // Update state
            window.reportState.visibleColumns[columnKey] = isVisible;

            // Apply visibility
            updateColumnVisibility();
        });
    });

    // Initial visibility setup
    updateColumnVisibility();
}

function updateColumnVisibility() {
    const columnClasses = {
        date: 'date-column',
        creditableDate: 'creditable-date-column',
        description: 'description-column',
        account: 'account-column',
        booker: 'booker-column',
        quantity: 'quantity-column',
        debit: 'debit-column',
        credit: 'credit-column',
        balance: 'balance-column'
    };

    // Update all column cells
    Object.keys(columnClasses).forEach(key => {
        const isVisible = window.reportState.visibleColumns[key];
        const displayValue = isVisible ? '' : 'none';

        document.querySelectorAll('.' + columnClasses[key]).forEach(cell => {
            cell.style.display = displayValue;
        });
    });

    // Update footer colspan
    updateFooterColspan();
}

function updateFooterColspan() {
    const footerTotalCell = document.querySelector('#masterReportTable tfoot td:first-child');
    if (!footerTotalCell) return;

    // Count visible columns before the numeric columns
    // Fixed columns: ID (1)
    // Optional columns: date, creditableDate, description, account, booker
    let colspan = 1; // Start with ID column

    if (window.reportState.visibleColumns.date) colspan++;
    if (window.reportState.visibleColumns.creditableDate) colspan++;
    if (window.reportState.visibleColumns.description) colspan++;
    if (window.reportState.visibleColumns.account) colspan++;
    if (window.reportState.visibleColumns.booker) colspan++;

    footerTotalCell.setAttribute('colspan', colspan);
}

function initSearchFunctionality() {
    const globalSearchInput = document.getElementById('globalReportSearch');
    if (globalSearchInput) {
        globalSearchInput.addEventListener('keyup', debounce(performSearch, 250));
        // Removed clear button logic
    }

    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.addEventListener('keyup', debounce(function () {
            filterReportData(this.value.toLowerCase());
        }, 250));
    }
}

function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

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

    filterReportData(searchText);
    filterSummaryData('accountGroupData', searchText);
    filterSummaryData('categoryGroupData', searchText);

    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.value = searchText;
    }
}

function filterReportData(searchText) {
    const rows = document.getElementById('reportData').getElementsByTagName('tr');
    let hasVisibleRows = false;

    for (const row of rows) {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }

    handleNoMatchingRows('reportData', 'noSearchResults', !hasVisibleRows && rows.length > 0,
        searchText, 10); // Max possible columns
}

function filterSummaryData(tableId, searchText) {
    const rows = document.getElementById(tableId).getElementsByTagName('tr');
    let hasVisibleRows = false;

    for (const row of rows) {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }

    handleNoMatchingRows(tableId, 'noResults' + tableId, !hasVisibleRows && rows.length > 0,
        searchText, 5);
}

function handleNoMatchingRows(tableId, noResultsId, shouldShowNoResults, searchText, colSpan) {
    const previousNoResults = document.getElementById(noResultsId);
    if (previousNoResults) previousNoResults.remove();

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

    resetSelect(categorySelect, 'All Categories', true);
    resetAccountDropdown();

    if (!projectId) return;

    categorySelect.innerHTML = '<option value="" disabled selected>Loading categories...</option>';

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

    accountSelect.innerHTML = '<option value="" disabled selected>Loading accounts...</option>';

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

function loadAllBookers() {
    const bookerSelect = document.getElementById('bookerId');

    bookerSelect.innerHTML = '<option value="" disabled selected>Loading bookers...</option>';

    fetch('/Reporting/GetAllBookers')
        .then(response => response.json())
        .then(bookers => {
            resetSelect(bookerSelect, 'All Bookers');

            if (bookers?.length > 0) {
                bookers.forEach(booker => {
                    const option = document.createElement('option');
                    option.value = booker.id;
                    option.textContent = booker.name;
                    bookerSelect.appendChild(option);
                });
            }
        })
        .catch(error => {
            console.error('Error loading bookers:', error);
            resetSelect(bookerSelect, 'All Bookers');
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
    alert(message);
}

// ==================== REPORT GENERATION ====================

function generateReport() {
    initializeReportState();

    const startDateValue = document.getElementById('startDate').value;
    const endDateValue = document.getElementById('endDate').value;

    const params = {
        StartDate: startDateValue ? startDateValue : null,
        EndDate: endDateValue ? endDateValue : null,
        Page: 0,
        PageSize: window.reportState.pageSize
    };

    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;
    const bookerId = document.getElementById('bookerId').value;

    if (projectId) params.ProjectId = projectId;
    if (categoryId) params.CategoryId = categoryId;
    if (accountId) params.AccountId = accountId;
    if (bookerId) params.BookerId = bookerId;

    window.reportState.reportParams = params;

    clearReportData();

    document.getElementById('reportResults').style.display = 'block';
    document.getElementById('reportData').innerHTML = createLoadingRow(10);

    fetchReportChunk();
}

function clearReportData() {
    document.getElementById('reportData').innerHTML = '';
    document.getElementById('accountGroupData').innerHTML = '';
    document.getElementById('categoryGroupData').innerHTML = '';
    document.getElementById('totalQuantity').textContent = '0';
    document.getElementById('totalDebit').textContent = '0.00';
    document.getElementById('totalCredit').textContent = '0.00';
    document.getElementById('netBalance').textContent = '0.00';

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

    // Replace deprecated pageYOffset with scrollY
    const scrollPosition = window.innerHeight + window.scrollY;
    const bodyHeight = document.body.offsetHeight;
    const scrollThreshold = bodyHeight - 500;

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

    const params = { ...window.reportState.reportParams };
    params.Page = window.reportState.currentPage;

    const timestamp = new Date().getTime();
    const url = `${routes.getMasterReportData}?_=${timestamp}`;

    $.ajax({
        url: url,
        method: 'GET',
        data: params,
        dataType: 'json',
        cache: false
    })
        .done(data => {
            if (window.reportState.currentPage === 0) {
                initializeDisplay();
            }

            processAndDisplayEntries(data.entries);

            window.reportState.currentPage++;
            window.reportState.hasMoreData = data.hasMoreData;
            window.reportState.totalRecords = data.totalRecords;
            window.reportState.previousBalance = data.previousBalance || 0;

            updateLoadingStatus();
        })
        .fail(() => {
            document.getElementById('reportData').innerHTML =
                '<tr><td colspan="10" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
        })
        .always(() => {
            window.reportState.isLoading = false;
            updateLoadMoreButton();
        });
}

// ==================== DATA PROCESSING ====================

function processAndDisplayEntries(entries) {
    if (!entries?.length) return;

    window.reportState.allEntries = [...window.reportState.allEntries, ...entries];

    renderAllEntriesWithBalance();
    generateAccountGroups(window.reportState.allEntries);
    generateCategoryGroups(window.reportState.allEntries);

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
            <td class="creditable-date-column">${formatDate(entry.creditableDate)}</td>
            <td class="description-column">${entry.description || ''}</td>
            <td class="account-column">${entry.accountName || ''}</td>
            <td class="booker-column">${entry.bookerName || ''}</td>
            <td class="quantity-column text-end">${entry.quantity ?? ''}</td>
            <td class="debit-column text-end">${formatCurrency(entry.debit)}</td>
            <td class="credit-column text-end">${formatCurrency(entry.credit)}</td>
            <td class="balance-column text-end">${formatCurrency(entry.balance)}</td>
        `;
        if (isOpening) {
            row.classList.add('table-info');
        }
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
    updateColumnVisibility();
}

function generateAccountGroups(entries) {
    const groupedByAccount = {};

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

    renderSummaryTable('accountGroupData', groupedByAccount);
}

function generateCategoryGroups(entries) {
    const groupedByCategory = {};

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

    renderSummaryTable('categoryGroupData', groupedByCategory);
}

function renderSummaryTable(tableId, groupedData) {
    const tbody = document.getElementById(tableId);
    tbody.innerHTML = '';

    if (Object.keys(groupedData).length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">No data available</td></tr>';
        return;
    }

    const fragment = document.createDocumentFragment();

    Object.values(groupedData).forEach(data => {
        const balance = data.debit - data.credit;
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${data.id}</td>
            <td>${data.name}</td>
            <td class="text-end">${formatCurrency(data.debit)}</td>
            <td class="text-end">${formatCurrency(data.credit)}</td>
            <td class="text-end">${formatCurrency(balance)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    document.getElementById('reportData').innerHTML = '';
    window.reportState.runningBalance = 0;

    document.getElementById('accountGroupData').innerHTML = '';
    document.getElementById('categoryGroupData').innerHTML = '';

    removeElements(['loadingStatus', 'autoLoadIndicator']);

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
    const existingStatus = document.getElementById('loadingStatus');
    if (existingStatus) {
        existingStatus.remove();
    }

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

    updateLoadingStatus();
}

function updateTotals() {
    const totalQuantity = window.reportState.allEntries.reduce((sum, entry) => sum + (entry.quantity || 0), 0);
    const totalDebit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.debit, 0);
    const totalCredit = window.reportState.allEntries.reduce((sum, entry) => sum + entry.credit, 0);
    const netBalance = totalDebit - totalCredit;

    document.getElementById('totalQuantity').textContent = totalQuantity;
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