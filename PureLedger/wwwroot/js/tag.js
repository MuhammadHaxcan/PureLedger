document.addEventListener('DOMContentLoaded', function () {
    // Initialize DataTable
    initializeDataTable();

    // Initialize delete buttons
    initializeDeleteButtons();

    // Handle form submission with validation
    initializeFormSubmission();
});

function initializeDataTable() {
    // Check if table exists before initializing
    if (document.getElementById('tagsTable')) {
        const table = $('#tagsTable').DataTable({
            "order": [[1, "asc"]],
            "language": {
                "emptyTable": "No tags available",
                "search": "Search tags:",
                "info": "Showing _START_ to _END_ of _TOTAL_ tags",
                "lengthMenu": "Show _MENU_ tags"
            },
            "pageLength": 10,
            "lengthMenu": [[5, 10, 25, 50, -1], [5, 10, 25, 50, "All"]],
            "columnDefs": [
                { "orderable": false, "targets": 2 }
            ]
        });

        // Store table reference in window for other functions to access
        window.tagsDataTable = table;
    }
}

function initializeDeleteButtons() {
    // Handle click on both button types
    // This handles both .delete-tag (from the main view) and .delete-tag-btn (potentially used elsewhere)
    $(document).on('click', '.delete-tag, .delete-tag-btn', function () {
        const tagId = $(this).data('id');
        const tagName = $(this).data('name');
        
        // Determine which modal is available - support both modal structures
        const modalId = document.getElementById('deleteModal') ? 'deleteModal' : 'deleteConfirmModal';
        const nameElementId = document.getElementById('deleteTagName') ? 'deleteTagName' : 'tagNameToDelete';
        
        // Update the modal with tag information
        document.getElementById(nameElementId).textContent = tagName;
        
        // Set up the confirm button
        const confirmBtn = modalId === 'deleteModal' ? 
            document.getElementById('confirmDelete') : 
            document.getElementById('confirmDeleteBtn');
            
        // Store the tagId on the button
        $(confirmBtn).data('id', tagId);
        
        // Set up the confirmation action
        confirmBtn.onclick = function () {
            // Show loading state
            confirmBtn.disabled = true;
            confirmBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Deleting...';
            
            // Get the anti-forgery token if available
            const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenElement ? tokenElement.value : null;
            
            // Determine the proper delete URL/action based on token availability
            if (token) {
                // Use PureLedger utility for AJAX with token
                PureLedger.ajax(`/MetaData/DeleteTag/${tagId}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    }
                })
                .done(processDeleteResponse)
                .fail(handleDeleteError)
                .always(resetButtonState);
            } else {
                // Use jQuery AJAX with traditional method
                $.ajax({
                    url: '/MetaData/DeleteTag',
                    type: 'POST',
                    data: { id: tagId },
                    success: processDeleteResponse,
                    error: handleDeleteError,
                    complete: resetButtonState
                });
            }
        };
        
        // Show the modal
        const modal = bootstrap.Modal.getOrCreateInstance(document.getElementById(modalId));
        modal.show();
    });
}

function processDeleteResponse(response) {
    // Check if the response has a success property
    if (response.success) {
        // Determine which modal is open
        const modalId = document.getElementById('deleteModal') ? 'deleteModal' : 'deleteConfirmModal';
        const modal = bootstrap.Modal.getInstance(document.getElementById(modalId));
        modal.hide();
        
        // Remove the row if DataTable is available
        const tagId = $('#' + modalId === 'deleteModal' ? 'confirmDelete' : 'confirmDeleteBtn').data('id');
        
        if (window.tagsDataTable && document.getElementById('tag-' + tagId)) {
            window.tagsDataTable.row($('#tag-' + tagId)).remove().draw();
            showAlert('success', 'Tag deleted successfully!');
        } else {
            // If no DataTable or we can't find the row, reload the page
            window.location.reload();
        }
    } else {
        // Show error message
        showAlert('danger', response.message || 'Failed to delete tag');
        
        // Close the modal
        const modalId = document.getElementById('deleteModal') ? 'deleteModal' : 'deleteConfirmModal';
        bootstrap.Modal.getInstance(document.getElementById(modalId)).hide();
    }
}

function handleDeleteError(xhr, status, error) {
    console.error('Error deleting tag:', error);
    
    // Show error message
    showAlert('danger', 'An error occurred while trying to delete the tag.');
    
    // Close the modal
    const modalId = document.getElementById('deleteModal') ? 'deleteModal' : 'deleteConfirmModal';
    bootstrap.Modal.getInstance(document.getElementById(modalId)).hide();
}

function resetButtonState() {
    // Reset state of the button that triggered the action
    const modalId = document.getElementById('deleteModal') ? 'deleteModal' : 'deleteConfirmModal';
    const confirmBtn = modalId === 'deleteModal' ? 
        document.getElementById('confirmDelete') : 
        document.getElementById('confirmDeleteBtn');
        
    confirmBtn.disabled = false;
    confirmBtn.innerHTML = modalId === 'deleteModal' ? 
        '<i class="fas fa-trash me-1"></i> Delete' : 'Delete';
}

function initializeFormSubmission() {
    // Handle form submission with validation
    $('#createTagForm').on('submit', function(e) {
        const tagName = $('#TagName').val().trim();
        if (!tagName) {
            e.preventDefault();
            showAlert('warning', 'Please enter a tag name.');
            $('#TagName').focus();
            return false;
        }
        
        // Form is valid - let it submit normally
    });
}

// Reusable alert function
function showAlert(type, message) {
    const icon = type === 'success' ? 'check-circle' :
        type === 'warning' ? 'exclamation-triangle' :
            'exclamation-circle';

    const alert = $(`
        <div class="alert alert-${type} alert-dismissible fade show" role="alert">
            <i class="fas fa-${icon} me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `);

    $('#alertArea').append(alert);

    // Auto dismiss after 3 seconds
    setTimeout(function () {
        alert.alert('close');
    }, 3000);
}