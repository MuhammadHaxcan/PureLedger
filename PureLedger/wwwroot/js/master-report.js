/**
 * Master Report JavaScript
 * Handles all functionality for the Master Report page including:
 * - Searchable dropdown filters
 * - Column visibility toggles
 * - Data fetching and rendering with infinite scrolling
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
    // Create select-container structure for existing dropdowns if needed
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');
    convertToSearchableSelect('accountId', 'Search account...');
    convertToSearchableSelect('bookerId', 'Search booker...');

    // Populate initial dropdown options
    populateDropdownOptions();

    // Initialize dropdown states
    initializeDropdownStates();
}

function convertToSearchableSelect(selectId, placeholder) {
    const select = document.getElementById(selectId);
    if (!select) return;

    // Only proceed if not already converted
    if (select.parentElement.classList.contains('select-container')) return;

    // Get the icon class based on the select ID
    let iconClass = 'fa-list';
    if (selectId === 'projectId') iconClass = 'fa-project-diagram';
    else if (selectId === 'categoryId') iconClass = 'fa-sitemap';
    else if (selectId === 'accountId') iconClass = 'fa-user';
    else if (selectId === 'bookerId') iconClass = 'fa-user-tag';

    // Get any existing input-group parent
    const inputGroup = select.closest('.input-group');
    const selectParent = inputGroup ? inputGroup.parentElement : select.parentElement;

    // Create the new structure
    const container = document.createElement('div');
    container.className = 'select-container';

    const newInputGroup = document.createElement('div');
    newInputGroup.className = 'input-group';

    // Icon span
    const iconSpan = document.createElement('span');
    iconSpan.className = 'input-group-text';
    iconSpan.innerHTML = `<i class="fas ${iconClass}"></i>`;

    // Search input
    const searchInput = document.createElement('input');
    searchInput.type = 'text';
    searchInput.className = `form-control select-search ${selectId}-search`;
    searchInput.placeholder = placeholder;
    searchInput.autocomplete = 'off';
    if (select.disabled) searchInput.disabled = true;

    // Dropdown div
    const dropdownDiv = document.createElement('div');
    dropdownDiv.className = `select-dropdown ${selectId}-dropdown`;

    // Clone the select and hide it
    const clonedSelect = select.cloneNode(true);
    clonedSelect.className = 'form-select d-none';

    // Build the structure
    newInputGroup.appendChild(iconSpan);
    newInputGroup.appendChild(searchInput);
    container.appendChild(newInputGroup);
    container.appendChild(clonedSelect);
    container.appendChild(dropdownDiv);

    // Replace the old select with our new structure
    selectParent.replaceChild(container, inputGroup || select);
}

function populateDropdownOptions() {
    // Populate options for all select-container elements
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
    // Handle input in search box
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

    // Show dropdown on focus
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

    // Handle keyboard navigation
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

    // Handle click on dropdown option
    $(document).on('click', '.select-option:not(.filtered-out)', function () {
        selectOption($(this), $(this).closest('.select-container'));
    });

    // Close dropdown when clicking outside
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.select-container').length) {
            $('.select-dropdown').removeClass('show');
        }
    });
}

function selectOption($option, $container) {
    const value = $option.data('value');
    const text = $option.text();

    // Update the hidden select and trigger change event
    const $select = $container.find('select');
    $select.val(value).trigger('change');

    // Update the search input text
    $container.find('.select-search').val(text);

    // Update the visual state of the dropdown
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
    const form = document.getElementById('masterReportForm');
    if (form) {
        form.addEventListener('submit', e => {
            e.preventDefault();
            generateReport();
        });
    }

    initColumnToggles();
    initSearchFunctionality();
    setupSelectSearch();

    // Use jQuery event handlers for consistency with dropdown functionality
    $(document).on('change', '#projectId', handleProjectChange);
    $(document).on('change', '#categoryId', handleCategoryChange);

    window.addEventListener('scroll', throttle(checkIfShouldLoadMore, 100));
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
    const reportData = document.getElementById('reportData');
    if (!reportData) return;

    const rows = reportData.getElementsByTagName('tr');
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
    const tableElement = document.getElementById(tableId);
    if (!tableElement) return;

    const rows = tableElement.getElementsByTagName('tr');
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
    const projectId = $('#projectId').val();

    // Get the elements we need to update
    const $categoryContainer = $('.select-container').has('#categoryId');
    const $categorySearch = $categoryContainer.find('.select-search');
    const $categoryDropdown = $categoryContainer.find('.select-dropdown');
    const categorySelect = document.getElementById('categoryId');

    // Reset category and account
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

                    // Add option to dropdown
                    if ($categoryDropdown.length) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = category.id;
                        div.textContent = category.name;
                        $categoryDropdown.append(div);
                    }
                });
            }
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

    // Get the elements we need to update
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

                    // Add option to dropdown
                    if ($accountDropdown.length) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = account.id;
                        div.textContent = account.name;
                        $accountDropdown.append(div);
                    }
                });
            }
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

                    // Add option to dropdown
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

function generateReport() {
    initializeReportState();

    const startDate = document.getElementById('startDate');
    const endDate = document.getElementById('endDate');
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');
    const bookerId = document.getElementById('bookerId');

    if (!projectId) {
        showError('Project field not found.');
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
        return;
    }

    params.ProjectId = projectId.value;
    if (categoryId && categoryId.value) params.CategoryId = categoryId.value;
    if (accountId && accountId.value) params.AccountId = accountId.value;
    if (bookerId && bookerId.value) params.BookerId = bookerId.value;

    window.reportState.reportParams = params;

    clearReportData();

    const reportResults = document.getElementById('reportResults');
    if (reportResults) reportResults.style.display = 'block';

    const reportData = document.getElementById('reportData');
    if (reportData) reportData.innerHTML = createLoadingRow(10);

    fetchReportChunk();
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

// ==================== INFINITE SCROLL ====================

function checkIfShouldLoadMore() {
    if (!window.reportState?.hasMoreData || window.reportState?.isLoading) {
        return;
    }

    const reportResults = document.getElementById('reportResults');
    if (!reportResults || reportResults.style.display === 'none') {
        return;
    }

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
            const reportData = document.getElementById('reportData');
            if (reportData) {
                reportData.innerHTML = '<tr><td colspan="10" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
            }
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
    if (!tbody) return;

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

        groupedByAccount[accountKey].debit += entry.debit || 0;
        groupedByAccount[accountKey].credit += entry.credit || 0;
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

        groupedByCategory[categoryKey].debit += entry.debit || 0;
        groupedByCategory[categoryKey].credit += entry.credit || 0;
    });

    renderSummaryTable('categoryGroupData', groupedByCategory);
}

function renderSummaryTable(tableId, groupedData) {
    const tbody = document.getElementById(tableId);
    if (!tbody) return;

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
            <td class="text-end ${balance < 0 ? 'negative-balance' : ''}">${formatCurrency(balance)}</td>
        `;
        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
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
}

function updateTotals() {
    const totalQuantity = window.reportState.allEntries.reduce((sum, entry) => sum + (entry.quantity || 0), 0);
    const totalDebit = window.reportState.allEntries.reduce((sum, entry) => sum + (entry.debit || 0), 0);
    const totalCredit = window.reportState.allEntries.reduce((sum, entry) => sum + (entry.credit || 0), 0);
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