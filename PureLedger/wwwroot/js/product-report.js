/**
 * Product Report JavaScript
 * Handles all functionality for the Product Report page including:
 * - Searchable dropdown filters
 * - Column visibility toggles
 * - Data fetching and rendering with auto-loading
 */

document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    initializeSelectDropdowns();
    setupEventListeners();
    setDefaultDates();
});

function initializeReportState() {
    window.reportState = {
        currentPage: 0,
        pageSize: 1,
        isLoading: false,
        hasMoreData: true,
        totalRecords: 0,
        allProducts: [],
        reportParams: null,
        totals: {
            totalDebitQty: 0,
            totalCreditQty: 0,
            netQuantity: 0,
            totalDebit: 0,
            totalCredit: 0,
            totalCOGS: 0,
            totalMargin: 0,
            marginPercentage: 0
        },
        visibleColumns: {
            product: true,
            voucher: true,
            entryDate: true,
            dueDate: true,
            debitQty: true,
            creditQty: true,
            balanceQty: true,
            avgRate: true,
            totalDebit: true,
            totalCredit: true,
            cogs: true,
            margin: true,
            marginPercent: true
        }
    };
}

function initializeSelectDropdowns() {
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');
    convertToSearchableSelect('accountId', 'Search product...');
    convertToSearchableSelect('locationId', 'Search location...');

    populateDropdownOptions();
}

