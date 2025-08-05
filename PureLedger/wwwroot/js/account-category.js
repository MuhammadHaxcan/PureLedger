$(document).ready(function () {
    $('#ProjectId').on('change', function () {
        var projectId = $(this).val();
        var $categorySelect = $('#CategoryId');
        $categorySelect.empty();
        $categorySelect.append($('<option>', { value: '', text: 'Select Category' }));

        if (projectId) {
            // Use PureLedger utility for AJAX
            PureLedger.get('/MetaData/GetCategoriesByProject', { projectId: projectId })
                .done(function (categories) {
                    $.each(categories, function (i, category) {
                        $categorySelect.append($('<option>', {
                            value: category.categoryId,
                            text: category.categoryName
                        }));
                    });
                })
                .fail(function (xhr, status, error) {
                    console.error('Error loading categories:', error);
                    alert('Failed to load categories. Please try again.');
                });
        }
    });
});