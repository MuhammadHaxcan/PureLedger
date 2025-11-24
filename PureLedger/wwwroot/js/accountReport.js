/**
 * Account Report JavaScript
 * Handles all functionality for the Account Report page including:
 * - Searchable dropdown filters
 * - Data fetching and rendering with auto-loading
 * - Project requirement validation
 * - Total row calculations
 * - CSV export (added)
 */

document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    initializeSelectDropdowns();
    setupEventListeners();
});

// ==================== STATE MANAGEMENT ====================

function initializeReportState() {
    window.reportState = {
        currentPage: 0,
        pageSize: 500,
        isLoading: false,
        hasMoreData: true,
        totalRecords: 0,
        allEntries: [],
        reportParams: null,
        grandTotals: {
            totalDebit: 0,
            totalCredit: 0,
            totalBalance: 0
        }
    };
}

// ==================== SEARCHABLE DROPDOWN LOGIC ====================

function initializeSelectDropdowns() {
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');
    convertToSearchableSelect('accountId', 'Search account...');

    populateDropdownOptions();
    initializeDropdownStates();
}

function convertToSearchableSelect(selectId, placeholder) {
    const select = document.getElementById(selectId);
    if (!select) return;

    if (select.parentElement.classList.contains('select-container')) return;

    let iconClass = 'fa-list';
    if (selectId === 'projectId') iconClass = 'fa-project-diagram';
    else if (selectId === 'categoryId') iconClass = 'fa-sitemap';
    else if (selectId === 'accountId') iconClass = 'fa-user';

    const inputGroup = select.closest('.input-group');
    const selectParent = inputGroup ? inputGroup.parentElement : select.parentElement;

    const container = document.createElement('div');
    container.className = 'select-container';

    const newInputGroup = document.createElement('div');
    newInputGroup.className = 'input-group';

    const iconSpan = document.createElement('span');
    iconSpan.className = 'input-group-text';
    iconSpan.innerHTML = `<i class="fas ${iconClass}"></i>`;

    const searchInput = document.createElement('input');
    searchInput.type = 'text';
    searchInput.className = `form-control select-search ${selectId}-search`;
    searchInput.placeholder = placeholder;
    searchInput.autocomplete = 'off';
    if (select.disabled) searchInput.disabled = true;

    const dropdownDiv = document.createElement('div');
    dropdownDiv.className = `select-dropdown ${selectId}-dropdown`;

    const clonedSelect = select.cloneNode(true);
    clonedSelect.className = 'form-select d-none';

    newInputGroup.appendChild(iconSpan);
    newInputGroup.appendChild(searchInput);
    container.appendChild(newInputGroup);
    container.appendChild(clonedSelect);
    container.appendChild(dropdownDiv);

    selectParent.replaceChild(container, inputGroup || select);
}

function populateDropdownOptions() {
    document.querySelectorAll('.select-container').forEach(container => {
        const select = container.querySelector('select');
        const dropdown = container.querySelector('.select-dropdown');

        if (select && dropdown) {
            dropdown.innerHTML = '';

            Array.from(select.options).forEach(option => {
                const div = document.createElement('div');
                div.className = 'select-option';
                div.dataset.value = option.value;
                div.textContent = option.textContent;
                dropdown.appendChild(div);
            });
        }
    });
}

function initializeDropdownStates() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    if (!projectId || !categoryId || !accountId) return;

    const $categorySearch = $('.category-search');
    const $accountSearch = $('.account-search');

    if ($categorySearch.length) $categorySearch.prop('disabled', true);
    if ($accountSearch.length) $accountSearch.prop('disabled', true);

    resetSelect(categoryId, 'All Categories', true);
    resetSelect(accountId, 'All Accounts', true);

    if (projectId.value) {
        categoryId.disabled = false;
        if ($categorySearch.length) $categorySearch.prop('disabled', false);
    }
}

