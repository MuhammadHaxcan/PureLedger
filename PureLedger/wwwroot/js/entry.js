function getRows() {
    return document.querySelectorAll("#entryRows tr");
}

// Format date as dd/MM/yyyy
function formatDateDDMMYYYY(date) {
    const d = date.getDate().toString().padStart(2, "0");
    const m = (date.getMonth() + 1).toString().padStart(2, "0");
    const y = date.getFullYear();
    return `${d}/${m}/${y}`;
}

// Set default due date to today on page load in dd/MM/yyyy format
document.addEventListener("DOMContentLoaded", function () {
    const dueDateInput = document.querySelector("input[name='DueDate']");
    if (dueDateInput) {
        const today = new Date();
        dueDateInput.type = "text";
        dueDateInput.value = formatDateDDMMYYYY(today);
    }
    updateTotals();
});

// Add new row
document.getElementById("addRowBtn").addEventListener("click", function () {
    const rows = getRows();
    const idx = rows.length;
    const template = rows[0].cloneNode(true);

    // Clear all input/select values in the cloned row and update names
    template.querySelectorAll("input,select").forEach(function (el) {
        if (el.tagName === "SELECT") {
            el.selectedIndex = 0;
        } else {
            el.value = "";
        }
        // Update name attribute for model binding
        if (el.name) {
            el.name = el.name.replace(/\[\d+\]/, `[${idx}]`);
        }
    });

    // Attach remove event
    template.querySelector(".remove-row-btn").addEventListener("click", function () {
        this.closest("tr").remove();
        updateTotals();
    });

    // Attach calculation events
    attachCalculationEvents(template);

    document.getElementById("entryRows").appendChild(template);
    updateTotals();
});

// Remove row event for initial row
document.querySelectorAll(".remove-row-btn").forEach(function (btn) {
    btn.addEventListener("click", function () {
        if (getRows().length > 1) {
            this.closest("tr").remove();
            updateTotals();
        }
    });
});

// Attach calculation and validation events to all rows
function attachCalculationEvents(row) {
    // Discount calculation on % change
    row.querySelector(".percent-input").addEventListener("input", function () {
        updateDiscount(row);
        calculateDebitCredit(row);
    });

    // When qty, rate, or disc changes, recalculate discount and debit/credit
    ["input", "blur"].forEach(evt => {
        row.querySelector(".qty-input").addEventListener(evt, function () {
            updateDiscount(row);
            calculateDebitCredit(row);
        });
        row.querySelector(".rate-input").addEventListener(evt, function () {
            updateDiscount(row);
            calculateDebitCredit(row);
        });
        row.querySelector(".disc-input").addEventListener(evt, function () {
            calculateDebitCredit(row);
        });
    });

    // When debit/credit is focused, fill only if empty
    row.querySelector(".debit-input").addEventListener("focus", function () {
        if (!this.value) calculateDebitCredit(row, "debit");
    });
    row.querySelector(".credit-input").addEventListener("focus", function () {
        if (!this.value) calculateDebitCredit(row, "credit");
    });

    // When debit/credit is changed by user, update totals
    row.querySelector(".debit-input").addEventListener("input", function () { updateTotals(); });
    row.querySelector(".credit-input").addEventListener("input", function () { updateTotals(); });
}

// Attach to initial row
attachCalculationEvents(document.querySelector("#entryRows tr"));

// Update discount field based on qty, rate, percent
function updateDiscount(row) {
    const percent = parseFloat(row.querySelector(".percent-input").value) || 0;
    const qty = parseFloat(row.querySelector(".qty-input").value) || 0;
    const rate = parseFloat(row.querySelector(".rate-input").value) || 0;
    const discInput = row.querySelector(".disc-input");
    const disc = ((qty * rate) * percent) / 100;
    discInput.value = Math.round(disc);
}

// Calculate debit/credit for a row
function calculateDebitCredit(row, focusField) {
    const rbpValue = row.querySelector("select[name*='Rbp']").value;
    const qty = parseFloat(row.querySelector(".qty-input").value) || 0;
    const rate = parseFloat(row.querySelector(".rate-input").value) || 0;
    const percent = parseFloat(row.querySelector(".percent-input").value) || 0;
    const disc = parseFloat(row.querySelector(".disc-input").value) || 0;

    let amount;

    // Check if RBP is "Y" and we have the pkg value stored
    if (rbpValue === "Y" && row.hasAttribute("data-pkg")) {
        const pkg = parseInt(row.getAttribute("data-pkg")) || 0;
        amount = pkg * qty * rate;
    } else {
        amount = qty * rate;
    }

    // Apply discount
    let discount = disc;
    if (!disc && percent) {
        discount = (amount * percent) / 100;
    }
    amount = amount - discount;
    amount = Math.round(amount); // Ensure integer

    if (focusField === "debit") {
        const debitInput = row.querySelector(".debit-input");
        if (!debitInput.value) {
            debitInput.value = amount > 0 ? amount : "";
        }
    } else if (focusField === "credit") {
        const creditInput = row.querySelector(".credit-input");
        if (!creditInput.value) {
            creditInput.value = amount > 0 ? amount : "";
        }
    }
    updateTotals();
}

