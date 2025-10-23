/**
 * Account Report JavaScript
 * Handles all functionality for the Account Report page including:
 * - Searchable dropdown filters
 * - Data fetching and rendering
 * - Project requirement validation
 */

document.addEventListener('DOMContentLoaded', () => {
    initializeSelectDropdowns();
    setupEventListeners();
});

let dataTable = null;

// ==================== SEARCHABLE DROPDOWN LOGIC ====================

function initializeSelectDropdowns() {
    // Create select-container structure for existing dropdowns if needed
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');
    convertToSearchableSelect('accountId', 'Search account...');

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
    else if (selectId === 'categoryId') iconClass = 'fa-list';
    else if (selectId === 'accountId') iconClass = 'fa-user';

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

function initializeDropdownStates() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    if (!projectId || !categoryId || !accountId) return;

    const $categorySearch = document.querySelector('.category-search');
    const $accountSearch = document.querySelector('.account-search');

    if ($categorySearch) $categorySearch.disabled = true;
    if ($accountSearch) $accountSearch.disabled = true;

    resetSelect(categoryId, 'All Categories', true);
    resetSelect(accountId, 'All Accounts', true);
}

function setupSelectSearch() {
    // Handle input in search box
    document.addEventListener('input', function (e) {
        if (!e.target.classList.contains('select-search')) return;

        const input = e.target;
        if (input.disabled) return;

        const container = input.closest('.select-container');
        const dropdown = container.querySelector('.select-dropdown');
        const searchTerm = input.value.toLowerCase();

        let visibleCount = 0;
        dropdown.querySelectorAll('.select-option').forEach(option => {
            const text = option.textContent.toLowerCase();
            const shouldShow = text.includes(searchTerm) && visibleCount < 8;
            option.classList.toggle('filtered-out', !shouldShow);
            if (shouldShow) visibleCount++;
        });

        dropdown.classList.add('show');
    });

    // Show dropdown on focus
    document.addEventListener('focus', function (e) {
        if (!e.target.classList.contains('select-search')) return;

        const input = e.target;
        if (input.disabled) return;

        // Hide all other dropdowns
        document.querySelectorAll('.select-dropdown').forEach(dd => dd.classList.remove('show'));

        const container = input.closest('.select-container');
        const dropdown = container.querySelector('.select-dropdown');
        const select = container.querySelector('select');
        const currentValue = select.value;

        dropdown.querySelectorAll('.select-option').forEach(option => {
            option.classList.remove('selected', 'filtered-out');
        });

        if (currentValue) {
            const selectedOption = dropdown.querySelector(`.select-option[data-value="${currentValue}"]`);
            if (selectedOption) {
                selectedOption.classList.add('selected');
            }
        }

        dropdown.classList.add('show');
    }, true);

    // Handle keyboard navigation
    document.addEventListener('keydown', function (e) {
        if (!e.target.classList.contains('select-search')) return;

        const dropdown = e.target.closest('.select-container').querySelector('.select-dropdown');
        const options = dropdown.querySelectorAll('.select-option:not(.filtered-out)');
        let selected = dropdown.querySelector('.select-option.selected');

        if (e.key === 'Tab' && dropdown.classList.contains('show')) {
            dropdown.classList.remove('show');
            return;
        }

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                if (!selected) {
                    options[0]?.classList.add('selected');
                } else {
                    const currentIndex = Array.from(options).indexOf(selected);
                    if (currentIndex < options.length - 1) {
                        selected.classList.remove('selected');
                        options[currentIndex + 1].classList.add('selected');
                    }
                }
                break;

            case 'ArrowUp':
                e.preventDefault();
                if (!selected) {
                    options[options.length - 1]?.classList.add('selected');
                } else {
                    const currentIndex = Array.from(options).indexOf(selected);
                    if (currentIndex > 0) {
                        selected.classList.remove('selected');
                        options[currentIndex - 1].classList.add('selected');
                    }
                }
                break;

            case 'Enter':
                if (dropdown.classList.contains('show')) {
                    e.preventDefault();
                    selected = dropdown.querySelector('.select-option.selected');
                    if (selected) {
                        selectOption(selected, e.target.closest('.select-container'));
                    } else if (options.length > 0) {
                        selectOption(options[0], e.target.closest('.select-container'));
                    }
                }
                break;

            case 'Escape':
                e.preventDefault();
                dropdown.classList.remove('show');
                break;
        }
    });

    // Handle click on dropdown option
    document.addEventListener('click', function (e) {
        if (e.target.classList.contains('select-option') && !e.target.classList.contains('filtered-out')) {
            selectOption(e.target, e.target.closest('.select-container'));
        }
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', function (e) {
        if (!e.target.closest('.select-container')) {
            document.querySelectorAll('.select-dropdown').forEach(dd => dd.classList.remove('show'));
        }
    });
}

