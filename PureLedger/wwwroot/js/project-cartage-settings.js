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

    async function loadCategories(projectId) {
        if (!projectId) {
            resetDropdowns();
            return;
        }

        setLoadingState(true);
        try {
            const categories = await $.getJSON('/Admin/GetCategoriesByProject', { projectId: projectId });
            updateCategoryDropdowns(categories);
        } catch (error) {
            console.error('Error loading categories:', error);
            showError('Failed to load categories. Please try again.');
        } finally {
            setLoadingState(false);
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
            return;
        }

        setLoadingState(true);
        try {
            const accounts = await $.getJSON('/Admin/GetAccountsByCategory', { categoryId: categoryId });
            updateAccountDropdown(accounts);
        } catch (error) {
            console.error('Error loading accounts:', error);
            showError('Failed to load accounts. Please try again.');
        } finally {
            setLoadingState(false);
        }
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
        loadCategories($(this).val());
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
        setLoadingState(true);
    });

    // Initialize if project is selected
    const initialProjectId = $('#ProjectId').val();
    if (initialProjectId) {
        loadCategories(initialProjectId);
    }
});