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

// Row management
class RowManager {
    static getNextIndex() {
        return utils.getRows().length;
    }

    static createNewRow() {
        // Clone the first row
        const templateRow = utils.getRows()[0];
        const newRow = templateRow.cloneNode(true);
        const newIndex = this.getNextIndex();

        // Update row ID
        newRow.id = `entryRow-${newIndex}`;

        // First destroy Select2 on template row to prevent data copying
        const templateTagsSelect = templateRow.querySelector('.tags-input');
        if ($(templateTagsSelect).data('select2')) {
            $(templateTagsSelect).select2('destroy');
        }

        // Reset elements and update indices
        this.resetRowElements(newRow, newIndex);

        // Clean up Select2 remnants
        newRow.querySelectorAll('.select2, .select2-container').forEach(el => el.remove());

        // Reset the tags select specifically
        const tagsSelect = newRow.querySelector('.tags-input');
        $(tagsSelect)
            .empty()
            .removeAttr('data-select2-id')
            .removeAttr('aria-hidden')
            .removeData()
            .removeClass('select2-hidden-accessible')
            .off();

        // Clean any select2 data attributes from the row and all its children
        newRow.querySelectorAll('*').forEach(el => {
            const attrs = Array.from(el.attributes);
            attrs.forEach(attr => {
                if (attr.name.startsWith('data-select2') || attr.name.startsWith('aria-')) {
                    el.removeAttribute(attr.name);
                }
            });
        });

        // Return the row first, then initialize Select2
        return newRow;
    }

    static resetRowElements(row, index) {
        row.querySelectorAll("input, select").forEach(el => {
            if (el.tagName === "SELECT") {
                if (el.classList.contains("tags-input")) {
                    // Complete reset for tags select
                    $(el)
                        .empty()
                        .removeData()
                        .removeAttr('data-select2-id')
                        .removeAttr('aria-hidden')
                        .removeClass('select2-hidden-accessible')
                        .off();
                } else {
                    // Reset other selects
                    el.selectedIndex = 0;
                }
            } else {
                // Reset inputs
                el.value = "";
            }

            // Update IDs and names
            if (el.id) el.id = el.id.replace(/_\d+_/, `_${index}_`);
            if (el.name) el.name = el.name.replace(/\[\d+\]/, `[${index}]`);
        });

        // Remove any data attributes we set
        row.removeAttribute('data-pkg');
        row.removeAttribute('data-total-amount');
        row.removeAttribute('data-account-type');

        // Remove all select2 containers
        row.querySelectorAll('.select2-container').forEach(el => el.remove());
    }

    static attachRowEvents(row) {
        // Remove existing event listeners
        row.querySelectorAll("input, select, button").forEach(el => {
            const newEl = el.cloneNode(true);
            el.parentNode.replaceChild(newEl, el);
        });

        // Attach new events
        this.attachCalculationEvents(row);
        this.attachRemoveEvent(row);
    }

    static initializeTagsSelect2(index) {
        const selector = `#Entries_${index}__Tags`;
        const $element = $(selector);
        
        // Destroy if exists
        if ($element.hasClass("select2-hidden-accessible")) {
            $element.select2('destroy');
        }
        
        // Remove all select2 related elements and data
        $element.removeData()
            .find('option').remove();
        
        // Initialize fresh Select2
        $element.select2({
            placeholder: 'Search tags',
            tags: false,
            minimumInputLength: 1,
            width: '100%',
            ajax: {
                url: '/MetaData/SearchTags',
                dataType: 'json',
                delay: 250,
                data: params => ({ term: params.term }),
                processResults: data => ({ results: data })
            }
        }).val(null).trigger('change');
    }

    static attachCalculationEvents(row) {
        const inputs = {
            percent: row.querySelector(".percent-input"),
            qty: row.querySelector(".qty-input"),
            rate: row.querySelector(".rate-input"),
            disc: row.querySelector(".disc-input"),
            debit: row.querySelector(".debit-input"),
            credit: row.querySelector(".credit-input"),
            pkg: row.querySelector(".pkg-input") // Add this line
        };

        // Attach events
        inputs.percent.addEventListener("input", () => Calculator.updateRowCalculations(row));
        ["input", "blur"].forEach(evt => {
            inputs.qty.addEventListener(evt, () => Calculator.updateRowCalculations(row));
            inputs.rate.addEventListener(evt, () => Calculator.updateRowCalculations(row));
            inputs.disc.addEventListener(evt, () => Calculator.updateTotalsOnly(row));
            
            // Add event for pkg input
            if (inputs.pkg) {
                inputs.pkg.addEventListener(evt, () => {
                    // Update the data attribute
                    const pkgValue = utils.parseIntOrZero(inputs.pkg.value);
                    row.setAttribute("data-pkg", pkgValue);
                    Calculator.updateRowCalculations(row);
                });
            }
        });

        // Focus events for debit/credit
        inputs.debit.addEventListener("focus", () => {
            if (!inputs.debit.value) Calculator.updateRowCalculations(row, "debit");
        });
        inputs.credit.addEventListener("focus", () => {
            if (!inputs.credit.value) Calculator.updateRowCalculations(row, "credit");
        });

        // Update totals on any debit/credit change
        inputs.debit.addEventListener("input", Calculator.updateTotals);
        inputs.credit.addEventListener("input", Calculator.updateTotals);
    }

