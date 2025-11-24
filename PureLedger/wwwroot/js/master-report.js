/**
 * Master Report JavaScript
 * Handles all functionality for the Master Report page including:
 * - Searchable dropdown filters
 * - Column visibility toggles
 * - Data fetching and rendering with auto-loading
 * - CSV exports for main report, account summary and category summary (rewritten)
 */

document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    initializeSelectDropdowns();
    setupEventListeners();
    setDefaultDates();
    loadAllBookers();
});

// ==================== SEARCHABLE DROPDOWN LOGIC ====================

function initializeSelectDropdowns() {
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');
    convertToSearchableSelect('accountId', 'Search account...');
    convertToSearchableSelect('bookerId', 'Search booker...');

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
    else if (selectId === 'bookerId') iconClass = 'fa-user-tag';

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
        previousBalance: 0,
        totals: { quantity: 0, debit: 0, credit: 0 },
        accountGroupsMap: {},
        categoryGroupsMap: {},
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
    const form = document.getElementById('masterReportForm');
    if (form) {
        form.addEventListener('submit', e => {
            e.preventDefault();
            setGenerateReportEnabled(false);
            generateReport();
        });
    }

    initColumnToggles();
    initSearchFunctionality();
    setupSelectSearch();
    initializeExportButton();

    // Summary export buttons are attached here too
    const accountBtn = document.getElementById('exportAccountSummaryCsvBtn');
    if (accountBtn) accountBtn.addEventListener('click', exportAccountSummaryToCsv);

    const categoryBtn = document.getElementById('exportCategorySummaryCsvBtn');
    if (categoryBtn) categoryBtn.addEventListener('click', exportCategorySummaryToCsv);

    $(document).on('change', '#projectId', handleProjectChange);
    $(document).on('change', '#categoryId', handleCategoryChange);
}

function setDefaultDates() {
    const startDate = document.getElementById('startDate');
    const endDate = document.getElementById('endDate');

    if (startDate) startDate.value = '';
    if (endDate) endDate.value = '';
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
            if (!columnKey) return;

            const isVisible = this.checked;
            window.reportState.visibleColumns[columnKey] = isVisible;
            updateColumnVisibility();
        });
    });

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

    Object.keys(columnClasses).forEach(key => {
        const isVisible = window.reportState.visibleColumns[key];
        const displayValue = isVisible ? '' : 'none';

        document.querySelectorAll('.' + columnClasses[key]).forEach(cell => {
            cell.style.display = displayValue;
        });
    });

    updateFooterColspan();
}

