/**
 * Categories Page JavaScript
 * Simple architecture: Dropdown + Generate + Clear buttons only
 */

document.addEventListener('DOMContentLoaded', () => {
    initializePage();
    setupEventListeners();
    loadProjects();
});

function initializePage() {
    window.categoriesState = {
        allCategories: [],
        selectedProject: null,
        isLoading: false
    };
}

function setupEventListeners() {
    // Project selection change
    const projectSelect = document.getElementById('projectId');
    if (projectSelect) {
        projectSelect.addEventListener('change', handleProjectChange);
    }
    
    // Generate categories button
    const generateBtn = document.getElementById('generateCategoriesBtn');
    if (generateBtn) {
        generateBtn.addEventListener('click', handleGenerateCategories);
    }
    
    // Clear project button
    const clearBtn = document.getElementById('clearProjectBtn');
    if (clearBtn) {
        clearBtn.addEventListener('click', clearProjectSelection);
    }
    
    // Category search (only if categories card exists)
    const categorySearch = document.getElementById('categorySearch');
    if (categorySearch) {
        categorySearch.addEventListener('input', debounce(filterCategories, 250));
    }
    
    // Keyboard shortcut (Alt + N) for "Add New Category"
    document.addEventListener('keydown', function (e) {
        if (e.altKey && e.key.toLowerCase() === 'n') {
            e.preventDefault();
            const addBtn = document.getElementById('addCategoryBtn');
            if (addBtn) {
                addBtn.click();
            }
        }
    });

    // Setup searchable dropdown
    setupSearchableDropdown();
    
    // Setup delete handlers
    setupDeleteHandlers();
}

