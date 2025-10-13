$(function () {
    // Handle project selection change
    $('#projectSelect').on('change', function() {
        const projectId = $(this).val();
        if (projectId) {
            window.location.href = `/Home/Recipes?projectId=${projectId}`;
        } else {
            window.location.href = '/Home/Recipes';
        }
    });

    // Initialize modal
    const recipeItemsModal = new bootstrap.Modal(document.getElementById('recipeItemsModal'));

    // Handle view recipe button click
    $('.view-recipe-btn').on('click', function() {
        const recipeId = $(this).data('recipe-id');
        const recipeName = $(this).data('recipe-name');
        
        // Update modal title
        $('#recipeNameDisplay').text(recipeName);
        
        // Clear previous items
        $('#recipeItemsTableBody').html('<tr><td colspan="3" class="text-center">Loading items...</td></tr>');
        
        // Show modal
        recipeItemsModal.show();
        
        // Fetch recipe items
        fetchRecipeItems(recipeId);
    });

    function fetchRecipeItems(recipeId) {
        $.ajax({
            url: `/Home/GetRecipeItems?recipeId=${recipeId}`,
            type: 'GET',
            dataType: 'json',
            success: function(response) {
                if (response.error) {
                    showError(response.error);
                    return;
                }
                
                // Update recipe details
                $('#recipeNameDisplay').text(response.recipeName);
                $('#projectNameDisplay').text(`Project: ${response.projectName}`);
                
                // Update items table
                updateRecipeItemsTable(response.items);
            },
            error: function(xhr, status, error) {
                showError('Failed to load recipe items. Please try again.');
                console.error('Error fetching recipe items:', error);
            }
        });
    }

    function updateRecipeItemsTable(items) {
        const $tableBody = $('#recipeItemsTableBody');
        
        // Clear previous content
        $tableBody.empty();
        
        if (items && items.length > 0) {
            // Add rows for each item
            items.forEach(item => {
                $tableBody.append(`
                    <tr>
                        <td>${item.recipeItemId}</td>
                        <td>${item.categoryName}</td>
                        <td>${item.accountName}</td>
                    </tr>
                `);
            });
        } else {
            // Show no items message
            $tableBody.append('<tr><td colspan="3" class="text-center">No items found for this recipe.</td></tr>');
        }
    }

    function showError(message) {
        const $tableBody = $('#recipeItemsTableBody');
        $tableBody.html(`<tr><td colspan="3" class="text-center text-danger">${message}</td></tr>`);
    }
});