$(document).ready(function () {
    // Handle showing/hiding account type specific fields
    initializeAccountGroupVisibility();

    // Set up project change handler for loading categories
    initializeProjectCategoryRelationship();
});

function initializeAccountGroupVisibility() {
    // Handle showing/hiding account type specific fields based on account group selection
    $('#accountGroupSelect').change(function () {
        var selectedText = $(this).find('option:selected').text().toLowerCase().trim();

        if (selectedText === 'account') {
            $('#accountDataFields').show();
            $('#productDataFields').hide();
        } else if (selectedText === 'product') {
            $('#productDataFields').show();
            $('#accountDataFields').hide();
        } else {
            $('#accountDataFields').hide();
            $('#productDataFields').hide();
        }
    });

    // Trigger change event on page load to set initial visibility
    $('#accountGroupSelect').trigger('change');
}

function initializeProjectCategoryRelationship() {
    // Project selection: fetch categories
    $('#ProjectId').change(function () {
        var projectId = $(this).val();
        var $categorySelect = $('#CategoryId');

        // Reset the category dropdown
        $categorySelect.empty();
        $categorySelect.append($('<option>', { value: '', text: '-- Select Category --' }));

        if (!projectId) {
            return; // Exit if no project selected
        }

        // Show loading indicator
        $categorySelect.append($('<option>', { value: '', text: 'Loading...', disabled: true, selected: true }));

        // Use PureLedger utility for AJAX
        PureLedger.get('/MetaData/GetCategoriesByProject', { projectId: projectId })
            .done(function (categories) {
                // Reset dropdown first
                $categorySelect.empty();
                $categorySelect.append($('<option>', { value: '', text: '-- Select Category --' }));

                // Add categories
                if (categories && categories.length > 0) {
                    $.each(categories, function (i, category) {
                        $categorySelect.append($('<option>', {
                            value: category.categoryId || category.id, // Handle both property naming conventions
                            text: category.categoryName || category.name // Handle both property naming conventions
                        }));
                    });
                } else {
                    console.warn('No categories received for project:', projectId);
                }
            })
            .fail(function (xhr, status, error) {
                console.error('Error loading categories:', error);
                $categorySelect.empty();
                $categorySelect.append($('<option>', { value: '', text: '-- Select Category --' }));
                $categorySelect.append($('<option>', { value: '', text: 'Error loading categories', disabled: true }));
            });
    });
}