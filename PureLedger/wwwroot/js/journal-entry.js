class JournalEntryManager {
    constructor() {
        this.rowCounter = 0;
        this.isSubmitting = false;
        this.init();
        this.setupSelectSearch(); // Add this line
    }

    init() {
        this.setupFormAction();
        this.initializeDatePickers();
        this.setupEventHandlers();
        this.loadInitialData();
    }

    setupFormAction() {
        try {
            const formElement = document.getElementById('entryForm');
            const basePath = PureLedger?.getCurrentBasePath?.() || '';

            if (formElement && basePath) {
                let formAction = formElement.getAttribute('action') || '/Home/Entry';

                formAction = formAction.replace(/\/route[12]\//, '/');
                formElement.setAttribute('action', basePath + formAction);

                console.log('Form action set to:', formElement.action);
            }
        } catch (error) {
            console.error('Error setting up form action:', error);
        }
    }

    initializeDatePickers() {
        try {
            const today = new Date();
            const minDate = new Date(today);
            minDate.setMonth(minDate.getMonth() - 1);

            // Only initialize datepicker for EntryDate
            $('#EntryDate').datepicker({
                dateFormat: 'dd/mm/yy',
                changeMonth: true,
                changeYear: true,
                showButtonPanel: true,
                maxDate: today,
                minDate: minDate,
                onSelect: (dateText) => {
                    this.updateDueDate();
                }
            });

            // If entry date is empty, set today's date
            if (!$('#EntryDate').val()) {
                const defaultDate = this.formatDateDDMMYYYY(today);
                $('#EntryDate').val(defaultDate);
                $('#DueDate').val(defaultDate);
            }

            // Properly disable the due date input
            $('#DueDate')
                .prop('disabled', true)
                .css({
                    'background-color': '#e9ecef',
                    'cursor': 'not-allowed'
                });

            // Remove click handler from due date calendar icon
            $('.calendar-icon').each((_, icon) => {
                const $icon = $(icon);
                const isForDueDate = $icon.siblings('#DueDate').length > 0;
                if (isForDueDate) {
                    $icon.css('cursor', 'not-allowed');
                    $icon.off('click');
                } else {
                    $icon.css('cursor', 'pointer');
                    $icon.on('click', (e) => {
                        $(e.currentTarget).siblings('input.datepicker').focus();
                    });
                }
            });

        } catch (error) {
            console.error('Error initializing date pickers:', error);
        }
    }

    setupEventHandlers() {
        $(document).on('click', '.calendar-icon', (e) => {
            $(e.currentTarget).siblings('input.datepicker')[0].focus();
        });

        $('#CreditDays, #EntryDate').on('input change', () => this.updateDueDate());

        $('#ProjectId').on('change', (e) => this.handleProjectChange(e.target.value));

        $(document).on('change', '.category-select', (e) => this.handleCategoryChange(e));

        $(document).on('change', '.account-select', (e) => this.handleAccountChange(e));

        this.setupTagHandlers();

        this.setupCalculationHandlers();

        $('#addRowBtn').on('click', () => this.addNewRow());
        $(document).on('click', '.remove-row-btn', (e) => this.removeRow(e));

        $('#entryForm').on('submit', (e) => this.handleFormSubmit(e));

        $('#submitBtn').on('click', () => $('#formAction').val('submit'));
        $('#submitPrintBtn').on('click', () => $('#formAction').val('submit_print'));

        // Add new keyboard shortcut handler
        $(document).on('keydown', (e) => {
            if (e.altKey && e.key.toLowerCase() === 'n') {
                e.preventDefault();
                this.addNewRow();
            }
        });

        // Update the Add Row button text to include shortcut
        $('#addRowBtn').html('<i class="fas fa-plus"></i> Add Row <span class="text-muted">[Alt+N]</span>');

        this.setupValidationClearance();

        // Add new handler for input focus
        $(document).on('click focus', 'input', (e) => {
            // Close all dropdowns except the one in the current container
            const $currentContainer = $(e.target).closest('.select-container');
            $('.select-dropdown').each(function() {
                const $dropdown = $(this);
                if (!$currentContainer || !$dropdown.closest('.select-container').is($currentContainer)) {
                    $dropdown.removeClass('show');
                    $dropdown.find('.select-option').removeClass('selected');
                }
            });
        });
    }

    loadInitialData() {
        const projectId = $('#ProjectId').val();
        if (projectId) {
            this.handleProjectChange(projectId);
        }
        this.updateDueDate();
        this.updateProductRequiredIndicator();
        this.updateBaseVoucherCheckbox();
    }

    updateDueDate() {
        try {
            const creditDays = this.parseIntOrZero($('#CreditDays').val());
            const entryDateStr = $('#EntryDate').val();

            if (!entryDateStr) {
                const today = this.formatDateDDMMYYYY(new Date());
                $('#EntryDate').val(today);
                $('#DueDate').val(today);
                return;
            }

            const [day, month, year] = entryDateStr.split('/').map(Number);
            const entryDate = new Date(year, month - 1, day); // month is 0-based

            if (isNaN(entryDate.getTime())) {
                console.error('Invalid entry date');
                return;
            }

            const dueDate = new Date(entryDate);
            dueDate.setDate(dueDate.getDate() + creditDays);
            $('#DueDate').val(this.formatDateDDMMYYYY(dueDate));
        } catch (error) {
            console.error('Error updating due date:', error);
        }
    }

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

    async loadCategories($categorySelect, projectId) {
        try {
            const $container = $categorySelect.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $searchInput = $container.find('.select-search');
            
            $searchInput.val('').prop('disabled', true);
            $dropdown.empty().append('<div class="select-option">Loading...</div>');

            const categories = await this.apiGet('/Home/GetCategoriesByProject', { projectId });

            $dropdown.empty();
            $categorySelect.empty().append('<option value="">Select Category</option>');

            if (categories?.length > 0) {
                categories.forEach(category => {
                    $categorySelect.append(new Option(category.name, category.id));
                    $dropdown.append(
                        `<div class="select-option" data-value="${category.id}">${category.name}</div>`
                    );
                });
            }
            
            $searchInput.prop('disabled', false);
        } catch (error) {
            console.error('Error loading categories:', error);
            $categorySelect.empty().append('<option value="">Error loading categories</option>');
            this.showError('Failed to load categories. Please try again.');
        }
    }

    async handleCategoryChange(event) {
        const $row = $(event.target).closest('tr');
        const categoryId = event.target.value;

        await this.loadAccounts($row, categoryId);
        this.clearAccountRelatedData($row);
    }

    async loadAccounts($row, categoryId) {
        const $accountSelect = $row.find('.account-select');
        const $container = $accountSelect.closest('.select-container');
        const $dropdown = $container.find('.select-dropdown');
        const $searchInput = $container.find('.select-search');

        try {
            if (!categoryId) {
                $accountSelect.empty().append('<option value="">Select Account</option>');
                $dropdown.empty();
                $searchInput.val('').prop('disabled', true);
                return;
            }

            $searchInput.val('').prop('disabled', true);
            $dropdown.empty().append('<div class="select-option">Loading...</div>');

            const accounts = await this.apiGet('/Home/GetAccountsByCategory', { categoryId });

            $dropdown.empty();
            $accountSelect.empty().append('<option value="">Select Account</option>');

            if (accounts?.length > 0) {
                accounts.forEach(account => {
                    const $option = $(new Option(account.name, account.id));
                    $option.data('account-type', account.accountTypeName || '');
                    $accountSelect.append($option);
                    
                    $dropdown.append(
                        `<div class="select-option" data-value="${account.id}">${account.name}</div>`
                    );
                });
            }
            
            $searchInput.prop('disabled', false);
        } catch (error) {
            console.error('Error loading accounts:', error);
            $accountSelect.empty().append('<option value="">Error loading accounts</option>');
            this.showError('Failed to load accounts. Please try again.');
        }
    }

    async handleAccountChange(event) {
        const $row = $(event.target).closest('tr');
        const accountId = event.target.value;
        const $selected = $(event.target).find('option:selected');

        this.resetAccountRelatedFields($row);

        if (!accountId) return;

        const accountType = $selected.data('account-type') || '';
        $row[0].setAttribute('data-account-type', accountType);

        try {
            await this.loadProductDetails($row, accountId);
            this.manageInputsBasedOnAccountGroup($row);
            this.updateBaseVoucherCheckbox();
        } catch (error) {
            console.error('Error loading product details:', error);
            this.showError('Failed to load product details.');
        }
    }

    async loadProductDetails($row, accountId) {
        try {
            const product = await this.apiGet('/Home/GetProductDetails', { accountId });

            if (product) {
                if (product.saleRate) {
                    $row.find('.rate-input').val(product.saleRate);
                }

                if (product.pkg) {
                    $row[0].setAttribute('data-pkg', product.pkg);
                    $row.find('.pkg-input').val(product.pkg);
                }

                if (product.accountGroupId) {
                    const accountGroup = product.accountGroupId === 1 ? 'account' : 'product';
                    $row[0].setAttribute('data-account-group', accountGroup);

                    if (accountGroup === 'product') {
                        this.setupProductRequirements($row);
                    }
                }

                this.updateRowCalculations($row[0]);
                this.updateProductRequiredIndicator();
            }
        } catch (error) {
            console.error('Error loading product details:', error);
            throw error;
        }
    }

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
            $rbpSelect.prop('disabled', true).val('Y');
            $qtyInput.prop('disabled', true).val('0');
            $rateInput.prop('disabled', true).val('0');
            $discInput.prop('disabled', true).val('0');
            $percentInput.prop('disabled', true).val('0');
        } else {
            $rbpSelect.prop('disabled', false);
            $qtyInput.prop('disabled', false);
            $rateInput.prop('disabled', false);
            $discInput.prop('disabled', false);
            $percentInput.prop('disabled', false);
        }
    }

    setupTagHandlers() {
        $(document).on('focus', '.tag-search-input', (e) => {
            const $suggestions = $(e.target).closest('.tag-multibox').siblings('.tag-suggestions');
            $suggestions.addClass('show');
        });

        $(document).on('blur', '.tag-search-input', (e) => {
            const $suggestions = $(e.target).closest('.tag-multibox').siblings('.tag-suggestions');
            setTimeout(() => $suggestions.removeClass('show'), 200);
        });

        $(document).on('input', '.tag-search-input', (e) => this.handleTagSearch(e));

        $(document).on('click', '.tag-suggestions .dropdown-item', (e) => this.handleTagSelection(e));

        $(document).on('click', '.tag-chip-remove', (e) => this.handleTagRemoval(e));
    }

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

    handleTagSelection(event) {
        event.preventDefault();

        const $item = $(event.target);
        const tagId = $item.data('id');
        const tagName = $item.data('name');
        const $row = $item.closest('tr');
        const $tagBox = $item.closest('td').find('.tag-multibox');
        const $input = $tagBox.find('.tag-search-input');

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

    handleTagRemoval(event) {
        event.preventDefault();

        const $chip = $(event.target).closest('.tag-chip');
        const $row = $chip.closest('tr');

        $chip.remove();
        this.updateTagInputs($row);
    }

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

    setupCalculationHandlers() {
        $(document).on('input blur', '.qty-input, .rate-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            this.updateRowCalculations(row);
            this.validateQuantityForProduct(e.target);
        });

        $(document).on('input blur', '.pkg-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            const pkgValue = this.parseIntOrZero($(e.target).val());
            row.setAttribute('data-pkg', pkgValue);
            this.updateRowCalculations(row);
        });

        $(document).on('input', '.percent-input', (e) => {
            this.updateRowCalculations($(e.target).closest('tr')[0]);
        });

        $(document).on('input blur', '.disc-input', () => this.updateTotals());

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

        $(document).on('input', '.debit-input, .credit-input', () => this.updateTotals());
    }

    updateRowCalculations(row, focusField = null) {
        this.calculateAmountWithPkg(row);
        this.updateDiscount(row);
        this.calculateDebitCredit(row, focusField);
        this.updateTotals();
    }

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

    updateDiscount(row) {
        const $row = $(row);
        const percent = this.parseFloatOrZero($row.find('.percent-input').val());
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');

        const discount = (totalAmount * percent) / 100;
        $row.find('.disc-input').val(Math.round(discount));
    }

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

    addNewRow() {
        this.rowCounter++;
        const newIndex = this.rowCounter;

        const $newRow = $('#entryRow-0').clone();
        $newRow.attr('id', `entryRow-${newIndex}`);

        this.resetRowElements($newRow, newIndex);
        $('#entryRows').append($newRow);

        // Get the first select-search input in the new row
        const $firstSearchInput = $newRow.find('.select-search').first();

        const projectId = $('#ProjectId').val();
        if (projectId) {
            const $categorySelect = $newRow.find('.category-select');
            this.loadCategories($categorySelect, projectId).then(() => {
                // Focus on the category search input after categories are loaded
                $firstSearchInput.focus();
                
                // Ensure the dropdown is shown
                $firstSearchInput.closest('.select-container')
                    .find('.select-dropdown')
                    .addClass('show');
            });
        } else {
            // If no project selected, still focus on the first input
            $firstSearchInput.focus();
        }

        this.updateBaseVoucherCheckbox();
        this.updateTotals();
    }

    resetRowElements($row, index) {
        $row.find('input').each(function () {
            const $input = $(this);

            if ($input.attr('id')) {
                $input.attr('id', $input.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($input.attr('name')) {
                $input.attr('name', $input.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            $input.val('').removeClass('is-invalid required-field').removeAttr('required');
            $input.next('.invalid-feedback').remove();
        });

        $row.find('select').each(function () {
            const $select = $(this);

            if ($select.attr('id')) {
                $select.attr('id', $select.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($select.attr('name')) {
                $select.attr('name', $select.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            $select.prop('selectedIndex', 0);
        });

        const $tagSearch = $row.find('.tag-search-input');
        $tagSearch.attr('data-index', index).val('');
        $row.find('.tag-chip').remove();
        $row.find('.tag-hidden-inputs').empty();

        const rowElement = $row[0];
        ['data-pkg', 'data-total-amount', 'data-account-type', 'data-account-group']
            .forEach(attr => rowElement.removeAttribute(attr));
    }

    removeRow(event) {
        const $rows = $('#entryRows tr');

        if ($rows.length > 1) {
            $(event.target).closest('tr').remove();
            this.updateTotals();
            this.updateProductRequiredIndicator();
            this.updateBaseVoucherCheckbox();
        }
    }

    // Handle form submission
    async handleFormSubmit(event) {
        event.preventDefault();

        if (this.isSubmitting) return;

        this.hideValidationAlert();
        this.showSubmitSpinner();

        try {
            await new Promise(resolve => setTimeout(resolve, 100));

            const validationErrors = this.validateJournalEntry();

            if (validationErrors.length > 0) {
                this.showValidationErrors(validationErrors);
                return;
            }

            // Format dates correctly before submission
            this.formatDatesForSubmission();

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

    // Add this new method to format dates in ISO format for submission
    formatDatesForSubmission() {
        // Get the entry date value
        const entryDateStr = $('#EntryDate').val();
        const dueDateStr = $('#DueDate').val();
        
        if (entryDateStr) {
            try {
                // Parse the dd/MM/yyyy format
                const [day, month, year] = entryDateStr.split('/').map(Number);
                
                // Create a valid ISO date string (yyyy-MM-dd)
                const formattedDate = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
                
                // Create a hidden field with the ISO formatted date
                const $entryDateHidden = $('<input>')
                    .attr('type', 'hidden')
                    .attr('name', 'EntryDate')
                    .val(formattedDate);
                    
                // Replace the original input with this hidden field
                $('#EntryDate').prop('disabled', true).after($entryDateHidden);
            } catch (err) {
                console.error('Error formatting entry date:', err);
            }
        }
        
        if (dueDateStr) {
            try {
                // Parse the dd/MM/yyyy format
                const [day, month, year] = dueDateStr.split('/').map(Number);
                
                // Create a valid ISO date string (yyyy-MM-dd)
                const formattedDate = `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
                
                // Create a hidden field with the ISO formatted date
                const $dueDateHidden = $('<input>')
                    .attr('type', 'hidden')
                    .attr('name', 'DueDate')
                    .val(formattedDate);
                    
                // Replace the original input with this hidden field
                $('#DueDate').prop('disabled', true).after($dueDateHidden);
            } catch (err) {
                console.error('Error formatting due date:', err);
            }
        }
    }

    validateJournalEntry() {
        const errors = [];

        const projectId = this.parseIntOrZero($('#ProjectId').val());
        if (projectId <= 0) {
            errors.push('Project is required.');
        }

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

            if (categoryId <= 0) errors.push(`${entryPrefix}Category is required.`);
            if (accountId <= 0) errors.push(`${entryPrefix}Account is required.`);

            if (categoryId <= 0 || accountId <= 0) return;

            if (debit <= 0 && credit <= 0) {
                errors.push(`${entryPrefix}Either Debit or Credit must be filled.`);
            } else if (debit > 0 && credit > 0) {
                errors.push(`${entryPrefix}Only one of Debit or Credit can be filled, not both.`);
            }

            totalDebit += debit;
            totalCredit += credit;

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

        if (Math.abs(totalDebit - totalCredit) > 0.01) {
            const difference = Math.abs(totalDebit - totalCredit).toFixed(2);
            errors.push(
                `Debits (${totalDebit.toFixed(2)}) and credits (${totalCredit.toFixed(2)}) must be equal. ` +
                `Difference: ${difference}`
            );
        }

        return errors;
    }

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

    setupProductRequirements($row) {
        $row.find('.qty-input, .rate-input').addClass('required-field');
        $row.find('.qty-input').attr('required', 'required');
        this.validateQuantityForProduct($row.find('.qty-input')[0]);
    }

    updateProductRequiredIndicator() {
        const hasProductAccount = $('#entryRows tr').toArray()
            .some(row => $(row).attr('data-account-group') === 'product');
        $('.product-required-indicator').toggleClass('d-none', !hasProductAccount);
    }

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

    // Update base voucher checkbox state based on account entries
    updateBaseVoucherCheckbox() {
        const $baseVoucherCheckbox = $('#IsBaseVoucher');
        
        // Count account entries (account group type 1)
        let accountEntriesCount = 0;
        $('#entryRows tr').each(function() {
            const accountGroup = $(this).attr('data-account-group');
            if (accountGroup === 'account') {
                accountEntriesCount++;
            }
        });

        // If we don't have exactly one account entry
        if (accountEntriesCount !== 1) {
            // Uncheck and disable the checkbox
            $baseVoucherCheckbox.prop('checked', false)
                              .prop('disabled', true);
                              
            // Add tooltip explaining why it's disabled
            const tooltipText = accountEntriesCount === 0 
                ? 'Base voucher requires exactly one account entry'
                : 'Base voucher cannot have multiple account entries';
                
            $baseVoucherCheckbox.attr('title', tooltipText);
        } else {
            // Enable the checkbox when we have exactly one account entry
            $baseVoucherCheckbox.prop('disabled', false)
                              .attr('title', 'Mark this as a base voucher');
        }
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
        try {
            const day = String(date.getDate()).padStart(2, '0');
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const year = date.getFullYear();
            return `${day}/${month}/${year}`;
        } catch (error) {
            console.error('Error formatting date:', error);
            return '';
        }
    }

    setupSelectSearch() {
        $(document).on('input', '.select-search', (e) => {
            const $input = $(e.target);
            const $container = $input.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const searchTerm = $input.val().toLowerCase();

            let visibleCount = 0;
            $dropdown.find('.select-option').each(function() {
                const text = $(this).text().toLowerCase();
                const shouldShow = text.includes(searchTerm) && visibleCount < 5;
                $(this).toggleClass('filtered-out', !shouldShow);
                if (shouldShow) visibleCount++;
            });

            $dropdown.addClass('show');
        });

        $(document).on('keydown', '.select-search', (e) => {
            const $input = $(e.target);
            const $container = $input.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $options = $dropdown.find('.select-option:not(.filtered-out)');
            const $selected = $dropdown.find('.select-option.selected');
            
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
                case 'Tab':
                    if ($dropdown.hasClass('show')) {
                        e.preventDefault();
                        const $toSelect = $selected.length ? $selected : $options.first();
                        if ($toSelect.length) {
                            this.selectOption($toSelect, $container);
                        }
                    }
                    break;
            }
        });

        $(document).on('click', '.select-option:not(.filtered-out)', (e) => {
            const $option = $(e.target);
            const $container = $option.closest('.select-container');
            this.selectOption($option, $container);
        });

        $(document).on('click', (e) => {
            if (!$(e.target).closest('.select-container').length) {
                $('.select-dropdown').removeClass('show');
                $('.select-option').removeClass('selected');
            }
        });

        $(document).on('focus', '.select-search', (e) => {
            const $input = $(e.target);
            const $container = $input.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            $dropdown.addClass('show');
        });
    }

    selectOption($option, $container) {
        const $select = $container.find('select');
        const $input = $container.find('.select-search');
        const $dropdown = $container.find('.select-dropdown');

        $select.val($option.data('value')).trigger('change');
        $input.val($option.text());
        $dropdown.removeClass('show');
        $('.select-option').removeClass('selected');
    }
}

$(function() {
    window.journalEntryManager = new JournalEntryManager();
});