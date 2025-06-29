document.addEventListener('DOMContentLoaded', function () {
    // Initialize delete buttons
    document.querySelectorAll('.delete-tag-btn').forEach(button => {
        button.addEventListener('click', function () {
            const tagId = this.dataset.id;
            const tagName = this.dataset.name;

            document.getElementById('tagNameToDelete').textContent = tagName;
            const confirmBtn = document.getElementById('confirmDeleteBtn');

            confirmBtn.onclick = async function () {
                try {
                    const response = await fetch(`/MetaData/DeleteTag/${tagId}`, {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                        }
                    });

                    const result = await response.json();

                    if (result.success) {
                        window.location.reload();
                    } else {
                        alert(result.message || 'Failed to delete tag');
                        bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
                    }
                } catch (error) {
                    console.error('Error deleting tag:', error);
                    alert('An error occurred while deleting the tag');
                    bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
                }
            };

            const deleteModal = new bootstrap.Modal(document.getElementById('deleteConfirmModal'));
            deleteModal.show();
        });
    });
});