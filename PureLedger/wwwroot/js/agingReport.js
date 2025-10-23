/**
 * Aging Report JavaScript
 * Handles all functionality for the Aging Report page including:
 * - Searchable dropdown filters
 * - AJAX category loading based on project selection
 * - Data fetching and rendering
 */

document.addEventListener('DOMContentLoaded', () => {
    initializeSelectDropdowns();
    initializeForm();
    setDefaultReferenceDate();
});

let dataTable = null;

// ==================== SEARCHABLE DROPDOWN LOGIC ====================

function initializeSelectDropdowns() {
    // Create select-container structure for existing dropdowns if needed
    convertToSearchableSelect('projectId', 'Search project...');
    convertToSearchableSelect('categoryId', 'Search category...');

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

    if (!projectId || !categoryId) return;

    const categorySearch = document.querySelector('.category-search');

    // Lock category dropdown by default
    if (categorySearch) categorySearch.disabled = true;
    resetSelect(categoryId, 'Select Category', true);

    // If project is already selected (for some reason), enable category
    if (projectId.value) {
        categoryId.disabled = false;
        if (categorySearch) categorySearch.disabled = false;
    }
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

// ==================== DROPDOWN LOCKING LOGIC ====================

function handleProjectChange() {
    const projectId = document.getElementById('projectId');
    const categoryId = document.getElementById('categoryId');

    const categoryContainer = document.querySelector('.select-container:has(#categoryId)');
    const categorySearch = categoryContainer?.querySelector('.select-search');
    const categoryDropdown = categoryContainer?.querySelector('.select-dropdown');

    if (!projectId || !categoryId) return;

    if (projectId.value) {
        // Project selected - enable category and load categories via AJAX
        resetSelect(categoryId, 'Select Category', false);
        if (categorySearch) {
            categorySearch.value = '';
            categorySearch.disabled = false;
        }
        loadCategoriesForProject();
    } else {
        // No project selected - lock category
        resetSelect(categoryId, 'Select Category', true);
        if (categorySearch) {
            categorySearch.value = '';
            categorySearch.disabled = true;
        }
        if (categoryDropdown) categoryDropdown.innerHTML = '';
    }
}

function loadCategoriesForProject() {
    const projectId = document.getElementById('projectId').value;

    // Get the elements we need to update
    const categoryContainer = document.querySelector('.select-container:has(#categoryId)');
    const categorySearch = categoryContainer?.querySelector('.select-search');
    const categoryDropdown = categoryContainer?.querySelector('.select-dropdown');
    const categorySelect = document.getElementById('categoryId');

    // Reset category dropdown
    resetSelect(categorySelect, 'Select Category', true);
    if (categorySearch) {
        categorySearch.value = '';
        categorySearch.disabled = true;
    }
    if (categoryDropdown) categoryDropdown.innerHTML = '';

    if (!projectId) return;

    // Show loading state
    if (categoryDropdown) {
        categoryDropdown.innerHTML = '<div class="select-option">Loading categories...</div>';
    }

    // Use jQuery AJAX to match master-report.js implementation
    $.ajax({
        url: routes.getCategoriesByProject,
        method: 'GET',
        data: { projectId: projectId },
        dataType: 'json'
    })
        .done(function (categories) {
            // Reset and enable category dropdown
            resetSelect(categorySelect, 'Select Category');
            categorySelect.disabled = false;
            if (categorySearch) categorySearch.disabled = false;

            // Clear dropdown and add default option
            if (categoryDropdown) {
                categoryDropdown.innerHTML = '';
                const defaultOption = document.createElement('div');
                defaultOption.className = 'select-option';
                defaultOption.dataset.value = '';
                defaultOption.textContent = 'Select Category';
                categoryDropdown.appendChild(defaultOption);
            }

            // Add categories to both select and dropdown
            if (categories?.length > 0) {
                categories.forEach(category => {
                    // Add to select element
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);

                    // Add to dropdown
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
            console.error('Error loading categories:', error);

            // Reset to disabled state on error
            resetSelect(categorySelect, 'Select Category', true);
            if (categorySearch) categorySearch.disabled = true;
            if (categoryDropdown) categoryDropdown.innerHTML = '';

            showAlert('danger', 'Failed to load categories: ' + (error || 'Unknown error'));
        });
}

function resetSelect(select, defaultText, disable = false) {
    if (!select) return;

    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

// ==================== EVENT LISTENERS ====================

function initializeForm() {
    try {
        const form = document.getElementById('agingReportForm');
        if (form) {
            form.addEventListener('submit', async function (e) {
                e.preventDefault();
                await generateReport();
            });
        }

        setupSelectSearch();

        // Set up project change handlers for AJAX category loading
        document.addEventListener('change', function (e) {
            if (e.target.id === 'projectId') {
                handleProjectChange();
            }
        });

        // Press Enter to submit on key inputs
        const referenceDate = document.getElementById('referenceDate');
        if (referenceDate) {
            referenceDate.addEventListener('keypress', function (e) {
                if (e.which === 13) {
                    form.dispatchEvent(new Event('submit'));
                }
            });
        }
    } catch (err) {
        console.error('Error initializing form:', err);
        showAlert('danger', 'Error initializing form: ' + err.message);
    }
}

function setDefaultReferenceDate() {
    // Set default reference date to today if not set
    const refDateInput = document.getElementById('referenceDate');
    if (refDateInput && !refDateInput.value) {
        refDateInput.valueAsDate = new Date();
    }
}

// ==================== REPORT GENERATION ====================

async function generateReport() {
    const referenceDate = document.getElementById('referenceDate').value;
    const projectId = document.getElementById('projectId').value;
    const categoryId = document.getElementById('categoryId').value;

    if (!referenceDate || !categoryId) {
        showAlert('warning', 'Please select a reference date and category.');
        return;
    }

    // Clear previous results and show loading
    document.getElementById('reportData').innerHTML = '<tr><td colspan="4" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>';
    document.getElementById('reportResults').style.display = 'block';

    const params = {
        referenceDate: referenceDate,
        projectId: projectId || '',
        categoryId: categoryId
    };

    try {
        // Use jQuery AJAX for consistency with category loading
        const data = await new Promise((resolve, reject) => {
            $.ajax({
                url: routes.getAgingReportData,
                method: 'GET',
                data: params,
                dataType: 'json'
            })
                .done(resolve)
                .fail((xhr, status, error) => {
                    reject(new Error(error || 'Unknown error'));
                });
        });

        displayReport(data);
    } catch (error) {
        console.error('Error generating report:', error);
        let errorMessage = 'Error generating report: ' + error.message;
        document.getElementById('reportData').innerHTML = `<tr><td colspan="4" class="text-center text-danger">${errorMessage}</td></tr>`;
        showAlert('danger', 'Failed to generate report: ' + error.message);
    }
}

function displayReport(data) {
    // Destroy existing DataTable if it exists
    if (dataTable) {
        dataTable.destroy();
        dataTable = null;
    }

    const tbody = document.getElementById('reportData');
    tbody.innerHTML = '';

    if (!data || data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" class="text-center">No data found</td></tr>';
        updateTotals(0, 0, 0);
        return;
    }

    let totalDebit = 0;
    let totalCredit = 0;
    let totalNet = 0;

    data.forEach(item => {
        const debit = parseFloat(item.totalDebit) || 0;
        const credit = parseFloat(item.totalCredit) || 0;
        const net = parseFloat(item.netBalance) || 0;

        totalDebit += debit;
        totalCredit += credit;
        totalNet += net;

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${item.accountName}</td>
            <td class="text-end">${formatCurrency(debit)}</td>
            <td class="text-end">${formatCurrency(credit)}</td>
            <td class="text-end ${net < 0 ? 'negative-balance' : ''}">${formatCurrency(net)}</td>
        `;
        tbody.appendChild(row);
    });

    // Update totals in footer
    updateTotals(totalDebit, totalCredit, totalNet);

    // Initialize DataTable
    try {
        dataTable = $('#agingReportTable').DataTable({
            "order": [[0, "asc"]],
            "language": {
                "emptyTable": "No aging data available",
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
                    title: 'Aging Report ' + document.getElementById('referenceDate').value,
                    footer: true
                },
                {
                    extend: 'pdf',
                    text: '<i class="fas fa-file-pdf"></i> PDF',
                    className: 'btn btn-sm btn-danger',
                    title: 'Aging Report ' + document.getElementById('referenceDate').value,
                    footer: true
                },
                {
                    extend: 'print',
                    text: '<i class="fas fa-print"></i> Print',
                    className: 'btn btn-sm btn-info',
                    title: 'Aging Report ' + document.getElementById('referenceDate').value,
                    footer: true
                }
            ]
        });
    } catch (error) {
        console.error('Error initializing DataTable:', error);
    }

    updateReportSummary(data.length, totalNet);
}

// ==================== UTILITY FUNCTIONS ====================

function updateTotals(totalDebit, totalCredit, totalNet) {
    document.getElementById('totalDebit').textContent = formatCurrency(totalDebit);
    document.getElementById('totalCredit').textContent = formatCurrency(totalCredit);
    document.getElementById('totalNet').textContent = formatCurrency(totalNet);
}

function updateReportSummary(count, totalBalance = 0) {
    const summaryElement = document.getElementById('reportSummary');
    if (!summaryElement) return;

    if (count > 0) {
        const balanceClass = totalBalance < 0 ? 'text-danger' : 'text-success';
        summaryElement.innerHTML = `
            <span class="badge bg-primary">${count} accounts</span>
            <span class="badge bg-warning ms-2">Total Balance: <span class="${balanceClass}">${formatCurrency(totalBalance)}</span></span>
        `;
    } else {
        summaryElement.innerHTML = `<span class="badge bg-secondary">No data</span>`;
    }
}

function formatCurrency(value) {
    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(value);
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
        alertArea.innerHTML = ''; // Clear existing alerts
        alertArea.appendChild(alertElement);

        // Auto dismiss after 5 seconds
        setTimeout(function () {
            if (alertElement.parentNode) {
                alertElement.parentNode.removeChild(alertElement);
            }
        }, 5000);
    }
}