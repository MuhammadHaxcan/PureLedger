document.addEventListener('DOMContentLoaded', function () {
    // Form submission handler
    document.getElementById('masterReportForm').addEventListener('submit', function (e) {
        e.preventDefault();
        generateReport();
    });

    // Initialize column toggles
    initColumnToggles();
    
    // Initialize search functionality
    initSearchFunctionality();

    // Initialize category selection change handler
    document.getElementById('categoryId').addEventListener('change', loadAccountsForCategory);

    // Add project change handler to load categories
    document.getElementById('projectId').addEventListener('change', loadCategoriesForProject);

    // Set default dates to today
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('startDate').value = today;
    document.getElementById('endDate').value = today;
});

function initColumnToggles() {
    // Toggle column visibility
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
    // Global search functionality
    const globalSearchInput = document.getElementById('globalReportSearch');
    if (globalSearchInput) {
        globalSearchInput.addEventListener('keyup', performSearch);
        
        // Clear search button
        const clearButton = document.getElementById('clearSearch');
        if (clearButton) {
            clearButton.addEventListener('click', function() {
                globalSearchInput.value = '';
                performSearch();
            });
        }
    }
    
    // Existing report search functionality
    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.addEventListener('keyup', function () {
            const searchText = this.value.toLowerCase();
            filterReportData(searchText);
        });
    }
}

// Unified search function for both search inputs
function performSearch() {
    const searchText = document.getElementById('globalReportSearch').value.toLowerCase();
    filterReportData(searchText);
    
    // Also filter the account and category summary tables
    filterSummaryData('accountGroupData', searchText);
    filterSummaryData('categoryGroupData', searchText);
    
    // Update the in-table search box if it exists
    const reportSearch = document.getElementById('reportSearch');
    if (reportSearch) {
        reportSearch.value = searchText;
    }
}

function filterReportData(searchText) {
    const rows = document.getElementById('reportData').getElementsByTagName('tr');
    let hasVisibleRows = false;

    for (let row of rows) {
        const text = row.textContent.toLowerCase();
        const isVisible = text.includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }
    
    // Show no data message if all rows are filtered out
    if (!hasVisibleRows && rows.length > 0) {
        const noDataRow = document.createElement('tr');
        noDataRow.id = 'noSearchResults';
        noDataRow.innerHTML = '<td colspan="7" class="text-center">No matching records found</td>';
        
        // Remove previous no results message if exists
        const previousNoResults = document.getElementById('noSearchResults');
        if (previousNoResults) previousNoResults.remove();
        
        // Add no results message if search text isn't empty
        if (searchText.length > 0) {
            document.getElementById('reportData').appendChild(noDataRow);
        }
    } else {
        // Remove no results message if we have visible rows
        const previousNoResults = document.getElementById('noSearchResults');
        if (previousNoResults) previousNoResults.remove();
    }
}

function filterSummaryData(tableId, searchText) {
    const rows = document.getElementById(tableId).getElementsByTagName('tr');
    let hasVisibleRows = false;

    for (let row of rows) {
        const text = row.textContent.toLowerCase();
        const isVisible = text.includes(searchText);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    }
    
    // Handle no visible rows
    if (!hasVisibleRows && rows.length > 0 && searchText.length > 0) {
        const noDataRowId = 'noResults' + tableId;
        const noDataRow = document.createElement('tr');
        noDataRow.id = noDataRowId;
        noDataRow.innerHTML = '<td colspan="5" class="text-center">No matching records found</td>';
        
        // Remove previous no results message if exists
        const previousNoResults = document.getElementById(noDataRowId);
        if (previousNoResults) previousNoResults.remove();
        
        document.getElementById(tableId).appendChild(noDataRow);
    } else {
        // Remove no results message if we have visible rows
        const noDataRowId = 'noResults' + tableId;
        const previousNoResults = document.getElementById(noDataRowId);
        if (previousNoResults) previousNoResults.remove();
    }
}