function updateFooterColspan() {
    const footerTotalCell = document.querySelector('#masterReportTable tfoot td:first-child');
    if (!footerTotalCell) return;

    let colspan = 1;

    if (window.reportState.visibleColumns.date) colspan++;
    if (window.reportState.visibleColumns.creditableDate) colspan++;
    if (window.reportState.visibleColumns.description) colspan++;
    if (window.reportState.visibleColumns.account) colspan++;
    if (window.reportState.visibleColumns.booker) colspan++;

    footerTotalCell.setAttribute('colspan', colspan);
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

// ==================== CSV EXPORT (REWRITTEN) ====================
// All CSV export functions now use the underlying reportState data (not DOM scraping).
// Group IDs formatting adjusted per requirement: keep dashes for both account and category (no slashes).

function initializeExportButton() {
    const btn = document.getElementById('exportCsvBtn');
    if (!btn) return;
    btn.addEventListener('click', function () {
        exportReportToCsv();
    });

    // Ensure button initial state
    updateExportButtonState();

    // Ensure summary export buttons initial disabled state
    updateSummaryExportButtons();
}

function updateExportButtonState() {
    const btn = document.getElementById('exportCsvBtn');
    if (!btn) return;

    const allLoaded = !window.reportState.isLoading
        && !window.reportState.hasMoreData
        && window.reportState.allEntries
        && window.reportState.allEntries.length > 0;

    btn.disabled = !allLoaded;
}

// CSV helpers
function csvEscape(value) {
    if (value === null || value === undefined) return '';
    const str = String(value);
    const escaped = str.replace(/"/g, '""');
    return `"${escaped}"`;
}

function formatNumericForCsv(value) {
    if (value === null || value === undefined || value === '') return '';
    const n = Number(value);
    if (isNaN(n)) return String(value);
    // Dot decimal, no thousands separators
    return n.toFixed(2);
}

// Format group id: preserve dashes for account and category (no slash conversion)
function formatGroupIdForCsv(rawId) {
    if (!rawId) return '';
    // Preserve original dash-separated id like "1-1" or "1-1-1"
    return String(rawId);
}

// Export main master report (uses visible column toggles)
function exportReportToCsv() {
    if (!window.reportState.allEntries || window.reportState.allEntries.length === 0) {
        showError('No data available to export.');
        return;
    }

    const cols = [
        { key: 'id', label: 'ID' },
        { key: 'transactionDate', label: 'Entry Date' },
        { key: 'creditableDate', label: 'Creditable Date' },
        { key: 'description', label: 'Description' },
        { key: 'accountName', label: 'Account' },
        { key: 'bookerName', label: 'Booker' },
        { key: 'quantity', label: 'Quantity' },
        { key: 'debit', label: 'Debit' },
        { key: 'credit', label: 'Credit' },
        { key: 'balance', label: 'Balance' }
    ];

    const visibleCols = cols.filter(col => {
        switch (col.key) {
            case 'transactionDate': return window.reportState.visibleColumns.date;
            case 'creditableDate': return window.reportState.visibleColumns.creditableDate;
            case 'description': return window.reportState.visibleColumns.description;
            case 'accountName': return window.reportState.visibleColumns.account;
            case 'bookerName': return window.reportState.visibleColumns.booker;
            case 'quantity': return window.reportState.visibleColumns.quantity;
            case 'debit': return window.reportState.visibleColumns.debit;
            case 'credit': return window.reportState.visibleColumns.credit;
            case 'balance': return window.reportState.visibleColumns.balance;
            default: return true; // ID always visible
        }
    });

    const header = visibleCols.map(c => csvEscape(c.label)).join(',');

    const rows = window.reportState.allEntries.map(entry => {
        const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";
        let idVal = '';
        if (isOpening) {
            idVal = 'OPENING';
        } else {
            const parts = [];
            if (entry.journalVoucherId !== null && entry.journalVoucherId !== undefined && entry.journalVoucherId !== '') {
                parts.push(`JV-${entry.journalVoucherId}`);
            }
            if (entry.id !== null && entry.id !== undefined && entry.id !== '') {
                parts.push(`Entry: ${entry.id}`);
            }
            idVal = parts.join(', ');
        }

        const record = {
            id: idVal,
            transactionDate: formatDate(entry.transactionDate),
            creditableDate: formatDate(entry.creditableDate),
            description: entry.description ?? '',
            accountName: entry.accountName ?? '',
            bookerName: entry.bookerName ?? '',
            quantity: (entry.quantity === null || entry.quantity === undefined || entry.quantity === '') ? '' : String(entry.quantity),
            debit: formatNumericForCsv(entry.debit),
            credit: formatNumericForCsv(entry.credit),
            balance: formatNumericForCsv(entry.balance)
        };

        return visibleCols.map(c => csvEscape(record[c.key])).join(',');
    });

    const csvContent = [header].concat(rows).join('\r\n');
    downloadCsvBlob(csvContent, `MasterReport_${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
}

// Export account summary using reportState.accountGroupsMap (not DOM)
function exportAccountSummaryToCsv() {
    const map = window.reportState?.accountGroupsMap || {};
    const keys = Object.keys(map);
    if (keys.length === 0) {
        alert('No account summary data to export.');
        return;
    }

    const header = ['ID', 'Account', 'Debit', 'Credit', 'Balance'];
    const rows = [header.map(csvEscape).join(',')];

    keys.forEach(k => {
        const d = map[k];
        const balance = (d.debit || 0) - (d.credit || 0);
        // Keep dash-separated id "projectId-categoryId-accountId" like "1-1-1"
        const id = formatGroupIdForCsv(d.id);
        rows.push([
            csvEscape(id),
            csvEscape(d.name || ''),
            csvEscape(formatNumericForCsv(d.debit)),
            csvEscape(formatNumericForCsv(d.credit)),
            csvEscape(formatNumericForCsv(balance))
        ].join(','));
    });

    const csvContent = rows.join('\r\n');
    downloadCsvBlob(csvContent, `AccountSummary_${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
}

// Export category summary using reportState.categoryGroupsMap (not DOM)
function exportCategorySummaryToCsv() {
    const map = window.reportState?.categoryGroupsMap || {};
    const keys = Object.keys(map);
    if (keys.length === 0) {
        alert('No category summary data to export.');
        return;
    }

    const header = ['ID', 'Category', 'Debit', 'Credit', 'Balance'];
    const rows = [header.map(csvEscape).join(',')];

    keys.forEach(k => {
        const d = map[k];
        const balance = (d.debit || 0) - (d.credit || 0);
        // Keep dash-separated id "projectId-categoryId" like "1-1"
        const id = formatGroupIdForCsv(d.id);
        rows.push([
            csvEscape(id),
            csvEscape(d.name || ''),
            csvEscape(formatNumericForCsv(d.debit)),
            csvEscape(formatNumericForCsv(d.credit)),
            csvEscape(formatNumericForCsv(balance))
        ].join(','));
    });

    const csvContent = rows.join('\r\n');
    downloadCsvBlob(csvContent, `CategorySummary_${new Date().toISOString().replace(/[:.]/g, '-')}.csv`);
}

// Generic blob download
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

// Update summary export buttons state (called from renderSummaryTable)
function updateSummaryExportButtons() {
    const accountBtn = document.getElementById('exportAccountSummaryCsvBtn');
    const categoryBtn = document.getElementById('exportCategorySummaryCsvBtn');

    const hasAccountData = window.reportState.accountGroupsMap && Object.keys(window.reportState.accountGroupsMap).length > 0;
    const hasCategoryData = window.reportState.categoryGroupsMap && Object.keys(window.reportState.categoryGroupsMap).length > 0;

    // Buttons enabled only when all data has finished loading AND the respective summary maps have entries.
    const ready = !window.reportState.isLoading && !window.reportState.hasMoreData;

    if (accountBtn) accountBtn.disabled = !(ready && hasAccountData);
    if (categoryBtn) categoryBtn.disabled = !(ready && hasCategoryData);
}

// ==================== SEARCH FUNCTIONALITY ====================

function filterReportData(searchText) {
    const reportData = document.getElementById('reportData');
    if (!reportData) return;

    const existingNoResults = document.getElementById('noSearchResults');
    if (existingNoResults) existingNoResults.remove();

    const rows = Array.from(reportData.getElementsByTagName('tr'));
    let hasVisibleRows = false;
    let filteredTotals = { quantity: 0, debit: 0, credit: 0, balance: 0 };
    let runningBalance = window.reportState.previousBalance || 0;

    rows.forEach(row => {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';

        if (isVisible) {
            hasVisibleRows = true;
            const cells = row.querySelectorAll('td');
            if (cells.length >= 10) {
                const debit = parseFloat(cells[7].textContent.replace(/,/g, '')) || 0;
                const credit = parseFloat(cells[8].textContent.replace(/,/g, '')) || 0;
                const quantity = parseFloat(cells[6].textContent.replace(/,/g, '')) || 0;

                runningBalance += debit - credit;
                filteredTotals.quantity += quantity;
                filteredTotals.debit += debit;
                filteredTotals.credit += credit;
                filteredTotals.balance = runningBalance;

                cells[9].textContent = formatCurrency(runningBalance);
                cells[9].className = 'balance-column text-end' + (runningBalance < 0 ? ' text-danger' : ' text-success');
            }
        }
    });

    if (searchText.length > 0) {
        updateElementText('totalQuantity', formatNumber(filteredTotals.quantity));
        updateElementText('totalDebit', formatCurrency(filteredTotals.debit));
        updateElementText('totalCredit', formatCurrency(filteredTotals.credit));
        updateElementText('netBalance', formatCurrency(filteredTotals.balance));
    } else {
        updateTotals();
    }

    if (!hasVisibleRows && rows.length > 0 && searchText.length > 0) {
        const noResultsRow = document.createElement('tr');
        noResultsRow.id = 'noSearchResults';
        noResultsRow.innerHTML = '<td colspan="10" class="text-center">No matching accounts found</td>';
        reportData.appendChild(noResultsRow);

        updateElementText('totalQuantity', '0');
        updateElementText('totalDebit', '0.00');
        updateElementText('totalCredit', '0.00');
        updateElementText('netBalance', '0.00');
    }
}

function filterSummaryData(tableId, searchText) {
    const tableElement = document.getElementById(tableId);
    if (!tableElement) return;

    const rows = tableElement.getElementsByTagName('tr');
    let hasVisibleRows = false;

    for (const row of rows) {
        const isVisible = row.textContent.toLowerCase().includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }

    handleNoMatchingRows(tableId, 'noResults' + tableId, !hasVisibleRows && rows.length > 0, searchText, 5);
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

function loadAllBookers() {
    const bookerSelect = document.getElementById('bookerId');
    if (!bookerSelect) return;

    const $bookerContainer = $('.select-container').has('#bookerId');
    const $bookerSearch = $bookerContainer.find('.select-search');
    const $bookerDropdown = $bookerContainer.find('.select-dropdown');

    if ($bookerSearch.length) $bookerSearch.val('').prop('disabled', true);
    if ($bookerDropdown.length) $bookerDropdown.html('<div class="select-option">Loading bookers...</div>');

    fetch('/Reporting/GetAllBookers')
        .then(response => response.json())
        .then(bookers => {
            resetSelect(bookerSelect, 'All Bookers');
            if ($bookerSearch.length) $bookerSearch.prop('disabled', false);

            if ($bookerDropdown.length) {
                $bookerDropdown.empty();
                $bookerDropdown.append('<div class="select-option" data-value="">All Bookers</div>');
            }

            if (bookers?.length > 0) {
                bookers.forEach(booker => {
                    const option = document.createElement('option');
                    option.value = booker.id;
                    option.textContent = booker.name;
                    bookerSelect.appendChild(option);

                    if ($bookerDropdown.length) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = booker.id;
                        div.textContent = booker.name;
                        $bookerDropdown.append(div);
                    }
                });
            }
        })
        .catch(error => {
            console.error('Error loading bookers:', error);
            resetSelect(bookerSelect, 'All Bookers');
            if ($bookerSearch.length) $bookerSearch.prop('disabled', false);
            if ($bookerDropdown.length) $bookerDropdown.empty();
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
    const btn = document.querySelector('#masterReportForm button[type="submit"]');
    if (btn) btn.disabled = !enabled;
}

function generateReport() {
    const searchBox = document.getElementById('searchBox');
    if (searchBox && searchBox.value.trim() !== '') {
        searchBox.value = '';
    }

    initializeReportState();

    const startDate = document.getElementById('startDate');
    const endDate = document.getElementById('endDate');
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');
    const bookerId = document.getElementById('bookerId');

    if (!projectId) {
        showError('Project field not found.');
        setGenerateReportEnabled(true);
        return;
    }

    const startDateValue = startDate ? startDate.value : null;
    const endDateValue = endDate ? endDate.value : null;

    const params = {
        StartDate: startDateValue || null,
        EndDate: endDateValue || null,
        Page: 0,
        PageSize: window.reportState.pageSize
    };

    if (!projectId.value) {
        showError('Project selection is required.');
        setGenerateReportEnabled(true);
        return;
    }

    params.ProjectId = projectId.value;
    if (categoryId && categoryId.value) params.CategoryId = categoryId.value;
    if (accountId && accountId.value) params.AccountId = accountId.value;
    if (bookerId && bookerId.value) params.BookerId = bookerId.value;

    window.reportState.reportParams = params;

    clearReportData();

    setSearchBoxEnabled(false);

    const reportResults = document.getElementById('reportResults');
    if (reportResults) reportResults.style.display = 'block';

    const reportData = document.getElementById('reportData');
    if (reportData) reportData.innerHTML = createLoadingRow(10);

    fetchReportChunk(true);
}

function clearReportData() {
    updateElement('reportData', '');
    updateElement('accountGroupData', '');
    updateElement('categoryGroupData', '');
    updateElement('totalQuantity', '0');
    updateElement('totalDebit', '0.00');
    updateElement('totalCredit', '0.00');
    updateElement('netBalance', '0.00');

    removeElements(['loadMoreContainer', 'loadingStatus', 'autoLoadIndicator']);

    // Disable master export while data is being loaded
    const exportBtn = document.getElementById('exportCsvBtn');
    if (exportBtn) exportBtn.disabled = true;

    // Disable summary exports until data arrives
    updateSummaryExportButtons();
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
            const reportData = document.getElementById('reportData');
            if (reportData) {
                reportData.innerHTML = '<tr><td colspan="10" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
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

            updateExportButtonState();
            updateSummaryExportButtons();
        });
}

// ==================== DATA PROCESSING ====================

function processAndDisplayEntries(entries) {
    if (!entries?.length) return;

    for (const entry of entries) {
        window.reportState.allEntries.push(entry);
        window.reportState.totals.quantity += (entry.quantity || 0);
        window.reportState.totals.debit += (entry.debit || 0);
        window.reportState.totals.credit += (entry.credit || 0);

        if (entry.accountId) {
            const key = String(entry.accountId);
            const existing = window.reportState.accountGroupsMap[key] || {
                id: `${entry.projectId}-${entry.categoryId}-${entry.accountId}`,
                name: entry.accountName,
                debit: 0,
                credit: 0
            };
            existing.debit += (entry.debit || 0);
            existing.credit += (entry.credit || 0);
            window.reportState.accountGroupsMap[key] = existing;
        }

        if (entry.categoryId) {
            const ckey = String(entry.categoryId);
            const cexisting = window.reportState.categoryGroupsMap[ckey] || {
                id: `${entry.projectId}-${entry.categoryId}`,
                name: entry.categoryName,
                debit: 0,
                credit: 0
            };
            cexisting.debit += (entry.debit || 0);
            cexisting.credit += (entry.credit || 0);
            window.reportState.categoryGroupsMap[ckey] = cexisting;
        }
    }

    appendRows(entries);
    renderSummaryTable('accountGroupData', window.reportState.accountGroupsMap);
    renderSummaryTable('categoryGroupData', window.reportState.categoryGroupsMap);

    updateTotals();
}

// ==================== TABLE RENDERING ====================
function appendRows(entries) {
    const tbody = document.getElementById('reportData');
    if (!tbody) return;

    const fragment = document.createDocumentFragment();

    entries.forEach((entry) => {
        const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                ${isOpening ? '<span class="fw-bold text-primary">OPENING</span>' : `
                <div class="id-container">
                    <div>
                        <a href="/Reporting/PrintVoucher?journalVoucherId=${entry.journalVoucherId}&type=JV" 
                           class="voucher-link" 
                           target="_blank" 
                           title="Open voucher in new window">
                            JV-${entry.journalVoucherId}
                        </a>
                        <a href="/Reporting/PrintVoucher?journalVoucherId=${entry.journalVoucherId}&type=INV" 
                           class="voucher-link ms-1" 
                           target="_blank" 
                           title="Open invoice in new window">
                            INV-${entry.journalVoucherId}
                        </a>
                    </div>
                    <div class="entry-id mt-1">Entry: ${entry.id}</div>
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

function renderSummaryTable(tableId, groupedData) {
    const tbody = document.getElementById(tableId);
    if (!tbody) return;

    tbody.innerHTML = '';

    const keys = Object.keys(groupedData);
    if (keys.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">No data available</td></tr>';
        updateSummaryExportButtons();
        return;
    }

    const fragment = document.createDocumentFragment();

    Object.values(groupedData).forEach(data => {
        const balance = data.debit - data.credit;
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${formatGroupIdForCsv(data.id)}</td>
            <td>${data.name}</td>
            <td class="text-end">${formatCurrency(data.debit)}</td>
            <td class="text-end">${formatCurrency(data.credit)}</td>
            <td class="text-end ${balance < 0 ? 'text-danger' : 'text-success'}">${formatCurrency(balance)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
    updateSummaryExportButtons();
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    updateElement('reportData', '');
    window.reportState.runningBalance = 0;

    updateElement('accountGroupData', '');
    updateElement('categoryGroupData', '');

    removeElements(['loadingStatus', 'autoLoadIndicator']);

    addLoadingStatus();
    addAutoLoadIndicator();
}

function addLoadingStatus() {
    const tableContainer = document.querySelector('#masterReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded 0 of ${window.reportState.totalRecords} records`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function addAutoLoadIndicator() {
    const tableContainer = document.querySelector('#masterReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.getElementById('loadingStatus');
    if (!loadingStatus) return;

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

    const tableContainer = document.querySelector('#masterReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

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
    updateExportButtonState();
}

function updateTotals() {
    const totalQuantity = window.reportState.totals.quantity;
    const totalDebit = window.reportState.totals.debit;
    const totalCredit = window.reportState.totals.credit;
    const netBalance = totalDebit - totalCredit;

    updateElementText('totalQuantity', formatNumber(totalQuantity));
    updateElementText('totalDebit', formatCurrency(totalDebit));
    updateElementText('totalCredit', formatCurrency(totalCredit));
    updateElementText('netBalance', formatCurrency(netBalance));
}

function updateElementText(id, text) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = text;
    }
}

// ==================== UTILITY FUNCTIONS ====================

function formatNumber(value) {
    return value?.toLocaleString() || '0';
}

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        style: 'decimal',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value || 0);
}

function formatDate(dateString) {
    if (!dateString) return '';

    const date = new Date(dateString);
    if (isNaN(date.getTime())) return '';

    return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit'
    });
}