function selectOption(option, container) {
    const value = option.dataset.value;
    const text = option.textContent;

    // Update the hidden select and trigger change event
    const select = container.querySelector('select');
    select.value = value;

    // Create and dispatch change event
    const changeEvent = new Event('change', { bubbles: true });
    select.dispatchEvent(changeEvent);

    // Update the search input text
    const searchInput = container.querySelector('.select-search');
    searchInput.value = text;

    // Update the visual state of the dropdown
    const dropdown = container.querySelector('.select-dropdown');
    dropdown.classList.remove('show');
    dropdown.querySelectorAll('.select-option').forEach(opt => opt.classList.remove('selected'));
    option.classList.add('selected');
}

// ==================== EVENT LISTENERS ====================

function setupEventListeners() {
    const form = document.getElementById('accountReportForm');
    if (form) {
        form.addEventListener('submit', e => {
            e.preventDefault();
            loadReport();
        });
    }

    setupSelectSearch();

    // Set up project and category change handlers
    document.addEventListener('change', function (e) {
        if (e.target.id === 'projectId') {
            handleProjectChange();
        } else if (e.target.id === 'categoryId') {
            handleCategoryChange();
        }
    });

    // Search box functionality
    const searchBox = document.getElementById('searchBox');
    if (searchBox) {
        searchBox.addEventListener('input', function () {
            if (dataTable) {
                dataTable.search(this.value).draw();
            } else {
                // Fallback to manual filtering if DataTable isn't initialized
                const val = this.value.toLowerCase();
                document.querySelectorAll('#reportData tr').forEach(row => {
                    const shouldShow = row.textContent.toLowerCase().indexOf(val) > -1;
                    row.style.display = shouldShow ? '' : 'none';
                });
            }
        });
    }
}

function handleProjectChange() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');

    const categoryContainer = document.querySelector('.select-container:has(#categoryId)');
    const categorySearch = categoryContainer?.querySelector('.select-search');
    const categoryDropdown = categoryContainer?.querySelector('.select-dropdown');

    if (!projectId || !categoryId) return;

    if (projectId.value) {
        resetSelect(categoryId, 'All Categories', false);
        if (categorySearch) {
            categorySearch.value = '';
            categorySearch.disabled = false;
        }
        resetAccountDropdown();
        loadCategoriesForProject();
    } else {
        resetSelect(categoryId, 'All Categories', true);
        if (categorySearch) {
            categorySearch.value = '';
            categorySearch.disabled = true;
        }
        if (categoryDropdown) categoryDropdown.innerHTML = '';
        resetAccountDropdown();
    }
}

function handleCategoryChange() {
    const categoryId = document.getElementById('categoryId');
    const accountId = document.getElementById('accountId');

    const accountContainer = document.querySelector('.select-container:has(#accountId)');
    const accountSearch = accountContainer?.querySelector('.select-search');
    const accountDropdown = accountContainer?.querySelector('.select-dropdown');

    if (!categoryId || !accountId) return;

    if (categoryId.value) {
        resetSelect(accountId, 'All Accounts', false);
        if (accountSearch) {
            accountSearch.value = '';
            accountSearch.disabled = false;
        }
        loadAccountsForCategory();
    } else {
        resetSelect(accountId, 'All Accounts', true);
        if (accountSearch) {
            accountSearch.value = '';
            accountSearch.disabled = true;
        }
        if (accountDropdown) accountDropdown.innerHTML = '';
    }
}