function loadCategoriesForProject() {
    const projectId = document.getElementById('projectId').value;
    const categorySelect = document.getElementById('categoryId');
    
    // Reset category and account dropdowns
    categorySelect.innerHTML = '<option value="">All Categories</option>';
    categorySelect.disabled = true; // Disable while loading
    resetAccountDropdown();
    
    if (!projectId) {
        return;
    }
    
    // Show loading indicator in the select
    categorySelect.innerHTML = '<option value="" disabled selected>Loading categories...</option>';
    
    // Use PureLedger utility for AJAX
    PureLedger.get('/Reporting/GetCategoriesByProject', { projectId: projectId })
        .done(function(categories) {
            // Reset the select first
            categorySelect.innerHTML = '<option value="">All Categories</option>';
            categorySelect.disabled = false;
            
            console.log(`Received ${categories.length} categories:`, categories);
            
            if (categories && categories.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            } else {
                console.warn('No categories received from server');
            }
        })
        .fail(function(xhr, status, error) {
            console.error('Error loading categories:', error);
            categorySelect.innerHTML = '<option value="">All Categories</option>';
            categorySelect.disabled = false;
            alert('Failed to load categories. Please try again or contact support.');
        });
}

function loadAccountsForCategory() {
    const categoryId = document.getElementById('categoryId').value;
    const accountSelect = document.getElementById('accountId');

    accountSelect.innerHTML = '<option value="">All Accounts</option>';
    accountSelect.disabled = true;

    if (!categoryId) {
        return;
    }

    // Show loading indicator
    accountSelect.innerHTML = '<option value="" disabled selected>Loading accounts...</option>';
    
    // Use PureLedger utility for AJAX
    PureLedger.get('/Reporting/GetAccountsByCategory', { categoryId: categoryId })
        .done(function(accounts) {
            console.log(`Received ${accounts.length} accounts:`, accounts);
            
            accountSelect.innerHTML = '<option value="">All Accounts</option>';
            accountSelect.disabled = false;
            updateAccountDropdown(accounts);
        })
        .fail(function(xhr, status, error) {
            console.error('Error loading accounts:', error);
            accountSelect.innerHTML = '<option value="">All Accounts</option>';
            accountSelect.disabled = false;
            alert('Failed to load accounts. Please try again or contact support.');
        });
}

function updateAccountDropdown(accounts) {
    const accountSelect = document.getElementById('accountId');
    accountSelect.innerHTML = '<option value="">All Accounts</option>';

    accounts.forEach(account => {
        const option = document.createElement('option');
        option.value = account.id;
        option.textContent = account.name;
        accountSelect.appendChild(option);
    });
}

function resetAccountDropdown() {
    const accountSelect = document.getElementById('accountId');
    accountSelect.innerHTML = '<option value="">All Accounts</option>';
    accountSelect.disabled = true;
}

function generateReport() {
    // Show loading indicator
    const resultsSection = document.getElementById('reportResults');
    resultsSection.style.display = 'none';
    const loadingIndicator = document.getElementById('loadingIndicator');
    if (loadingIndicator) {
        loadingIndicator.style.display = 'block';
    }

    // Get form values
    const startDate = document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;

    // Build parameters object
    const params = {
        StartDate: startDate,
        EndDate: endDate
    };
    
    if (projectId) params.ProjectId = projectId;
    if (categoryId) params.CategoryId = categoryId;
    if (accountId) params.AccountId = accountId;

    // Use PureLedger utility for AJAX
    PureLedger.get('/Reporting/GetMasterReportData', params)
        .done(function(data) {
            displayReport(data);
        })
        .fail(function(xhr, status, error) {
            console.error('Error generating report:', error);
            alert('Failed to generate report: ' + error);
        })
        .always(function() {
            // Hide loading indicator
            if (loadingIndicator) {
                loadingIndicator.style.display = 'none';
            }
        });
}