function setupSearchableDropdown() {
    const container = document.querySelector('.select-container');
    if (!container) return;

    const searchInput = container.querySelector('.select-search');
    const dropdown = container.querySelector('.select-dropdown');
    const select = container.querySelector('select');

    if (!searchInput || !dropdown || !select) return;

    // Show dropdown on focus
    searchInput.addEventListener('focus', function() {
        dropdown.classList.add('show');
        populateDropdownOptions();
    });

    // Filter options on input
    searchInput.addEventListener('input', function() {
        const searchTerm = this.value.toLowerCase();
        filterDropdownOptions(dropdown, searchTerm);
        dropdown.classList.add('show');
    });

    // Handle keyboard navigation
    searchInput.addEventListener('keydown', function(e) {
        handleKeyboardNavigation(e, dropdown, select, searchInput);
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

function populateDropdownOptions() {
    const dropdown = document.querySelector('.select-dropdown');
    const select = document.getElementById('projectId');
    
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
    if (typeof routes === 'undefined' || !routes.getAllProjectsForCategories) {
        console.error('Routes not defined or missing getAllProjectsForCategories');
        showError('Configuration error. Please refresh the page.');
        return;
    }

    fetch(routes.getAllProjectsForCategories)
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
        populateDropdownOptions();
    }
}

function handleProjectChange() {
    const projectSelect = document.getElementById('projectId');
    const generateBtn = document.getElementById('generateCategoriesBtn');
    
    if (!projectSelect || !generateBtn) return;
    
    const projectId = projectSelect.value;
    const selectedOption = projectSelect.querySelector(`option[value="${projectId}"]`);
    
    if (projectId && selectedOption) {
        const projectName = selectedOption.textContent;
        window.categoriesState.selectedProject = { id: projectId, name: projectName };
        
        // Enable generate button
        generateBtn.disabled = false;
        generateBtn.classList.remove('btn-secondary');
        generateBtn.classList.add('btn-success');
        
        clearAlerts();
    } else {
        // Disable generate button
        generateBtn.disabled = true;
        generateBtn.classList.remove('btn-success');
        generateBtn.classList.add('btn-secondary');
        
        window.categoriesState.selectedProject = null;
    }
}

function handleGenerateCategories() {
    const selectedProject = window.categoriesState.selectedProject;
    
    if (!selectedProject) {
        showError('Please select a project first.');
        return;
    }
    
    if (window.categoriesState.isLoading) {
        return; // Prevent multiple clicks
    }
    
    loadCategoriesForProject(selectedProject.id, selectedProject.name);
}

function loadCategoriesForProject(projectId, projectName) {
    // Check if routes is defined
    if (typeof routes === 'undefined' || !routes.getCategoriesByProject) {
        console.error('Routes not defined or missing getCategoriesByProject');
        showError('Configuration error. Please refresh the page.');
        return;
    }

    window.categoriesState.isLoading = true;
    
    showLoadingIndicator();
    showCategoriesCard();
    
    // Update generate button state
    const generateBtn = document.getElementById('generateCategoriesBtn');
    if (generateBtn) {
        generateBtn.disabled = true;
        generateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Loading...';
    }
    
    const selectedProjectNameEl = document.getElementById('selectedProjectName');
    if (selectedProjectNameEl) {
        selectedProjectNameEl.textContent = projectName;
    }
    
    fetch(`${routes.getCategoriesByProject}?projectId=${projectId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(categories => {
            window.categoriesState.allCategories = categories;
            renderCategories(categories);
            hideLoadingIndicator();
            
            // Reset generate button
            if (generateBtn) {
                generateBtn.disabled = false;
                generateBtn.innerHTML = '<i class="fas fa-sync me-1"></i> Refresh';
            }
        })
        .catch(error => {
            console.error('Error loading categories:', error);
            showError('Failed to load categories. Please try again.');
            hideLoadingIndicator();
            
            // Reset generate button
            if (generateBtn) {
                generateBtn.disabled = false;
                generateBtn.innerHTML = '<i class="fas fa-play me-1"></i> Generate';
            }
            
            hideCategoriesCard();
        })
        .finally(() => {
            window.categoriesState.isLoading = false;
        });
}

function renderCategories(categories) {
    const tbody = document.getElementById('categoriesTableBody');
    const noCategoriesMessage = document.getElementById('noCategoriesMessage');
    
    if (!tbody) return;
    
    if (!categories || categories.length === 0) {
        tbody.innerHTML = '';
        if (noCategoriesMessage) {
            noCategoriesMessage.style.display = 'block';
        }
        return;
    }
    
    if (noCategoriesMessage) {
        noCategoriesMessage.style.display = 'none';
    }
    
    tbody.innerHTML = categories.map(category => `
        <tr>
            <td>${category.categoryId || 'N/A'}</td>
            <td>${category.categoryName || 'N/A'}</td>
            <td>${category.accountType?.accountTypeName || 'N/A'}</td>
            <td>
                <a href="/MetaData/EditCategory/${category.categoryId}" 
                   class="btn btn-sm btn-outline-primary me-1">
                    <i class="fas fa-edit me-1"></i>Edit
                </a>
                <button type="button"
                        class="btn btn-sm btn-outline-danger delete-category-btn"
                        data-id="${category.categoryId}"
                        data-name="${category.categoryName}">
                    <i class="fas fa-trash me-1"></i>Delete
                </button>
            </td>
        </tr>
    `).join('');
    
    // Re-setup delete handlers for new buttons
    setupDeleteHandlers();
}

function setupDeleteHandlers() {
    // Handle Delete Category Button
    document.querySelectorAll('.delete-category-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            const categoryId = this.dataset.id;
            const categoryName = this.dataset.name;

            const nameEl = document.getElementById('deleteCategoryName');
            const idEl = document.getElementById('deleteCategoryId');
            const errorEl = document.getElementById('deleteCategoryError');
            const formEl = document.getElementById('deleteCategoryForm');

            if (nameEl) nameEl.textContent = categoryName;
            if (idEl) idEl.value = categoryId;
            if (errorEl) errorEl.classList.add('d-none');
            if (formEl) formEl.style.display = '';

            // Check if category can be deleted
            fetch(`${routes.canDeleteCategory}/${categoryId}`)
                .then(response => response.json())
                .then(result => {
                    if (result.canDelete) {
                        new bootstrap.Modal(document.getElementById('deleteCategoryModal')).show();
                    } else {
                        if (errorEl) {
                            errorEl.textContent = result.message;
                            errorEl.classList.remove('d-none');
                        }
                        if (formEl) formEl.style.display = 'none';
                        new bootstrap.Modal(document.getElementById('deleteCategoryModal')).show();
                    }
                })
                .catch(err => {
                    if (errorEl) {
                        errorEl.textContent = 'An error occurred while checking if the category can be deleted.';
                        errorEl.classList.remove('d-none');
                    }
                    if (formEl) formEl.style.display = 'none';
                    new bootstrap.Modal(document.getElementById('deleteCategoryModal')).show();
                });
        });
    });
}

function filterCategories() {
    const categorySearch = document.getElementById('categorySearch');
    const noCategoriesMessage = document.getElementById('noCategoriesMessage');
    
    if (!categorySearch) return;
    
    const searchTerm = categorySearch.value.toLowerCase();
    const rows = document.querySelectorAll('#categoriesTableBody tr');
    let visibleCount = 0;
    
    rows.forEach(row => {
        const text = row.textContent.toLowerCase();
        const shouldShow = text.includes(searchTerm);
        row.style.display = shouldShow ? '' : 'none';
        if (shouldShow) visibleCount++;
    });
    
    // Show/hide no results message
    if (noCategoriesMessage) {
        if (visibleCount === 0 && rows.length > 0) {
            noCategoriesMessage.style.display = 'block';
            noCategoriesMessage.innerHTML = `
                <i class="fas fa-search fa-3x text-muted mb-3"></i>
                <p class="text-muted">No categories match your search criteria.</p>
            `;
        } else if (rows.length === 0) {
            noCategoriesMessage.style.display = 'block';
            noCategoriesMessage.innerHTML = `
                <i class="fas fa-inbox fa-3x text-muted mb-3"></i>
                <p class="text-muted">No categories found for the selected project.</p>
            `;
        } else {
            noCategoriesMessage.style.display = 'none';
        }
    }
}

function clearProjectSelection() {
    const projectSelect = document.getElementById('projectId');
    const searchInput = document.querySelector('.select-search');
    const generateBtn = document.getElementById('generateCategoriesBtn');
    
    if (projectSelect) {
        projectSelect.value = '';
    }
    
    if (searchInput) {
        searchInput.value = '';
    }
    
    window.categoriesState.selectedProject = null;
    window.categoriesState.allCategories = [];
    
    // Reset generate button
    if (generateBtn) {
        generateBtn.disabled = true;
        generateBtn.classList.remove('btn-success');
        generateBtn.classList.add('btn-secondary');
        generateBtn.innerHTML = '<i class="fas fa-play me-1"></i> Generate';
    }
    
    hideCategoriesCard();
    clearAlerts();
}

function showCategoriesCard() {
    const categoriesCard = document.getElementById('categoriesCard');
    if (categoriesCard) {
        categoriesCard.style.display = 'block';
    }
}

function hideCategoriesCard() {
    const categoriesCard = document.getElementById('categoriesCard');
    if (categoriesCard) {
        categoriesCard.style.display = 'none';
    }
}

function showLoadingIndicator() {
    const loadingIndicator = document.getElementById('loadingIndicator');
    const categoriesContainer = document.getElementById('categoriesContainer');
    
    if (loadingIndicator) {
        loadingIndicator.style.display = 'block';
    }
    
    if (categoriesContainer) {
        categoriesContainer.style.display = 'none';
    }
}

function hideLoadingIndicator() {
    const loadingIndicator = document.getElementById('loadingIndicator');
    const categoriesContainer = document.getElementById('categoriesContainer');
    
    if (loadingIndicator) {
        loadingIndicator.style.display = 'none';
    }
    
    if (categoriesContainer) {
        categoriesContainer.style.display = 'block';
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