function setupSelectSearch() {
    $(document).on('input', '.select-search', function () {
        const $input = $(this);

        if ($input.prop('disabled')) {
            return;
        }

        const $container = $input.closest('.select-container');
        const $dropdown = $container.find('.select-dropdown');
        const searchTerm = $input.val().toLowerCase();

        let visibleCount = 0;
        $dropdown.find('.select-option').each(function () {
            const text = $(this).text().toLowerCase();
            const shouldShow = text.includes(searchTerm) && visibleCount < 8;
            $(this).toggleClass('filtered-out', !shouldShow);
            if (shouldShow) visibleCount++;
        });

        $dropdown.addClass('show');
    });

    $(document).on('focus', '.select-search', function () {
        const $input = $(this);

        if ($input.prop('disabled')) {
            return;
        }

        $('.select-dropdown').removeClass('show');

        const $container = $input.closest('.select-container');
        const $dropdown = $container.find('.select-dropdown');
        const $select = $container.find('select');
        const currentValue = $select.val();

        $dropdown.find('.select-option').removeClass('selected filtered-out');

        if (currentValue) {
            const $selectedOption = $dropdown.find(`.select-option[data-value="${currentValue}"]`);
            if ($selectedOption.length) {
                $selectedOption.addClass('selected');
            }
        }

        $dropdown.addClass('show');
    });

    $(document).on('keydown', '.select-search', function (e) {
        const $dropdown = $(this).closest('.select-container').find('.select-dropdown');
        const $options = $dropdown.find('.select-option:not(.filtered-out)');
        let $selected = $dropdown.find('.select-option.selected');

        if (e.key === 'Tab' && $dropdown.hasClass('show')) {
            $dropdown.removeClass('show');
            return;
        }

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (!$selected.length) {
                    $options.first().addClass('selected');
                } else {
                    const $next = $selected.nextAll('.select-option:not(.filtered-out)').first();
                    if ($next.length) {
                        $selected.removeClass('selected');
                        $next.addClass('selected');
                    }
                }
                break;

            case 'ArrowUp':
                e.preventDefault();
                if (!$selected.length) {
                    $options.last().addClass('selected');
                } else {
                    const $prev = $selected.prevAll('.select-option:not(.filtered-out)').first();
                    if ($prev.length) {
                        $selected.removeClass('selected');
                        $prev.addClass('selected');
                    }
                }
                break;

            case 'Enter':
                if ($dropdown.hasClass('show')) {
                    e.preventDefault();
                    $selected = $dropdown.find('.select-option.selected');
                    if ($selected.length) {
                        selectOption($selected, $(this).closest('.select-container'));
                    } else {
                        const $firstOption = $options.first();
                        if ($firstOption.length) {
                            selectOption($firstOption, $(this).closest('.select-container'));
                        }
                    }
                }
                break;

            case 'Escape':
                e.preventDefault();
                $dropdown.removeClass('show');
                break;
        }
    });

    $(document).on('click', '.select-option:not(.filtered-out)', function () {
        selectOption($(this), $(this).closest('.select-container'));
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('.select-container').length) {
            $('.select-dropdown').removeClass('show');
        }
    });
}

function selectOption($option, $container) {
    const value = $option.data('value');
    const text = $option.text();

    const $select = $container.find('select');
    $select.val(value).trigger('change');

    $container.find('.select-search').val(text);

    $container.find('.select-dropdown').removeClass('show');
    $container.find('.select-option').removeClass('selected');
    $option.addClass('selected');
}

// ==================== DROPDOWN LOCKING LOGIC ====================

function handleProjectChange() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');

    const $categoryContainer = $('.select-container').has('#categoryId');
    const $categorySearch = $categoryContainer.find('.select-search');
    const $categoryDropdown = $categoryContainer.find('.select-dropdown');

    if (!projectId || !categoryId) return;

    if (projectId.value) {
        resetSelect(categoryId, 'All Categories', false);
        $categorySearch.val('').prop('disabled', false);
        resetAccountDropdown();
        loadCategoriesForProject();
    } else {
        resetSelect(categoryId, 'All Categories', true);
        $categorySearch.val('').prop('disabled', true);
        if ($categoryDropdown.length) $categoryDropdown.empty();
        resetAccountDropdown();
    }
}

function handleCategoryChange() {
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    const $accountContainer = $('.select-container').has('#accountId');
    const $accountSearch = $accountContainer.find('.select-search');
    const $accountDropdown = $accountContainer.find('.select-dropdown');

    if (!categoryId || !accountId) return;

    if (categoryId.value) {
        resetSelect(accountId, 'All Accounts', false);
        $accountSearch.val('').prop('disabled', false);
        loadAccountsForCategory();
    } else {
        resetSelect(accountId, 'All Accounts', true);
        $accountSearch.val('').prop('disabled', true);
        if ($accountDropdown.length) $accountDropdown.empty();
    }
}

// ==================== EVENT LISTENERS ====================