    static attachRemoveEvent(row) {
        row.querySelector(".remove-row-btn").addEventListener("click", function () {
            if (utils.getRows().length > 1) {
                this.closest("tr").remove();
                Calculator.updateTotals();
            }
        });
    }
}

// Calculator class
class Calculator {
    static updateRowCalculations(row, focusField = null) {
        this.calculateAmountWithPkg(row);
        this.updateDiscount(row);
        this.calculateDebitCredit(row, focusField);
        this.updateTotals();
    }

    static updateTotalsOnly() {
        this.updateTotals();
    }

    // New method to calculate amount with package
    static calculateAmountWithPkg(row) {
        const rbpValue = row.querySelector("select[name*='Rbp']").value;
        const inputs = {
            qty: utils.parseFloatOrZero(row.querySelector(".qty-input").value),
            rate: utils.parseFloatOrZero(row.querySelector(".rate-input").value)
        };

        // Get package value from the pkg-input or data-pkg attribute
        const pkgInput = row.querySelector(".pkg-input");
        let pkg = 0;
        
        if (pkgInput && pkgInput.value) {
            pkg = utils.parseIntOrZero(pkgInput.value);
            // Also update the data attribute for consistency
            row.setAttribute("data-pkg", pkg);
        } else if (row.hasAttribute("data-pkg")) {
            pkg = utils.parseIntOrZero(row.getAttribute("data-pkg"));
            // Update the input field if it exists
            if (pkgInput) pkgInput.value = pkg;
        }

        // Calculate total amount based on RBP, qty, rate, and pkg
        let totalAmount = rbpValue === "Y" && pkg > 0
            ? pkg * inputs.qty * inputs.rate
            : inputs.qty * inputs.rate;
        
        // Store the amount for discount calculation
        row.setAttribute("data-total-amount", totalAmount);
    }

    static updateDiscount(row) {
        const percent = utils.parseFloatOrZero(row.querySelector(".percent-input").value);
        const totalAmount = utils.parseFloatOrZero(row.getAttribute("data-total-amount") || "0");

        // Calculate discount based on total amount (with package considered)
        const disc = (totalAmount * percent) / 100;
        row.querySelector(".disc-input").value = Math.round(disc);
    }

    static calculateDebitCredit(row, focusField) {
        const totalAmount = utils.parseFloatOrZero(row.getAttribute("data-total-amount") || "0");
        const disc = utils.parseFloatOrZero(row.querySelector(".disc-input").value);

        // Calculate net amount after discount
        let amount = Math.round(totalAmount - disc);

        if (amount > 0) {
            if (focusField === "debit") row.querySelector(".debit-input").value = amount;
            if (focusField === "credit") row.querySelector(".credit-input").value = amount;
        }
    }

    static updateTotals() {
        let totals = { debit: 0, credit: 0 };
        utils.getRows().forEach(row => {
            totals.debit += utils.parseIntOrZero(row.querySelector(".debit-input").value);
            totals.credit += utils.parseIntOrZero(row.querySelector(".credit-input").value);
        });

        document.getElementById("totalDebit").textContent = totals.debit;
        document.getElementById("totalCredit").textContent = totals.credit;
    }
}

