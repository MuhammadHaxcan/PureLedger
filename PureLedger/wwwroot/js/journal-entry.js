/**
 * Journal Entry Management System
 * Handles journal entry form, validation, calculations, and API interactions
 */

class JournalEntryManager {
    constructor() {
        this.rowCounter = 0;
        this.isSubmitting = false;
        this.init();
    }

    // Initialize the journal entry system
    init() {
        this.setupFormAction();
        this.initializeDatePickers();
        this.setupEventHandlers();
        this.loadInitialData();
    }

    // Setup form action with base path
    setupFormAction() {
        try {
            const formElement = document.getElementById('entryForm');
            const basePath = PureLedger?.getCurrentBasePath?.() || '';

            if (formElement && basePath) {
                let formAction = formElement.getAttribute('action') || '/Home/Entry';

                // Clean up any duplicate prefixes
                formAction = formAction.replace(/\/route[12]\//, '/');
                formElement.setAttribute('action', basePath + formAction);

                console.log('Form action set to:', formElement.action);
            }
        } catch (error) {
            console.error('Error setting up form action:', error);
        }
    }

    // Initialize date pickers
    initializeDatePickers() {
        $('.datepicker').datepicker({
            dateFormat: 'dd/mm/yy',
            changeMonth: true,
            changeYear: true,
            showButtonPanel: true,
            maxDate: new Date(),
            onError: (error) => console.error('Datepicker error:', error)
        });
    }

    // Setup all event handlers
    setupEventHandlers() {
        // Calendar icon clicks
        $(document).on('click', '.calendar-icon', (e) => {
            $(e.currentTarget).siblings('input.datepicker')[0].focus();
        });

        // Credit days and entry date changes
        $('#CreditDays, #EntryDate').on('input change', () => this.updateDueDate());

        // Project selection
        $('#ProjectId').on('change', (e) => this.handleProjectChange(e.target.value));

        // Category selection
        $(document).on('change', '.category-select', (e) => this.handleCategoryChange(e));

        // Account selection
        $(document).on('change', '.account-select', (e) => this.handleAccountChange(e));

        // Tag system handlers
        this.setupTagHandlers();

        // Calculation handlers
        this.setupCalculationHandlers();

        // Row management
        $('#addRowBtn').on('click', () => this.addNewRow());
        $(document).on('click', '.remove-row-btn', (e) => this.removeRow(e));

        // Form submission
        $('#entryForm').on('submit', (e) => this.handleFormSubmit(e));

        // Button action handlers
        $('#submitBtn').on('click', () => $('#formAction').val('submit'));
        $('#submitPrintBtn').on('click', () => $('#formAction').val('submit_print'));

        // Clear validation on field changes
        this.setupValidationClearance();
    }

    // Load initial data if project is selected
    loadInitialData() {
        const projectId = $('#ProjectId').val();
        if (projectId) {
            this.handleProjectChange(projectId);
        }
        this.updateDueDate();
        this.updateProductRequiredIndicator();
    }

    // Update due date based on entry date and credit days
    updateDueDate() {
        try {
            const creditDays = this.parseIntOrZero($('#CreditDays').val());
            const entryDateStr = $('#EntryDate').val();

            if (!entryDateStr) return;

            const dateParts = entryDateStr.split('/');
            if (dateParts.length !== 3) return;

            const day = parseInt(dateParts[0], 10);
            const month = parseInt(dateParts[1], 10) - 1;
            const year = parseInt(dateParts[2], 10);

            const entryDate = new Date(year, month, day);
            if (isNaN(entryDate.getTime())) return;

            entryDate.setDate(entryDate.getDate() + creditDays);
            $('#DueDate').val(this.formatDateDDMMYYYY(entryDate));
        } catch (error) {
            console.error('Error updating due date:', error);
        }
    }

    // Handle project selection change
    async handleProjectChange(projectId) {
        if (!projectId) {
            this.clearAllDropdowns();
            return;
        }

        const rows = document.querySelectorAll('#entryRows tr');

        for (const row of rows) {
            const $row = $(row);
            const $categorySelect = $row.find('.category-select');

            await this.loadCategories($categorySelect, projectId);
            this.clearAccountsAndSubsequent($row);
        }
    }

    // Load categories for a project
    async loadCategories($categorySelect, projectId) {
        try {
            $categorySelect.empty().append('<option value="">Loading...</option>');

            const categories = await this.apiGet('/Home/GetCategoriesByProject', { projectId });

            $categorySelect.empty().append('<option value="">Select Category</option>');

            if (categories?.length > 0) {
                categories.forEach(category => {
                    $categorySelect.append(new Option(category.name, category.id));
                });
            }
        } catch (error) {
            console.error('Error loading categories:', error);
            $categorySelect.empty().append('<option value="">Error loading categories</option>');
            this.showError('Failed to load categories. Please try again.');
        }
    }

    // Handle category selection change
    async handleCategoryChange(event) {
        const $row = $(event.target).closest('tr');
        const categoryId = event.target.value;

        await this.loadAccounts($row, categoryId);
        this.clearAccountRelatedData($row);
    }

    // Load accounts for a category
    async loadAccounts($row, categoryId) {
        const $accountSelect = $row.find('.account-select');

        try {
            if (!categoryId) {
                $accountSelect.empty().append('<option value="">Select Account</option>');
                return;
            }

            $accountSelect.empty().append('<option value="">Loading...</option>');

            const accounts = await this.apiGet('/Home/GetAccountsByCategory', { categoryId });

            $accountSelect.empty().append('<option value="">Select Account</option>');

            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const $option = $(new Option(account.name, account.id));
                    $option.data('account-type', account.accountTypeName || '');
                    $accountSelect.append($option);
                });
            }
        } catch (error) {
            console.error('Error loading accounts:', error);
            $accountSelect.empty().append('<option value="">Error loading accounts</option>');
            this.showError('Failed to load accounts. Please try again.');
        }
    }

    // Handle account selection change
    async handleAccountChange(event) {
        const $row = $(event.target).closest('tr');
        const accountId = event.target.value;
        const $selected = $(event.target).find('option:selected');

        // Reset row state
        this.resetAccountRelatedFields($row);

        if (!accountId) return;

        // Set account type
        const accountType = $selected.data('account-type') || '';
        $row[0].setAttribute('data-account-type', accountType);

        try {
            await this.loadProductDetails($row, accountId);
            
            // After loading product details, manage input fields based on account group
            this.manageInputsBasedOnAccountGroup($row);
        } catch (error) {
            console.error('Error loading product details:', error);
            this.showError('Failed to load product details.');
        }
    }

    // Load product details for an account
    async loadProductDetails($row, accountId) {
        try {
            const product = await this.apiGet('/Home/GetProductDetails', { accountId });

            if (product) {
                // Set sale rate
                if (product.saleRate) {
                    $row.find('.rate-input').val(product.saleRate);
                }

                // Set PKG
                if (product.pkg) {
                    $row[0].setAttribute('data-pkg', product.pkg);
                    $row.find('.pkg-input').val(product.pkg);
                }

                // Handle product account group
                if (product.accountGroupId) {
                    const accountGroup = product.accountGroupId === 1 ? 'account' : 'product';
                    $row[0].setAttribute('data-account-group', accountGroup);

                    if (accountGroup === 'product') {
                        this.setupProductRequirements($row);
                    }
                }

                // Update calculations
                this.updateRowCalculations($row[0]);
                this.updateProductRequiredIndicator();
            }
        } catch (error) {
            console.error('Error loading product details:', error);
            throw error;
        }
    }

    // Manage input fields based on account group
    manageInputsBasedOnAccountGroup($row) {
        const accountGroup = $row.attr('data-account-group') || '';
        const $pkgInput = $row.find('.pkg-input');
        const $rbpSelect = $row.find('.rbp-select');
        const $qtyInput = $row.find('.qty-input');
        const $rateInput = $row.find('.rate-input');
        const $discInput = $row.find('.disc-input');
        const $percentInput = $row.find('.percent-input');
        $pkgInput.prop('disabled', true); 
        
        if (accountGroup === 'account') {
            // Disable and clear fields for account group type "account"
            $rbpSelect.prop('disabled', true).val('Y');
            $qtyInput.prop('disabled', true).val('0');
            $rateInput.prop('disabled', true).val('0');
            $discInput.prop('disabled', true).val('0');
            $percentInput.prop('disabled', true).val('0');
        } else {
            // Enable fields for other account groups (e.g., "product")
            $qtyInput.prop('disabled', false);
            $rateInput.prop('disabled', false);
            $discInput.prop('disabled', false);
            $percentInput.prop('disabled', false);
        }
    }

    // Setup tag-related event handlers
    setupTagHandlers() {
        // Tag input focus/blur
        $(document).on('focus', '.tag-search-input', (e) => {
            const $suggestions = $(e.target).closest('.tag-multibox').siblings('.tag-suggestions');
            $suggestions.addClass('show');
        });

        $(document).on('blur', '.tag-search-input', (e) => {
            const $suggestions = $(e.target).closest('.tag-multibox').siblings('.tag-suggestions');
            setTimeout(() => $suggestions.removeClass('show'), 200);
        });

        // Tag search input
        $(document).on('input', '.tag-search-input', (e) => this.handleTagSearch(e));

        // Tag selection
        $(document).on('click', '.tag-suggestions .dropdown-item', (e) => this.handleTagSelection(e));

        // Tag removal
        $(document).on('click', '.tag-chip-remove', (e) => this.handleTagRemoval(e));
    }

    // Handle tag search
    async handleTagSearch(event) {
        const $input = $(event.target);
        const term = $input.val().trim();
        const $suggestions = $input.closest('.tag-multibox').siblings('.tag-suggestions');

        if (term.length === 0) {
            $suggestions.removeClass('show');
            return;
        }

        try {
            const tags = await this.apiGet('/MetaData/SearchTags', { term });

            $suggestions.empty();

            if (tags?.length > 0) {
                tags.forEach(tag => {
                    $suggestions.append(
                        `<a class="dropdown-item" href="#" data-id="${tag.id}" data-name="${tag.text}">
                            ${tag.text}
                        </a>`
                    );
                });
                $suggestions.addClass('show');
            } else {
                $suggestions.removeClass('show');
            }
        } catch (error) {
            console.error('Error searching tags:', error);
            $suggestions.removeClass('show');
        }
    }

    // Handle tag selection
    handleTagSelection(event) {
        event.preventDefault();

        const $item = $(event.target);
        const tagId = $item.data('id');
        const tagName = $item.data('name');
        const $row = $item.closest('tr');
        const $tagBox = $item.closest('td').find('.tag-multibox');
        const $input = $tagBox.find('.tag-search-input');

        // Create tag chip
        const $chip = $(`
            <div class="tag-chip me-1 mb-1" data-tag-id="${tagId}">
                <span>${tagName}</span>
                <a href="#" class="tag-chip-remove ms-1">
                    <i class="fas fa-times"></i>
                </a>
            </div>
        `);

        $chip.insertBefore($input);
        $input.val('');

        this.updateTagInputs($row);
        $item.closest('.tag-suggestions').removeClass('show');
    }

    // Handle tag removal
    handleTagRemoval(event) {
        event.preventDefault();

        const $chip = $(event.target).closest('.tag-chip');
        const $row = $chip.closest('tr');

        $chip.remove();
        this.updateTagInputs($row);
    }

    // Update hidden tag inputs for form submission
    updateTagInputs($row) {
        const rowIndex = $row.attr('id').split('-')[1];
        const $container = $row.find('.tag-hidden-inputs');

        $container.empty();

        $row.find('.tag-chip').each(function (index) {
            const tagId = $(this).data('tag-id');
            if (tagId) {
                const $input = $('<input>')
                    .attr('type', 'hidden')
                    .attr('name', `Entries[${rowIndex}].Tags`)
                    .attr('value', tagId);
                $container.append($input);
            }
        });
    }

    // Setup calculation-related event handlers
    setupCalculationHandlers() {
        // Quantity and rate changes
        $(document).on('input blur', '.qty-input, .rate-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            this.updateRowCalculations(row);
            this.validateQuantityForProduct(e.target);
        });

        // PKG changes
        $(document).on('input blur', '.pkg-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            const pkgValue = this.parseIntOrZero($(e.target).val());
            row.setAttribute('data-pkg', pkgValue);
            this.updateRowCalculations(row);
        });

        // Percentage changes
        $(document).on('input', '.percent-input', (e) => {
            this.updateRowCalculations($(e.target).closest('tr')[0]);
        });

        // Discount changes
        $(document).on('input blur', '.disc-input', () => this.updateTotals());

        // Debit/Credit focus handlers
        $(document).on('focus', '.debit-input', (e) => {
            if (!$(e.target).val()) {
                this.updateRowCalculations($(e.target).closest('tr')[0], 'debit');
            }
        });

        $(document).on('focus', '.credit-input', (e) => {
            if (!$(e.target).val()) {
                this.updateRowCalculations($(e.target).closest('tr')[0], 'credit');
            }
        });

        // Debit/Credit input changes
        $(document).on('input', '.debit-input, .credit-input', () => this.updateTotals());
    }

    // Update row calculations
    updateRowCalculations(row, focusField = null) {
        this.calculateAmountWithPkg(row);
        this.updateDiscount(row);
        this.calculateDebitCredit(row, focusField);
        this.updateTotals();
    }

    // Calculate amount considering PKG
    calculateAmountWithPkg(row) {
        const $row = $(row);
        const rbpValue = $row.find('.rbp-select').val();
        const qty = this.parseFloatOrZero($row.find('.qty-input').val());
        const rate = this.parseFloatOrZero($row.find('.rate-input').val());

        let pkg = 0;
        const $pkgInput = $row.find('.pkg-input');

        if ($pkgInput.val()) {
            pkg = this.parseIntOrZero($pkgInput.val());
            row.setAttribute('data-pkg', pkg);
        } else if (row.hasAttribute('data-pkg')) {
            pkg = this.parseIntOrZero(row.getAttribute('data-pkg'));
            $pkgInput.val(pkg);
        }

        const totalAmount = rbpValue === 'Y' && pkg > 0
            ? pkg * qty * rate
            : qty * rate;

        row.setAttribute('data-total-amount', totalAmount.toString());
    }

    // Update discount based on percentage
    updateDiscount(row) {
        const $row = $(row);
        const percent = this.parseFloatOrZero($row.find('.percent-input').val());
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');

        const discount = (totalAmount * percent) / 100;
        $row.find('.disc-input').val(Math.round(discount));
    }

    // Calculate debit/credit amounts
    calculateDebitCredit(row, focusField) {
        const $row = $(row);
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');
        const discount = this.parseFloatOrZero($row.find('.disc-input').val());

        const amount = Math.round(totalAmount - discount);

        if (amount > 0) {
            if (focusField === 'debit') {
                $row.find('.debit-input').val(amount);
                $row.find('.credit-input').val('');
            } else if (focusField === 'credit') {
                $row.find('.credit-input').val(amount);
                $row.find('.debit-input').val('');
            }
        }
    }

    // Update total amounts
    updateTotals() {    
        let totalDebit = 0;
        let totalCredit = 0;

        $('#entryRows tr').each((index, row) => {
            const $row = $(row);
            totalDebit += this.parseIntOrZero($row.find('.debit-input').val());
            totalCredit += this.parseIntOrZero($row.find('.credit-input').val());
        });

        $('#totalDebit').text(totalDebit);
        $('#totalCredit').text(totalCredit);
    }

    // Add new entry row
    addNewRow() {
        this.rowCounter++;
        const newIndex = this.rowCounter;

        const $newRow = $('#entryRow-0').clone();
        $newRow.attr('id', `entryRow-${newIndex}`);

        this.resetRowElements($newRow, newIndex);
        $('#entryRows').append($newRow);

        // Load categories if project is selected
        const projectId = $('#ProjectId').val();
        if (projectId) {
            const $categorySelect = $newRow.find('.category-select');
            this.loadCategories($categorySelect, projectId);
            
            // Set focus to the category select in the new row - fixed deprecated focus() method
            setTimeout(() => {
                $categorySelect[0].focus();
            }, 50);
        }

        this.updateTotals();
    }

    // Reset row elements for new row
    resetRowElements($row, index) {
        // Reset inputs
        $row.find('input').each(function () {
            const $input = $(this);

            // Update IDs and names
            if ($input.attr('id')) {
                $input.attr('id', $input.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($input.attr('name')) {
                $input.attr('name', $input.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            // Clear values and validation
            $input.val('').removeClass('is-invalid required-field').removeAttr('required');
            $input.next('.invalid-feedback').remove();
        });

        // Reset selects
        $row.find('select').each(function () {
            const $select = $(this);

            // Update IDs and names
            if ($select.attr('id')) {
                $select.attr('id', $select.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($select.attr('name')) {
                $select.attr('name', $select.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            $select.prop('selectedIndex', 0);
        });

        // Reset tag elements
        const $tagSearch = $row.find('.tag-search-input');
        $tagSearch.attr('data-index', index).val('');
        $row.find('.tag-chip').remove();
        $row.find('.tag-hidden-inputs').empty();

        // Clear row data attributes
        const rowElement = $row[0];
        ['data-pkg', 'data-total-amount', 'data-account-type', 'data-account-group']
            .forEach(attr => rowElement.removeAttribute(attr));
    }

    // Remove entry row
    removeRow(event) {
        const $rows = $('#entryRows tr');

        if ($rows.length > 1) {
            $(event.target).closest('tr').remove();
            this.updateTotals();
            this.updateProductRequiredIndicator();
        }
    }

    // Handle form submission
    async handleFormSubmit(event) {
        event.preventDefault();

        if (this.isSubmitting) return;

        this.hideValidationAlert();
        this.showSubmitSpinner();

        try {
            // Small delay to show spinner
            await new Promise(resolve => setTimeout(resolve, 100));

            const validationErrors = this.validateJournalEntry();

            if (validationErrors.length > 0) {
                this.showValidationErrors(validationErrors);
                return;
            }

            console.log('Validation passed, submitting form...');
            this.isSubmitting = true;
            event.target.submit();

        } catch (error) {
            console.error('Form submission error:', error);
            this.showError('An error occurred while submitting the form.');
        } finally {
            if (!this.isSubmitting) {
                this.resetSubmitButtons();
            }
        }
    }

    // Validate journal entry form
    validateJournalEntry() {
        const errors = [];

        // Validate project selection
        const projectId = this.parseIntOrZero($('#ProjectId').val());
        if (projectId <= 0) {
            errors.push('Project is required.');
        }

        // Validate entries
        const $rows = $('#entryRows tr');
        if ($rows.length === 0) {
            errors.push('At least one entry is required.');
            return errors;
        }

        let totalDebit = 0;
        let totalCredit = 0;

        $rows.each((index, row) => {
            const $row = $(row);
            const entryPrefix = `Entry ${index + 1}: `;

            const categoryId = this.parseIntOrZero($row.find('.category-select').val());
            const accountId = this.parseIntOrZero($row.find('.account-select').val());
            const debit = this.parseFloatOrZero($row.find('.debit-input').val());
            const credit = this.parseFloatOrZero($row.find('.credit-input').val());
            const rate = this.parseFloatOrZero($row.find('.rate-input').val());
            const qty = this.parseFloatOrZero($row.find('.qty-input').val());
            const rbp = $row.find('.rbp-select').val();
            const accountGroup = $row.attr('data-account-group') || '';

            // Required field validation
            if (categoryId <= 0) errors.push(`${entryPrefix}Category is required.`);
            if (accountId <= 0) errors.push(`${entryPrefix}Account is required.`);

            // Skip further validation if basic fields are missing
            if (categoryId <= 0 || accountId <= 0) return;

            // Debit/Credit validation
            if (debit <= 0 && credit <= 0) {
                errors.push(`${entryPrefix}Either Debit or Credit must be filled.`);
            } else if (debit > 0 && credit > 0) {
                errors.push(`${entryPrefix}Only one of Debit or Credit can be filled, not both.`);
            }

            totalDebit += debit;
            totalCredit += credit;

            // Product-specific validation
            if (accountGroup === 'product') {
                if (rate <= 0) {
                    errors.push(`${entryPrefix}Rate must be greater than zero for product accounts.`);
                }
                if (qty <= 0) {
                    errors.push(`${entryPrefix}Quantity must be greater than zero for product accounts.`);
                }
                if (!rbp || (rbp !== 'Y' && rbp !== 'N')) {
                    errors.push(`${entryPrefix}RBP must be Y or N for product accounts.`);
                }
            }
        });

        // Balance validation
        if (Math.abs(totalDebit - totalCredit) > 0.01) {
            const difference = Math.abs(totalDebit - totalCredit).toFixed(2);
            errors.push(
                `Debits (${totalDebit.toFixed(2)}) and credits (${totalCredit.toFixed(2)}) must be equal. ` +
                `Difference: ${difference}`
            );
        }

        return errors;
    }

    // Validate quantity for product accounts
    validateQuantityForProduct(qtyInput) {
        const $input = $(qtyInput);
        const $row = $input.closest('tr');
        const accountGroup = $row.attr('data-account-group') || '';
        const qty = this.parseFloatOrZero($input.val());

        if (accountGroup === 'product' && qty <= 0) {
            $input.addClass('is-invalid');
            if (!$input.next('.invalid-feedback').length) {
                $input.after('<div class="invalid-feedback">Quantity must be greater than zero for product accounts</div>');
            }
            return false;
        } else {
            $input.removeClass('is-invalid');
            $input.next('.invalid-feedback').remove();
            return true;
        }
    }

    // Setup product account requirements
    setupProductRequirements($row) {
        $row.find('.qty-input, .rate-input').addClass('required-field');
        $row.find('.qty-input').attr('required', 'required');
        this.validateQuantityForProduct($row.find('.qty-input')[0]);
    }

    // Update product required indicator visibility
    updateProductRequiredIndicator() {
        const hasProductAccount = $('#entryRows tr').toArray()
            .some(row => $(row).attr('data-account-group') === 'product');
        $('.product-required-indicator').toggleClass('d-none', !hasProductAccount);
    }

    // Setup validation clearance handlers
    setupValidationClearance() {
        const fieldsToWatch = [
            '#ProjectId', '.category-select', '.account-select',
            '.debit-input', '.credit-input', '.rate-input',
            '.qty-input', '.rbp-select'
        ];

        $(document).on('change input', fieldsToWatch.join(', '), () => {
            this.hideValidationAlert();
        });
    }

    // Clear all dropdowns when no project selected
    clearAllDropdowns() {
        $('#entryRows tr').each(function () {
            const $row = $(this);
            $row.find('.category-select').empty().append('<option value="">Select Category</option>');
            $row.find('.account-select').empty().append('<option value="">Select Account</option>');
        });
    }

    // Clear accounts and subsequent data when category changes
    clearAccountsAndSubsequent($row) {
        $row.find('.account-select').empty().append('<option value="">Select Account</option>');
        this.clearAccountRelatedData($row);
    }

    // Clear account-related data
    clearAccountRelatedData($row) {
        this.resetAccountRelatedFields($row);
        $row[0].removeAttribute('data-account-type');
        $row[0].removeAttribute('data-account-group');
        $row[0].removeAttribute('data-pkg');
        this.updateProductRequiredIndicator();
    }

    // Reset account-related input fields
    resetAccountRelatedFields($row) {
        $row.find('.qty-input, .rate-input')
            .removeClass('required-field is-invalid')
            .removeAttr('required')
            .prop('disabled', false)  // Ensure inputs are enabled by default
            .next('.invalid-feedback').remove();
            
        $row.find('.disc-input, .percent-input')
            .prop('disabled', false);  // Ensure inputs are enabled by default
    }

    // API helper method
    async apiGet(url, params = {}) {
        return new Promise((resolve, reject) => {
            if (typeof PureLedger === 'undefined' || !PureLedger.get) {
                reject(new Error('PureLedger API not available'));
                return;
            }

            PureLedger.get(url, params)
                .done(resolve)
                .fail((xhr, status, error) => {
                    console.error(`API Error - ${url}:`, { xhr, status, error });
                    reject(new Error(`API request failed: ${error}`));
                });
        });
    }

    // UI Helper Methods
    showSubmitSpinner() {
        $('#submitBtn').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Submitting...');
        $('#submitPrintBtn').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Submitting...');
    }

    resetSubmitButtons() {
        $('#submitBtn').prop('disabled', false)
            .html('<i class="fas fa-save me-1"></i> Submit Journal Entry');
        $('#submitPrintBtn').prop('disabled', false)
            .html('<i class="fas fa-print me-1"></i> Submit and Print');
    }

    showValidationErrors(errors) {
        if (errors.length === 0) return;

        const errorHtml = '<ul class="mb-0">' +
            errors.map(error => `<li>${error}</li>`).join('') +
            '</ul>';

        $('#validationMessage').html(errorHtml);
        $('#validationAlert').removeClass('d-none');

        // Scroll to validation alert
        $('html, body').animate({
            scrollTop: $('#validationAlert').offset().top - 100
        }, 300);

        this.resetSubmitButtons();
    }

    hideValidationAlert() {
        $('#validationAlert').addClass('d-none');
        $('#validationMessage').html('');
    }

    showError(message) {
        console.error(message);
        // Could also show a toast or alert here
    }

    // Utility Methods
    parseFloatOrZero(value) {
        const parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    parseIntOrZero(value) {
        const parsed = parseInt(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    formatDateDDMMYYYY(date) {
        const day = date.getDate().toString().padStart(2, '0');
        const month = (date.getMonth() + 1).toString().padStart(2, '0');
        const year = date.getFullYear();
        return `${day}/${month}/${year}`;
    }
}

// Initialize when document is ready - updated to non-deprecated syntax
$(function() {
    window.journalEntryManager = new JournalEntryManager();
});