// Update total debit and credit in the table footer
function updateTotals() {
    let debit = 0, credit = 0;
    getRows().forEach(row => {
        debit += parseInt(row.querySelector(".debit-input").value) || 0;
        credit += parseInt(row.querySelector(".credit-input").value) || 0;
    });
    document.getElementById("totalDebit").textContent = debit;
    document.getElementById("totalCredit").textContent = credit;
}

// --- Credit Days to Due Date logic ---
const creditDaysInput = document.querySelector("input[name='CreditDays']");
const dueDateInput = document.querySelector("input[name='DueDate']");

if (creditDaysInput && dueDateInput) {
    creditDaysInput.addEventListener("input", function () {
        const days = parseInt(this.value, 10) || 0;
        const today = new Date();
        today.setDate(today.getDate() + days);
        dueDateInput.type = "text";
        dueDateInput.value = formatDateDDMMYYYY(today);
    });
}


// Modified validation function to skip certain fields
function validateRow(row) {

    return true;
}

// Validate empty rows and debit/credit match on submit
document.getElementById("entryForm").addEventListener("submit", function (e) {
    let valid = true;
    let debit = 0, credit = 0;

    getRows().forEach(row => {
        if (!validateRow(row)) {
            valid = false;
        }

        debit += parseInt(row.querySelector(".debit-input").value) || 0;
        credit += parseInt(row.querySelector(".credit-input").value) || 0;
    });

    const alert = document.getElementById("validationAlert");
    const msg = document.getElementById("validationMessage");

    if (!valid) {
        e.preventDefault();
        msg.textContent = "Please fill all required fields in each entry row.";
        alert.classList.remove("d-none");
        return false;
    }
    if (debit !== credit) {
        e.preventDefault();
        msg.textContent = `Total Debit (${debit}) and Credit (${credit}) do not match.`;
        alert.classList.remove("d-none");
        return false;
    }
    alert.classList.add("d-none");
});

$(document).ready(function () {
    // When project changes, reload all category selects in all rows
    $('#ProjectId').on('change', function () {
        var projectId = $(this).val();
        $('#entryRows tr').each(function () {
            var $row = $(this);
            var $categorySelect = $row.find('.category-select');
            var $accountSelect = $row.find('.account-select');
            $categorySelect.empty().append('<option value="">Select Category</option>');
            $accountSelect.empty().append('<option value="">Select Account</option>');
            if (projectId) {
                $.get('/Home/GetCategoriesByProject', { projectId: projectId }, function (categories) {
                    $.each(categories, function (i, c) {
                        $categorySelect.append($('<option>', { value: c.categoryId, text: c.categoryName }));
                    });
                });
            }
        });
});

// When category changes, load accounts for that row
$(document).on('change', '.category-select', function () {
    var $row = $(this).closest('tr');
    var categoryId = $(this).val();
    var $accountSelect = $row.find('.account-select');
    $accountSelect.empty().append('<option value="">Select Account</option>');
    if (categoryId) {
        $.get('/Home/GetAccountsByCategory', { categoryId: categoryId }, function (accounts) {
            $.each(accounts, function (i, a) {
                $accountSelect.append($('<option>', {
                    value: a.accountId,
                    text: a.accountName,
                    'data-type': a.type || '',
                    'data-account-type': a.accountTypeName || ''
                }));
            });
        });
    }
    // Clear the type field
    $row.find('.type-input').val('');
    // Remove any account type data attribute
    $row[0].removeAttribute('data-account-type');
});

// When account changes, set the Type field and get product data
$(document).on('change', '.account-select', function () {
    var $row = $(this).closest('tr');
    var accountId = $(this).val();
    var selected = $(this).find('option:selected');
    var type = selected.data('type') || '';
    var accountType = selected.data('account-type') || '';

    $row.find('.type-input').val(type);

    // Store account type as data attribute on the row for validation
    $row[0].setAttribute('data-account-type', accountType);

    // Get product details for the selected account
    if (accountId) {
        $.get('/Home/GetProductDetails', { accountId: accountId }, function (product) {
            if (product && product.saleRate) {
                $row.find('.rate-input').val(product.saleRate);
            }

            // Store pkg value as data attribute on the row for later calculations
            if (product && product.pkg) {
                $row[0].setAttribute('data-pkg', product.pkg);
            } else {
                $row[0].removeAttribute('data-pkg');
            }
        });
    }
});



// Remove row logic
$(document).on('click', '.remove-row-btn', function () {
    if ($('#entryRows tr').length > 1) {
        $(this).closest('tr').remove();
        updateTotals();
    }
});

// On page load, if a project is selected, trigger change to load categories
if ($('#ProjectId').val()) {
    $('#ProjectId').trigger('change');
}
});