// ==================== DATA LOADING ====================

function loadCategoriesForProject() {
    const projectId = document.getElementById('projectId').value;

    // Get the elements we need to update
    const categoryContainer = document.querySelector('.select-container:has(#categoryId)');
    const categorySearch = categoryContainer?.querySelector('.select-search');
    const categoryDropdown = categoryContainer?.querySelector('.select-dropdown');
    const categorySelect = document.getElementById('categoryId');

    // Reset category and account
    resetSelect(categorySelect, 'All Categories', true);
    if (categorySearch) {
        categorySearch.value = '';
        categorySearch.disabled = true;
    }
    if (categoryDropdown) categoryDropdown.innerHTML = '';
    resetAccountDropdown();

    if (!projectId) return;

    if (categoryDropdown) {
        categoryDropdown.innerHTML = '<div class="select-option">Loading categories...</div>';
    }

    // Use jQuery for AJAX since PureLedger utility isn't available
    $.ajax({
        url: routes.getCategoriesByProject,
        method: 'GET',
        data: { projectId: projectId },
        dataType: 'json'
    })
        .done(function (categories) {
            resetSelect(categorySelect, 'All Categories');
            categorySelect.disabled = false;
            if (categorySearch) categorySearch.disabled = false;

            if (categoryDropdown) {
                categoryDropdown.innerHTML = '';
                const allOption = document.createElement('div');
                allOption.className = 'select-option';
                allOption.dataset.value = '';
                allOption.textContent = 'All Categories';
                categoryDropdown.appendChild(allOption);
            }

            if (categories?.length > 0) {
                categories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);

                    // Add option to dropdown
                    if (categoryDropdown) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = category.id;
                        div.textContent = category.name;
                        categoryDropdown.appendChild(div);
                    }
                });
            }
        })
        .fail(function (xhr, status, error) {
            resetSelect(categorySelect, 'All Categories');
            if (categorySearch) categorySearch.disabled = true;
            if (categoryDropdown) categoryDropdown.innerHTML = '';
            showAlert('danger', 'Failed to load categories: ' + (error || 'Unknown error'));
        });
}

function loadAccountsForCategory() {
    const categoryId = document.getElementById('categoryId').value;

    // Get the elements we need to update
    const accountContainer = document.querySelector('.select-container:has(#accountId)');
    const accountSearch = accountContainer?.querySelector('.select-search');
    const accountDropdown = accountContainer?.querySelector('.select-dropdown');
    const accountSelect = document.getElementById('accountId');

    resetSelect(accountSelect, 'All Accounts', true);
    if (accountSearch) {
        accountSearch.value = '';
        accountSearch.disabled = true;
    }
    if (accountDropdown) accountDropdown.innerHTML = '';

    if (!categoryId) return;

    if (accountDropdown) {
        accountDropdown.innerHTML = '<div class="select-option">Loading accounts...</div>';
    }

    // Use jQuery for AJAX
    $.ajax({
        url: routes.getAccountsByCategory,
        method: 'GET',
        data: { categoryId: categoryId },
        dataType: 'json'
    })
        .done(function (accounts) {
            resetSelect(accountSelect, 'All Accounts');
            accountSelect.disabled = false;
            if (accountSearch) accountSearch.disabled = false;

            if (accountDropdown) {
                accountDropdown.innerHTML = '';
                const allOption = document.createElement('div');
                allOption.className = 'select-option';
                allOption.dataset.value = '';
                allOption.textContent = 'All Accounts';
                accountDropdown.appendChild(allOption);
            }

            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);

                    // Add option to dropdown
                    if (accountDropdown) {
                        const div = document.createElement('div');
                        div.className = 'select-option';
                        div.dataset.value = account.id;
                        div.textContent = account.name;
                        accountDropdown.appendChild(div);
                    }
                });
            }
        })
        .fail(function (xhr, status, error) {
            resetSelect(accountSelect, 'All Accounts');
            if (accountSearch) accountSearch.disabled = true;
            if (accountDropdown) accountDropdown.innerHTML = '';
            showAlert('danger', 'Failed to load accounts: ' + (error || 'Unknown error'));
        });
}

