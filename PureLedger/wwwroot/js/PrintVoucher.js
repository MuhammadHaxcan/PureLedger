document.addEventListener("DOMContentLoaded", function () {
    const type = document.body.getAttribute("data-voucher-type");
    const id = document.body.getAttribute("data-voucher-id");

    if (type && id) {
        document.title = `${type}_${id}`;
    }

    // Trigger print
    setTimeout(function () {
        window.print();
    }, 500);
});
