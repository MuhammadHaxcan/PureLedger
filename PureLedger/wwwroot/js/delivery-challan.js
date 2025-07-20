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

    if (!sinceDate.value) {
        const thirtyDaysAgo = new Date();
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        sinceDate.value = thirtyDaysAgo.toISOString().split('T')[0];
    }

    if (!toDate.value) {
        const today = new Date();
        toDate.value = today.toISOString().split('T')[0];
    }

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
            .catch(error => console.error('Error loading categories:', error));
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
            .catch(error => console.error('Error loading accounts:', error));
    }

    function loadFilteredEntries() {
        const params = new URLSearchParams({
            SinceDate: sinceDate.value,
            ToDate: toDate.value
        });

        if (projectSelect.value) params.append('ProjectId', projectSelect.value);
        if (categorySelect.value) params.append('CategoryId', categorySelect.value);
        if (accountSelect.value) params.append('AccountId', accountSelect.value);

        fetch(`/DeliveryChallan/GetFilteredEntries?${params.toString()}`)
            .then(response => response.json())
            .then(data => renderResults(data))
            .catch(error => console.error('Error loading entries:', error));
    }

    function renderResults(data) {
        resultsTable.innerHTML = '';
        if (!data.vouchers || data.vouchers.length === 0) {
            resultsTable.innerHTML = `<tr><td colspan="6" class="text-center">No entries found matching the criteria</td></tr>`;
            return;
        }

        data.vouchers.forEach(voucher => {
            voucher.entries.forEach(entry => {
                const row = document.createElement('tr');
                row.dataset.voucherId = voucher.jvId;
                row.dataset.entryId = entry.entryId;

                row.innerHTML = `
                    <td>JV${voucher.jvId}<br>E-Id ${entry.entryId}</td>
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
            row.style.display = text.includes(searchTerm) ? '' : 'none';
        });
    }

    function formatDate(dateString) {
        if (!dateString) return '';
        const parts = dateString.split('-');
        if (parts.length === 3 && parts[0].length === 4) {
            return `${parts[2]}-${parts[1]}-${parts[0]}`;
        }
        return dateString;
    }

    function formatDateForInput(dateString) {
        if (!dateString) return '';
        const parts = dateString.split('-');
        if (parts.length === 3 && parts[0].length === 2) {
            return `${parts[2]}-${parts[1]}-${parts[0]}`;
        }
        return dateString;
    }

    window.openDeliveryChallanModal = function (voucherId, entryId) {
        document.getElementById('dcJournalVoucherId').value = voucherId;
        document.getElementById('dcJournalEntryId').value = entryId || '';

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
        fetch(`/DeliveryChallan/GetVoucherEntries?voucherId=${voucherId}`)
            .then(res => res.json())
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
                                <input type="checkbox" class="entry-checkbox" value="${entry.entryId}" 
                                       ${entry.entryId == entryId ? 'checked' : ''}>
                            </td>
                        `;
                        entriesTable.appendChild(row);
                    }
                });
            })
            .catch(error => {
                console.error('Error loading voucher entries:', error);
                alert('Error loading voucher entries: ' + error.message);
            });

        // Check for existing challan
        fetch(`/DeliveryChallan/CheckExistingChallan?voucherId=${voucherId}`)
            .then(res => res.json())
            .then(data => {
                const challanId = document.getElementById('dcChallanId');
                if (data.exists) {
                    document.getElementById('existingChallanMessage').classList.remove('d-none');
                    challanId.value = data.challanId;
                    document.getElementById('dcDate').value = formatDateForInput(data.challan.challanDate);
                    document.getElementById('dcRemarks').value = data.challan.remarks || '';
                    document.getElementById('dcLocation').value = data.challan.locationId;
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
            cartageBtn.onclick = () => openCartageModal(voucherId, entryId);
            modalFooter.insertBefore(cartageBtn, modalFooter.firstChild);
        }

        const modal = new bootstrap.Modal(document.getElementById('deliveryChallanModal'));
        modal.show();
    };

    function openCartageModal(voucherId, entryId) {
        document.getElementById('cartageJournalVoucherId').value = voucherId;
        document.getElementById('cartageEntryId').value = entryId;

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
        fetch('/api/common/accounts/4')
            .then(response => response.json())
            .then(transporters => {
                transporters.forEach(t => {
                    const option = document.createElement('option');
                    option.value = t.id;
                    option.textContent = t.name;
                    transporterSelect.appendChild(option);
                });
            })
            .catch(error => console.error('Error loading transporters:', error));
    }

    function loadLocations() {
        fetch('/DeliveryChallan/GetLocations')
            .then(res => res.json())
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
            });
    }



    // Post Single button handler
    const postSingleBtn = document.getElementById('postSingleBtn');
    if (postSingleBtn) {
        postSingleBtn.addEventListener('click', function () {
            let entryId = document.getElementById('dcJournalEntryId').value;
            if (!entryId) {
                const selected = document.querySelector('.entry-checkbox:checked');
                if (!selected) {
                    alert('Please select an entry to process');
                    return;
                }
                entryId = selected.value;
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

            fetch('/DeliveryChallan/PostSingleEntry', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();
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
    document.getElementById('addCartageBtn').addEventListener('click', function() {
        const voucherId = document.getElementById('cartageJournalVoucherId').value;
        const entryId = document.getElementById('cartageEntryId').value;
        const transporterId = document.getElementById('transporterAccount').value;
        const amount = document.getElementById('cartageAmount').value;
        const postToCustomer = document.getElementById('postToCustomer').checked;

        if (!transporterId || !amount) {
            alert('Please fill in all required fields');
            return;
        }

        const data = {
            journalVoucherId: parseInt(voucherId),
            entryId: parseInt(entryId),
            transporterId: parseInt(transporterId),
            amount: parseFloat(amount),
            postToCustomer: postToCustomer
        };

        fetch('/DeliveryChallan/AddCartage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
        .then(response => response.json())
        .then(result => {
            if (result.success) {
                // Close both modals
                bootstrap.Modal.getInstance(document.getElementById('cartageModal')).hide();
                bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();

                // Refresh the entries
                loadFilteredEntries();

                // Show success message
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

    // Initialize the page
    loadFilteredEntries();

    // Initialize tooltips
    document.addEventListener('DOMContentLoaded', function() {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function(tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    });
});