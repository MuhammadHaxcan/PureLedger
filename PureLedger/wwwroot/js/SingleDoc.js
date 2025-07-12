document.getElementById("btnGo").addEventListener("click", function () {
    const id = document.getElementById("voucherId").value;
    const type = document.getElementById("voucherType").value;

    if (!id || !Number.isInteger(Number(id)) || Number(id) < 1) {
        alert("Please enter a valid Journal Voucher ID.");
        return;
    }

    if (!type) {
        alert("Please select a Voucher Type.");
        return;
    }

    // Open report in a new tab
    const url = `/Reporting/PrintVoucher?journalVoucherId=${id}&type=${type}`;
    window.open(url, '_blank');
});