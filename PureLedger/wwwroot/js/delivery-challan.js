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

    // Track the currently open modal data
    let currentModalData = {
        voucherId: null,
        entryId: null
    };

    // Set default dates if not already set
    if (!sinceDate.value) {
        const thirtyDaysAgo = new Date();
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        sinceDate.value = thirtyDaysAgo.toISOString().split('T')[0];
    }

    if (!toDate.value) {
        const today = new Date();
        toDate.value = today.toISOString().split('T')[0];
    }

    // Event listeners
    projectSelect.addEventListener('change', function () {
        categorySelect.innerHTML = '<option value="">Select Category</option>';
        accountSelect.innerHTML = '<option value="">Select Account</option>';
        categorySelect.disabled = true;
        accountSelect.disabled = true;

        if (this.value) {
            fetchCategories(this.value);
        }
    });

    categorySelect.addEventListener('change', function () {
        accountSelect.innerHTML = '<option value="">Select Account</option>';
        accountSelect.disabled = true;

        if (this.value) {
            fetchAccounts(this.value);
        }
    });

    searchInput.addEventListener('keyup', function () {
        filterResults(this.value);
    });

    goButton.addEventListener('click', function () {
        loadFilteredEntries();
    });

    document.getElementById('transferBtn')?.addEventListener('click', function () {
        alert('Transfer functionality will be implemented in the future.');
    });

    // Listen for modal close events to clean up
    const deliveryChallanModal = document.getElementById('deliveryChallanModal');
    if (deliveryChallanModal) {
        deliveryChallanModal.addEventListener('hidden.bs.modal', function () {
            // Reset form and state when modal is closed
            document.getElementById('dcJournalVoucherId').value = '';
            document.getElementById('dcJournalEntryId').value = '';
            document.getElementById('dcChallanId').value = '';
            currentModalData = { voucherId: null, entryId: null };

            // Clear any selected checkboxes
            const checkboxes = document.querySelectorAll('.entry-checkbox:not([disabled])');
            checkboxes.forEach(cb => cb.checked = false);
        });
    }

    function fetchCategories(projectId) {
        fetch(`/api/common/categories/${projectId}`)
            .then(response => response.json())
            .then(data => {
                categorySelect.disabled = false;
                data.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            })
            .catch(error => {
                console.error('Error loading categories:', error);
                // Show error to user
                alert('Error loading categories. Please try again.');
            });
    }

    function fetchAccounts(categoryId) {
        fetch(`/api/common/accounts/${categoryId}`)
            .then(response => response.json())
            .then(data => {
                accountSelect.disabled = false;
                data.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);
                });
            })
            .catch(error => {
                console.error('Error loading accounts:', error);
                // Show error to user
                alert('Error loading accounts. Please try again.');
            });
    }

    function loadFilteredEntries() {
        // Show loading indicator
        resultsTable.innerHTML = '<tr><td colspan="6" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>';

        const params = new URLSearchParams({
            SinceDate: sinceDate.value,
            ToDate: toDate.value
        });

        if (projectSelect.value) params.append('ProjectId', projectSelect.value);
        if (categorySelect.value) params.append('CategoryId', categorySelect.value);
        if (accountSelect.value) params.append('AccountId', accountSelect.value);

        console.log('Fetching data with params:', params.toString());

        fetch(`/DeliveryChallan/GetFilteredEntries?${params.toString()}`)
            .then(response => {
                if (!response.ok) {
                    return response.text().then(text => {
                        throw new Error(`Server returned ${response.status}: ${text}`);
                    });
                }
                return response.json();
            })
            .then(data => {
                console.log('Received data:', data);
                renderResults(data);
            })
            .catch(error => {
                console.error('Error loading entries:', error);
                resultsTable.innerHTML = `<tr><td colspan="6" class="text-center text-danger">Error loading data: ${error.message}</td></tr>`;
            });
    }

    function renderResults(data) {
        resultsTable.innerHTML = '';

        if (!data || !data.vouchers || data.vouchers.length === 0) {
            resultsTable.innerHTML = `<tr><td colspan="6" class="text-center">No entries found matching the criteria</td></tr>`;
            return;
        }

        data.vouchers.forEach(voucher => {
            if (!voucher.entries || voucher.entries.length === 0) {
                return; // Skip vouchers with no entries
            }

            voucher.entries.forEach(entry => {
                const row = document.createElement('tr');
                row.dataset.voucherId = voucher.jvId;
                row.dataset.entryId = entry.entryId;

                // Add a class if the entry is already processed
                if (entry.isProcessed) {
                    row.classList.add('table-success');
                }

                row.innerHTML = `
                    <td>JV${voucher.jvId}<br>E-Id ${entry.entryId}</td>
                    <td>${formatDate(voucher.entryDate)}</td>
                    <td>${entry.description || ''}</td>
                    <td>${entry.accountName || ''}</td>
                    <td>${entry.quantity}</td>
                    <td>
                        <button class="btn btn-sm ${entry.isProcessed ? 'btn-success' : 'btn-primary'} create-dc-btn" 
                                data-voucher-id="${voucher.jvId}" 
                                data-entry-id="${entry.entryId}">
                            <i class="bi bi-${entry.isProcessed ? 'check-circle' : 'pencil'}"></i> 
                            ${entry.isProcessed ? 'Processed' : 'Edit'}
                        </button>
                    </td>
                `;
                resultsTable.appendChild(row);
            });
        });

        // Add event listeners to all create-dc-btn buttons
        document.querySelectorAll('.create-dc-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                const voucherId = this.dataset.voucherId;
                const entryId = this.dataset.entryId;
                openDeliveryChallanModal(parseInt(voucherId), parseInt(entryId));
            });
        });
    }

    function filterResults(searchTerm) {
        const rows = resultsTable.querySelectorAll('tr');
        searchTerm = searchTerm.toLowerCase();
        rows.forEach(row => {
            const text = row.textContent.toLowerCase();
            row.style.display = text.includes(searchTerm) ? '' : 'none';
        });
    }

    function formatDate(dateString) {
        if (!dateString) return '';

        try {
            const parts = dateString.split('-');
            if (parts.length === 3 && parts[0].length === 4) {
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
            return dateString;
        } catch (error) {
            console.error('Error formatting date:', error, dateString);
            return dateString || '';
        }
    }

    function formatDateForInput(dateString) {
        if (!dateString) return '';

        try {
            const parts = dateString.split('-');
            if (parts.length === 3 && parts[0].length === 2) {
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
            return dateString;
        } catch (error) {
            console.error('Error formatting date for input:', error, dateString);
            return dateString || '';
        }
    }

    function loadVoucherEntries(voucherId, entryId) {
        console.log(`Loading voucher entries for voucher: ${voucherId}, selected entry: ${entryId}`);

        // Load voucher entries
        fetch(`/DeliveryChallan/GetVoucherEntries?voucherId=${voucherId}`)
            .then(res => {
                if (!res.ok) {
                    return res.text().then(text => {
                        throw new Error(`Server returned ${res.status}: ${text}`);
                    });
                }
                return res.json();
            })
            .then(entries => {
                const entriesTable = document.getElementById('dcEntriesTable');
                entriesTable.innerHTML = '';

                // Count unprocessed entries for UI feedback
                let unprocessedCount = 0;

                entries.forEach(entry => {
                    if (entry.quantity > 0) {
                        const row = document.createElement('tr');
                        row.dataset.entryId = entry.entryId;

                        // Mark rows differently if they're already processed
                        if (entry.isProcessed) {
                            row.classList.add('table-success');
                        } else {
                            unprocessedCount++;
                        }

                        // Check if this is the selected entry
                        const isSelected = entry.entryId == entryId && !entry.isProcessed;

                        row.innerHTML = `
                            <td>${entry.accountName || ''}</td>
                            <td>${entry.description || ''}</td>
                            <td>${entry.quantity}</td>
                            <td>
                                <input type="checkbox" class="entry-checkbox" data-entry-id="${entry.entryId}" 
                                       ${entry.isProcessed ? 'disabled checked' : ''}
                                       ${isSelected ? 'checked' : ''}>
                            </td>
                        `;
                        entriesTable.appendChild(row);
                    }
                });

                // Add event listeners to checkboxes after they're created
                document.querySelectorAll('.entry-checkbox:not([disabled])').forEach(checkbox => {
                    checkbox.addEventListener('change', function () {
                        const entryId = this.dataset.entryId;

                        // Uncheck all other checkboxes
                        document.querySelectorAll('.entry-checkbox:not([disabled])').forEach(cb => {
                            if (cb !== this) {
                                cb.checked = false;
                            }
                        });

                        // Update hidden form field
                        if (this.checked) {
                            document.getElementById('dcJournalEntryId').value = entryId;
                            console.log(`Selected entry ID updated to: ${entryId}`);
                        } else {
                            document.getElementById('dcJournalEntryId').value = '';
                            console.log('Entry selection cleared');
                        }
                    });
                });

                // If an entry was specified but no checkbox is checked, make sure form field is updated
                const checkedBox = document.querySelector('.entry-checkbox:checked:not([disabled])');
                if (checkedBox) {
                    document.getElementById('dcJournalEntryId').value = checkedBox.dataset.entryId;
                    console.log(`Initial entry ID set to: ${checkedBox.dataset.entryId}`);
                } else if (entryId && !document.querySelector(`.entry-checkbox[data-entry-id="${entryId}"]:disabled`)) {
                    // If the specified entry isn't checked (and isn't disabled), make sure it's not in the form
                    document.getElementById('dcJournalEntryId').value = '';
                    console.log('No entry selected');
                }

                // Update UI based on process status
                const postAllBtn = document.getElementById('postAllBtn');
                if (postAllBtn) {
                    if (unprocessedCount === 0) {
                        postAllBtn.disabled = true;
                        postAllBtn.title = 'All entries already processed';
                    } else {
                        postAllBtn.disabled = false;
                        postAllBtn.title = '';
                    }
                }
            })
            .catch(error => {
                console.error('Error loading voucher entries:', error);
                alert('Error loading voucher entries: ' + error.message);
            });
    }

    window.openDeliveryChallanModal = function (voucherId, entryId) {
        console.log(`Opening modal for voucher: ${voucherId}, entry: ${entryId}`);

        // Store current modal data
        currentModalData = {
            voucherId: voucherId,
            entryId: entryId
        };

        // Reset and set form values
        const voucherIdField = document.getElementById('dcJournalVoucherId');
        const entryIdField = document.getElementById('dcJournalEntryId');

        // Clear previous values
        voucherIdField.value = '';
        entryIdField.value = '';

        // Set new values
        voucherIdField.value = voucherId;
        if (entryId) {
            entryIdField.value = entryId;
        }

        // Show the all entries view and create the table structure
        const allEntriesView = document.getElementById('allEntriesView');
        allEntriesView.classList.remove('d-none');

        // Create the entries table structure if it doesn't exist
        if (!document.getElementById('dcEntriesTable')) {
            allEntriesView.innerHTML = `
                <h6>Available Entries:</h6>
                <div class="table-responsive">
                    <table class="table table-sm">
                        <thead>
                            <tr>
                                <th>Account</th>
                                <th>Description</th>
                                <th>Quantity</th>
                                <th>Select</th>
                            </tr>
                        </thead>
                        <tbody id="dcEntriesTable">
                        </tbody>
                    </table>
                </div>
            `;
        }

        loadLocations();

        // Load voucher entries
        loadVoucherEntries(voucherId, entryId);

        // Check for existing challan
        fetch(`/DeliveryChallan/CheckExistingChallan?voucherId=${voucherId}`)
            .then(res => {
                if (!res.ok) {
                    return res.text().then(text => {
                        throw new Error(`Server returned ${res.status}: ${text}`);
                    });
                }
                return res.json();
            })
            .then(data => {
                const challanId = document.getElementById('dcChallanId');
                if (data.exists) {
                    document.getElementById('existingChallanMessage').classList.remove('d-none');
                    challanId.value = data.challanId;

                    // Handle date format
                    let challanDate = '';
                    if (data.challan.challanDate) {
                        challanDate = formatDateForInput(data.challan.challanDate);
                    }

                    document.getElementById('dcDate').value = challanDate || new Date().toISOString().split('T')[0];
                    document.getElementById('dcRemarks').value = data.challan.remarks || '';
                    document.getElementById('dcLocation').value = data.challan.locationId || '';
                    document.getElementById('dcOtherRef').value = data.challan.otherRef || '';
                } else {
                    document.getElementById('existingChallanMessage').classList.add('d-none');
                    challanId.value = '';
                    document.getElementById('dcDate').value = new Date().toISOString().split('T')[0];
                    document.getElementById('dcRemarks').value = '';
                    document.getElementById('dcOtherRef').value = '';
                }
            })
            .catch(error => {
                console.error('Error checking existing challan:', error);
            });

        // Add cartage button to modal footer
        const modalFooter = document.querySelector('#deliveryChallanModal .modal-footer');
        if (!document.getElementById('openCartageBtn')) {
            const cartageBtn = document.createElement('button');
            cartageBtn.id = 'openCartageBtn';
            cartageBtn.className = 'btn btn-info';
            cartageBtn.textContent = 'Add Cartage';
            cartageBtn.onclick = function () {
                // Use the current modal data when opening cartage
                openCartageModal(currentModalData.voucherId, currentModalData.entryId);
            };
            modalFooter.insertBefore(cartageBtn, modalFooter.firstChild);
        } else {
            // Update the existing cartage button
            document.getElementById('openCartageBtn').onclick = function () {
                openCartageModal(currentModalData.voucherId, currentModalData.entryId);
            };
        }

        const modal = new bootstrap.Modal(document.getElementById('deliveryChallanModal'));
        modal.show();
    };

    function openCartageModal(voucherId, entryId) {
        console.log(`Opening cartage modal for voucher: ${voucherId}, entry: ${entryId}`);

        // Get fresh values from current modal
        const cartageVoucherId = document.getElementById('cartageJournalVoucherId');
        const cartageEntryId = document.getElementById('cartageEntryId');

        // Clear previous values
        cartageVoucherId.value = '';
        cartageEntryId.value = '';

        // Set new values
        cartageVoucherId.value = voucherId;

        // Get the currently selected entry ID (could be different from the original)
        const selectedEntryId = document.getElementById('dcJournalEntryId').value || entryId;
        if (selectedEntryId) {
            cartageEntryId.value = selectedEntryId;
        }

        // Load transporters (from transporter category)
        loadTransporters();

        // Show cartage modal
        const cartageModal = new bootstrap.Modal(document.getElementById('cartageModal'));
        cartageModal.show();
    }

    function loadTransporters() {
        const transporterSelect = document.getElementById('transporterAccount');
        transporterSelect.innerHTML = '<option value="">Select Transporter</option>';

        // Fetch transporter accounts from categoryId 4
        fetch('/api/common/accounts/2')
            .then(response => {
                if (!response.ok) {
                    return response.text().then(text => {
                        throw new Error(`Server returned ${response.status}: ${text}`);
                    });
                }
                return response.json();
            })
            .then(transporters => {
                transporters.forEach(t => {
                    const option = document.createElement('option');
                    option.value = t.id;
                    option.textContent = t.name;
                    transporterSelect.appendChild(option);
                });
            })
            .catch(error => {
                console.error('Error loading transporters:', error);
                alert('Error loading transporters: ' + error.message);
            });
    }

    function loadLocations() {
        fetch('/DeliveryChallan/GetLocations')
            .then(res => {
                if (!res.ok) {
                    return res.text().then(text => {
                        throw new Error(`Server returned ${res.status}: ${text}`);
                    });
                }
                return res.json();
            })
            .then(locations => {
                const locationSelect = document.getElementById('dcLocation');
                locationSelect.innerHTML = '<option value="">Select Location</option>';
                locations.forEach(loc => {
                    const opt = document.createElement('option');
                    opt.value = loc.locationId;
                    opt.textContent = loc.name;
                    locationSelect.appendChild(opt);
                });
            })
            .catch(error => {
                console.error('Error loading locations:', error);
                alert('Error loading locations: ' + error.message);
            });
    }

    // Post Single button handler
    const postSingleBtn = document.getElementById('postSingleBtn');
    if (postSingleBtn) {
        postSingleBtn.addEventListener('click', function () {
            // Get the current voucher ID
            const voucherId = document.getElementById('dcJournalVoucherId').value;

            // Get the specific entry ID either from the form or from the selected checkbox
            let entryId = document.getElementById('dcJournalEntryId').value;

            console.log(`Posting entry - Current form entryId: ${entryId}`);

            // If no entry ID is set in the form field, check for a selected checkbox
            if (!entryId) {
                const selected = document.querySelector('.entry-checkbox:checked:not([disabled])');
                if (!selected) {
                    alert('Please select an unprocessed entry to process');
                    return;
                }
                entryId = selected.dataset.entryId;
                console.log(`Selected checkbox entryId: ${entryId}`);
            }

            const locationId = document.getElementById('dcLocation').value;
            const dcDate = document.getElementById('dcDate').value;
            const remarks = document.getElementById('dcRemarks').value;
            const otherRef = document.getElementById('dcOtherRef').value;

            if (!locationId) {
                alert('Please select a location');
                return;
            }

            const data = {
                JournalEntryId: parseInt(entryId),
                LocationId: parseInt(locationId),
                DCDate: dcDate,
                Remarks: remarks,
                OtherReference: otherRef
            };

            // For debugging
            console.log('Posting entry with data:', data);

            fetch('/DeliveryChallan/PostSingleEntry', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
                .then(res => {
                    if (!res.ok) {
                        return res.text().then(text => {
                            throw new Error(`Server returned ${res.status}: ${text}`);
                        });
                    }
                    return res.json();
                })
                .then(data => {
                    if (data.success) {
                        // Close the modal
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                        // Reset form field and state
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        currentModalData = { voucherId: null, entryId: null };

                        // Refresh data
                        loadFilteredEntries();
                        alert('Entry processed successfully');
                    } else {
                        alert(data.message || 'Error processing entry');
                    }
                })
                .catch(err => {
                    console.error(err);
                    alert('Error: ' + err.message);
                });
        });
    }

    // Post All button handler
    const postAllBtn = document.getElementById('postAllBtn');
    if (postAllBtn) {
        postAllBtn.addEventListener('click', function () {
            const voucherId = document.getElementById('dcJournalVoucherId').value;
            if (!voucherId) {
                alert('No voucher selected');
                return;
            }

            const locationId = document.getElementById('dcLocation').value;
            if (!locationId) {
                alert('Please select a location');
                return;
            }

            const dcDate = document.getElementById('dcDate').value;
            const remarks = document.getElementById('dcRemarks').value;
            const otherRef = document.getElementById('dcOtherRef').value;

            // Prepare data for sending
            const data = {
                JournalVoucherId: parseInt(voucherId),
                LocationId: parseInt(locationId),
                DCDate: dcDate,
                Remarks: remarks,
                OtherReference: otherRef
            };

            // Send request
            fetch('/DeliveryChallan/ProcessAll', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
                .then(response => {
                    if (!response.ok) {
                        return response.text().then(text => {
                            throw new Error('Server returned ' + response.status + ': ' + text);
                        });
                    }
                    return response.json();
                })
                .then(data => {
                    if (data.success) {
                        // Close modal
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                        // Reset form field and state
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        currentModalData = { voucherId: null, entryId: null };

                        // Refresh data
                        loadFilteredEntries();

                        // Show success message
                        alert(data.message || 'Entries processed successfully');
                    } else {
                        alert(data.message || 'Error processing entries');
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert('Error processing entries: ' + error.message);
                });
        });
    }

    // Add cartage button handler
    const addCartageBtn = document.getElementById('addCartageBtn');
    if (addCartageBtn) {
        addCartageBtn.addEventListener('click', function () {
            const voucherId = document.getElementById('cartageJournalVoucherId').value;
            const entryId = document.getElementById('cartageEntryId').value;
            const transporterId = document.getElementById('transporterAccount').value;
            const amount = document.getElementById('cartageAmount').value;
            const postToCustomer = document.getElementById('postToCustomer').checked;

            console.log(`Adding cartage for voucher: ${voucherId}, entry: ${entryId}`);

            // Collect DC fields from the main modal
            const dcDate = document.getElementById('dcDate').value;
            const dcRemarks = document.getElementById('dcRemarks').value;
            const dcStation = document.getElementById('dcStation')?.value || '';
            const dcOtherRef = document.getElementById('dcOtherRef').value;
            const dcLocation = document.getElementById('dcLocation').value;

            if (!transporterId || !amount) {
                alert('Please fill in all required fields');
                return;
            }

            if (!dcLocation) {
                alert('Please select a location');
                return;
            }

            const data = {
                journalVoucherId: parseInt(voucherId),
                entryId: parseInt(entryId),
                transporterId: parseInt(transporterId),
                amount: parseFloat(amount),
                postToCustomer: postToCustomer,
                dcDate: dcDate,
                dcRemarks: dcRemarks,
                dcStation: dcStation,
                dcOtherRef: dcOtherRef,
                dcLocation: parseInt(dcLocation)
            };

            // For debugging
            console.log('Posting cartage with data:', data);

            fetch('/DeliveryChallan/AddCartage', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
                .then(response => {
                    if (!response.ok) {
                        return response.text().then(text => {
                            throw new Error(`Server returned ${response.status}: ${text}`);
                        });
                    }
                    return response.json();
                })
                .then(result => {
                    if (result.success) {
                        // Close both modals
                        bootstrap.Modal.getInstance(document.getElementById('cartageModal')).hide();
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                        // Reset form field and state
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        document.getElementById('cartageJournalVoucherId').value = '';
                        document.getElementById('cartageEntryId').value = '';
                        currentModalData = { voucherId: null, entryId: null };

                        // Refresh data
                        loadFilteredEntries();

                        alert('Cartage added and delivery challan processed successfully');
                    } else {
                        alert(result.message || 'Error processing cartage');
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert('Error processing cartage: ' + error.message);
                });
        });
    }

    // Initialize the page
    console.log('Initializing Delivery Challan page');
    loadFilteredEntries();

    // Initialize tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});