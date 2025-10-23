document.addEventListener('DOMContentLoaded', () => {
    initializeReportState();
    setupEventListeners();
    setDefaultDates();
});

function initializeReportState() {
    window.reportState = {
        isLoading: false,
        allProducts: {},
        reportParams: null
    };
}

function setupEventListeners() {
    document.getElementById('productReportForm').addEventListener('submit', e => {
        e.preventDefault();
        generateReport();
    });

    initSearchFunctionality();
    document.getElementById('projectId').addEventListener('change', loadCategoriesForProject);
    document.getElementById('categoryId').addEventListener('change', loadAccountsForCategory);
    
    // Add event listener for the clear dates button
    const clearDatesBtn = document.getElementById('clearDatesBtn');
    if (clearDatesBtn) {
        clearDatesBtn.addEventListener('click', clearDates);
    }
}

function setDefaultDates() {
    // Leave date fields empty by default - similar to master-report.js
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
}

function clearDates() {
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
}

// ==================== UI CONTROLS ====================

function initSearchFunctionality() {
    const globalSearchInput = document.getElementById('globalReportSearch');
    if (globalSearchInput) {
        globalSearchInput.addEventListener('keyup', debounce(performSearch, 250));

        const clearButton = document.getElementById('clearSearch');
        if (clearButton) {
            clearButton.addEventListener('click', () => {
                globalSearchInput.value = '';
                performSearch();
            });
        }
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

function performSearch() {
    const searchText = document.getElementById('globalReportSearch').value.toLowerCase();
    filterReportData(searchText);

    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.value = searchText;
    }
}

function filterReportData(searchText) {
    const rows = document.getElementById('reportData').getElementsByTagName('tr');

    // Track products for grouping
    const productGroups = {};

    // First pass: identify which product groups should be visible
    for (let i = 0; i < rows.length; i++) {
        const row = rows[i];
        const isProductRow = !row.classList.contains('previous-balance-row');

        // For product rows, check if they match the search
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

    // Second pass: show/hide rows based on product visibility
    let hasVisibleRows = false;

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
            if (isVisible) hasVisibleRows = true;
        }
    }

    handleNoMatchingRows('reportData', 'noSearchResults', !hasVisibleRows && rows.length > 0, searchText, 13);
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

    $.ajax({
        url: routes.getCategoriesByProject,
        data: { projectId: projectId },
        success: function (categories) {
            resetSelect(categorySelect, 'All Categories');
            categorySelect.disabled = false;

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

    accountSelect.innerHTML = '<option value="" disabled selected>Loading products...</option>';

    $.ajax({
        url: routes.getAccountsByCategory,
        data: { categoryId: categoryId },
        success: function (accounts) {
            resetSelect(accountSelect, 'All Products');
            accountSelect.disabled = false;
            
            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);
                });
            }
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
    window.reportState.isLoading = true;
    window.reportState.allProducts = {};

    const startDateValue = document.getElementById('startDate').value;
    const endDateValue = document.getElementById('endDate').value;
    
    // Create params object with null dates if fields are empty
    const params = {
        startDate: startDateValue || null,
        endDate: endDateValue || null
    };

    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;
    const locationId = document.getElementById('locationId').value;

    if (!projectId) {
        showError('Project selection is required.');
        window.reportState.isLoading = false;
        return;
    }

    params.projectId = projectId;
    if (categoryId) params.categoryId = categoryId;
    if (accountId) params.accountId = accountId;
    if (locationId) params.locationId = locationId;

    window.reportState.reportParams = params;

    clearReportData();

    document.getElementById('reportResults').style.display = 'block';
    document.getElementById('reportData').innerHTML = createLoadingRow(13);

    fetchProductData();
}

function clearReportData() {
    document.getElementById('reportData').innerHTML = '';
    window.reportState.allProducts = {};

    // Reset summary totals
    document.getElementById('totalDebitQty').textContent = '0';
    document.getElementById('totalCreditQty').textContent = '0';
    document.getElementById('netQty').textContent = '0';
    document.getElementById('totalDebit').textContent = '0.00';
    document.getElementById('totalCredit').textContent = '0.00';
    document.getElementById('totalCOGS').textContent = '0.00';
    document.getElementById('totalMargin').textContent = '0.00';
    document.getElementById('avgMarginPercent').textContent = '0.00%';
}

function createLoadingRow(colSpan) {
    return `<tr><td colspan="${colSpan}" class="text-center">
        <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading...</span>
        </div>
    </td></tr>`;
}

// ==================== DATA FETCHING ====================

function fetchProductData() {
    if (window.reportState.isLoading) {
        const params = { ...window.reportState.reportParams };

        console.log(`Fetching product report data with params:`, params);

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
                console.log('Received product data:', data);
                processProductData(data);
            })
            .fail(error => {
                console.error('Error fetching data:', error);
                document.getElementById('reportData').innerHTML =
                    '<tr><td colspan="13" class="text-center text-danger">Error loading report data. Please try again.</td></tr>';
                window.reportState.isLoading = false;
            })
            .always(() => {
                window.reportState.isLoading = false;
            });
    }
}