function setupEventListeners() {
    const form = document.getElementById('accountReportForm');
    if (form) {
        form.addEventListener('submit', e => {
            e.preventDefault();
            setGenerateReportEnabled(false);
            generateReport();
        });
    }

    setupSelectSearch();
    initSearchFunctionality();
    initializeExportButton();

    $(document).on('change', '#projectId', handleProjectChange);
    $(document).on('change', '#categoryId', handleCategoryChange);
}

function initSearchFunctionality() {
    const searchBox = document.getElementById('searchBox');
    if (searchBox) {
        searchBox.addEventListener('input', debounce(function () {
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

// ==================== SEARCH FUNCTIONALITY ====================

function filterReportData(searchText) {
    const reportData = document.getElementById('reportData');
    if (!reportData) return;

    const existingNoResults = document.getElementById('noSearchResults');
    if (existingNoResults) existingNoResults.remove();

    const rows = Array.from(reportData.getElementsByTagName('tr'));
    let hasVisibleRows = false;
    let filteredTotals = { totalDebit: 0, totalCredit: 0, totalBalance: 0 };

    rows.forEach(row => {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';

        if (isVisible) {
            hasVisibleRows = true;
            const cells = row.querySelectorAll('td');
            if (cells.length >= 6) {
                const debit = parseFloat(cells[3].textContent.replace(/,/g, '')) || 0;
                const credit = parseFloat(cells[4].textContent.replace(/,/g, '')) || 0;
                const balance = parseFloat(cells[5].textContent.replace(/,/g, '')) || 0;

                filteredTotals.totalDebit += debit;
                filteredTotals.totalCredit += credit;
                filteredTotals.totalBalance += balance;
            }
        }
    });

    if (searchText.length > 0) {
        updateTotalRow(filteredTotals.totalDebit, filteredTotals.totalCredit, filteredTotals.totalBalance);
    } else {
        updateTotalRow(
            window.reportState.grandTotals.totalDebit,
            window.reportState.grandTotals.totalCredit,
            window.reportState.grandTotals.totalBalance
        );
    }

    if (!hasVisibleRows && rows.length > 0 && searchText.length > 0) {
        const noResultsRow = document.createElement('tr');
        noResultsRow.id = 'noSearchResults';
        noResultsRow.innerHTML = '<td colspan="6" class="text-center">No matching accounts found</td>';
        reportData.appendChild(noResultsRow);

        updateTotalRow(0, 0, 0);
    }
}

// ==================== DROPDOWNS & DATA LOADING ====================

function loadCategoriesForProject() {
    const projectId = $('#projectId').val();

    const $categoryContainer = $('.select-container').has('#categoryId');
    const $categorySearch = $categoryContainer.find('.select-search');
    const $categoryDropdown = $categoryContainer.find('.select-dropdown');
    const categorySelect = document.getElementById('categoryId');

    resetSelect(categorySelect, 'All Categories', true);
    if ($categorySearch.length) $categorySearch.val('').prop('disabled', true);
    if ($categoryDropdown.length) $categoryDropdown.empty();
    resetAccountDropdown();

    if (!projectId) return;

    if ($categoryDropdown.length) {
        $categoryDropdown.html('<div class="select-option">Loading categories...</div>');
    }

    $.ajax({
        url: routes.getCategoriesByProject,
        data: { projectId: projectId },
        method: 'GET',
        dataType: 'json',
        success: function (categories) {
            resetSelect(categorySelect, 'All Categories');
            categorySelect.disabled = false;
            if ($categorySearch.length) $categorySearch.prop('disabled', false);

            if ($categoryDropdown.length) {
                $categoryDropdown.empty();
                $categoryDropdown.append('<div class="select-option" data-value="">All Categories</div>');
            }

            if (categories?.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);

                    if ($categoryDropdown.length) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = category.id;
                        div.textContent = category.name;
                        $categoryDropdown.append(div);
                    }
                });
            }
            categorySelect.value = "";
            $categorySearch.val('All Categories');
        },
        error: function () {
            resetSelect(categorySelect, 'All Categories');
            if ($categorySearch.length) $categorySearch.prop('disabled', true);
            if ($categoryDropdown.length) $categoryDropdown.empty();
            showError('Failed to load categories. Please try again.');
        }
    });
}

function loadAccountsForCategory() {
    const categoryId = $('#categoryId').val();

    const $accountContainer = $('.select-container').has('#accountId');
    const $accountSearch = $accountContainer.find('.select-search');
    const $accountDropdown = $accountContainer.find('.select-dropdown');
    const accountSelect = document.getElementById('accountId');

    resetSelect(accountSelect, 'All Accounts', true);
    if ($accountSearch.length) $accountSearch.val('').prop('disabled', true);
    if ($accountDropdown.length) $accountDropdown.empty();

    if (!categoryId) return;

    if ($accountDropdown.length) {
        $accountDropdown.html('<div class="select-option">Loading accounts...</div>');
    }

    $.ajax({
        url: routes.getAccountsByCategory,
        data: { categoryId: categoryId },
        method: 'GET',
        dataType: 'json',
        success: function (accounts) {
            resetSelect(accountSelect, 'All Accounts');
            accountSelect.disabled = false;
            if ($accountSearch.length) $accountSearch.prop('disabled', false);

            if ($accountDropdown.length) {
                $accountDropdown.empty();
                $accountDropdown.append('<div class="select-option" data-value="">All Accounts</div>');
            }

            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);

                    if ($accountDropdown.length) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = account.id;
                        div.textContent = account.name;
                        $accountDropdown.append(div);
                    }
                });
            }
            accountSelect.value = "";
            $accountSearch.val('All Accounts');
        },
        error: function () {
            resetSelect(accountSelect, 'All Accounts');
            if ($accountSearch.length) $accountSearch.prop('disabled', true);
            if ($accountDropdown.length) $accountDropdown.empty();
            showError('Failed to load accounts. Please try again.');
        }
    });
}

