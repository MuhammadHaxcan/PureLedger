/**
 * Accounts Page JavaScript
 * Simple architecture: Project Dropdown + Category Dropdown + Generate + Clear buttons
 */

document.addEventListener('DOMContentLoaded', () => {
    initializePage();
    setupEventListeners();
    loadProjects();
});

function initializePage() {
    window.accountsState = {
        allAccounts: [],
        selectedProject: null,
        selectedCategory: null,
        isLoading: false
    };
}

function setupEventListeners() {
    // Project selection change
    const projectSelect = document.getElementById('projectId');
    if (projectSelect) {
        projectSelect.addEventListener('change', handleProjectChange);
    }
    
    // Category selection change
    const categorySelect = document.getElementById('categoryId');
    if (categorySelect) {
        categorySelect.addEventListener('change', handleCategoryChange);
    }
    
    // Generate accounts button
    const generateBtn = document.getElementById('generateAccountsBtn');
    if (generateBtn) {
        generateBtn.addEventListener('click', handleGenerateAccounts);
    }
    
    // Clear selection button
    const clearBtn = document.getElementById('clearSelectionBtn');
    if (clearBtn) {
        clearBtn.addEventListener('click', clearAllSelections);
    }
    
    // Account search
    const accountSearch = document.getElementById('accountSearch');
    if (accountSearch) {
        accountSearch.addEventListener('input', debounce(filterAccounts, 250));
    }
    
    // Keyboard shortcut (Alt + N) for "Add New Account"
    document.addEventListener('keydown', function (e) {
        if (e.altKey && e.key.toLowerCase() === 'n') {
            e.preventDefault();
            const addBtn = document.getElementById('addAccountBtn');
            if (addBtn) {
                addBtn.click();
            }
        }
    });

    // Setup searchable dropdowns
    setupSearchableDropdowns();
    
    // Setup delete handlers
    setupDeleteHandlers();
}

function setupSearchableDropdowns() {
    // Setup project dropdown
    setupDropdown('.project-container', 'projectId');
    
    // Setup category dropdown
    setupDropdown('.category-container', 'categoryId');
}

function setupDropdown(containerSelector, selectId) {
    const container = document.querySelector(containerSelector);
    if (!container) return;

    const searchInput = container.querySelector('.select-search');
    const dropdown = container.querySelector('.select-dropdown');
    const select = container.querySelector('select');

    if (!searchInput || !dropdown || !select) return;

    // Show dropdown on focus
    searchInput.addEventListener('focus', function() {
        if (!this.disabled) {
            dropdown.classList.add('show');
            populateDropdownOptions(selectId, dropdown);
        }
    });

    // Filter options on input
    searchInput.addEventListener('input', function() {
        if (!this.disabled) {
            const searchTerm = this.value.toLowerCase();
            filterDropdownOptions(dropdown, searchTerm);
            dropdown.classList.add('show');
        }
    });

    // Handle keyboard navigation
    searchInput.addEventListener('keydown', function(e) {
        if (!this.disabled) {
            handleKeyboardNavigation(e, dropdown, select, searchInput);
        }
    });

    // Handle option clicks
    dropdown.addEventListener('click', function(e) {
        if (e.target.classList.contains('select-option')) {
            selectOption(e.target, select, searchInput, dropdown);
        }
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', function(e) {
        if (!container.contains(e.target)) {
            dropdown.classList.remove('show');
        }
    });
}

function populateDropdownOptions(selectId, dropdown) {
    const select = document.getElementById(selectId);
    if (!dropdown || !select) return;
    
    dropdown.innerHTML = '';
    
    Array.from(select.options).forEach(option => {
        if (option.value) { // Skip the empty option
            const div = document.createElement('div');
            div.className = 'select-option';
            div.dataset.value = option.value;
            div.textContent = option.textContent;
            dropdown.appendChild(div);
        }
    });
}

function filterDropdownOptions(dropdown, searchTerm) {
    if (!dropdown) return;
    
    const options = dropdown.querySelectorAll('.select-option');
    let visibleCount = 0;
    
    options.forEach(option => {
        const text = option.textContent.toLowerCase();
        const shouldShow = text.includes(searchTerm) && visibleCount < 10;
        option.style.display = shouldShow ? 'block' : 'none';
        if (shouldShow) visibleCount++;
    });
}

