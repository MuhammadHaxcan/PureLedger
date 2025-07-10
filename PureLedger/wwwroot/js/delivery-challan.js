document.addEventListener('DOMContentLoaded', function () {
    // Elements
    const sinceDate = document.getElementById('sinceDate');
    const toDate = document.getElementById('toDate');
    const projectSelect = document.getElementById('projectSelect');
    const categorySelect = document.getElementById('categorySelect');
    const accountSelect = document.getElementById('accountSelect');
    const goButton = document.getElementById('goButton');
    const searchInput = document.getElementById('searchInput');
    const resultsTable = document.getElementById('resultsTable');

    // Set default dates if empty
    if (!sinceDate.value) {
        const thirtyDaysAgo = new Date();
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        sinceDate.value = thirtyDaysAgo.toISOString().split('T')[0];
    }

    if (!toDate.value) {
        const today = new Date();
        toDate.value = today.toISOString().split('T')[0];
    }

    // Event handlers for dropdowns
    projectSelect.addEventListener('change', function () {
        // Reset category and account dropdowns
        categorySelect.innerHTML = '<option value="">Select Category</option>';
        accountSelect.innerHTML = '<option value="">Select Account</option>';
        categorySelect.disabled = true;
        accountSelect.disabled = true;

        if (this.value) {
            // Fetch categories for selected project
            fetchCategories(this.value);
        }
    });

    categorySelect.addEventListener('change', function () {
        // Reset account dropdown
        accountSelect.innerHTML = '<option value="">Select Account</option>';
        accountSelect.disabled = true;

        if (this.value) {
            // Fetch accounts for selected category
            fetchAccounts(this.value);
        }
    });

    // Event handler for search
    searchInput.addEventListener('keyup', function () {
        filterResults(this.value);
    });

    // Event handler for Go button
    goButton.addEventListener('click', function () {
        loadFilteredEntries();
    });

    // Transfer button handler
    document.getElementById('transferBtn').addEventListener('click', function () {
        // This is a placeholder for future functionality
        alert('Transfer functionality will be implemented in the future.');
    });

    // Functions
    function fetchCategories(projectId) {
        fetch(`/DeliveryChallan/GetCategoriesByProject?projectId=${projectId}`)
            .then(response => response.json())
            .then(data => {
                categorySelect.disabled = false;

                data.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.categoryId;
                    option.textContent = category.name || category.categoryName;
                    categorySelect.appendChild(option);
                });
            })
            .catch(error => console.error('Error loading categories:', error));
    }

    function fetchAccounts(categoryId) {
        fetch(`/DeliveryChallan/GetAccountsByCategory?categoryId=${categoryId}`)
            .then(response => response.json())
            .then(data => {
                accountSelect.disabled = false;

                data.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.accountId;
                    option.textContent = account.name || account.accountName;
                    accountSelect.appendChild(option);
                });
            })
            .catch(error => console.error('Error loading accounts:', error));
    }

    function loadFilteredEntries() {
        const params = new URLSearchParams({
            SinceDate: sinceDate.value,
            ToDate: toDate.value
        });

        if (projectSelect.value) {
            params.append('ProjectId', projectSelect.value);
        }

        if (categorySelect.value) {
            params.append('CategoryId', categorySelect.value);
        }

        if (accountSelect.value) {
            params.append('AccountId', accountSelect.value);
        }

        fetch(`/DeliveryChallan/GetFilteredEntries?${params.toString()}`)
            .then(response => response.json())
            .then(data => {
                renderResults(data);
            })
            .catch(error => console.error('Error loading entries:', error));
    }

    function renderResults(data) {
        resultsTable.innerHTML = '';

        if (!data.vouchers || data.vouchers.length === 0) {
            const row = document.createElement('tr');
            row.innerHTML = `<td colspan="6" class="text-center">No entries found matching the criteria</td>`;
            resultsTable.appendChild(row);
            return;
        }

        // Render each entry
        data.vouchers.forEach(voucher => {
            voucher.entries.forEach(entry => {
                const row = document.createElement('tr');
                row.dataset.voucherId = voucher.jvId;
                row.dataset.entryId = entry.entryId;

                row.innerHTML = `
                    <td>
                        JV${voucher.jvId}<br>
                        E-Id ${entry.entryId}
                    </td>
                    <td>${formatDate(voucher.entryDate)}</td>
                    <td>${entry.description || ''}</td>
                    <td>${entry.accountName || ''}</td>
                    <td>${entry.quantity}</td>
                    <td>
                        <button class="btn btn-sm btn-primary create-dc-btn" 
                                onclick="openDeliveryChallanModal(${voucher.jvId}, ${entry.entryId})">
                            <i class="bi bi-pencil"></i> Edit
                        </button>
                    </td>
                `;

                resultsTable.appendChild(row);
            });
        });
    }

    function filterResults(searchTerm) {
        const rows = resultsTable.querySelectorAll('tr');
        searchTerm = searchTerm.toLowerCase();

        rows.forEach(row => {
            const text = row.textContent.toLowerCase();
            if (text.includes(searchTerm)) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    }

    function formatDate(dateString) {
        if (!dateString) return '';

        // Handle different date formats
        let date;
        if (typeof dateString === 'string' && dateString.includes('-')) {
            // Split date parts and reorder if needed
            const parts = dateString.split('-');
            if (parts[0].length === 4) {
                // yyyy-MM-dd format
                date = new Date(`${parts[0]}-${parts[1]}-${parts[2]}`);
            } else {
                // dd-MM-yyyy format
                date = new Date(`${parts[2]}-${parts[1]}-${parts[0]}`);
            }
        } else {
            date = new Date(dateString);
        }

        if (isNaN(date.getTime())) return dateString;

        return `${date.getDate().toString().padStart(2, '0')}-${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getFullYear()}`;
    }

    // Load locations for delivery challan modal
    function loadLocations() {
        fetch('/DeliveryChallan/GetLocations')
            .then(response => response.json())
            .then(locations => {
                const locationSelect = document.getElementById('dcLocation');
                locationSelect.innerHTML = '<option value="">Select Location</option>';

                locations.forEach(location => {
                    const option = document.createElement('option');
                    option.value = location.locationId;
                    option.textContent = location.name;
                    locationSelect.appendChild(option);
                });
            })
            .catch(error => console.error('Error loading locations:', error));
    }

    // Initialize the page
    loadLocations();

    // Expose functions to global scope
    window.openDeliveryChallanModal = function (voucherId, entryId) {
        // Set up the modal for creating a delivery challan
        document.getElementById('dcJournalVoucherId').value = voucherId;

        if (entryId) {
            // Single entry mode - We keep this logic but remove the UI elements
            document.getElementById('dcJournalEntryId').value = entryId;
        } else {
            // All entries mode
            document.getElementById('dcJournalEntryId').value = '';
        }

        // Always show all entries view now
        document.getElementById('allEntriesView').classList.remove('d-none');

        // Fetch all entries for voucher
        fetch(`/DeliveryChallan/GetVoucherEntries?voucherId=${voucherId}`)
            .then(response => response.json())
            .then(entries => {
                const entriesTable = document.getElementById('dcEntriesTable');
                entriesTable.innerHTML = '';

                entries.forEach(entry => {
                    if (entry.quantity > 0 && !entry.isProcessed) {
                        const row = document.createElement('tr');
                        row.innerHTML = `
                            <td>${entry.accountName}</td>
                            <td>${entry.description || ''}</td>
                            <td>${entry.quantity}</td>
                            <td>
                                <div class="form-check">
                                    <input type="checkbox" class="form-check-input entry-checkbox" 
                                           value="${entry.entryId}" name="selectedEntries">
                                </div>
                            </td>
                        `;
                        entriesTable.appendChild(row);
                    }
                });
            })
            .catch(error => console.error('Error loading voucher entries:', error));

        // Check if challan already exists for this voucher
        fetch(`/DeliveryChallan/CheckExistingChallan?voucherId=${voucherId}`)
            .then(response => response.json())
            .then(data => {
                if (data.exists) {
                    document.getElementById('existingChallanMessage').classList.remove('d-none');
                    document.getElementById('dcChallanId').value = data.challanId;

                    // Pre-fill data if available
                    if (data.challan) {
                        document.getElementById('dcDate').value = formatDateForInput(data.challan.challanDate);
                        document.getElementById('dcRemarks').value = data.challan.remarks || '';
                        document.getElementById('dcLocation').value = data.challan.locationId;
                        document.getElementById('dcStation').value = data.challan.station || '';
                        document.getElementById('dcOtherRef').value = data.challan.otherRef || '';
                    }
                } else {
                    document.getElementById('existingChallanMessage').classList.add('d-none');
                    document.getElementById('dcChallanId').value = '';

                    // Set default date to today
                    document.getElementById('dcDate').value = new Date().toISOString().split('T')[0];
                    document.getElementById('dcRemarks').value = '';
                    document.getElementById('dcStation').value = '';
                    document.getElementById('dcOtherRef').value = '';
                }
            })
            .catch(error => console.error('Error checking existing challan:', error));

        // Show the modal
        const modal = new bootstrap.Modal(document.getElementById('deliveryChallanModal'));
        modal.show();
    };

    function formatDateForInput(dateString) {
        if (!dateString) return '';

        // Convert dd-MM-yyyy to yyyy-MM-dd for input[type="date"]
        if (typeof dateString === 'string' && dateString.includes('-')) {
            const parts = dateString.split('-');
            if (parts[0].length === 2) {
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
        }

        return dateString;
    }

    // Post Single button handler
    document.getElementById('postSingleBtn').addEventListener('click', function () {
        const form = document.getElementById('deliveryChallanForm');
        const formData = new FormData(form);

        fetch('/DeliveryChallan/CreateSingle', {
            method: 'POST',
            body: formData
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    // Close modal
                    bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                    // Refresh data
                    loadFilteredEntries();

                    // Show success message
                    alert('Entry processed successfully');
                } else {
                    alert(data.message || 'Error processing entry');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                alert('Error processing entry');
            });
    });

    // Post All button handler
    document.getElementById('postAllBtn').addEventListener('click', function () {
        const form = document.getElementById('deliveryChallanForm');
        const formData = new FormData(form);

        // Get selected entries
        const selectedEntries = [];
        document.querySelectorAll('.entry-checkbox:checked').forEach(checkbox => {
            selectedEntries.push(checkbox.value);
        });

        // Add selected entries to form data
        selectedEntries.forEach(entryId => {
            formData.append('selectedEntries', entryId);
        });

        if (selectedEntries.length === 0) {
            alert('Please select at least one entry');
            return;
        }

        fetch('/DeliveryChallan/ProcessAll', {
            method: 'POST',
            body: formData
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                if (data.success) {
                    // Close modal
                    bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                    // Refresh data
                    loadFilteredEntries();

                    // Show success message
                    alert('Entries processed successfully');
                } else {
                    alert(data.message || 'Error processing entries');
                }
            })
            .catch(error => {
                console.error('Error:', error);
                alert('Error processing entries');
            });
    });

    // Load initial data
    loadFilteredEntries();
});