function convertToSearchableSelect(selectId, placeholder) {
    const select = document.getElementById(selectId);
    if (!select) return;

    if (select.parentElement.classList.contains('select-container')) return;

    let iconClass = 'fa-list';
    if (selectId === 'projectId') iconClass = 'fa-project-diagram';
    else if (selectId === 'categoryId') iconClass = 'fa-sitemap';
    else if (selectId === 'accountId') iconClass = 'fa-box';
    else if (selectId === 'locationId') iconClass = 'fa-map-marker-alt';

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

function setupEventListeners() {
    document.getElementById('productReportForm').addEventListener('submit', e => {
        e.preventDefault();
        setGenerateReportEnabled(false);
        generateReport();
    });

    initColumnToggles();
    initSearchFunctionality();
    setupSelectSearch();

    $(document).on('change', '#projectId', loadCategoriesForProject);
    $(document).on('change', '#categoryId', loadAccountsForCategory);
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

function setDefaultDates() {
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
}

// ==================== UI CONTROLS ====================

function initColumnToggles() {
    const columnMap = {
        'showProduct': 'product',
        'showVoucher': 'voucher',
        'showEntryDate': 'entryDate',
        'showDueDate': 'dueDate',
        'showDebitQty': 'debitQty',
        'showCreditQty': 'creditQty',
        'showBalanceQty': 'balanceQty',
        'showAvgRate': 'avgRate',
        'showTotalDebit': 'totalDebit',
        'showTotalCredit': 'totalCredit',
        'showCOGS': 'cogs',
        'showMargin': 'margin',
        'showMarginPercent': 'marginPercent'
    };

    document.querySelectorAll('.column-toggles input[type="checkbox"]').forEach(checkbox => {
        checkbox.addEventListener('change', function () {
            const columnKey = columnMap[this.id];
            const isVisible = this.checked;

            window.reportState.visibleColumns[columnKey] = isVisible;
            updateColumnVisibility();
        });
    });

    updateColumnVisibility();
}

function updateColumnVisibility() {
    const columnClasses = {
        product: 'product-column',
        voucher: 'voucher-column',
        entryDate: 'entry-date-column',
        dueDate: 'due-date-column',
        debitQty: 'debit-qty-column',
        creditQty: 'credit-qty-column',
        balanceQty: 'balance-qty-column',
        avgRate: 'avg-rate-column',
        totalDebit: 'total-debit-column',
        totalCredit: 'total-credit-column',
        cogs: 'cogs-column',
        margin: 'margin-column',
        marginPercent: 'margin-percent-column'
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
    const footerTotalCell = document.querySelector('#productReportTable tfoot td:first-child');
    if (!footerTotalCell) return;

    let colspan = 0;

    if (window.reportState.visibleColumns.product) colspan++;
    if (window.reportState.visibleColumns.voucher) colspan++;
    if (window.reportState.visibleColumns.entryDate) colspan++;
    if (window.reportState.visibleColumns.dueDate) colspan++;

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

function filterReportData(searchText) {
    const rows = document.getElementById('reportData')?.getElementsByTagName('tr');
    if (!rows || rows.length === 0) return;

    // Remove existing "no results" row if present
    const existingNoResults = document.getElementById('noSearchResults');
    if (existingNoResults) existingNoResults.remove();

    const productGroups = {};

    // First pass: identify which product groups should be visible
    for (let i = 0; i < rows.length; i++) {
        const row = rows[i];
        const isProductRow = !row.classList.contains('previous-balance-row');

        if (isProductRow) {
            const productName = row.querySelector('td:first-child').textContent;
            if (!productGroups[productName]) {
                productGroups[productName] = {
                    visible: row.textContent.toLowerCase().includes(searchText),
                    rowIndices: []
                };
            }
            productGroups[productName].rowIndices.push(i);
        }
    }

    let hasVisibleRows = false;
    let filteredTotals = {
        totalDebitQty: 0,
        totalCreditQty: 0,
        netQuantity: 0,
        totalDebit: 0,
        totalCredit: 0,
        totalCOGS: 0,
        totalMargin: 0
    };

    // Second pass: show/hide rows based on product visibility and calculate filtered totals
    for (let i = 0; i < rows.length; i++) {
        const row = rows[i];
        const isPreviousBalanceRow = row.classList.contains('previous-balance-row');

        if (isPreviousBalanceRow) {
            // Previous balance row visibility depends on the next row (product row)
            if (i + 1 < rows.length) {
                const nextRow = rows[i + 1];
                const productName = nextRow.querySelector('td:first-child').textContent;
                const isVisible = productGroups[productName]?.visible || false;

                row.style.display = isVisible ? '' : 'none';
                if (isVisible) hasVisibleRows = true;
            } else {
                row.style.display = 'none';
            }
        } else {
            // Regular product row
            const productName = row.querySelector('td:first-child').textContent;
            const isVisible = productGroups[productName]?.visible || false;

            row.style.display = isVisible ? '' : 'none';
            
            if (isVisible) {
                hasVisibleRows = true;
                
                // Calculate filtered totals
                const cells = row.querySelectorAll('td');
                if (cells.length >= 13) {
                    const debitQty = parseFloat(cells[4].textContent.replace(/,/g, '')) || 0;
                    const creditQty = parseFloat(cells[5].textContent.replace(/,/g, '')) || 0;
                    const balanceQty = parseFloat(cells[6].textContent.replace(/[(),]/g, '')) || 0;
                    const totalDebit = parseFloat(cells[8].textContent.replace(/,/g, '')) || 0;
                    const totalCredit = parseFloat(cells[9].textContent.replace(/,/g, '')) || 0;
                    const cogs = parseFloat(cells[10].textContent.replace(/,/g, '')) || 0;
                    const margin = parseFloat(cells[11].textContent.replace(/[(),]/g, '')) || 0;

                    filteredTotals.totalDebitQty += debitQty;
                    filteredTotals.totalCreditQty += creditQty;
                    filteredTotals.totalDebit += totalDebit;
                    filteredTotals.totalCredit += totalCredit;
                    filteredTotals.totalCOGS += cogs;
                    filteredTotals.totalMargin += margin;
                }
            }
        }
    }

    // Calculate net quantity and margin percentage
    filteredTotals.netQuantity = filteredTotals.totalDebitQty - filteredTotals.totalCreditQty;
    filteredTotals.marginPercentage = filteredTotals.totalCredit > 0 
        ? (filteredTotals.totalMargin / filteredTotals.totalCredit) * 100 
        : 0;

    // Update totals based on search
    if (searchText.length > 0) {
        updateElement('totalDebitQty', formatNumber(filteredTotals.totalDebitQty));
        updateElement('totalCreditQty', formatNumber(filteredTotals.totalCreditQty));
        updateElement('netQty', formatNumberWithSign(filteredTotals.netQuantity));
        updateElement('totalDebit', formatDecimal(filteredTotals.totalDebit));
        updateElement('totalCredit', formatDecimal(filteredTotals.totalCredit));
        updateElement('totalCOGS', formatDecimal(filteredTotals.totalCOGS));
        updateElement('totalMargin', formatDecimalWithSign(filteredTotals.totalMargin));
        updateElement('avgMarginPercent', formatPercent(filteredTotals.marginPercentage));
    } else {
        // Reset to original totals when no filter
        updateTotals(window.reportState.totals);
    }

    // Show "no results" message if needed
    if (!hasVisibleRows && rows.length > 0 && searchText.length > 0) {
        const reportData = document.getElementById('reportData');
        if (reportData) {
            const noResultsRow = document.createElement('tr');
            noResultsRow.id = 'noSearchResults';
            noResultsRow.innerHTML = '<td colspan="13" class="text-center">No matching products found</td>';
            reportData.appendChild(noResultsRow);
        }

        // Reset totals to zero when no results
        updateElement('totalDebitQty', '0');
        updateElement('totalCreditQty', '0');
        updateElement('netQty', '0');
        updateElement('totalDebit', '0.00');
        updateElement('totalCredit', '0.00');
        updateElement('totalCOGS', '0.00');
        updateElement('totalMargin', '0.00');
        updateElement('avgMarginPercent', '0.00%');
    }
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
    $categorySearch.val('').prop('disabled', true);
    $categoryDropdown.empty();
    resetAccountDropdown();

    if (!projectId) return;

    $categoryDropdown.html('<div class="select-option">Loading categories...</div>');

    $.ajax({
        url: routes.getCategoriesByProject,
        data: { projectId: projectId },
        success: function (categories) {
            resetSelect(categorySelect, 'All Categories');
            categorySelect.disabled = false;
            $categorySearch.prop('disabled', false);

            $categoryDropdown.empty();
            $categoryDropdown.append('<div class="select-option" data-value="">All Categories</div>');

            if (categories?.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);

                    const div = document.createElement('div');
                    div.className = 'select-option';
                    div.dataset.value = category.id;
                    div.textContent = category.name;
                    $categoryDropdown.append(div);
                });
            }
        },
        error: function () {
            resetSelect(categorySelect, 'All Categories');
            $categorySearch.prop('disabled', true);
            $categoryDropdown.empty();
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

    resetSelect(accountSelect, 'All Products', true);
    $accountSearch.val('').prop('disabled', true);
    $accountDropdown.empty();

    if (!categoryId) return;

    $accountDropdown.html('<div class="select-option">Loading products...</div>');

    $.ajax({
        url: routes.getAccountsByCategory,
        data: { categoryId: categoryId },
        success: function (accounts) {
            resetSelect(accountSelect, 'All Products');
            accountSelect.disabled = false;
            $accountSearch.prop('disabled', false);

            $accountDropdown.empty();
            $accountDropdown.append('<div class="select-option" data-value="">All Products</div>');

            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);

                    const div = document.createElement('div');
                    div.className = 'select-option';
                    div.dataset.value = account.id;
                    div.textContent = account.name;
                    $accountDropdown.append(div);
                });
            }
        },
        error: function () {
            resetSelect(accountSelect, 'All Products');
            $accountSearch.prop('disabled', true);
            $accountDropdown.empty();
            showError('Failed to load products. Please try again.');
        }
    });
}

