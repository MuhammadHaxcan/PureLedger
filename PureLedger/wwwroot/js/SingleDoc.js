document.getElementById("btnGo").addEventListener("click", async function () {
    const id = document.getElementById("voucherId").value.trim();
    const type = document.getElementById("voucherType").value;

    if (!id || !Number.isInteger(Number(id)) || Number(id) < 1) {
        alert("Please enter a valid Journal Voucher ID.");
        return;
    }

    if (!type) {
        alert("Please select a Voucher Type.");
        return;
    }

    // ✅ First, validate using AJAX
    const validateUrl = `/Reporting/ValidateVoucher?journalVoucherId=${id}&type=${type}`;

    try {
        const response = await fetch(validateUrl);
        const result = await response.json();

        if (result.exists) {
            // ✅ If valid, open print page in new tab
            const printUrl = `/Reporting/PrintVoucher?journalVoucherId=${id}&type=${type}`;
            window.open(printUrl, '_blank');
        } else {
            // ❌ Show error message on the same page
            const errorMessage = type === "DC"
                ? "No Delivery Challan exists for this Voucher ID."
                : "No Journal Voucher found for this ID.";

            showError(errorMessage);
        }
    } catch (error) {
        showError("An error occurred while validating the voucher.");
        console.error(error);
    }
});

function showError(message) {
    let alertBox = document.getElementById("errorBox");

    if (!alertBox) {
        alertBox = document.createElement("div");
        alertBox.className = "alert alert-danger mt-3";
        alertBox.id = "errorBox";
        document.querySelector(".container").prepend(alertBox);
    }

    alertBox.innerText = message;
}
