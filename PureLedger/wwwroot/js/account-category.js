$(document).ready(function () {
    $('#ProjectId').on('change', function () {
        var projectId = $(this).val();
        var $categorySelect = $('#CategoryId');
        $categorySelect.empty();
        $categorySelect.append($('<option>', { value: '', text: 'Select Category' }));

        if (projectId) {
            $.ajax({
                url: '/MetaData/GetCategoriesByProject',
                type: 'GET',
                data: { projectId: projectId },
                success: function (categories) {
                    $.each(categories, function (i, category) {
                        $categorySelect.append($('<option>', {
                            value: category.categoryId,
                            text: category.categoryName
                        }));
                    });
                },
                error: function () {
                    alert('Failed to load categories.');
                }
            });
        }
    });
});