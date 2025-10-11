$(document).ready(function () {
    // Initialize dropdowns
    initializeDropdowns();

    // Form submission handler
    $("#productReportForm").on("submit", function (e) {
        e.preventDefault();
        generateReport();
    });

    // Column visibility toggles
    $(".column-toggles .btn-check").on("change", function () {
        const columnClass = this.id.replace("show", "").toLowerCase() + "-column";
        if ($(this).prop("checked")) {
            $(`.${columnClass}`).show();
        } else {
            $(`.${columnClass}`).hide();
        }
    });

    // Search functionality
    $("#reportSearch").on("keyup", function () {
        const value = $(this).val().toLowerCase();
        $("#reportData tr").filter(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(value) > -1);
        });
    });

    // Clear search
    $("#clearSearch").on("click", function () {
        $("#globalReportSearch").val("");
    });

    // Global search
    $("#globalReportSearch").on("keyup", function () {
        if ($("#reportResults").is(":visible")) {
            const value = $(this).val().toLowerCase();
            $("#reportData tr").filter(function () {
                $(this).toggle($(this).text().toLowerCase().indexOf(value) > -1);
            });
        }
    });
});

function initializeDropdowns() {
    // Project change event
    $("#projectId").on("change", function () {
        const projectId = $(this).val();

        // Clear category and account dropdowns
        $("#categoryId").html('<option value="">All Categories</option>');
        $("#accountId").html('<option value="">All Products</option>');

        if (projectId) {
            // Load categories for selected project
            $.ajax({
                url: routes.getCategoriesByProject,
                data: { projectId: projectId },
                success: function (data) {
                    populateDropdown("#categoryId", data, "All Categories");
                },
                error: function (xhr) {
                    showError("Failed to load categories");
                }
            });
        }
    });

    // Category change event
    $("#categoryId").on("change", function () {
        const categoryId = $(this).val();

        // Clear account dropdown
        $("#accountId").html('<option value="">All Products</option>');

        if (categoryId) {
            // Load accounts for selected category
            $.ajax({
                url: routes.getAccountsByCategory,
                data: { categoryId: categoryId },
                success: function (data) {
                    populateDropdown("#accountId", data, "All Products");
                },
                error: function (xhr) {
                    showError("Failed to load products");
                }
            });
        }
    });
}

function populateDropdown(selector, data, defaultText) {
    const dropdown = $(selector);
    dropdown.empty();
    dropdown.append(`<option value="">${defaultText}</option>`);

    $.each(data, function (i, item) {
        dropdown.append($('<option>', {
            value: item.id,
            text: item.name
        }));
    });
}

function generateReport() {
    // Show loading indicator
    $("#reportResults").show();
    $("#reportData").html('<tr><td colspan="17" class="text-center"><i class="fas fa-spinner fa-spin me-2"></i>Loading report data...</td></tr>');
    $("#productGroupData").html('<tr><td colspan="10" class="text-center"><i class="fas fa-spinner fa-spin me-2"></i>Loading summary data...</td></tr>');

    // Get form values
    const params = {
        startDate: $("#startDate").val(),
        endDate: $("#endDate").val(),
        projectId: $("#projectId").val(),
        categoryId: $("#categoryId").val(),
        accountId: $("#accountId").val(),
        locationId: $("#locationId").val(),
        page: 1,
        pageSize: 100
    };

    // Call API to get report data
    $.ajax({
        url: routes.getProductReportData,
        data: params,
        success: function (data) {
            renderReportData(data);
            renderProductSummary(data);
        },
        error: function (xhr) {
            showError("Failed to generate report");
            $("#reportData").html('<tr><td colspan="17" class="text-center text-danger"><i class="fas fa-exclamation-circle me-2"></i>Error loading report data</td></tr>');
            $("#productGroupData").html('<tr><td colspan="10" class="text-center text-danger"><i class="fas fa-exclamation-circle me-2"></i>Error loading summary data</td></tr>');
        }
    });
}

