// Helper to get all rows
function getRows() {
    return document.querySelectorAll("#entryRows tr");
}

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
        validateDebitCredit();
    });

    // Attach calculation events
    attachCalculationEvents(template);

    document.getElementById("entryRows").appendChild(template);
});

// Remove row event for initial row
document.querySelectorAll(".remove-row-btn").forEach(function (btn) {
    btn.addEventListener("click", function () {
        if (getRows().length > 1) {
            this.closest("tr").remove();
            validateDebitCredit();
        }
    });
});

// Attach calculation and validation events to all rows
function attachCalculationEvents(row) {
    // Discount calculation on % change
    row.querySelector(".percent-input").addEventListener("input", function () {
        const percent = parseFloat(this.value) || 0;
        const qty = parseFloat(row.querySelector(".qty-input").value) || 0;
        const rate = parseFloat(row.querySelector(".rate-input").value) || 0;
        const discInput = row.querySelector(".disc-input");
        const disc = ((qty * rate) * percent) / 100;
        discInput.value = disc.toFixed(2);
        calculateDebitCredit(row);
    });

    // When qty, rate, or disc changes, recalculate debit/credit
    ["input", "blur"].forEach(evt => {
        row.querySelector(".qty-input").addEventListener(evt, function () { calculateDebitCredit(row); });
        row.querySelector(".rate-input").addEventListener(evt, function () { calculateDebitCredit(row); });
        row.querySelector(".disc-input").addEventListener(evt, function () { calculateDebitCredit(row); });
    });

    // When debit/credit is focused or changed, recalculate
    row.querySelector(".debit-input").addEventListener("focus", function () { calculateDebitCredit(row, "debit"); });
    row.querySelector(".credit-input").addEventListener("focus", function () { calculateDebitCredit(row, "credit"); });
    row.querySelector(".debit-input").addEventListener("input", function () { calculateDebitCredit(row, "debit"); });
    row.querySelector(".credit-input").addEventListener("input", function () { calculateDebitCredit(row, "credit"); });
}

// Attach to initial row
attachCalculationEvents(document.querySelector("#entryRows tr"));

// Calculate debit/credit for a row
function calculateDebitCredit(row, focusField) {
    const qty = parseFloat(row.querySelector(".qty-input").value) || 0;
    const rate = parseFloat(row.querySelector(".rate-input").value) || 0;
    const percent = parseFloat(row.querySelector(".percent-input").value) || 0;
    const disc = parseFloat(row.querySelector(".disc-input").value) || 0;

    let amount = qty * rate;
    let discount = disc;
    if (!disc && percent) {
        discount = (amount * percent) / 100;
    }
    amount = amount - discount;

    if (focusField === "debit") {
        row.querySelector(".debit-input").value = amount > 0 ? amount.toFixed(2) : "";
    } else if (focusField === "credit") {
        row.querySelector(".credit-input").value = amount > 0 ? amount.toFixed(2) : "";
    }
}

// Validate debit/credit totals and show alert if not matching
function validateDebitCredit() {
    let debit = 0, credit = 0;
    getRows().forEach(row => {
        debit += parseFloat(row.querySelector(".debit-input").value) || 0;
        credit += parseFloat(row.querySelector(".credit-input").value) || 0;
    });
    const alert = document.getElementById("validationAlert");
    const msg = document.getElementById("validationMessage");
    if (debit !== credit) {
        msg.textContent = `Total Debit (${debit.toFixed(2)}) and Credit (${credit.toFixed(2)}) do not match.`;
        alert.classList.remove("d-none");
    } else {
        alert.classList.add("d-none");
    }
}

// Validate empty rows on submit
document.getElementById("entryForm").addEventListener("submit", function (e) {
    let valid = true;
    getRows().forEach(row => {
        // Check if all required fields are filled
        const requiredFields = ["select[name*='Category']", "select[name*='Account']", "input[name*='Type']", "select[name*='Rbp']", "input[name*='Tags']", "input[name*='Qty']", "input[name*='Rate']"];
        requiredFields.forEach(sel => {
            const el = row.querySelector(sel);
            if (el && !el.value) {
                valid = false;
            }
        });
    });
    if (!valid) {
        e.preventDefault();
        const alert = document.getElementById("validationAlert");
        const msg = document.getElementById("validationMessage");
        msg.textContent = "Please fill all required fields in each entry row.";
        alert.classList.remove("d-none");
        return false;
    }
    validateDebitCredit();
    if (!document.getElementById("validationAlert").classList.contains("d-none")) {
        e.preventDefault();
        return false;
    }
});