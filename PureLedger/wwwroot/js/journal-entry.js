class JournalEntryManager {
    constructor() {
        this.rowCounter = 0;
        this.isSubmitting = false;
        this.rowTemplate = null;
        this.dataService = window.journalDataService;
        this.requireQuantity = true;

        this.captureRowTemplate();
        this.init();
        this.setupSelectSearch();
        console.log("JournalEntryManager initialized");
    }

    // Capture the initial row as a template
    captureRowTemplate() {
        const initialRow = document.getElementById('entryRow-0');
        if (initialRow) {
            this.rowTemplate = initialRow.cloneNode(true);
        }
    }

    init() {
        console.log("Initializing Journal Entry Manager");
        this.rowCounter = $('#entryRows tr').length - 1;
        this.setupFormAction();
        this.initializeDatePickers();

        // Make all discount inputs readonly
        $('.disc-input').prop('readonly', true).attr('tabindex', '-1');

        this.setupEventHandlers();

        // Handle edit mode and load initial data
        if (this.isEditMode()) {
            console.log("Edit mode detected - processing existing data");
            this.processExistingData();
        }

        this.loadInitialData();
        this.updateSubmitButtonsState();
    }

    isEditMode() {
        return this.dataService.isEditMode();
    }

    // Process data that was already loaded from the model
    async processExistingData() {
        await this.dataService.processExistingData();

        // Apply account-specific settings to each row
        $('#entryRows tr').each((index, row) => {
            const $row = $(row);

            // Apply account type specific settings
            this.manageInputsBasedOnAccountGroup($row);

            // Explicitly trigger row calculations for product accounts
            if ($row.attr('data-account-group') === 'product') {
                // Only auto-calculate if both debit and credit are empty
                const debit = parseFloat($row.find('.debit-input').val()) || 0;
                const credit = parseFloat($row.find('.credit-input').val()) || 0;
                if (debit === 0 && credit === 0) {
                    this.updateRowCalculations($row[0], 'debit');
                }
            }
        });
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

            // Set up the entry date input without any date restrictions
            const entryDateInput = document.getElementById('EntryDate');
            if (entryDateInput) {
                // Remove min/max date restrictions
                entryDateInput.removeAttribute('min');
                entryDateInput.removeAttribute('max');

                // If entry date is empty, set today's date
                if (!entryDateInput.value) {
                    entryDateInput.value = today.toISOString().split('T')[0];
                }

                // Add change event to update due date
                entryDateInput.addEventListener('change', () => this.updateDueDate());
            }

            // Properly disable the due date input
            $('#DueDate')
                .prop('disabled', true)
                .css({
                    'background-color': '#e9ecef',
                    'cursor': 'not-allowed'
                });

            // Set up calendar icons
            $('.calendar-icon').each((_, icon) => {
                const $icon = $(icon);
                const isForDueDate = $icon.siblings('#DueDate').length > 0;
                if (isForDueDate) {
                    $icon.css('cursor', 'not-allowed');
                } else {
                    $icon.css('cursor', 'pointer');
                }
            });
        } catch (error) {
            console.error('Error initializing date pickers:', error);
        }
    }

    setupEventHandlers() {
        $(document).on('click', '.calendar-icon', (e) => {
            const $input = $(e.currentTarget).siblings('input[type="date"]');
            if ($input.length > 0 && !$input.prop('disabled')) {
                $input[0].showPicker();
            }
        });

        $('#CreditDays, #EntryDate').on('input change', () => this.updateDueDate());

        $('#ProjectId').on('change', (e) => this.handleProjectChange(e.target.value));

        $(document).on('change', '.category-select', (e) => this.handleCategoryChange(e));

        $(document).on('change', '.account-select', (e) => this.handleAccountChange(e));

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

        $('#addRowBtn').html('<i class="fas fa-plus"></i> Add Row <span class="text-muted">[Alt+N]</span>');

        this.setupValidationClearance();

        $(document).on('click focus', 'input', (e) => {
            const $currentContainer = $(e.target).closest('.select-container');
            $('.select-dropdown').each(function () {
                const $dropdown = $(this);
                if (!$currentContainer || !$dropdown.closest('.select-container').is($currentContainer)) {
                    $dropdown.removeClass('show');
                    $dropdown.find('.select-option').removeClass('selected');
                }
            });
        });

        $('#SaveAsRecipe').on('change', (e) => {
            this.toggleRecipeNameInput(e.target.checked);
        });

        // Handle Require Quantity checkbox
        $('#RequireQuantity').on('change', (e) => {
            this.requireQuantity = e.target.checked;
            this.updateProductRequiredIndicator();

            // Re-validate existing quantity fields
            $('#entryRows tr').each((index, row) => {
                const $row = $(row);
                if ($row.attr('data-account-group') === 'product') {
                    this.validateQuantityForProduct($row.find('.qty-input')[0]);
                }
            });
        });

        // Handle BookerId change to update submit buttons
        $('#BookerId').on('change', () => {
            this.updateSubmitButtonsState();
        });
    }

    loadInitialData() {
        const projectId = $('#ProjectId').val();

        // For normal new entries (not edit mode and not recipe-based)
        if (projectId && !this.isEditMode()) {
            this.handleProjectChange(projectId);
        }

        // For recipe-based entries
        if (!this.isEditMode()) {
            this.processRecipeEntries();
        }

        // Set initial state of requireQuantity based on checkbox
        this.requireQuantity = $('#RequireQuantity').is(':checked');

        this.updateDueDate();
        this.updateProductRequiredIndicator();
        this.updateBaseVoucherCheckbox();
        this.updateSaveRecipeCheckbox();
        this.updateTotals();
    }

    updateDueDate() {
        try {
            const creditDays = this.parseIntOrZero($('#CreditDays').val());
            const entryDateStr = $('#EntryDate').val(); // Now in yyyy-MM-dd format

            if (!entryDateStr) {
                const today = new Date().toISOString().split('T')[0]; // Format: yyyy-MM-dd
                $('#EntryDate').val(today);
                $('#DueDate').val(today);
                return;
            }

            // Parse the date from the HTML5 date input (yyyy-MM-dd)
            const entryDate = new Date(entryDateStr);

            if (isNaN(entryDate.getTime())) {
                console.error('Invalid entry date');
                return;
            }

            const dueDate = new Date(entryDate);
            dueDate.setDate(dueDate.getDate() + creditDays);

            // Format for HTML5 date input
            $('#DueDate').val(dueDate.toISOString().split('T')[0]);
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
            const existingCategoryId = $row.data('category-id');

            await this.dataService.loadCategories($categorySelect, projectId);

            // If we have a data attribute with category ID, set it after loading options
            if (existingCategoryId) {
                $categorySelect.val(existingCategoryId);
                const categoryText = $categorySelect.find('option:selected').text();
                $row.find('.category-search').val(categoryText);

                // Also load accounts based on this category
                await this.dataService.loadAccounts($row, existingCategoryId);

                // Set account value if we have it
                const existingAccountId = $row.data('account-id');
                if (existingAccountId) {
                    $row.find('.account-select').val(existingAccountId);
                    const accountText = $row.find('.account-select option:selected').text();
                    $row.find('.account-search').val(accountText);
                }
            } else {
                this.clearAccountsAndSubsequent($row);
            }
        }
    }

    async handleCategoryChange(event) {
        const $row = $(event.target).closest('tr');
        const categoryId = event.target.value;

        await this.dataService.loadAccounts($row, categoryId);
        this.clearAccountRelatedData($row);

        $row.data('category-id', categoryId);
    }

    async handleAccountChange(event) {
        const $row = $(event.target).closest('tr');
        const accountId = event.target.value;
        const $selected = $(event.target).find('option:selected');

        this.resetAccountRelatedFields($row);

        if (!accountId) return;

        const accountType = $selected.data('account-type') || '';
        $row[0].setAttribute('data-account-type', accountType);

        // Update the data attribute
        $row.data('account-id', accountId);

        try {
            await this.dataService.loadProductDetails($row, accountId);
            this.manageInputsBasedOnAccountGroup($row);
            this.updateBaseVoucherCheckbox();
            this.updateSaveRecipeCheckbox();

            // For product accounts, set up requirements and trigger calculations
            if ($row.attr('data-account-group') === 'product') {
                this.setupProductRequirements($row);

                // Only trigger calculations if both debit and credit are empty (new row, not edit)
                const debit = this.parseFloatOrZero($row.find('.debit-input').val());
                const credit = this.parseFloatOrZero($row.find('.credit-input').val());
                const qty = this.parseFloatOrZero($row.find('.qty-input').val());
                if (qty > 0 && debit === 0 && credit === 0) {
                    this.updateRowCalculations($row[0], 'debit');
                }
            }

            this.updateRowCalculations($row[0]);
            this.updateProductRequiredIndicator();
        } catch (error) {
            console.error('Error loading product details:', error);
            this.showError('Failed to load product details.');
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
        $discInput.prop('readonly', true).attr('tabindex', '-1');
        $pkgInput.prop('disabled', true);

        if (accountGroup === 'account') {
            $rbpSelect.prop('disabled', true).val('Y');
            $qtyInput.prop('disabled', true).val('0');
            $rateInput.prop('disabled', true).val('0');
            $discInput.val('0');
            $percentInput.prop('disabled', true).val('0');
        } else {
            $rbpSelect.prop('disabled', false);
            $qtyInput.prop('disabled', false);
            $rateInput.prop('disabled', false);
            $percentInput.prop('disabled', false);
        }
    }

    setupCalculationHandlers() {
        // Qty blur
        $(document).on('blur', '.qty-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            this.calculateJournalDebit(row, 'debit');
            this.updateTotals();
            this.validateQuantityForProduct(e.target);
        });

        // Rate blur
        $(document).on('blur', '.rate-input', (e) => {
            const $input = $(e.target);
            if ($input.val()) {
                $input.val(this.formatDecimalValue($input.val()));
            }
            const row = $input.closest('tr')[0];
            this.calculateJournalDebit(row, 'debit');
            this.updateTotals();
            this.validateQuantityForProduct($input.closest('tr').find('.qty-input')[0]);
        });

        // Percent blur
        $(document).on('blur', '.percent-input', (e) => {
            const row = $(e.target).closest('tr')[0];
            this.calculateJournalDebit(row, 'debit');
            this.updateTotals();
        });

        // Debit focus
        $(document).on('focus', '.debit-input', (e) => {
            const $row = $(e.target).closest('tr');
            if (!$(e.target).val()) {
                this.calculateJournalDebit($row[0], 'debit');
                this.updateTotals();
            }
        });

        // Credit focus
        $(document).on('focus', '.credit-input', (e) => {
            const $row = $(e.target).closest('tr');
            if (!$(e.target).val()) {
                this.calculateJournalDebit($row[0], 'credit');
                this.updateTotals();
            }
        });

        // Manual input in debit/credit updates totals
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

        row.setAttribute('data-total-amount', totalAmount.toFixed(4));
    }

    updateDiscount(row) {
        const $row = $(row);
        const $percentInput = $row.find('.percent-input');
        if ($percentInput.data('manual-percent')) {
            // Do not overwrite manual percent
            return;
        }
        const percent = this.parseFloatOrZero($percentInput.val());
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');
        const discount = (totalAmount * percent) / 100;
        $row.find('.disc-input').val(discount.toFixed(4));
    }

    calculateDebitWithManualPercent(row) {
        const $row = $(row);
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');
        const percent = this.parseFloatOrZero($row.find('.percent-input').val());
        const discount = (totalAmount * percent) / 100;
        $row.find('.disc-input').val(discount.toFixed(4));
        const $debit = $row.find('.debit-input');
        const $credit = $row.find('.credit-input');
        if (!$credit.val() || this.parseFloatOrZero($credit.val()) === 0) {
            $debit.val((totalAmount - discount).toFixed(4));
        }
        this.updateTotals();
    }

    calculateDebitWithManualDiscount(row) {
        const $row = $(row);
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');
        const discount = this.parseFloatOrZero($row.find('.disc-input').val());
        const $debit = $row.find('.debit-input');
        const $credit = $row.find('.credit-input');
        if (!$credit.val() || this.parseFloatOrZero($credit.val()) === 0) {
            $debit.val((totalAmount - discount).toFixed(4));
        }
        this.updateTotals();
    }

    calculateDebitCredit(row, focusField) {
        const $row = $(row);
        const totalAmount = this.parseFloatOrZero(row.getAttribute('data-total-amount') || '0');
        const discount = this.parseFloatOrZero($row.find('.disc-input').val());

        const debitValue = this.parseFloatOrZero($row.find('.debit-input').val());
        const creditValue = this.parseFloatOrZero($row.find('.credit-input').val());

        // If either debit or credit is already set, do NOT overwrite (edit mode)
        if (debitValue > 0 || creditValue > 0) {
            return;
        }

        const amount = totalAmount - discount;

        if (amount > 0) {
            if (focusField === 'debit' || (!focusField && !debitValue && !creditValue)) {
                $row.find('.debit-input').val(amount.toFixed(4));
                $row.find('.credit-input').val('');
            } else if (focusField === 'credit') {
                $row.find('.credit-input').val(amount.toFixed(4));
                $row.find('.debit-input').val('');
            }
        }
    }

    updateTotals() {
        let totalDebit = 0;
        let totalCredit = 0;

        $('#entryRows tr').each((index, row) => {
            const $row = $(row);
            totalDebit += this.parseFloatOrZero($row.find('.debit-input').val());
            totalCredit += this.parseFloatOrZero($row.find('.credit-input').val());
        });

        $('#totalDebit').text(totalDebit.toFixed(4));
        $('#totalCredit').text(totalCredit.toFixed(4));
    }

    addNewRow() {
        this.rowCounter++;
        const newIndex = this.rowCounter;

        let $newRow;
        const firstRow = document.getElementById('entryRow-0');

        if (firstRow) {
            $newRow = $(firstRow).clone();
        } else if (this.rowTemplate) {
            $newRow = $(this.rowTemplate).clone();
        } else {
            console.error("No row template available");
            return; // Exit if we can't create a new row
        }

        $newRow.attr('id', `entryRow-${newIndex}`);
        this.resetRowElements($newRow, newIndex);
        $('#entryRows').append($newRow);

        // Get the first select-search input in the new row
        const $firstSearchInput = $newRow.find('.select-search').first();

        const projectId = $('#ProjectId').val();
        if (projectId) {
            const $categorySelect = $newRow.find('.category-select');
            this.dataService.loadCategories($categorySelect, projectId).then(() => {
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
        this.updateSaveRecipeCheckbox();
        this.updateTotals();
    }

    // Update IDs and names with a more efficient regex
    resetRowElements($row, index) {
        $row.find('[id]').each(function () {
            const $el = $(this);
            $el.attr('id', $el.attr('id').replace(/(_\d+_|_\d+$)/, `_${index}_`));
        });

        $row.find('[name]').each(function () {
            const $el = $(this);
            $el.attr('name', $el.attr('name').replace(/\[\d+\]/, `[${index}]`));
        });

        // Clear all input values and validation states
        $row.find('input').val('').removeClass('is-invalid required-field').removeAttr('required')
            .next('.invalid-feedback').remove();

        // Reset selects to first option
        $row.find('select').prop('selectedIndex', 0);

        // Clear data attributes
        const rowElement = $row[0];
        ['data-pkg', 'data-total-amount', 'data-account-type', 'data-account-group',
            'data-category-id', 'data-account-id']
            .forEach(attr => {
                rowElement.removeAttribute(attr);
                $(rowElement).data(attr.replace('data-', ''), null);
            });

        // Make sure discount input is readonly in new rows
        $row.find('.disc-input').prop('readonly', true).attr('tabindex', '-1');
    }

    updateSaveRecipeCheckbox() {
        const $saveRecipeCheckboxRow = $('#saveRecipeCheckboxRow');
        const $saveRecipeCheckbox = $('#SaveAsRecipe');
        const entriesCount = $('#entryRows tr').length;

        let productEntriesCount = 0;
        let nonProductEntries = 0;

        $('#entryRows tr').each(function () {
            const accountGroup = $(this).attr('data-account-group');
            if (accountGroup === 'product') {
                productEntriesCount++;
            } else if (accountGroup) {
                nonProductEntries++;
            }
        });
        const hasEnoughEntries = entriesCount >= 2;
        const allAssignedEntriesAreProducts = nonProductEntries === 0;
        const hasAtLeastOneProduct = productEntriesCount > 0;

        const shouldShow = hasEnoughEntries && allAssignedEntriesAreProducts && hasAtLeastOneProduct;

        if (shouldShow) {
            $saveRecipeCheckboxRow.show();
        } else {
            $saveRecipeCheckbox.prop('checked', false);
            $saveRecipeCheckboxRow.hide();
            this.toggleRecipeNameInput(false);
        }
    }

    toggleRecipeNameInput(show) {
        const $recipeNameContainer = $('#recipeNameContainer');
        const $recipeNameInput = $('#RecipeName');

        if (show) {
            $recipeNameContainer.removeClass('d-none');
            $recipeNameInput.prop('required', true);
        } else {
            $recipeNameContainer.addClass('d-none');
            $recipeNameInput.prop('required', false).val('');
        }
    }

    setupSelectSearch() {
        const self = this;

        // Ensure all visible options are scrollable to selection
        this.ensureOptionVisible = function ($option) {
            if (!$option || !$option.length) return;
            const optEl = $option[0];
            // Prefer scrollIntoView for nearest behavior within the dropdown container
            try {
                optEl.scrollIntoView({ block: 'nearest', inline: 'nearest' });
            } catch (err) {
                // fallback: compute container scroll manually
                const $dropdown = $option.closest('.select-dropdown');
                if ($dropdown.length) {
                    const dropdown = $dropdown[0];
                    const optRect = optEl.getBoundingClientRect();
                    const ddRect = dropdown.getBoundingClientRect();
                    if (optRect.top < ddRect.top) {
                        dropdown.scrollTop -= (ddRect.top - optRect.top);
                    } else if (optRect.bottom > ddRect.bottom) {
                        dropdown.scrollTop += (optRect.bottom - ddRect.bottom);
                    }
                }
            }
        };

        $(document).on('input', '.select-search', function () {
            const $input = $(this);
            const $container = $input.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $select = $container.find('select');
            const searchTerm = $input.val().toLowerCase();

            // Filter options based on search term (no artificial cap)
            $dropdown.find('.select-option').each(function () {
                const text = $(this).text().toLowerCase();
                const shouldShow = text.includes(searchTerm);
                $(this).toggleClass('filtered-out', !shouldShow);
            });

            // Show dropdown
            $dropdown.addClass('show');
        });

        // Show dropdown on focus and highlight the currently selected option
        $(document).on('focus', '.select-search', function () {
            $('.select-dropdown').removeClass('show');

            const $container = $(this).closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $select = $container.find('select');
            const currentValue = $select.val();

            // Clear previous selection highlighting
            $dropdown.find('.select-option').removeClass('selected');

            // If there's a currently selected value, highlight it in the dropdown
            if (currentValue) {
                const $selectedOption = $dropdown.find(`.select-option[data-value="${currentValue}"]`);
                if ($selectedOption.length) {
                    $selectedOption.addClass('selected');
                    // ensure highlighted option is visible
                    self.ensureOptionVisible($selectedOption);
                }
            }

            $dropdown.addClass('show');
        });

        // Navigation with keyboard
        $(document).on('keydown', '.select-search', function (e) {
            const $dropdown = $(this).closest('.select-container').find('.select-dropdown');
            let $options = $dropdown.find('.select-option:not(.filtered-out)');
            let $selected = $dropdown.find('.select-option.selected');

            // If there are no visible options, do nothing
            if ($options.length === 0) return;

            // Ensure dropdown is visible when navigating via keyboard
            if (!$dropdown.hasClass('show')) {
                $dropdown.addClass('show');
            }

            // If Tab key and dropdown is showing, just close the dropdown without changing selection
            if (e.key === 'Tab' && $dropdown.hasClass('show')) {
                $dropdown.removeClass('show');
                return; // Allow normal tab behavior
            }

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    if (!$selected.length) {
                        const $first = $options.first();
                        $first.addClass('selected');
                        self.ensureOptionVisible($first);
                    } else {
                        const $next = $selected.nextAll('.select-option:not(.filtered-out)').first();
                        if ($next.length) {
                            $selected.removeClass('selected');
                            $next.addClass('selected');
                            self.ensureOptionVisible($next);
                        }
                    }
                    break;

                case 'ArrowUp':
                    e.preventDefault();
                    if (!$selected.length) {
                        const $last = $options.last();
                        $last.addClass('selected');
                        self.ensureOptionVisible($last);
                    } else {
                        const $prev = $selected.prevAll('.select-option:not(.filtered-out)').first();
                        if ($prev.length) {
                            $selected.removeClass('selected');
                            $prev.addClass('selected');
                            self.ensureOptionVisible($prev);
                        }
                    }
                    break;

                case 'Enter':
                    if ($dropdown.hasClass('show')) {
                        e.preventDefault();
                        $selected = $dropdown.find('.select-option.selected');
                        if ($selected.length) {
                            self.selectOption($selected, $(this).closest('.select-container'));
                        } else {
                            // If nothing is selected but dropdown is open, select first visible option
                            const $firstOption = $options.first();
                            if ($firstOption.length) {
                                self.selectOption($firstOption, $(this).closest('.select-container'));
                            }
                        }
                    }
                    break;

                case 'Escape':
                    e.preventDefault();
                    $dropdown.removeClass('show');
                    break;
            }
        });

        $(document).on('click', '.select-option:not(.filtered-out)', function () {
            self.selectOption($(this), $(this).closest('.select-container'));
        });

        $(document).on('click', function (e) {
            // Only close dropdowns when clicking outside of any select container
            if (!$(e.target).closest('.select-container').length) {
                $('.select-dropdown').removeClass('show');
            }
        });

        $(document).on('input', '.category-search', function () {
            const $input = $(this);
            const $container = $input.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $select = $container.find('select');
            const searchTerm = $input.val().toLowerCase();

            // Filter options based on search term (no artificial cap)
            $dropdown.find('.select-option').each(function () {
                const text = $(this).text().toLowerCase();
                const shouldShow = text.includes(searchTerm);
                $(this).toggleClass('filtered-out', !shouldShow);
            });

            // Show dropdown
            $dropdown.addClass('show');

            // --- NEW LOGIC: If input is cleared, clear selection and account ---
            if ($input.hasClass('category-search') && $input.val().trim() === '') {
                // Remove selection from category select
                $select.val('');
                // Find the account select in the same row and clear/disable it
                const $row = $input.closest('tr');
                const $accountSelect = $row.find('.account-select');
                const $accountSearch = $row.find('.account-search');
                $accountSelect.val('');
                $accountSelect.prop('disabled', true);
                $accountSearch.val('');
                // Optionally, clear the dropdown options as well
                $accountSelect.empty().append('<option value="">Select Account</option>');
                $row.removeAttr('data-account-id');
                $row.removeAttr('data-account-group');
                $row.removeAttr('data-account-type');
            } else {
                // Re-enable account select if category is not empty
                const $row = $input.closest('tr');
                $row.find('.account-select').prop('disabled', false);
            }
        });
    }

    selectOption($option, $container) {
        const value = $option.data('value');
        const text = $option.text();

        // Update select and input values
        $container.find('select').val(value).trigger('change');
        $container.find('.select-search').val(text);

        // Hide dropdown and update the selected class
        $container.find('.select-dropdown').removeClass('show');
        $container.find('.select-option').removeClass('selected');
        $option.addClass('selected'); // Keep the option marked as selected
    }

    async handleFormSubmit(event) {
        event.preventDefault();

        if (this.isSubmitting) return;

        this.hideValidationAlert();
        this.showSubmitSpinner();

        try {

            const validationErrors = this.validateJournalEntry();

            if (validationErrors.length > 0) {
                this.showValidationErrors(validationErrors);
                return;
            }

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

    formatDatesForSubmission() {
        const entryDateStr = $('#EntryDate').val();
        const dueDateStr = $('#DueDate').val();

        if (entryDateStr) {
            const $entryDateHidden = $('<input>')
                .attr('type', 'hidden')
                .attr('name', 'EntryDate')
                .val(entryDateStr);

            $('#EntryDate').prop('disabled', true).after($entryDateHidden);
        }

        if (dueDateStr) {
            const $dueDateHidden = $('<input>')
                .attr('type', 'hidden')
                .attr('name', 'DueDate')
                .val(dueDateStr);

            $('#DueDate').prop('disabled', true).after($dueDateHidden);
        }
    }

    validateJournalEntry() {
        const errors = [];

        // Check if booker is available
        const bookerId = this.parseIntOrZero($('#BookerId').val());
        if (bookerId <= 0) {
            errors.push('A valid Booker must be selected.');
        }

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
                if (rate < 0) {
                    errors.push(`${entryPrefix}Rate must be greater than or Equal to zero for product accounts.`);
                }
                // Only validate quantity if requireQuantity is enabled
                if (this.requireQuantity && qty <= 0) {
                    errors.push(`${entryPrefix}Quantity must be greater than zero for product accounts.`);
                }
                if (!rbp || (rbp !== 'Y' && rbp !== 'N')) {
                    errors.push(`${entryPrefix}RBP must be Y or N for product accounts.`);
                }
            }
        });

        if ($('#SaveAsRecipe').is(':checked')) {
            const recipeName = $('#RecipeName').val().trim();
            if (!recipeName) {
                errors.push('Recipe name is required when saving as a recipe.');
                $('#RecipeName').addClass('is-invalid');
            } else {
                $('#RecipeName').removeClass('is-invalid');
            }
        }

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
        // Only validate if requireQuantity is enabled
        if (!this.requireQuantity) {
            // Remove any validation styling if requirement is disabled
            const $input = $(qtyInput);
            $input.removeClass('is-invalid');
            $input.next('.invalid-feedback').remove();
            return true;
        }

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
        // Only add required styling if requireQuantity is enabled
        if (this.requireQuantity) {
            $row.find('.qty-input').addClass('required-field');
        }
        $row.find('.rate-input').addClass('required-field');
        this.validateQuantityForProduct($row.find('.qty-input')[0]);
    }

    updateProductRequiredIndicator() {
        const hasProductAccount = $('#entryRows tr').toArray()
            .some(row => $(row).attr('data-account-group') === 'product');

        // Show the asterisk only if there are product accounts AND requireQuantity is enabled
        const shouldShowIndicator = hasProductAccount && this.requireQuantity;
        $('.product-required-indicator').toggleClass('d-none', !shouldShowIndicator);
    }

    setupValidationClearance() {
        const fieldsToWatch = [
            '#ProjectId', '.category-select', '.account-select',
            '.debit-input', '.credit-input', '.rate-input',
            '.qty-input', '.rbp-select', '#BookerId'
        ];

        $(document).on('change input', fieldsToWatch.join(', '), () => {
            this.hideValidationAlert();
        });
    }

    updateSubmitButtonsState() {
        const hasBookers = window.hasBookers || false;
        const bookerId = this.parseIntOrZero($('#BookerId').val());
        const isValid = hasBookers && bookerId > 0;

        $('#submitBtn, #submitPrintBtn').prop('disabled', !isValid);

        if (!isValid) {
            $('#submitBtn, #submitPrintBtn').attr('title', 'Please select a valid booker to submit');
        } else {
            $('#submitBtn, #submitPrintBtn').removeAttr('title');
        }
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
        $row.find('.account-search').val('');
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

    // UI Helper Methods
    showSubmitSpinner() {
        $('#submitBtn').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Submitting...');
        $('#submitPrintBtn').prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Submitting...');
    }

    resetSubmitButtons() {
        const isEditMode = this.isEditMode();
        const hasBookers = window.hasBookers || false;
        const bookerId = this.parseIntOrZero($('#BookerId').val());
        const shouldEnable = hasBookers && bookerId > 0;

        $('#submitBtn').prop('disabled', !shouldEnable)
            .html(`<i class="fas fa-save me-1"></i> ${isEditMode ? 'Update Journal Voucher' : 'Submit Journal Entry'}`);
        $('#submitPrintBtn').prop('disabled', !shouldEnable)
            .html(`<i class="fas fa-print me-1"></i> ${isEditMode ? 'Update and Print' : 'Submit and Print'}`);
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

        // Display error in the validation alert area
        $('#validationMessage').html(message);
        $('#validationAlert').removeClass('d-none');

        // Scroll to the alert
        $('html, body').animate({
            scrollTop: $('#validationAlert').offset().top - 100
        }, 300);

        // Hide after 5 seconds
        setTimeout(() => {
            $('#validationAlert').addClass('d-none');
        }, 5000);
    }

    updateBaseVoucherCheckbox() {
        const $baseVoucherCheckboxRow = $('#baseVoucherCheckboxRow');
        const $baseVoucherCheckbox = $('#IsBaseVoucher');
        let accountEntriesCount = 0;
        let productEntriesCount = 0;

        $('#entryRows tr').each(function () {
            const accountGroup = $(this).attr('data-account-group');
            if (accountGroup === 'account') {
                accountEntriesCount++;
            } else if (accountGroup === 'product') {
                productEntriesCount++;
            }
        });

        const shouldShow = accountEntriesCount === 1 && productEntriesCount >= 1;

        if (shouldShow) {
            $baseVoucherCheckboxRow.show();
        } else {
            // Always uncheck and hide if not valid
            $baseVoucherCheckbox.prop('checked', false);
            $baseVoucherCheckboxRow.hide();
        }
    }

    parseFloatOrZero(value) {
        if (value === null || value === undefined || value === '') return 0;
        const parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    parseIntOrZero(value) {
        const parsed = parseInt(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    formatDecimalValue(value, decimals = 4) {
        if (value === null || value === undefined || value === '') return '';
        const parsed = parseFloat(value);
        return isNaN(parsed) ? '' : parsed.toFixed(decimals);
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

    removeRow(e) {
        try {
            const $button = $(e.target).closest('.remove-row-btn');
            const $row = $button.closest('tr');
            const $rows = $('#entryRows tr');

            if ($rows.length > 1) {
                // Remove the row from the DOM
                $row.remove();

                this.rowCounter = $rows.length - 2; // -1 for the removed row, -1 for zero-based indexing

                // Update the remaining rows' indices
                $('#entryRows tr').each((idx, row) => {
                    const $r = $(row);

                    // Update row ID
                    $r.attr('id', `entryRow-${idx}`);

                    $r.find('input, select').each(function () {
                        const $el = $(this);
                        const name = $el.attr('name');
                        const id = $el.attr('id');

                        if (name && name.includes('Entries[')) {
                            $el.attr('name', name.replace(/Entries\[\d+\]/, `Entries[${idx}]`));
                        }

                        if (id) {
                            $el.attr('id', id.replace(/(\d+)/, idx));
                        }
                    });
                });

                this.updateTotals();
                this.updateProductRequiredIndicator();
                this.updateBaseVoucherCheckbox();
                this.updateSaveRecipeCheckbox();

                console.log(`Row removed successfully, ${$('#entryRows tr').length} rows remaining`);
            } else {
                this.showError("Cannot delete the last row. At least one entry is required.");
            }
        } catch (error) {
            console.error("Error removing row:", error);
            this.showError("An error occurred while removing the row. Please try again.");
        }
    }

    /**
     * Process a recipe-based journal entry
     */
    processRecipeEntries() {
        // Check if there are any rows with preloaded data from a recipe
        let hasPreloadedData = false;
        const projectId = $('#ProjectId').val();

        $('#entryRows tr').each((index, row) => {
            const $row = $(row);
            const categoryId = $row.data('category-id');
            const accountId = $row.data('account-id');

            if (categoryId && accountId) {
                hasPreloadedData = true;
                // Process this recipe item row
                this.dataService.processRecipeRow($row, projectId, categoryId, accountId, this.formatDecimalValue.bind(this))
                    .then(() => {
                        // Apply account type specific settings
                        this.manageInputsBasedOnAccountGroup($row);

                        // Focus on quantity field for product accounts to prompt user to enter value
                        if ($row.attr('data-account-group') === 'product') {
                            $row.find('.qty-input').focus();

                            // Explicitly trigger row calculations
                            this.updateRowCalculations($row[0], 'debit');
                        }
                    });
            }
        });

        if (hasPreloadedData) {
            console.log("Processing recipe-based journal entry");
        }
    }

    calculateJournalDebit(row, targetField = 'debit') {
        const $row = $(row);
        const qty = this.parseFloatOrZero($row.find('.qty-input').val());
        const rate = this.parseFloatOrZero($row.find('.rate-input').val());
        const percent = this.parseFloatOrZero($row.find('.percent-input').val());
        const rbp = $row.find('.rbp-select').val();
        const pkg = this.parseIntOrZero($row.find('.pkg-input').val());
        let totalAmount = 0;

        if (rbp === 'Y' && pkg > 0) {
            totalAmount = pkg * qty * rate;
        } else {
            totalAmount = qty * rate;
        }

        const discount = (totalAmount * percent) / 100;
        const netAmount = totalAmount - discount;

        // Set discount field
        $row.find('.disc-input').val(discount.toFixed(4));

        // Only update if both debit and credit are empty, or if the user is focusing a specific field
        const $debit = $row.find('.debit-input');
        const $credit = $row.find('.credit-input');
        const debitVal = this.parseFloatOrZero($debit.val());
        const creditVal = this.parseFloatOrZero($credit.val());

        if (targetField === 'debit') {
            if (!creditVal) {
                $debit.val(netAmount > 0 ? netAmount.toFixed(4) : '');
                $credit.val('');
            }
        } else if (targetField === 'credit') {
            if (!debitVal) {
                $credit.val(netAmount > 0 ? netAmount.toFixed(4) : '');
                $debit.val('');
            }
        }
    }
}

$(function () {
    // Create the manager after ensuring the data service is available
    if (typeof window.journalDataService !== 'undefined') {
        window.journalEntryManager = new JournalEntryManager();
    } else {
        console.error('Journal Data Service not found! Make sure load-data-journal-entry.js is loaded first.');
    }
});