function handleKeyboardNavigation(e, dropdown, select, searchInput) {
    if (!dropdown || !select || !searchInput) return;
    
    const visibleOptions = dropdown.querySelectorAll('.select-option:not([style*="display: none"])');
    let selectedOption = dropdown.querySelector('.select-option.selected');

    switch (e.key) {
        case 'ArrowDown':
            e.preventDefault();
            if (!selectedOption) {
                if (visibleOptions.length > 0) {
                    visibleOptions[0].classList.add('selected');
                }
            } else {
                selectedOption.classList.remove('selected');
                const nextOption = getNextVisibleOption(selectedOption, visibleOptions);
                if (nextOption) nextOption.classList.add('selected');
            }
            break;

        case 'ArrowUp':
            e.preventDefault();
            if (!selectedOption) {
                if (visibleOptions.length > 0) {
                    visibleOptions[visibleOptions.length - 1].classList.add('selected');
                }
            } else {
                selectedOption.classList.remove('selected');
                const prevOption = getPreviousVisibleOption(selectedOption, visibleOptions);
                if (prevOption) prevOption.classList.add('selected');
            }
            break;

        case 'Enter':
            e.preventDefault();
            selectedOption = dropdown.querySelector('.select-option.selected');
            if (selectedOption) {
                selectOption(selectedOption, select, searchInput, dropdown);
            }
            break;

        case 'Escape':
            e.preventDefault();
            dropdown.classList.remove('show');
            break;
    }
}

function getNextVisibleOption(currentOption, visibleOptions) {
    const currentIndex = Array.from(visibleOptions).indexOf(currentOption);
    return currentIndex < visibleOptions.length - 1 ? visibleOptions[currentIndex + 1] : visibleOptions[0];
}

function getPreviousVisibleOption(currentOption, visibleOptions) {
    const currentIndex = Array.from(visibleOptions).indexOf(currentOption);
    return currentIndex > 0 ? visibleOptions[currentIndex - 1] : visibleOptions[visibleOptions.length - 1];
}

function selectOption(option, select, searchInput, dropdown) {
    if (!option || !select || !searchInput || !dropdown) return;
    
    const value = option.dataset.value;
    const text = option.textContent;

    select.value = value;
    searchInput.value = text;
    dropdown.classList.remove('show');
    
    // Clear previous selections in dropdown
    dropdown.querySelectorAll('.select-option').forEach(opt => opt.classList.remove('selected'));
    option.classList.add('selected');

    // Trigger change event
    select.dispatchEvent(new Event('change'));
}

function loadProjects() {
    // Check if routes is defined
    if (typeof routes === 'undefined' || !routes.getAllProjectsForAccounts) {
        console.error('Routes not defined or missing getAllProjectsForAccounts');
        showError('Configuration error. Please refresh the page.');
        return;
    }

    fetch(routes.getAllProjectsForAccounts)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(projects => {
            populateProjectDropdown(projects);
        })
        .catch(error => {
            console.error('Error loading projects:', error);
            showError('Failed to load projects. Please refresh the page.');
        });
}

function populateProjectDropdown(projects) {
    const select = document.getElementById('projectId');
    if (!select) return;
    
    // Clear existing options except the first one
    select.innerHTML = '<option value="">Select Project</option>';
    
    if (projects && Array.isArray(projects) && projects.length > 0) {
        projects.forEach(project => {
            const option = document.createElement('option');
            option.value = project.projectId;
            option.textContent = project.projectName;
            select.appendChild(option);
        });
        
        // Update dropdown options
        populateDropdownOptions('projectId', document.querySelector('.project-dropdown'));
    }
}