function resetAccountDropdown() {
    const accountSelect = document.getElementById('accountId');
    const $accountContainer = $('.select-container').has('#accountId');
    const $accountSearch = $accountContainer.find('.select-search');
    const $accountDropdown = $accountContainer.find('.select-dropdown');

    resetSelect(accountSelect, 'All Products', true);
    $accountSearch.val('').prop('disabled', true);
    $accountDropdown.empty();
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

function setSearchBoxEnabled(enabled) {
    const searchBox = document.getElementById('searchBox');
    if (searchBox) searchBox.disabled = !enabled;
}

function setGenerateReportEnabled(enabled) {
    const btn = document.querySelector('#productReportForm button[type="submit"]');
    if (btn) btn.disabled = !enabled;
}

function generateReport() {
    const searchBox = document.getElementById('searchBox');
    if (searchBox && searchBox.value.trim() !== '') {
        searchBox.value = '';
    }

    initializeReportState();

    const startDateValue = document.getElementById('startDate').value;
    const endDateValue = document.getElementById('endDate').value;

    const params = {
        startDate: startDateValue || null,
        endDate: endDateValue || null,
        Page: 0,
        PageSize: window.reportState.pageSize
    };

    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;
    const locationId = document.getElementById('locationId').value;

    if (!projectId) {
        showError('Project selection is required.');
        setGenerateReportEnabled(true);
        return;
    }

    params.projectId = projectId;
    if (categoryId) params.categoryId = categoryId;
    if (accountId) params.accountId = accountId;
    if (locationId) params.locationId = locationId;

    window.reportState.reportParams = params;

    clearReportData();

    setSearchBoxEnabled(false);

    document.getElementById('reportResults').style.display = 'block';
    document.getElementById('reportData').innerHTML = createLoadingRow(13);

    fetchReportChunk(true);
}

function clearReportData() {
    const reportDataElement = document.getElementById('reportData');
    if (reportDataElement) {
        reportDataElement.innerHTML = '';
    }

    window.reportState.allProducts = [];

    updateElement('totalDebitQty', '0');
    updateElement('totalCreditQty', '0');
    updateElement('netQty', '0');
    updateElement('totalDebit', '0.00');
    updateElement('totalCredit', '0.00');
    updateElement('totalCOGS', '0.00');
    updateElement('totalMargin', '0.00');
    updateElement('avgMarginPercent', '0.00%');

    removeElements(['loadingStatus', 'autoLoadIndicator']);
}

function updateElement(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value;
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
    const url = `${routes.getProductReportData}?_=${timestamp}`;

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

            processProductData(data);

            window.reportState.currentPage++;
            window.reportState.hasMoreData = data.hasMoreData || false;
            window.reportState.totalRecords = data.totalRecords || 0;

            updateLoadingStatus();
        })
        .fail(error => {
            console.error('Error fetching data:', error);
            document.getElementById('reportData').innerHTML =
                '<tr><td colspan="13" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
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

function processProductData(data) {
    if (!data || !data.productSummaries || data.productSummaries.length === 0) {
        if (window.reportState.currentPage === 0) {
            document.getElementById('reportData').innerHTML =
                '<tr><td colspan="13" class="text-center">No product data found for the selected criteria.</td></tr>';
        }
        return;
    }

    window.reportState.allProducts = [...window.reportState.allProducts, ...data.productSummaries];

    appendProductRows(data.productSummaries);

    // Accumulate totals instead of replacing them
    if (data.totals) {
        // Only reset totals on first page
        if (window.reportState.currentPage === 0) {
            window.reportState.totals = {
                totalDebitQty: 0,
                totalCreditQty: 0,
                netQuantity: 0,
                totalDebit: 0,
                totalCredit: 0,
                totalCOGS: 0,
                totalMargin: 0,
                marginPercentage: 0
            };
        }

        // Accumulate totals from each page
        window.reportState.totals.totalDebitQty += data.totals.totalDebitQty || 0;
        window.reportState.totals.totalCreditQty += data.totals.totalCreditQty || 0;
        window.reportState.totals.netQuantity += data.totals.netQuantity || 0;
        window.reportState.totals.totalDebit += data.totals.totalDebit || 0;
        window.reportState.totals.totalCredit += data.totals.totalCredit || 0;
        window.reportState.totals.totalCOGS += data.totals.totalCOGS || 0;
        window.reportState.totals.totalMargin += data.totals.totalMargin || 0;

        // Recalculate margin percentage based on accumulated values
        window.reportState.totals.marginPercentage = window.reportState.totals.totalCredit > 0 
            ? (window.reportState.totals.totalMargin / window.reportState.totals.totalCredit) * 100 
            : 0;

        updateTotals(window.reportState.totals);
    }

    updateColumnVisibility();
}

function appendProductRows(productSummaries) {
    const tbody = document.getElementById('reportData');
    if (!tbody) return;

    const fragment = document.createDocumentFragment();

    productSummaries.forEach(product => {
        if (product.hasPreviousBalance) {
            const prevBalanceRow = document.createElement('tr');
            prevBalanceRow.className = 'previous-balance-row';

            prevBalanceRow.innerHTML = `    
                <td class="product-column">Previous Balance</td>
                <td class="voucher-column text-end">-</td>
                <td class="entry-date-column text-end">-</td>
                <td class="due-date-column text-end">-</td>
                <td class="debit-qty-column text-end">${formatNumber(product.previousDebitQty)}</td>
                <td class="credit-qty-column text-end">${formatNumber(product.previousCreditQty)}</td>
                <td class="balance-qty-column text-end ${(product.previousBalance < 0) ? 'negative-balance' : ''}">${formatNumberWithSign(product.previousBalance)}</td>
                <td class="avg-rate-column text-end">${formatDecimal(product.previousAvgRate)}</td>
                <td class="total-debit-column text-end">${formatDecimal(product.previousDebit)}</td>
                <td class="total-credit-column text-end">${formatDecimal(product.previousCredit)}</td>
                <td class="cogs-column text-end">-</td>
                <td class="margin-column text-end">-</td>
                <td class="margin-percent-column text-end">-</td>
            `;

            fragment.appendChild(prevBalanceRow);
        }

        const row = document.createElement('tr');

        const voucherLink = `<a href="/Reporting/PrintVoucher?journalVoucherId=${product.lastVoucherId}&type=JV" 
                              class="voucher-link" 
                              target="_blank" 
                              title="Open voucher in new window">
                              JV-${product.lastVoucherId}
                           </a>`;

        row.innerHTML = `
            <td class="product-column">${product.productName}</td>
            <td class="voucher-column text-end">${voucherLink}</td>
            <td class="entry-date-column text-end">${product.entryDate}</td>
            <td class="due-date-column text-end">${product.creditableDate}</td>
            <td class="debit-qty-column text-end">${formatNumber(product.totalDebitQty)}</td>
            <td class="credit-qty-column text-end">${formatNumber(product.totalCreditQty)}</td>
            <td class="balance-qty-column text-end ${(product.qtyBalance < 0) ? 'negative-balance' : ''}">${formatNumberWithSign(product.qtyBalance)}</td>
            <td class="avg-rate-column text-end">${formatDecimal(product.averageRate)}</td>
            <td class="total-debit-column text-end">${formatDecimal(product.totalDebit)}</td>
            <td class="total-credit-column text-end">${formatDecimal(product.totalCredit)}</td>
            <td class="cogs-column text-end">${formatDecimal(product.cogs)}</td>
            <td class="margin-column text-end ${(product.margin < 0) ? 'negative-balance' : 'positive-margin'}">${formatDecimalWithSign(product.margin)}</td>
            <td class="margin-percent-column text-end ${(product.marginPercentage < 0) ? 'negative-balance' : 'positive-margin'}">${formatPercent(product.marginPercentage)}</td>
        `;

        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

function updateTotals(totals) {
    if (!totals) return;

    updateElement('totalDebitQty', formatNumber(totals.totalDebitQty));
    updateElement('totalCreditQty', formatNumber(totals.totalCreditQty));
    updateElement('netQty', formatNumberWithSign(totals.netQuantity));
    updateElement('totalDebit', formatDecimal(totals.totalDebit));
    updateElement('totalCredit', formatDecimal(totals.totalCredit));
    updateElement('totalCOGS', formatDecimal(totals.totalCOGS));
    updateElement('totalMargin', formatDecimalWithSign(totals.totalMargin));
    updateElement('avgMarginPercent', formatPercent(totals.marginPercentage));
}

// ==================== UI UPDATES ====================

function initializeDisplay() {
    updateElement('reportData', '');
    removeElements(['loadingStatus', 'autoLoadIndicator']);
    addLoadingStatus();
    addAutoLoadIndicator();
}

function addLoadingStatus() {
    const tableContainer = document.querySelector('#productReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded 0 of ${window.reportState.totalRecords} products`;
    tableContainer.parentNode.insertBefore(loadingStatus, tableContainer.nextSibling);
}

function addAutoLoadIndicator() {
    const tableContainer = document.querySelector('#productReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.getElementById('loadingStatus');
    if (!loadingStatus) return;

    const autoLoadIndicator = document.createElement('div');
    autoLoadIndicator.id = 'autoLoadIndicator';
    autoLoadIndicator.className = 'text-center mt-1 mb-3';
    autoLoadIndicator.innerHTML = `
        <div class="spinner-border spinner-border-sm text-primary d-none" id="loadMoreSpinner" role="status">
            <span class="visually-hidden">Loading more products...</span>
        </div>
    `;
    tableContainer.parentNode.insertBefore(autoLoadIndicator, loadingStatus.nextSibling);
}

function updateLoadingStatus() {
    const existingStatus = document.getElementById('loadingStatus');
    if (existingStatus) {
        existingStatus.remove();
    }

    const tableContainer = document.querySelector('#productReportTable')?.closest('.table-responsive');
    if (!tableContainer) return;

    const loadingStatus = document.createElement('div');
    loadingStatus.id = 'loadingStatus';
    loadingStatus.className = 'text-center mt-3 text-muted';
    loadingStatus.textContent = `Loaded ${window.reportState.allProducts.length} of ${window.reportState.totalRecords} products`;
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

function formatPercent(value) {
    return value?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '%' || '0.00%';
}