// Document ready handlers
document.addEventListener("DOMContentLoaded", () => {
    // Initialize first row
    RowManager.initializeTagsSelect2(0);
    RowManager.attachRowEvents(utils.getRows()[0]);
    Calculator.updateTotals();

    // Set default due date
    const dueDateInput = document.querySelector("input[name='DueDate']");
    if (dueDateInput && !dueDateInput.value) {
        dueDateInput.value = utils.formatDateDDMMYYYY(new Date());
    }

    // Add credit days change handler
    document.getElementById("CreditDays").addEventListener("input", updateDueDate);
    document.getElementById("CreditDays").addEventListener("change", updateDueDate);

    // Function to update due date based on credit days
    function updateDueDate() {
        const creditDays = utils.parseIntOrZero(this.value);
        const entryDate = document.getElementById("EntryDate").value;
        
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
                        document.getElementById("DueDate").value = utils.formatDateDDMMYYYY(date);
                    }
                }
            } catch (e) {
                console.error('Error calculating due date:', e);
            }
        }
    }

    // Also update due date when entry date changes
    $('.datepicker').on('change', function() {
        if (this.id === "EntryDate") {
            const creditDaysInput = document.getElementById("CreditDays");
            if (creditDaysInput) {
                creditDaysInput.dispatchEvent(new Event('input'));
            }
        }
        
        // Existing date format validation code...
        try {
            const dateArr = this.value.split('/');
            if (dateArr.length === 3) {
                const day = parseInt(dateArr[0], 10);
                const month = parseInt(dateArr[1], 10) - 1; // JavaScript months are 0-based
                const year = parseInt(dateArr[2], 10);
                const date = new Date(year, month, day);
                if (!isNaN(date.getTime())) {
                    this.value = utils.formatDateDDMMYYYY(date);
                }
            }
        } catch (e) {
            console.error('Date format error', e);
        }
    });

    // Modified Add row button handler
    document.getElementById("addRowBtn").addEventListener("click", () => {
        const newRow = RowManager.createNewRow();
        
        // First append the row
        document.getElementById("entryRows").appendChild(newRow);
        
        // Then initialize Select2 after the row is in the DOM
        const index = utils.getRows().length - 1;
        
        // Wait for next tick to ensure DOM is updated
        requestAnimationFrame(() => {
            const tagsSelect = newRow.querySelector('.tags-input');
            $(tagsSelect).select2({
                placeholder: 'Search tags',
                tags: false,
                minimumInputLength: 1,
                width: '100%',
                ajax: {
                    url: '/MetaData/SearchTags',
                    dataType: 'json',
                    delay: 250,
                    data: params => ({ term: params.term }),
                    processResults: data => ({ results: data })
                }
            }).val(null).trigger('change');
        });
        
        // Attach other events
        RowManager.attachRowEvents(newRow);
    });

    // Initialize datepickers
    $('.datepicker').datepicker({
        dateFormat: 'dd/mm/yy',
        changeMonth: true,
        changeYear: true,
        showButtonPanel: true,
        maxDate: new Date(), // Restrict future dates if needed
        beforeShow: function(input, inst) {
            // Position the datepicker correctly
            setTimeout(function () {
                inst.dpDiv.css({
                    top: $(input).offset().top + $(input).outerHeight(),
                    left: $(input).offset().left
                });
            }, 0);
        }
    });

    // Make the calendar icon clickable to open the datepicker
    $('.calendar-icon').on('click', function() {
        $(this).siblings('input.datepicker').focus();
    });

    // Ensure utils.formatDateDDMMYYYY is available for consistent date formatting
    if (utils && utils.formatDateDDMMYYYY) {
        $('.datepicker').on('change', function() {
            // Optional: Additional validation or reformatting if needed
            // This is useful if you want to ensure consistent date format
            try {
                const dateArr = this.value.split('/');
                if (dateArr.length === 3) {
                    const day = parseInt(dateArr[0], 10);
                    const month = parseInt(dateArr[1], 10) - 1; // JavaScript months are 0-based
                    const year = parseInt(dateArr[2], 10);
                    const date = new Date(year, month, day);
                    if (!isNaN(date.getTime())) {
                        this.value = utils.formatDateDDMMYYYY(date);
                    }
                }
            } catch (e) {
                console.error('Date format error', e);
            }
        });
    }
});

// Project and Category handling
$(document).ready(function () {
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
                $.get(`/api/common/categories/${projectId}`, function (categories) {
                    categories.forEach(c => {
                        $categorySelect.append(new Option(c.name, c.id));
                    });
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
        $row.find('.type-input').val('');
        $row[0].removeAttribute('data-account-type');

        if (categoryId) {
            $.get(`/api/common/accounts/${categoryId}`, function (accounts) {
                accounts.forEach(a => {
                    const $option = $(new Option(a.name, a.id));
                    $option.data('type', a.type || '');
                    $option.data('account-type', a.accountTypeName || '');
                    $accountSelect.append($option);
                });
            });
        }
    });

    // Account change handler
    $(document).on('change', '.account-select', function () {
        const $row = $(this).closest('tr');
        const accountId = $(this).val();
        const $selected = $(this).find('option:selected');

        $row.find('.type-input').val($selected.data('type') || '');
        $row[0].setAttribute('data-account-type', $selected.data('account-type') || '');

        if (accountId) {
            $.get('/Home/GetProductDetails', { accountId }, function (product) {
                if (product?.saleRate) {
                    $row.find('.rate-input').val(product.saleRate);
                }
                if (product?.pkg) {
                    // Update both the data attribute and the new pkg input field
                    $row[0].setAttribute('data-pkg', product.pkg);
                    $row.find('.pkg-input').val(product.pkg);
                    
                    // Recalculate values based on the new package
                    const row = $row[0];
                    Calculator.updateRowCalculations(row);
                } else {
                    $row[0].removeAttribute('data-pkg');
                    $row.find('.pkg-input').val('');
                }
            });
        }
    });

    // Initial project load
    if ($('#ProjectId').val()) {
        $('#ProjectId').trigger('change');
    }
});