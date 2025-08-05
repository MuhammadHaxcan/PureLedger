document.addEventListener('DOMContentLoaded', function () {
    // Get the location form
    const locationForm = document.getElementById('locationForm');

    if (locationForm) {
        locationForm.addEventListener('submit', function (e) {
            e.preventDefault();

            // Get form data
            const formData = new FormData(locationForm);

            // Show loading state
            const submitButton = locationForm.querySelector('button[type="submit"]');
            const originalButtonText = submitButton.innerHTML;
            submitButton.disabled = true;
            submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Saving...';

            // Use PureLedger utility for AJAX
            PureLedger.ajax('/MetaData/Location', {
                method: 'POST',
                data: formData,
                processData: false,
                contentType: false,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            })
                .done(function(result) {
                    if (result.success) {
                        // Add the new location to the list
                        addLocationToList(result.location);

                        // Reset form
                        locationForm.reset();

                        // Show success message
                        showSuccessMessage(result.message);
                    } else {
                        // Display validation errors
                        displayValidationErrors(result.errors);
                    }
                })
                .fail(function(xhr, status, error) {
                    console.error('Error:', error);
                    showErrorMessage('An error occurred while saving the location.');
                })
                .always(function() {
                    // Restore button state
                    submitButton.disabled = false;
                    submitButton.innerHTML = originalButtonText;
                });
        });
    }

    // Function to add location to the list
    function addLocationToList(location) {
        const locationList = document.getElementById('locationList');

        if (!locationList) return;

        const row = document.createElement('tr');
        row.innerHTML = `
            <td>${location.name}</td>
            <td>${location.phone || ''}</td>
            <td>${location.region || ''}</td>
            <td>
                <a href="/MetaData/EditLocation/${location.locationId}" class="btn btn-sm btn-primary">
                    <i class="bi bi-pencil"></i> Edit
                </a>
            </td>
        `;

        // Add to the beginning of the list
        if (locationList.firstChild) {
            locationList.insertBefore(row, locationList.firstChild);
        } else {
            locationList.appendChild(row);
        }
    }

    // Function to show success message
    function showSuccessMessage(message) {
        // Create success alert
        const alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-success alert-dismissible fade show';
        alertDiv.role = 'alert';
        alertDiv.innerHTML = `
            <i class="fas fa-check-circle me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;

        // Find location for alert (before the form card)
        const formCard = document.querySelector('.card');
        const container = formCard.parentElement.parentElement;
        container.insertBefore(alertDiv, container.firstChild);

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alertDiv);
            bsAlert.close();
        }, 5000);
    }

    // Function to show error message
    function showErrorMessage(message) {
        // Create error alert
        const alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-danger alert-dismissible fade show';
        alertDiv.role = 'alert';
        alertDiv.innerHTML = `
            <i class="fas fa-exclamation-circle me-2"></i>${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;

        // Find location for alert (before the form card)
        const formCard = document.querySelector('.card');
        const container = formCard.parentElement.parentElement;
        container.insertBefore(alertDiv, container.firstChild);

        // Auto-dismiss after 5 seconds
        setTimeout(() => {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alertDiv);
            bsAlert.close();
        }, 5000);
    }

    // Function to display validation errors
    function displayValidationErrors(errors) {
        // Clear existing validation errors
        const errorSpans = document.querySelectorAll('.field-validation-error');
        errorSpans.forEach(span => span.textContent = '');

        // Display new validation errors
        for (const field in errors) {
            // For empty field names (model-level errors)
            if (field === '' || field === 'undefined') {
                // Create an alert for model-level errors
                const alertDiv = document.createElement('div');
                alertDiv.className = 'alert alert-danger alert-dismissible fade show';
                alertDiv.role = 'alert';

                let errorMessages = '';
                errors[field].forEach(error => {
                    errorMessages += `<div>${error}</div>`;
                });

                alertDiv.innerHTML = `
                    <i class="fas fa-exclamation-circle me-2"></i>${errorMessages}
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                `;

                const formCard = document.querySelector('.card-body');
                formCard.insertBefore(alertDiv, formCard.firstChild);
                continue;
            }

            // For field-level errors
            const errorSpan = document.querySelector(`[data-valmsg-for="${field}"]`);
            if (errorSpan && errors[field].length > 0) {
                errorSpan.textContent = errors[field][0];
            }
        }
    }
});