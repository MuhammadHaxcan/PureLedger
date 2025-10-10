$(document).ready(function () {
    // Initialize loading state
    let isLoading = false;

    function setLoadingState(loading) {
        isLoading = loading;
        const $form = $('#cartage-settings-form');
        const $submitBtn = $form.find('button[type="submit"]');

        if (loading) {
            $submitBtn.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-2"></span>Saving...');
        } else {
            $submitBtn.prop('disabled', false)
                .html('<i class="fas fa-save me-1"></i> Save Settings');
        }
    }

    function showError(message) {
        const errorHtml = `
            <div class="alert alert-danger alert-dismissible fade show" role="alert">
                <i class="fas fa-exclamation-circle me-2"></i>${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>`;
        $('#alertArea').html(errorHtml);
    }

    async function loadProjectData(projectId) {
        if (!projectId) {
            resetDropdowns();
            return;
        }

        setLoadingState(true);
        try {
            console.log('Loading data for project:', projectId);
            console.log('Using route:', routes.getProjectCartageSettingsByProject);
            
            // Load categories first
            const categories = await $.getJSON(routes.getCategoriesByProject, { projectId });
            console.log('Categories loaded:', categories);
            updateCategoryDropdowns(categories);
            
            // Then try to load settings separately (with error handling)
            try {
                const settings = await $.getJSON(routes.getProjectCartageSettingsByProject, { projectId });
                console.log('Settings loaded:', settings);
                
                if (settings) {
                    await loadProjectSettings(settings);
                }
            } catch (settingsError) {
                console.error('Error loading settings:', settingsError);
                showError('Settings could not be loaded, but you can create new ones.');
            }
        } catch (error) {
            console.error('Error loading project data:', error);
            showError('Failed to load project data. Please try again.');
        } finally {
            setLoadingState(false);
        }
    }

    async function loadProjectSettings(settings) {
        console.log('Loading settings:', settings);

        try {
            // Load FOR expense category and select it
            if (settings.forExpenseAccountId) {
                console.log('Loading data for account ID:', settings.forExpenseAccountId);

                // Get the category for this account
                const forCategory = await getCategoryForAccount(settings.forExpenseAccountId);
                console.log('Retrieved category ID:', forCategory);

                if (forCategory) {
                    // Select the FOR expense category
                    $('#ForExpenseCategoryId').val(forCategory).trigger('change');

                    // Wait for accounts to load, then select the FOR expense account
                    await loadAccounts(forCategory);
                    $('#ForExpenseAccountId').val(settings.forExpenseAccountId);
                }
            }

            // Set the cartage transporter category
            if (settings.cartageTransporterCategoryId) {
                console.log('Setting cartage category:', settings.cartageTransporterCategoryId);
                $('#CartageTransporterCategoryId').val(settings.cartageTransporterCategoryId);
            }
        } catch (error) {
            console.error('Error loading project settings:', error);
            showError('Error loading project settings. Please try again.');
        }
    }

    async function getCategoryForAccount(accountId) {
        try {
            const response = await $.getJSON(routes.getCategoryForAccount, { accountId });
            return response.categoryId;
        } catch (error) {
            console.error('Error getting category for account:', error);
            return null;
        }
    }

    function updateCategoryDropdowns(categories) {
        const forCat = $('#ForExpenseCategoryId');
        const cartageCat = $('#CartageTransporterCategoryId');

        forCat.empty().append('<option value="">-- Select Category --</option>');
        cartageCat.empty().append('<option value="">-- Select Category --</option>');

        if (categories && categories.length > 0) {
            categories.forEach(cat => {
                forCat.append(new Option(cat.categoryName, cat.categoryId));
                cartageCat.append(new Option(cat.categoryName, cat.categoryId));
            });
        }

        resetAccountDropdown();
    }

    async function loadAccounts(categoryId) {
        if (!categoryId) {
            resetAccountDropdown();
            return Promise.resolve();
        }

        return new Promise((resolve, reject) => {
            $.getJSON(routes.getAccountsByCategory, { categoryId })
                .done(accounts => {
                    updateAccountDropdown(accounts);
                    resolve(accounts);
                })
                .fail(error => {
                    console.error('Error loading accounts:', error);
                    showError('Failed to load accounts. Please try again.');
                    reject(error);
                });
        });
    }

    function updateAccountDropdown(accounts) {
        const accSel = $('#ForExpenseAccountId');
        accSel.empty().append('<option value="">-- Select Account --</option>');

        if (accounts && accounts.length > 0) {
            accounts.forEach(acc => {
                accSel.append(new Option(acc.accountName, acc.accountId));
            });
        }
    }

    function resetDropdowns() {
        $('#ForExpenseCategoryId, #CartageTransporterCategoryId')
            .empty()
            .append('<option value="">-- Select Category --</option>');
        resetAccountDropdown();
    }

    function resetAccountDropdown() {
        $('#ForExpenseAccountId')
            .empty()
            .append('<option value="">-- Select Account --</option>');
    }

    // Event Handlers
    $('#ProjectId').change(function () {
        loadProjectData($(this).val());
    });

    $('#ForExpenseCategoryId').change(function () {
        loadAccounts($(this).val());
    });

    // Form submission handler
    $('#cartage-settings-form').on('submit', function (e) {
        if (isLoading) {
            e.preventDefault();
            return;
        }
        
        // Custom validation: At least one of FOR account or Cartage category must be selected
        const forExpenseAccount = $('#ForExpenseAccountId').val();
        const cartageCategory = $('#CartageTransporterCategoryId').val();
        
        if (!forExpenseAccount && !cartageCategory) {
            e.preventDefault();
            showError("At least one of FOR Expense Account or Cartage Transporter Category must be selected.");
            return;
        }
        
        setLoadingState(true);
    });

    // Initialize if project is selected
    const initialProjectId = $('#ProjectId').val();
    if (initialProjectId) {
        loadProjectData(initialProjectId);
    }
});