function resetAccountDropdown() {
    const accountSelect = document.getElementById('accountId');
    if (!accountSelect) return;

    const $accountContainer = $('.select-container').has('#accountId');
    const $accountSearch = $accountContainer.find('.select-search');
    const $accountDropdown = $accountContainer.find('.select-dropdown');

    resetSelect(accountSelect, 'All Accounts', true);
    if ($accountSearch.length) $accountSearch.val('').prop('disabled', true);
    if ($accountDropdown.length) $accountDropdown.empty();
}

function resetSelect(select, defaultText, disable = false) {
    if (!select) return;

    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

function showError(message) {
    const alertArea = document.getElementById('alertArea');
    if (!alertArea) {
        console.error(message);
        return;
    }

    alertArea.innerHTML = `
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <i class="fas fa-exclamation-circle me-2"></i> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;
}

// ==================== REPORT GENERATION ====================

function setSearchBoxEnabled(enabled) {
    const searchBox = document.getElementById('searchBox');
    if (searchBox) searchBox.disabled = !enabled;
}

function setGenerateReportEnabled(enabled) {
    const btn = document.querySelector('#accountReportForm button[type="submit"]');
    if (btn) btn.disabled = !enabled;
}

function generateReport() {
    const searchBox = document.getElementById('searchBox');
    if (searchBox && searchBox.value.trim() !== '') {
        searchBox.value = '';
    }

    initializeReportState();

    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    if (!projectId) {
        showError('Project field not found.');
        setGenerateReportEnabled(true);
        return;
    }

    if (!projectId.value) {
        showError('Project selection is required.');
        setGenerateReportEnabled(true);
        return;
    }

    const params = {
        ProjectId: projectId.value,
        CategoryId: categoryId ? categoryId.value : '',
        AccountId: accountId ? accountId.value : '',
        Page: 0,
        PageSize: window.reportState.pageSize
    };

    window.reportState.reportParams = params;

    clearReportData();

    setSearchBoxEnabled(false);

    const reportResults = document.getElementById('reportResults');
    if (reportResults) reportResults.style.display = 'block';

    const reportData = document.getElementById('reportData');
    if (reportData) reportData.innerHTML = createLoadingRow(6);

    fetchReportChunk(true);
}

function clearReportData() {
    updateElement('reportData', '');
    updateReportSummary(0);
    updateTotalRow(0, 0, 0);
    removeElements(['loadingStatus', 'autoLoadIndicator']);
}

function updateElement(id, content) {
    const element = document.getElementById(id);
    if (element) {
        element.innerHTML = content;
    }
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

// ==================== AUTO-LOAD DATA FETCHING ====================

function fetchReportChunk(autoLoad) {
    if (window.reportState.isLoading || !window.reportState.hasMoreData) {
        return;
    }

    window.reportState.isLoading = true;
    updateLoadMoreButton();

    const params = { ...window.reportState.reportParams };
    params.Page = window.reportState.currentPage;

    const timestamp = new Date().getTime();
    const url = `${routes.getAccountReportData}?_=${timestamp}`;

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

            processAndDisplayEntries(data.entries || []);

            window.reportState.currentPage++;
            window.reportState.hasMoreData = data.hasMoreData || false;
            window.reportState.totalRecords = data.totalRecords || 0;

            updateLoadingStatus();
        })
        .fail(() => {
            const reportData = document.getElementById('reportData');
            if (reportData) {
                reportData.innerHTML = '<tr><td colspan="6" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
            }
            setGenerateReportEnabled(true);
        })
        .always(() => {
            window.reportState.isLoading = false;
            updateLoadMoreButton();

            if (!window.reportState.hasMoreData && !window.reportState.isLoading) {
                setSearchBoxEnabled(true);
                setGenerateReportEnabled(true);
            }

            const searchBox = document.getElementById('searchBox');
            const isSearching = searchBox && searchBox.value.trim() !== '';
            if (autoLoad && window.reportState.hasMoreData && !isSearching) {
                fetchReportChunk(true);
            }
        });
}

// ==================== DATA PROCESSING ====================

function processAndDisplayEntries(entries) {
    if (!entries?.length) return;

    window.reportState.allEntries = [...window.reportState.allEntries, ...entries];
    renderAllEntries();
}

function renderAllEntries() {
    const tbody = document.getElementById('reportData');
    if (!tbody) return;

    if (!window.reportState.allEntries.length) {
        tbody.innerHTML = '<tr><td colspan="6" class="text-center">No accounts found</td></tr>';
        updateReportSummary(0);
        updateTotalRow(0, 0, 0);
        updateExportButtonState();
        return;
    }

    const fragment = document.createDocumentFragment();
    let totalDebit = 0;
    let totalCredit = 0;
    let totalBalance = 0;

    window.reportState.allEntries.forEach(entry => {
        const debit = parseFloat(entry.totalDebit) || 0;
        const credit = parseFloat(entry.totalCredit) || 0;
        const balance = parseFloat(entry.netBalance) || 0;

        totalDebit += debit;
        totalCredit += credit;
        totalBalance += balance;

        const projectId = entry.projectId || entry.ProjectId || '';
        const categoryId = entry.categoryId || entry.CategoryId || '';
        const accountId = entry.accountId || entry.AccountId || entry.AccountIdString || entry.AccountId || '';
        const compositeId = (projectId || categoryId) ? `${projectId}-${categoryId}-${accountId}` : (accountId || '');

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${compositeId}</td>
            <td>${entry.categoryName || '-'}</td>
            <td>${entry.accountName}</td>
            <td class="text-end">${formatCurrency(debit)}</td>
            <td class="text-end">${formatCurrency(credit)}</td>
            <td class="text-end ${balance < 0 ? 'text-danger' : 'text-success'}">${formatCurrency(balance)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.innerHTML = '';
    tbody.appendChild(fragment);

    window.reportState.grandTotals = {
        totalDebit: totalDebit,
        totalCredit: totalCredit,
        totalBalance: totalBalance
    };

    updateReportSummary(window.reportState.allEntries.length);
    updateTotalRow(totalDebit, totalCredit, totalBalance);

    // Update export button state after entries rendered
    updateExportButtonState();
}

// ==================== CSV EXPORT (NEW) ====================

function initializeExportButton() {
    // use delegated handler so button can be created dynamically inside reportSummary
    $(document).off('click', '#exportAccountCsvBtn').on('click', '#exportAccountCsvBtn', function (e) {
        e.preventDefault();
        const btn = this;
        if (btn.disabled) return;
        exportAccountReportToCsv();
    });

    // ensure button state updated on load
    updateExportButtonState();
}

function updateExportButtonState() {
    const btn = document.getElementById('exportAccountCsvBtn');
    if (!btn) return;

    const ready = !window.reportState.isLoading && !window.reportState.hasMoreData
        && Array.isArray(window.reportState.allEntries) && window.reportState.allEntries.length > 0;

    btn.disabled = !ready;
}

function csvEscape(value) {
    if (value === null || value === undefined) return '';
    const str = String(value);
    return `"${str.replace(/"/g, '""')}"`;
}