function resetAccountDropdown() {
    const accountSelect = document.getElementById('accountId');
    if (!accountSelect) return;

    const accountContainer = document.querySelector('.select-container:has(#accountId)');
    const accountSearch = accountContainer?.querySelector('.select-search');
    const accountDropdown = accountContainer?.querySelector('.select-dropdown');

    resetSelect(accountSelect, 'All Accounts', true);
    if (accountSearch) {
        accountSearch.value = '';
        accountSearch.disabled = true;
    }
    if (accountDropdown) accountDropdown.innerHTML = '';
}

function resetSelect(select, defaultText, disable = false) {
    if (!select) return;

    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

// ==================== REPORT GENERATION ====================

function loadReport() {
    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;
    const accountId = document.getElementById('accountId').value;

    // Project is now required
    if (!projectId) {
        showAlert('warning', 'Please select a Project');
        document.getElementById('reportData').innerHTML = '<tr><td colspan="6" class="text-center">Please select a Project to generate report</td></tr>';
        updateReportSummary(0);
        return;
    }

    document.getElementById('reportData').innerHTML = '<tr><td colspan="6" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>';

    // Use jQuery for AJAX
    $.ajax({
        url: routes.getAccountReportData,
        method: 'GET',
        data: {
            projectId: projectId,
            categoryId: categoryId,
            accountId: accountId
        },
        dataType: 'json'
    })
        .done(function (data) {
            // Destroy existing DataTable if it exists
            if (dataTable) {
                dataTable.destroy();
                dataTable = null;
            }

            if (!data || data.length === 0) {
                document.getElementById('reportData').innerHTML = '<tr><td colspan="6" class="text-center">No data found</td></tr>';
                updateReportSummary(0);
                return;
            }

            let rows = '';
            let totalDebit = 0;
            let totalCredit = 0;
            let totalBalance = 0;

            data.forEach(r => {
                const debit = parseFloat(r.totalDebit) || 0;
                const credit = parseFloat(r.totalCredit) || 0;
                const balance = parseFloat(r.netBalance) || 0;

                totalDebit += debit;
                totalCredit += credit;
                totalBalance += balance;

                // Determine if balance should be highlighted as negative
                const balanceClass = balance < 0 ? 'negative-balance' : '';

                rows += `<tr>
                <td>${r.accountId}</td>
                <td>${r.categoryName || '-'}</td>
                <td>${r.accountName}</td>
                <td class="text-end">${formatCurrency(debit)}</td>
                <td class="text-end">${formatCurrency(credit)}</td>
                <td class="text-end ${balanceClass}">${formatCurrency(balance)}</td>
            </tr>`;
            });

            document.getElementById('reportData').innerHTML = rows;

            // Initialize DataTable with improved configuration
            initializeDataTable();

            // Update the summary
            updateReportSummary(data.length, totalDebit, totalCredit, totalBalance);
        })
        .fail(function (xhr, status, error) {
            document.getElementById('reportData').innerHTML = '<tr><td colspan="6" class="text-center text-danger">Error loading data</td></tr>';
            showAlert('danger', 'Failed to load report data: ' + (error || 'Unknown error'));
            updateReportSummary(0);
        });
}

// ==================== DATA TABLE INITIALIZATION ====================

function initializeDataTable() {
    // Only initialize if we have data
    const tbody = document.getElementById('reportData');
    if (!tbody || !tbody.children.length) return;

    // Add a footer row to the table if it doesn't exist
    const table = document.getElementById('accountReportTable');
    if (!table.querySelector('tfoot')) {
        const tfoot = document.createElement('tfoot');
        tfoot.innerHTML = `
            <tr class="table-secondary fw-bold">
                <td colspan="3" class="text-end">TOTAL</td>
                <td class="text-end"></td>
                <td class="text-end"></td>
                <td class="text-end"></td>
            </tr>
        `;
        table.appendChild(tfoot);
    }

    try {
        dataTable = $('#accountReportTable').DataTable({
            "order": [[2, "asc"]],
            "language": {
                "emptyTable": "No accounts available",
                "search": "Search accounts:",
                "info": "Showing _START_ to _END_ of _TOTAL_ accounts",
                "lengthMenu": "Show _MENU_ accounts"
            },
            "pageLength": 25,
            "lengthMenu": [[10, 25, 50, 100, -1], [10, 25, 50, 100, "All"]],
            "dom": 'lBfrtip',
            "buttons": [
                {
                    extend: 'excel',
                    text: '<i class="fas fa-file-excel"></i> Excel',
                    className: 'btn btn-sm btn-success',
                    title: 'Account Report ' + new Date().toLocaleDateString(),
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5]
                    }
                },
                {
                    extend: 'pdf',
                    text: '<i class="fas fa-file-pdf"></i> PDF',
                    className: 'btn btn-sm btn-danger',
                    title: 'Account Report ' + new Date().toLocaleDateString(),
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5]
                    }
                },
                {
                    extend: 'print',
                    text: '<i class="fas fa-print"></i> Print',
                    className: 'btn btn-sm btn-info',
                    title: 'Account Report ' + new Date().toLocaleDateString(),
                    footer: true,
                    exportOptions: {
                        columns: [0, 1, 2, 3, 4, 5]
                    }
                }
            ],
            "footerCallback": function (row, data, start, end, display) {
                const api = this.api();
                function parseValue(value) {
                    if (typeof value === 'string') {
                        return parseFloat(value.replace(/,/g, '')) || 0;
                    }
                    if (typeof value === 'number') {
                        return value;
                    }
                    return 0;
                }
                // Calculate column sums for debit, credit, and balance
                const debitTotal = api.column(3, { page: 'current' }).data()
                    .reduce((acc, val) => acc + parseValue(val), 0);

                const creditTotal = api.column(4, { page: 'current' }).data()
                    .reduce((acc, val) => acc + parseValue(val), 0);

                const balanceTotal = api.column(5, { page: 'current' }).data()
                    .reduce((acc, val) => acc + parseValue(val), 0);

                $(api.column(3).footer()).html(formatCurrency(debitTotal));
                $(api.column(4).footer()).html(formatCurrency(creditTotal));
                $(api.column(5).footer()).html(formatCurrency(balanceTotal));
            }
        });
    } catch (error) {
        console.error('Error initializing DataTable:', error);
    }
}