function renderReportData(data) {
    const tbody = $("#reportData");
    tbody.empty();

    if (data.entries.length === 0) {
        tbody.html('<tr><td colspan="17" class="text-center">No data found for the selected criteria</td></tr>');
        return;
    }

    // Render entries
    $.each(data.entries, function (i, entry) {
        const row = $("<tr>");

        row.append(`<td>${entry.id}</td>`);
        row.append(`<td class="date-column">${entry.entryDate}</td>`);
        row.append(`<td class="description-column">${entry.description}</td>`);
        row.append(`<td class="account-column">${entry.accountName}</td>`);
        row.append(`<td class="qty-column text-end">${formatNumber(entry.quantity)}</td>`);
        row.append(`<td class="qty-balance-column text-end">${formatNumber(entry.qtyBalance)}</td>`);
        row.append(`<td class="rate-column text-end">${formatDecimal(entry.rate)}</td>`);
        row.append(`<td class="debit-qty-column text-end">${formatNumber(entry.debitQty)}</td>`);
        row.append(`<td class="credit-qty-column text-end">${formatNumber(entry.creditQty)}</td>`);
        row.append(`<td class="qty-balance-column text-end">${formatNumberWithSign(entry.quantityBalance)}</td>`);
        row.append(`<td class="avg-rate-column text-end">${formatDecimal(entry.avgRate)}</td>`);
        row.append(`<td class="debit-column text-end">${formatDecimal(entry.debit)}</td>`);
        row.append(`<td class="credit-column text-end">${formatDecimal(entry.credit)}</td>`);
        row.append(`<td class="balance-column text-end ${entry.balance < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(entry.balance)}</td>`);
        row.append(`<td class="cogs-column text-end">${formatDecimal(entry.cogs)}</td>`);
        row.append(`<td class="margin-column text-end ${entry.margin < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(entry.margin)}</td>`);
        row.append(`<td class="location-column">${entry.locationName || '-'}</td>`);

        tbody.append(row);
    });

    // Update totals
    $("#totalQty").text(formatNumber(data.totalQuantity));
    $("#totalDebitQty").text(formatNumber(data.totalDebitQty));
    $("#totalCreditQty").text(formatNumber(data.totalCreditQty));
    $("#netQty").text(formatNumberWithSign(data.netQuantity));
    $("#totalDebit").text(formatDecimal(data.totalDebit));
    $("#totalCredit").text(formatDecimal(data.totalCredit));
    $("#netBalance").text(formatDecimalWithSign(data.netBalance));

    // Calculate COGS and Margin totals from entries
    const totalCOGS = data.entries.reduce((sum, entry) => sum + entry.cogs, 0);
    const totalMargin = data.entries.reduce((sum, entry) => sum + entry.margin, 0);
    $("#totalCOGS").text(formatDecimal(totalCOGS));
    $("#totalMargin").text(formatDecimalWithSign(totalMargin));
}

function renderProductSummary(data) {
    const tbody = $("#productGroupData");
    tbody.empty();

    // Group data by product (accountName)
    const productGroups = {};

    data.entries.forEach(entry => {
        if (!productGroups[entry.accountName]) {
            productGroups[entry.accountName] = {
                totalQty: 0,
                debitQty: 0,
                creditQty: 0,
                balanceQty: 0,
                debit: 0,
                credit: 0,
                balance: 0,
                cogs: 0,
                margin: 0
            };
        }

        const group = productGroups[entry.accountName];
        group.totalQty += entry.quantity;
        group.debitQty += entry.debitQty;
        group.creditQty += entry.creditQty;
        group.balanceQty = group.debitQty - group.creditQty;
        group.debit += entry.debit;
        group.credit += entry.credit;
        group.balance = group.debit - group.credit;
        group.cogs += entry.cogs;
        group.margin += entry.margin;
    });

    // Render product summary
    Object.keys(productGroups).forEach(productName => {
        const group = productGroups[productName];
        const row = $("<tr>");

        row.append(`<td>${productName}</td>`);
        row.append(`<td class="text-end">${formatNumber(group.totalQty)}</td>`);
        row.append(`<td class="text-end">${formatNumber(group.debitQty)}</td>`);
        row.append(`<td class="text-end">${formatNumber(group.creditQty)}</td>`);
        row.append(`<td class="text-end ${group.balanceQty < 0 ? 'negative-balance' : ''}">${formatNumberWithSign(group.balanceQty)}</td>`);
        row.append(`<td class="text-end">${formatDecimal(group.debit)}</td>`);
        row.append(`<td class="text-end">${formatDecimal(group.credit)}</td>`);
        row.append(`<td class="text-end ${group.balance < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(group.balance)}</td>`);
        row.append(`<td class="text-end">${formatDecimal(group.cogs)}</td>`);
        row.append(`<td class="text-end ${group.margin < 0 ? 'negative-balance' : ''}">${formatDecimalWithSign(group.margin)}</td>`);

        tbody.append(row);
    });
}

function showError(message) {
    $("#alertArea").html(`
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <i class="fas fa-exclamation-circle me-2"></i> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `);
}

function formatNumber(value) {
    return value?.toLocaleString() || '0';
}

function formatNumberWithSign(value) {
    return value >= 0 ? formatNumber(value) : `(${formatNumber(Math.abs(value))})`;
}

function formatDecimal(value) {
    return value?.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) || '0.00';
}

function formatDecimalWithSign(value) {
    return value >= 0 ? formatDecimal(value) : `(${formatDecimal(Math.abs(value))})`;
}