function formatNumericForCsv(value) {
    if (value === null || value === undefined || value === '') return '';
    const n = Number(value);
    if (isNaN(n)) return String(value);
    return n.toFixed(2);
}

function exportAccountReportToCsv() {
    const entries = window.reportState?.allEntries || [];
    if (!entries.length) {
        alert('No account data to export.');
        return;
    }

    const headers = ['ID', 'Category', 'Account Name', 'Debit', 'Credit', 'Balance'];
    const rows = [headers.map(csvEscape).join(',')];

    entries.forEach(e => {
        // Build composite ID same as UI
        const projectId = e.projectId || e.ProjectId || '';
        const categoryId = e.categoryId || e.CategoryId || '';
        const accountId = e.accountId || e.AccountId || e.AccountIdString || e.AccountId || '';
        const compositeId = (projectId || categoryId) ? `${projectId}-${categoryId}-${accountId}` : (accountId || '');

        const id = compositeId ?? '';
        const category = e.categoryName ?? '';
        const name = e.accountName ?? '';
        const debit = formatNumericForCsv(e.totalDebit);
        const credit = formatNumericForCsv(e.totalCredit);
        const balance = formatNumericForCsv(e.netBalance);

        rows.push([csvEscape(id), csvEscape(category), csvEscape(name), csvEscape(debit), csvEscape(credit), csvEscape(balance)].join(','));
    });

    const csvContent = rows.join('\r\n');
    downloadCsvBlob(csvContent, `AccountReport_${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
}

function downloadCsvBlob(content, filename) {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    if (navigator.msSaveBlob) {
        navigator.msSaveBlob(blob, filename);
    } else {
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        link.setAttribute('href', url);
        link.setAttribute('download', filename);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }
}

// ==================== TOTAL ROW MANAGEMENT ====================

function updateTotalRow(totalDebit, totalCredit, totalBalance) {
    const totalDebitElement = document.getElementById('totalDebit');
    const totalCreditElement = document.getElementById('totalCredit');
    const totalBalanceElement = document.getElementById('totalBalance');

    if (totalDebitElement) {
        totalDebitElement.textContent = formatCurrency(totalDebit);
    }

    if (totalCreditElement) {
        totalCreditElement.textContent = formatCurrency(totalCredit);
    }

    if (totalBalanceElement) {
        totalBalanceElement.textContent = formatCurrency(totalBalance);
        totalBalanceElement.className = totalBalance < 0 ? 'text-end text-danger' : 'text-end text-success';
    }
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    updateElement('reportData', '');
    removeElements(['loadingStatus', 'autoLoadIndicator']);
    addLoadingStatus();
    addAutoLoadIndicator();
}

function addLoadingStatus() {
    const tableContainer = document.querySelector('#accountReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded 0 of ${window.reportState.totalRecords} accounts`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function addAutoLoadIndicator() {
    const tableContainer = document.querySelector('#accountReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.getElementById('loadingStatus');
    if (!loadingStatus) return;

    const autoLoadIndicator = document.createElement('div');
    autoLoadIndicator.id = 'autoLoadIndicator';
    autoLoadIndicator.className = 'text-center mt-1 mb-3';
    autoLoadIndicator.innerHTML = `
        <div class="spinner-border spinner-border-sm text-primary d-none" id="loadMoreSpinner" role="status">
            <span class="visually-hidden">Loading more accounts...</span>
        </div>
    `;
    tableContainer.parentNode.insertBefore(autoLoadIndicator, loadingStatus.nextSibling);
}

function updateLoadingStatus() {
    const existingStatus = document.getElementById('loadingStatus');
    if (existingStatus) {
        existingStatus.remove();
    }

    const tableContainer = document.querySelector('#accountReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded ${window.reportState.allEntries.length} of ${window.reportState.totalRecords} accounts`;
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
    updateExportButtonState();
}

function updateReportSummary(count, totalDebit = 0, totalCredit = 0, totalBalance = 0) {
    const summaryElement = document.getElementById('reportSummary');
    if (!summaryElement) return;

    if (count > 0) {
        // show count and export button (export button is managed by JS for enable/disable)
        summaryElement.innerHTML = `
            <span class="badge bg-primary">${count} accounts</span>
            <button id="exportAccountCsvBtn" class="btn btn-outline-success btn-sm ms-2" disabled title="Export CSV">
                <i class="fas fa-file-csv me-1"></i>Export CSV
            </button>
        `;
    } else {
        summaryElement.innerHTML = `<span class="badge bg-secondary">No data</span>`;
    }
    updateExportButtonState();
}

// ==================== UTILITY FUNCTIONS ====================

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value || 0);
}