function handleProjectChange() {
    const projectSelect = document.getElementById('projectId');
    const categorySelect = document.getElementById('categoryId');
    const categorySearch = document.querySelector('.category-search');
    const generateBtn = document.getElementById('generateAccountsBtn');
    
    if (!projectSelect || !categorySelect || !categorySearch || !generateBtn) return;
    
    const projectId = projectSelect.value;
    const selectedOption = projectSelect.querySelector(`option[value="${projectId}"]`);
    
    // Reset category selection
    resetCategoryDropdown();
    
    if (projectId && selectedOption) {
        const projectName = selectedOption.textContent;
        window.accountsState.selectedProject = { id: projectId, name: projectName };
        
        // Enable category dropdown and load categories
        categorySelect.disabled = false;
        categorySearch.disabled = false;
        categorySearch.placeholder = 'Search categories...';
        
        loadCategoriesForProject(projectId);
        clearAlerts();
    } else {
        // Disable category dropdown and generate button
        categorySelect.disabled = true;
        categorySearch.disabled = true;
        categorySearch.placeholder = 'Select a project first...';
        
        generateBtn.disabled = true;
        generateBtn.classList.remove('btn-success');
        generateBtn.classList.add('btn-secondary');
        
        window.accountsState.selectedProject = null;
    }
}