function displayReport(data) {
    // Display main report data
    const tbody = document.getElementById('reportData');
    tbody.innerHTML = '';

    if (!data.entries || data.entries.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="text-center">No data found matching the criteria</td></tr>';
        document.getElementById('reportResults').style.display = 'block';
        return;
    }

    data.entries.forEach(entry => {
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>
                <div class="id-container">
                    <a href="/Reporting/PrintVoucher?journalVoucherId=${entry.journalVoucherId}&type=JV" 
                       class="voucher-link" 
                       target="_blank" 
                       title="Open voucher in new window">
                        JV-${entry.journalVoucherId}
                    </a>
                    <span class="entry-id">Entry: ${entry.id}</span>
                </div>
            </td>
            <td class="date-column">${formatDate(entry.transactionDate)}</td>
            <td class="description-column">${entry.description || ''}</td>
            <td class="account-column">${entry.accountName}</td>
            <td class="debit-column text-right">${formatCurrency(entry.debit)}</td>
            <td class="credit-column text-right">${formatCurrency(entry.credit)}</td>
            <td class="balance-column text-right">${formatCurrency(entry.balance)}</td>
        `;
        tbody.appendChild(row);
    });

    // Update totals
    document.getElementById('totalDebit').textContent = formatCurrency(data.totalDebit);
    document.getElementById('totalCredit').textContent = formatCurrency(data.totalCredit);
    document.getElementById('netBalance').textContent = formatCurrency(data.netBalance);

    // Generate account groups
    generateAccountGroups(data.entries);

    // Generate category groups
    generateCategoryGroups(data.entries);

    // Show results section
    document.getElementById('reportResults').style.display = 'block';
}

function generateAccountGroups(entries) {
    const groupedByAccount = {};

    // Group by account
    entries.forEach(entry => {
        if (!entry.accountId) return;

        const accountKey = entry.accountId.toString();
        if (!groupedByAccount[accountKey]) {
            // Create the formatted ID as projectId-categoryId-accountId
            const formattedId = `${entry.projectId}-${entry.categoryId}-${entry.accountId}`;
            
            groupedByAccount[accountKey] = {
                debit: 0,
                credit: 0,
                id: formattedId, // Use the full formatted ID
                name: entry.accountName
            };
        }

        groupedByAccount[accountKey].debit += entry.debit;
        groupedByAccount[accountKey].credit += entry.credit;
    });

    // Generate rows
    const tbody = document.getElementById('accountGroupData');
    tbody.innerHTML = '';

    if (Object.keys(groupedByAccount).length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">No account data available</td></tr>';
        return;
    }

    Object.values(groupedByAccount).forEach(data => {
        const balance = data.debit - data.credit;
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${data.id}</td>
            <td>${data.name}</td>
            <td class="text-right">${formatCurrency(data.debit)}</td>
            <td class="text-right">${formatCurrency(data.credit)}</td>
            <td class="text-right">${formatCurrency(balance)}</td>
        `;
        tbody.appendChild(row);
    });
}

function generateCategoryGroups(entries) {
    const groupedByCategory = {};

    // Group by category
    entries.forEach(entry => {
        if (!entry.categoryId) return;

        const categoryKey = entry.categoryId.toString();
        if (!groupedByCategory[categoryKey]) {
            // Create the formatted ID as projectId-categoryId
            const formattedId = `${entry.projectId}-${entry.categoryId}`;
            
            groupedByCategory[categoryKey] = {
                debit: 0,
                credit: 0,
                id: formattedId, // Use the formatted ID
                name: entry.categoryName
            };
        }

        groupedByCategory[categoryKey].debit += entry.debit;
        groupedByCategory[categoryKey].credit += entry.credit;
    });

    // Generate rows
    const tbody = document.getElementById('categoryGroupData');
    tbody.innerHTML = '';

    if (Object.keys(groupedByCategory).length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">No category data available</td></tr>';
        return;
    }

    Object.values(groupedByCategory).forEach(data => {
        const balance = data.debit - data.credit;
        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${data.id}</td>
            <td>${data.name}</td>
            <td class="text-right">${formatCurrency(data.debit)}</td>
            <td class="text-right">${formatCurrency(data.credit)}</td>
            <td class="text-right">${formatCurrency(balance)}</td>
        `;
        tbody.appendChild(row);
    });
}

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