// ==================== UTILITY FUNCTIONS ====================

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value);
}

function updateReportSummary(count, totalDebit = 0, totalCredit = 0, totalBalance = 0) {
    const summaryElement = document.getElementById('reportSummary');
    if (!summaryElement) return;

    if (count > 0) {
        const balanceClass = totalBalance < 0 ? 'text-danger' : 'text-success';
        summaryElement.innerHTML = `
            <span class="badge bg-primary">${count} accounts</span>
            <span class="badge bg-secondary ms-2">Total Balance: <span class="${balanceClass}">${formatCurrency(totalBalance)}</span></span>
        `;
    } else {
        summaryElement.innerHTML = `<span class="badge bg-secondary">No data</span>`;
    }
}

function showAlert(type, message) {
    const icon = type === 'success' ? 'check-circle' :
        type === 'warning' ? 'exclamation-triangle' :
            'exclamation-circle';

    const alertElement = document.createElement('div');
    alertElement.className = `alert alert-${type} alert-dismissible fade show`;
    alertElement.setAttribute('role', 'alert');
    alertElement.innerHTML = `
        <i class="fas fa-${icon} me-2"></i>${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;

    const alertArea = document.getElementById('alertArea');
    if (alertArea) {
        alertArea.appendChild(alertElement);

        // Auto dismiss after 5 seconds
        setTimeout(function () {
            if (alertElement.parentNode) {
                alertElement.parentNode.removeChild(alertElement);
            }
        }, 5000);
    }
}