// ==================== DATA PROCESSING ====================

function processProductData(data) {
    if (!data || !data.productSummaries || data.productSummaries.length === 0) {
        document.getElementById('reportData').innerHTML =
            '<tr><td colspan="13" class="text-center">No product data found for the selected criteria.</td></tr>';
        return;
    }

    renderProductSummaries(data.productSummaries);
    updateTotals(data.totals);
}

function renderProductSummaries(productSummaries) {
    const tbody = document.getElementById('reportData');
    tbody.innerHTML = '';

    const fragment = document.createDocumentFragment();

    productSummaries.forEach(product => {
        // If this product has a previous balance, add a "Previous Balance" row first
        if (product.hasPreviousBalance) {
            const prevBalanceRow = document.createElement('tr');
            prevBalanceRow.className = 'previous-balance-row';

            prevBalanceRow.innerHTML = `
                <td>Previous Balance</td>
                <td class="text-end">-</td>
                <td class="text-end">-</td>
                <td class="text-end">-</td>
                <td class="text-end">${formatNumber(product.previousDebitQty)}</td>
                <td class="text-end">${formatNumber(product.previousCreditQty)}</td>
                <td class="text-end ${(product.previousBalance < 0) ? 'negative-balance' : ''}">${formatNumberWithSign(product.previousBalance)}</td>
                <td class="text-end">${formatDecimal(product.previousAvgRate)}</td>
                <td class="text-end">${formatDecimal(product.previousDebit)}</td>
                <td class="text-end">${formatDecimal(product.previousCredit)}</td>
                <td class="text-end">-</td>
                <td class="text-end">-</td>
                <td class="text-end">-</td>
            `;

            fragment.appendChild(prevBalanceRow);
        }

        // Add the regular product row
        const row = document.createElement('tr');

        // Format voucher link
        const voucherLink = `<a href="/Reporting/PrintVoucher?journalVoucherId=${product.lastVoucherId}&type=JV" 
                              class="voucher-link" 
                              target="_blank" 
                              title="Open voucher in new window">
                              JV-${product.lastVoucherId}
                           </a>`;

        row.innerHTML = `
            <td>${product.productName}</td>
            <td class="text-end">${voucherLink}</td>
            <td class="text-end">${product.entryDate}</td>
            <td class="text-end">${product.creditableDate}</td>
            <td class="text-end">${formatNumber(product.totalDebitQty)}</td>
            <td class="text-end">${formatNumber(product.totalCreditQty)}</td>
            <td class="text-end ${(product.qtyBalance < 0) ? 'negative-balance' : ''}">${formatNumberWithSign(product.qtyBalance)}</td>
            <td class="text-end">${formatDecimal(product.averageRate)}</td>
            <td class="text-end">${formatDecimal(product.totalDebit)}</td>
            <td class="text-end">${formatDecimal(product.totalCredit)}</td>
            <td class="text-end">${formatDecimal(product.cogs)}</td>
            <td class="text-end ${(product.margin < 0) ? 'negative-balance' : 'positive-margin'}">${formatDecimalWithSign(product.margin)}</td>
            <td class="text-end ${(product.marginPercentage < 0) ? 'negative-balance' : 'positive-margin'}">${formatPercent(product.marginPercentage)}</td>
        `;

        fragment.appendChild(row);
    });

    tbody.appendChild(fragment);
}

function updateTotals(totals) {
    if (!totals) return;

    document.getElementById('totalDebitQty').textContent = formatNumber(totals.totalDebitQty);
    document.getElementById('totalCreditQty').textContent = formatNumber(totals.totalCreditQty);
    document.getElementById('netQty').textContent = formatNumberWithSign(totals.netQuantity);
    document.getElementById('totalDebit').textContent = formatDecimal(totals.totalDebit);
    document.getElementById('totalCredit').textContent = formatDecimal(totals.totalCredit);
    document.getElementById('totalCOGS').textContent = formatDecimal(totals.totalCOGS);
    document.getElementById('totalMargin').textContent = formatDecimalWithSign(totals.totalMargin);
    document.getElementById('avgMarginPercent').textContent = formatPercent(totals.marginPercentage);
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