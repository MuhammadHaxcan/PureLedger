class JournalDataService {
    constructor() {
        console.log("Journal Data Service initialized");
    }

    /**
     * Check if we're in edit mode based on the presence of the voucherId field
     */
    isEditMode() {
        return $('#editingVoucherId').length > 0;
    }

    /**
     * Load categories based on project ID
     */
    async loadCategories($categorySelect, projectId) {
        try {
            const $container = $categorySelect.closest('.select-container');
            const $dropdown = $container.find('.select-dropdown');
            const $searchInput = $container.find('.select-search');

            $searchInput.val('').prop('disabled', true);
            $dropdown.empty().append('<div class="select-option">Loading...</div>');

            const categories = await this.apiGet('/Home/GetCategoriesByProject', { projectId });

            $dropdown.empty();

            const selectedValue = $categorySelect.val();

            $categorySelect.empty().append('<option value="">Select Category</option>');

            if (categories?.length > 0) {
                categories.forEach(category => {
                    $categorySelect.append(new Option(category.name, category.id));
                    $dropdown.append(
                        `<div class="select-option" data-value="${category.id}">${category.name}</div>`
                    );
                });

                if (selectedValue && $categorySelect.find(`option[value="${selectedValue}"]`).length) {
                    $categorySelect.val(selectedValue);
                }
            }

            $searchInput.prop('disabled', false);
            return categories;
        } catch (error) {
            console.error('Error loading categories:', error);
            $categorySelect.empty().append('<option value="">Error loading categories</option>');
            this.showError('Failed to load categories. Please try again.');
            return [];
        }
    }

    /**
     * Load accounts based on category ID
     */
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
                return [];
            }

            $searchInput.val('').prop('disabled', true);
            $dropdown.empty().append('<div class="select-option">Loading...</div>');

            const accounts = await this.apiGet('/Home/GetAccountsByCategory', { categoryId });

            $dropdown.empty();

            const selectedValue = $accountSelect.val();

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

                if (selectedValue && $accountSelect.find(`option[value="${selectedValue}"]`).length) {
                    $accountSelect.val(selectedValue);
                }
            }

            $searchInput.prop('disabled', false);
            return accounts;
        } catch (error) {
            console.error('Error loading accounts:', error);
            $accountSelect.empty().append('<option value="">Error loading accounts</option>');
            this.showError('Failed to load accounts. Please try again.');
            return [];
        }
    }

    /**
     * Load product details for an account
     */
    async loadProductDetails($row, accountId) {
        try {
            const product = await this.apiGet('/Home/GetProductDetails', { accountId });

            if (product) {
                const $rateInput = $row.find('.rate-input');
                const currentRate = $rateInput.val();
                
                if (product.saleRate !== null && product.saleRate !== undefined) {
                    if (!currentRate || !this.isEditMode()) {
                        const formattedRate = parseFloat(product.saleRate).toFixed(4);
                        $rateInput.val(formattedRate);
                    }
                }

                if (product.pkg) {
                    $row[0].setAttribute('data-pkg', product.pkg);
                    $row.find('.pkg-input').val(product.pkg);
                }

                if (product.accountGroupId) {
                    const accountGroup = product.accountGroupId === 1 ? 'account' : 'product';
                    $row[0].setAttribute('data-account-group', accountGroup);
                }
            }
            return product;
        } catch (error) {
            console.error('Error loading product details:', error);
            throw error;
        }
    }

    /**
     * Load category and account data for a row
     */
    async loadCategoryAndAccountData($row, projectId, categoryId, accountId) {
        try {
            // Load categories for this row
            await this.loadCategories($row.find('.category-select'), projectId);

            // Set category value and display text
            $row.find('.category-select').val(categoryId);
            const categoryText = $row.find('.category-select option:selected').text();
            $row.find('.category-search').val(categoryText);

            // Now load accounts based on category
            await this.loadAccounts($row, categoryId);

            // Set account value and display text
            $row.find('.account-select').val(accountId);
            const accountText = $row.find('.account-select option:selected').text();
            $row.find('.account-search').val(accountText);

            // Preserve the original rate value before loading product details
            const originalRate = $row.find('.rate-input').val();

            // Load product details to set account group attributes
            await this.loadProductDetails($row, accountId);
            
            // If we had an original rate value, restore it (it might have been overwritten by loadProductDetails)
            if (originalRate) {
                $row.find('.rate-input').val(parseFloat(originalRate).toFixed(4));
            }

            return true;
        } catch (error) {
            console.error('Error loading data for row:', error);
            return false;
        }
    }

    /**
     * Process data that was already loaded from the model in edit mode
     */
    async processExistingData() {
        const projectId = $('#ProjectId').val();
        if (!projectId) return;

        const rowPromises = [];

        $('#entryRows tr').each((index, row) => {
            const $row = $(row);
            const categoryId = $row.data('category-id');
            const accountId = $row.data('account-id');

            if (categoryId && accountId) {
                console.log(`Processing row ${index} with categoryId=${categoryId}, accountId=${accountId}`);
                
                // Add each row processing to our promises array
                rowPromises.push(this.loadCategoryAndAccountData($row, projectId, categoryId, accountId));
            }
        });

        // Wait for all rows to be processed
        await Promise.all(rowPromises);
        
        return true;
    }

    /**
     * Process a single row from a recipe
     */
    async processRecipeRow($row, projectId, categoryId, accountId, formatDecimalValue) {
        try {
            // Load categories for this row
            await this.loadCategories($row.find('.category-select'), projectId);
            
            // Set category value and display text
            $row.find('.category-select').val(categoryId);
            const categoryText = $row.find('.category-select option:selected').text();
            $row.find('.category-search').val(categoryText);
            
            // Load accounts based on category
            await this.loadAccounts($row, categoryId);
            
            // Set account value and display text
            $row.find('.account-select').val(accountId);
            const accountText = $row.find('.account-select option:selected').text();
            $row.find('.account-search').val(accountText);
            
            // Preserve rate value if it exists
            const originalRate = $row.find('.rate-input').val();
            
            // Load product details to set account group attributes
            await this.loadProductDetails($row, accountId);
            
            // If we had a rate value, restore it with proper formatting
            if (originalRate && formatDecimalValue) {
                $row.find('.rate-input').val(formatDecimalValue(originalRate));
            }
            
            return true;
        } catch (error) {
            console.error('Error processing recipe row:', error);
            return false;
        }
    }

    /**
     * API helper method for GET requests
     */
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

    /**
     * Show error message in validation alert
     */
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
}

// Export the service
window.journalDataService = new JournalDataService();