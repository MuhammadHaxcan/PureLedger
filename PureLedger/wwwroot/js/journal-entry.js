// Utility functions
const utils = {
    formatDateDDMMYYYY: (date) => {
        const d = date.getDate().toString().padStart(2, "0");
        const m = (date.getMonth() + 1).toString().padStart(2, "0");
        return `${d}/${m}/${date.getFullYear()}`;
    },
    getRows: () => document.querySelectorAll("#entryRows tr"),
    parseFloatOrZero: (value) => parseFloat(value) || 0,
    parseIntOrZero: (value) => parseInt(value) || 0
};

// Document ready handlers
$(document).ready(function () {
    // Add form action correction (after the utility functions declaration)
    // This ensures the form posts to the correct URL with the route prefix
    const formElement = document.getElementById('entryForm');
    if (formElement) {
        const basePath = PureLedger.getCurrentBasePath();
        formElement.setAttribute('action', basePath + '/Home/Entry');
        console.log("Form action set to:", formElement.action);
    }

    // Row counter for adding new rows
    let rowCounter = 0;

    // Initialize datepicker
    $('.datepicker').datepicker({
        dateFormat: 'dd/mm/yy',
        changeMonth: true,
        changeYear: true,
        showButtonPanel: true,
        maxDate: new Date()
    });

    // Make calendar icon clickable
    $('.calendar-icon').on('click', function () {
        $(this).siblings('input.datepicker').focus();
    });

    // Credit days change handler
    $('#CreditDays').on('input change', function () {
        updateDueDate();
    });

    // Entry date change handler
    $('#EntryDate').on('change', function () {
        updateDueDate();
    });

    function updateDueDate() {
        const creditDays = utils.parseIntOrZero($('#CreditDays').val());
        const entryDate = $('#EntryDate').val();

        if (entryDate) {
            try {
                // Parse date in DD/MM/YYYY format
                const parts = entryDate.split('/');
                if (parts.length === 3) {
                    const day = parseInt(parts[0], 10);
                    const month = parseInt(parts[1], 10) - 1; // JS months are 0-based
                    const year = parseInt(parts[2], 10);

                    const date = new Date(year, month, day);
                    if (!isNaN(date.getTime())) {
                        // Add credit days
                        date.setDate(date.getDate() + creditDays);

                        // Update due date field
                        $('#DueDate').val(utils.formatDateDDMMYYYY(date));
                    }
                }
            } catch (e) {
                console.error('Error calculating due date:', e);
            }
        }
    }

    // Project change handler
    $('#ProjectId').on('change', function () {
        const projectId = $(this).val();
        $('#entryRows tr').each(function () {
            const $row = $(this);
            const $categorySelect = $row.find('.category-select');
            const $accountSelect = $row.find('.account-select');

            $categorySelect.empty().append('<option value="">Select Category</option>');
            $accountSelect.empty().append('<option value="">Select Account</option>');

            if (projectId) {
                // Show loading indicator
                $categorySelect.append('<option value="" disabled selected>Loading...</option>');

                // Use PureLedger utility for AJAX
                PureLedger.get('/Home/GetCategoriesByProject', { projectId: projectId })
                    .done(function (categories) {
                        $categorySelect.empty().append('<option value="">Select Category</option>');

                        if (categories && categories.length > 0) {
                            categories.forEach(c => {
                                $categorySelect.append(new Option(c.name, c.id));
                            });
                        }
                    })
                    .fail(function (xhr, status, error) {
                        console.error('Error loading categories:', error);
                        $categorySelect.empty().append('<option value="">Select Category</option>');
                    });
            }
        });
    });

    // Category change handler
    $(document).on('change', '.category-select', function () {
        const $row = $(this).closest('tr');
        const categoryId = $(this).val();
        const $accountSelect = $row.find('.account-select');

        $accountSelect.empty().append('<option value="">Select Account</option>');
        $row[0].removeAttribute('data-account-type');

        if (categoryId) {
            // Show loading indicator
            $accountSelect.append('<option value="" disabled selected>Loading...</option>');

            // Use PureLedger utility for AJAX
            PureLedger.get('/Home/GetAccountsByCategory', { categoryId: categoryId })
                .done(function (accounts) {
                    $accountSelect.empty().append('<option value="">Select Account</option>');

                    if (accounts && accounts.length > 0) {
                        accounts.forEach(a => {
                            const $option = $(new Option(a.name, a.id));
                            $option.data('account-type', a.accountTypeName || '');
                            $accountSelect.append($option);
                        });
                    }
                })
                .fail(function (xhr, status, error) {
                    console.error('Error loading accounts:', error);
                    $accountSelect.empty().append('<option value="">Select Account</option>');
                });
        }
    });

    // Account change handler
    $(document).on('change', '.account-select', function () {
        const $row = $(this).closest('tr');
        const accountId = $(this).val();
        const $selected = $(this).find('option:selected');

        $row[0].setAttribute('data-account-type', $selected.data('account-type') || '');

        if (accountId) {
            // Use PureLedger utility for AJAX
            PureLedger.get('/Home/GetProductDetails', { accountId: accountId })
                .done(function (product) {
                    if (product?.saleRate) {
                        $row.find('.rate-input').val(product.saleRate);
                    }
                    if (product?.pkg) {
                        $row[0].setAttribute('data-pkg', product.pkg);
                        $row.find('.pkg-input').val(product.pkg);
                        updateRowCalculations($row[0]);
                    } else {
                        $row[0].removeAttribute('data-pkg');
                        $row.find('.pkg-input').val('');
                    }
                })
                .fail(function (xhr, status, error) {
                    console.error('Error loading product details:', error);
                });
        }
    });

    // Tags functionality
    $(document).on('focus', '.tag-search-input', function () {
        const $input = $(this);
        const $suggestions = $input.closest('.tag-multibox').siblings('.tag-suggestions');
        $suggestions.addClass('show');
    });

    $(document).on('blur', '.tag-search-input', function () {
        const $input = $(this);
        const $suggestions = $input.closest('.tag-multibox').siblings('.tag-suggestions');

        // Delay hiding to allow click on suggestion
        setTimeout(() => {
            $suggestions.removeClass('show');
        }, 200);
    });

    $(document).on('input', '.tag-search-input', function () {
        const $input = $(this);
        const term = $input.val().trim();
        const $suggestions = $input.closest('.tag-multibox').siblings('.tag-suggestions');

        if (term.length > 0) {
            // Use PureLedger utility for AJAX
            PureLedger.get('/MetaData/SearchTags', { term: term })
                .done(function (data) {
                    $suggestions.empty();

                    if (data.length > 0) {
                        data.forEach(tag => {
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
                })
                .fail(function (xhr, status, error) {
                    console.error('Error searching tags:', error);
                    $suggestions.removeClass('show');
                });
        } else {
            $suggestions.removeClass('show');
        }
    });

    // Updated tag suggestion click handler
    $(document).on('click', '.tag-suggestions .dropdown-item', function (e) {
        e.preventDefault();

        const $item = $(this);
        const tagId = $item.data('id');
        const tagName = $item.data('name');
        const $row = $item.closest('tr');
        const $tagBox = $item.closest('td').find('.tag-multibox');
        const $input = $tagBox.find('.tag-search-input');

        // Create tag chip
        const $chip = $(`
            <div class="tag-chip me-1 mb-1" data-tag-id="${tagId}">
                <span>${tagName}</span>
                <a href="#" class="tag-chip-remove ms-1" data-id="${tagId}" data-name="${tagName}">
                    <i class="fas fa-times"></i>
                </a>
            </div>
        `);

        // Insert before the input
        $chip.insertBefore($input);

        // Clear the input
        $input.val('');

        // Update hidden inputs for tags
        updateTagInputs($row);

        // Hide suggestions
        $item.closest('.tag-suggestions').removeClass('show');
    });

    // Updated remove tag handler
    $(document).on('click', '.tag-chip-remove', function (e) {
        e.preventDefault();

        const $remove = $(this);
        const $chip = $remove.closest('.tag-chip');
        const $row = $chip.closest('tr');

        // Remove the chip
        $chip.remove();

        // Update hidden inputs
        updateTagInputs($row);
    });

    // New function to update tag inputs
    function updateTagInputs($row) {
        const rowIndex = $row.attr('id').split('-')[1];
        const $tagBox = $row.find('.tag-multibox');
        const $container = $row.find('.tag-hidden-inputs');

        // Clear existing tag inputs
        $container.empty();

        // Create new hidden inputs for each tag
        $tagBox.find('.tag-chip').each(function () {
            const tagId = $(this).data('tag-id') || $(this).find('.tag-chip-remove').data('id');

            if (tagId) {
                const $input = $('<input>')
                    .attr('type', 'hidden')
                    .attr('name', `Entries[${rowIndex}].Tags`)
                    .attr('value', tagId);

                $container.append($input);
            }
        });

        // Log for debugging
        console.log(`Updated tags for row ${rowIndex}:`,
            $container.find('input').map(function () { return $(this).val(); }).get());
    }

    // Calculation functionality
    function updateRowCalculations(row, focusField = null) {
        calculateAmountWithPkg(row);
        updateDiscount(row);
        calculateDebitCredit(row, focusField);
        updateTotals();
    }

    function calculateAmountWithPkg(row) {
        const rbpValue = $(row).find(".rbp-select").val();
        const qty = utils.parseFloatOrZero($(row).find(".qty-input").val());
        const rate = utils.parseFloatOrZero($(row).find(".rate-input").val());

        // Get package value
        let pkg = 0;
        const pkgInput = $(row).find(".pkg-input");

        if (pkgInput.val()) {
            pkg = utils.parseIntOrZero(pkgInput.val());
            row.setAttribute("data-pkg", pkg);
        } else if (row.hasAttribute("data-pkg")) {
            pkg = utils.parseIntOrZero(row.getAttribute("data-pkg"));
            pkgInput.val(pkg);
        }

        // Calculate total amount
        let totalAmount = rbpValue === "Y" && pkg > 0
            ? pkg * qty * rate
            : qty * rate;

        // Store the amount for discount calculation
        row.setAttribute("data-total-amount", totalAmount);
    }

    function updateDiscount(row) {
        const percent = utils.parseFloatOrZero($(row).find(".percent-input").val());
        const totalAmount = utils.parseFloatOrZero(row.getAttribute("data-total-amount") || "0");

        // Calculate discount
        const disc = (totalAmount * percent) / 100;
        $(row).find(".disc-input").val(Math.round(disc));
    }

    function calculateDebitCredit(row, focusField) {
        const totalAmount = utils.parseFloatOrZero(row.getAttribute("data-total-amount") || "0");
        const disc = utils.parseFloatOrZero($(row).find(".disc-input").val());

        // Calculate net amount
        let amount = Math.round(totalAmount - disc);

        if (amount > 0) {
            if (focusField === "debit") $(row).find(".debit-input").val(amount);
            if (focusField === "credit") $(row).find(".credit-input").val(amount);
        }
    }

    function updateTotals() {
        let totalDebit = 0;
        let totalCredit = 0;

        $('#entryRows tr').each(function () {
            totalDebit += utils.parseIntOrZero($(this).find(".debit-input").val());
            totalCredit += utils.parseIntOrZero($(this).find(".credit-input").val());
        });

        $("#totalDebit").text(totalDebit);
        $("#totalCredit").text(totalCredit);
    }

    // Attach events to calculation inputs
    $(document).on('input', '.percent-input', function () {
        updateRowCalculations($(this).closest('tr')[0]);
    });

    $(document).on('input blur', '.qty-input, .rate-input', function () {
        updateRowCalculations($(this).closest('tr')[0]);
    });

    $(document).on('input blur', '.pkg-input', function () {
        const row = $(this).closest('tr')[0];
        const pkgValue = utils.parseIntOrZero($(this).val());
        row.setAttribute("data-pkg", pkgValue);
        updateRowCalculations(row);
    });

    $(document).on('input blur', '.disc-input', function () {
        updateTotals();
    });

    $(document).on('focus', '.debit-input', function () {
        if (!$(this).val()) {
            updateRowCalculations($(this).closest('tr')[0], "debit");
        }
    });

    $(document).on('focus', '.credit-input', function () {
        if (!$(this).val()) {
            updateRowCalculations($(this).closest('tr')[0], "credit");
        }
    });

    $(document).on('input', '.debit-input, .credit-input', function () {
        updateTotals();
    });

    // Add row functionality
    $('#addRowBtn').on('click', function () {
        rowCounter++;
        const newIndex = rowCounter;

        // Clone the first row
        const $newRow = $('#entryRow-0').clone();

        // Update ID
        $newRow.attr('id', `entryRow-${newIndex}`);

        // Reset elements and update indices
        resetRowElements($newRow, newIndex);

        // Append the new row
        $('#entryRows').append($newRow);

        // Update totals
        updateTotals();
    });

    // Updated resetRowElements function
    function resetRowElements($row, index) {
        // Reset inputs
        $row.find('input').each(function () {
            const $input = $(this);

            // Update ID and name
            if ($input.attr('id')) {
                $input.attr('id', $input.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($input.attr('name')) {
                $input.attr('name', $input.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            // Clear value
            $input.val('');
        });

        // Reset selects
        $row.find('select').each(function () {
            const $select = $(this);

            // Update ID and name
            if ($select.attr('id')) {
                $select.attr('id', $select.attr('id').replace(/_\d+_/, `_${index}_`));
            }
            if ($select.attr('name')) {
                $select.attr('name', $select.attr('name').replace(/\[\d+\]/, `[${index}]`));
            }

            // Reset selection
            $select.prop('selectedIndex', 0);
        });

        // Reset tag input
        const $tagSearch = $row.find('.tag-search-input');
        $tagSearch.attr('data-index', index);
        $tagSearch.val('');

        // Clear tag chips
        $row.find('.tag-chip').remove();

        // Clear tag hidden inputs container
        $row.find('.tag-hidden-inputs').empty();

        // Remove data attributes
        $row[0].removeAttribute('data-pkg');
        $row[0].removeAttribute('data-total-amount');
        $row[0].removeAttribute('data-account-type');

        // Ensure project change propagation
        if ($('#ProjectId').val()) {
            const projectId = $('#ProjectId').val();
            const $categorySelect = $row.find('.category-select');

            $categorySelect.empty().append('<option value="">Select Category</option>');

            if (projectId) {
                // Use PureLedger utility for AJAX
                PureLedger.get('/Home/GetCategoriesByProject', { projectId: projectId })
                    .done(function (categories) {
                        if (categories && categories.length > 0) {
                            categories.forEach(c => {
                                $categorySelect.append(new Option(c.name, c.id));
                            });
                        }
                    })
                    .fail(function (xhr, status, error) {
                        console.error('Error loading categories for new row:', error);
                    });
            }
        }
    }

    // Remove row functionality
    $(document).on('click', '.remove-row-btn', function () {
        const $rows = $('#entryRows tr');

        if ($rows.length > 1) {
            $(this).closest('tr').remove();
            updateTotals();
        }
    });

    // Form submission debugging for tags
    $('#entryForm').on('submit', function (e) {
        // Log all tag inputs to verify they're properly included in submission
        console.log('Tags being submitted:', $('input[name$="].Tags"]').map(function () {
            return { name: $(this).attr('name'), value: $(this).val() };
        }).get());
    });

    // Initialize first row
    if ($('#ProjectId').val()) {
        $('#ProjectId').trigger('change');
    }

    // Initial due date
    updateDueDate();
});