$(document).ready(function () {
    function loadCategories(projectId) {
        if (!projectId) {
            $('#ForExpenseCategoryId').empty();
            $('#CartageTransporterCategoryId').empty();
            $('#ForExpenseAccountId').empty();
            return;
        }
        $.getJSON('/Admin/GetCategoriesByProject', { projectId: projectId }, function (categories) {
            var forCat = $('#ForExpenseCategoryId');
            var cartageCat = $('#CartageTransporterCategoryId');
            forCat.empty();
            cartageCat.empty();
            forCat.append($('<option>').val('').text('-- Select Category --'));
            cartageCat.append($('<option>').val('').text('-- Select Category --'));
            $.each(categories, function (i, cat) {
                forCat.append($('<option>').val(cat.categoryId).text(cat.categoryName));
                cartageCat.append($('<option>').val(cat.categoryId).text(cat.categoryName));
            });
            $('#ForExpenseAccountId').empty();
            $('#ForExpenseAccountId').append($('<option>').val('').text('-- Select Account --'));
        });
    }

    function loadAccounts(categoryId) {
        if (!categoryId) {
            $('#ForExpenseAccountId').empty();
            $('#ForExpenseAccountId').append($('<option>').val('').text('-- Select Account --'));
            return;
        }
        $.getJSON('/Admin/GetAccountsByCategory', { categoryId: categoryId }, function (accounts) {
            var accSel = $('#ForExpenseAccountId');
            accSel.empty();
            accSel.append($('<option>').val('').text('-- Select Account --'));
            $.each(accounts, function (i, acc) {
                accSel.append($('<option>').val(acc.accountId).text(acc.accountName));
            });
        });
    }

    $('#ProjectId').change(function () {
        var projectId = $(this).val();
        loadCategories(projectId);
    });

    $('#ForExpenseCategoryId').change(function () {
        var categoryId = $(this).val();
        loadAccounts(categoryId);
    });

    // On page load, if a project is already selected, load categories
    var initialProjectId = $('#ProjectId').val();
    if (initialProjectId) {
        loadCategories(initialProjectId);
    }
});