function loadCategoriesForProject(projectId) {
    if (typeof routes === 'undefined' || !routes.getCategoriesByProject) {
        console.error('Routes not defined or missing getCategoriesByProject');
        showError('Configuration error. Please refresh the page.');
        return;
    }

    fetch(`${routes.getCategoriesByProject}?projectId=${projectId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(categories => {
            populateCategoryDropdown(categories);
        })
        .catch(error => {
            console.error('Error loading categories:', error);
            showError('Failed to load categories for the selected project.');
        });
}

function populateCategoryDropdown(categories) {
    const select = document.getElementById('categoryId');
    if (!select) return;
    
    // Clear existing options except the first one
    select.innerHTML = '<option value="">All Categories</option>';
    
    if (categories && Array.isArray(categories) && categories.length > 0) {
        categories.forEach(category => {
            const option = document.createElement('option');
            option.value = category.categoryId;
            option.textContent = category.categoryName;
            select.appendChild(option);
        });
    }
    
    // Update dropdown options
    populateDropdownOptions('categoryId', document.querySelector('.category-dropdown'));
}

function resetCategoryDropdown() {
    const categorySelect = document.getElementById('categoryId');
    const categorySearch = document.querySelector('.category-search');
    const generateBtn = document.getElementById('generateAccountsBtn');
    
    if (categorySelect) {
        categorySelect.value = '';
        categorySelect.innerHTML = '<option value="">All Categories</option>';
    }
    
    if (categorySearch) {
        categorySearch.value = '';
    }
    
    if (generateBtn) {
        generateBtn.disabled = true;
        generateBtn.classList.remove('btn-success');
        generateBtn.classList.add('btn-secondary');
    }
    
    window.accountsState.selectedCategory = null;
}

function handleCategoryChange() {
    const categorySelect = document.getElementById('categoryId');
    const generateBtn = document.getElementById('generateAccountsBtn');
    
    if (!categorySelect || !generateBtn) return;
    
    const categoryId = categorySelect.value;
    const selectedOption = categorySelect.querySelector(`option[value="${categoryId}"]`);
    
    if (categoryId && selectedOption) {
        const categoryName = selectedOption.textContent;
        window.accountsState.selectedCategory = { id: categoryId, name: categoryName };
    } else {
        window.accountsState.selectedCategory = { id: '', name: 'All Categories' };
    }
    
    // Enable generate button if project is selected
    if (window.accountsState.selectedProject) {
        generateBtn.disabled = false;
        generateBtn.classList.remove('btn-secondary');
        generateBtn.classList.add('btn-success');
    }
}

function handleGenerateAccounts() {
    const selectedProject = window.accountsState.selectedProject;
    
    if (!selectedProject) {
        showError('Please select a project first.');
        return;
    }
    
    if (window.accountsState.isLoading) {
        return; // Prevent multiple clicks
    }
    
    const selectedCategory = window.accountsState.selectedCategory;
    loadAccountsForCriteria(selectedProject.id, selectedCategory ? selectedCategory.id : '', 
                           selectedProject.name, selectedCategory ? selectedCategory.name : 'All Categories');
}

function loadAccountsForCriteria(projectId, categoryId, projectName, categoryName) {
    // Check if routes is defined
    if (typeof routes === 'undefined' || !routes.getAccountsByCriteria) {
        console.error('Routes not defined or missing getAccountsByCriteria');
        showError('Configuration error. Please refresh the page.');
        return;
    }

    window.accountsState.isLoading = true;
    
    showLoadingIndicator();
    showAccountsCard();
    
    // Update generate button state
    const generateBtn = document.getElementById('generateAccountsBtn');
    if (generateBtn) {
        generateBtn.disabled = true;
        generateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Loading...';
    }
    
    // Update header
    const selectedProjectNameEl = document.getElementById('selectedProjectName');
    const selectedCategoryInfoEl = document.getElementById('selectedCategoryInfo');
    if (selectedProjectNameEl) {
        selectedProjectNameEl.textContent = projectName;
    }
    if (selectedCategoryInfoEl) {
        selectedCategoryInfoEl.textContent = categoryId ? ` > ${categoryName}` : ' > All Categories';
    }
    
    const url = `${routes.getAccountsByCriteria}?projectId=${projectId}&categoryId=${categoryId}`;
    
    fetch(url)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(accounts => {
            window.accountsState.allAccounts = accounts;
            renderAccounts(accounts);
            hideLoadingIndicator();
            
            // Reset generate button
            if (generateBtn) {
                generateBtn.disabled = false;
                generateBtn.innerHTML = '<i class="fas fa-sync me-1"></i> Refresh';
            }
        })
        .catch(error => {
            console.error('Error loading accounts:', error);
            showError('Failed to load accounts. Please try again.');
            hideLoadingIndicator();
            
            // Reset generate button
            if (generateBtn) {
                generateBtn.disabled = false;
                generateBtn.innerHTML = '<i class="fas fa-play me-1"></i> Generate';
            }
            
            hideAccountsCard();
        })
        .finally(() => {
            window.accountsState.isLoading = false;
        });
}

function renderAccounts(accounts) {
    const tbody = document.getElementById('accountsTableBody');
    const noAccountsMessage = document.getElementById('noAccountsMessage');
    
    if (!tbody) return;
    
    if (!accounts || accounts.length === 0) {
        tbody.innerHTML = '';
        if (noAccountsMessage) {
            noAccountsMessage.style.display = 'block';
        }
        return;
    }
    
    if (noAccountsMessage) {
        noAccountsMessage.style.display = 'none';
    }
    
    tbody.innerHTML = accounts.map(account => `
        <tr>
            <td>${account.accountId || 'N/A'}</td>
            <td>${account.accountName || 'N/A'}</td>
            <td>${account.categoryName || 'N/A'}</td>
            <td>${account.accountGroupName || 'N/A'}</td>
            <td>
                <a href="/MetaData/EditAccount/${account.accountId}" 
                   class="btn btn-sm btn-outline-primary me-1">
                    <i class="fas fa-edit me-1"></i>Edit
                </a>
                <button type="button"
                        class="btn btn-sm btn-outline-danger delete-account-btn"
                        data-id="${account.accountId}"
                        data-name="${account.accountName}">
                    <i class="fas fa-trash me-1"></i>Delete
                </button>
            </td>
        </tr>
    `).join('');
    
    // Re-setup delete handlers for new buttons
    setupDeleteHandlers();
}

function filterAccounts() {
    const accountSearch = document.getElementById('accountSearch');
    const noAccountsMessage = document.getElementById('noAccountsMessage');
    
    if (!accountSearch) return;
    
    const searchTerm = accountSearch.value.toLowerCase();
    const rows = document.querySelectorAll('#accountsTableBody tr');
    let visibleCount = 0;
    
    rows.forEach(row => {
        const text = row.textContent.toLowerCase();
        const shouldShow = text.includes(searchTerm);
        row.style.display = shouldShow ? '' : 'none';
        if (shouldShow) visibleCount++;
    });
    
    // Show/hide no results message
    if (noAccountsMessage) {
        if (visibleCount === 0 && rows.length > 0) {
            noAccountsMessage.style.display = 'block';
            noAccountsMessage.innerHTML = `
                <i class="fas fa-search fa-3x text-muted mb-3"></i>
                <p class="text-muted">No accounts match your search criteria.</p>
            `;
        } else if (rows.length === 0) {
            noAccountsMessage.style.display = 'block';
            noAccountsMessage.innerHTML = `
                <i class="fas fa-inbox fa-3x text-muted mb-3"></i>
                <p class="text-muted">No accounts found for the selected criteria.</p>
            `;
        } else {
            noAccountsMessage.style.display = 'none';
        }
    }
}

function setupDeleteHandlers() {
    // Handle Delete Account Button
    document.querySelectorAll('.delete-account-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const accountId = this.dataset.id;
            const accountName = this.dataset.name;

            const nameEl = document.getElementById('deleteAccountName');
            const idEl = document.getElementById('deleteAccountId');
            const errorEl = document.getElementById('deleteAccountError');
            const formEl = document.getElementById('deleteAccountForm');

            if (nameEl) nameEl.textContent = accountName;
            if (idEl) idEl.value = accountId;
            if (errorEl) errorEl.classList.add('d-none');
            if (formEl) formEl.style.display = '';

            // Check if account can be deleted
            fetch(`${routes.canDeleteAccount}/${accountId}`)
                .then(response => response.json())
                .then(result => {
                    if (result.canDelete) {
                        new bootstrap.Modal(document.getElementById('deleteAccountModal')).show();
                    } else {
                        if (errorEl) {
                            errorEl.textContent = result.message;
                            errorEl.classList.remove('d-none');
                        }
                        if (formEl) formEl.style.display = 'none';
                        new bootstrap.Modal(document.getElementById('deleteAccountModal')).show();
                    }
                })
                .catch(err => {
                    if (errorEl) {
                        errorEl.textContent = 'An error occurred while checking if the account can be deleted.';
                        errorEl.classList.remove('d-none');
                    }
                    if (formEl) formEl.style.display = 'none';
                    new bootstrap.Modal(document.getElementById('deleteAccountModal')).show();
                });
        });
    });
}

function clearAllSelections() {
    const projectSelect = document.getElementById('projectId');
    const categorySelect = document.getElementById('categoryId');
    const projectSearch = document.querySelector('.project-search');
    const categorySearch = document.querySelector('.category-search');
    const generateBtn = document.getElementById('generateAccountsBtn');
    
    if (projectSelect) {
        projectSelect.value = '';
    }
    
    if (categorySelect) {
        categorySelect.value = '';
        categorySelect.disabled = true;
        categorySelect.innerHTML = '<option value="">All Categories</option>';
    }
    
    if (projectSearch) {
        projectSearch.value = '';
    }
    
    if (categorySearch) {
        categorySearch.value = '';
        categorySearch.disabled = true;
        categorySearch.placeholder = 'Select a project first...';
    }
    
    window.accountsState.selectedProject = null;
    window.accountsState.selectedCategory = null;
    window.accountsState.allAccounts = [];
    
    // Reset generate button
    if (generateBtn) {
        generateBtn.disabled = true;
        generateBtn.classList.remove('btn-success');
        generateBtn.classList.add('btn-secondary');
        generateBtn.innerHTML = '<i class="fas fa-play me-1"></i> Generate';
    }
    
    hideAccountsCard();
    clearAlerts();
}

function showAccountsCard() {
    const accountsCard = document.getElementById('accountsCard');
    if (accountsCard) {
        accountsCard.style.display = 'block';
    }
}

function hideAccountsCard() {
    const accountsCard = document.getElementById('accountsCard');
    if (accountsCard) {
        accountsCard.style.display = 'none';
    }
}

function showLoadingIndicator() {
    const loadingIndicator = document.getElementById('loadingIndicator');
    const accountsContainer = document.getElementById('accountsContainer');
    
    if (loadingIndicator) {
        loadingIndicator.style.display = 'block';
    }
    
    if (accountsContainer) {
        accountsContainer.style.display = 'none';
    }
}

function hideLoadingIndicator() {
    const loadingIndicator = document.getElementById('loadingIndicator');
    const accountsContainer = document.getElementById('accountsContainer');
    
    if (loadingIndicator) {
        loadingIndicator.style.display = 'none';
    }
    
    if (accountsContainer) {
        accountsContainer.style.display = 'block';
    }
}

function showError(message) {
    const alertArea = document.getElementById('alertArea');
    if (!alertArea) {
        console.error('Alert area not found:', message);
        return;
    }
    
    alertArea.innerHTML = `
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <i class="fas fa-exclamation-circle me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;
}

function clearAlerts() {
    const alertArea = document.getElementById('alertArea');
    if (alertArea) {
        alertArea.innerHTML = '';
    }
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}