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
        showAlert('warning', 'Transfer functionality will be implemented in the future.');
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
        });
    }

    function fetchCategories(projectId) {
        PureLedger.get('/DeliveryChallan/GetCategoriesByProject', { projectId: projectId })
            .done(function (data) {
                categorySelect.disabled = false;
                data.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            })
            .fail(function (xhr, status, error) {
                console.error('Error loading categories:', error);
                showAlert('danger', 'Error loading categories. Please try again.');
            });
    }

    function fetchAccounts(categoryId) {
        PureLedger.get('/DeliveryChallan/GetAccountsByCategory', { categoryId: categoryId })
            .done(function (data) {
                accountSelect.disabled = false;
                data.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);
                });
            })
            .fail(function (xhr, status, error) {
                console.error('Error loading accounts:', error);
                showAlert('danger', 'Error loading accounts. Please try again.');
            });
    }

    function loadFilteredEntries() {
        // Destroy DataTable if it exists before updating table content
        if ($.fn.DataTable.isDataTable('#entriesTable')) {
            $('#entriesTable').DataTable().clear().destroy();
        }
        resultsTable.innerHTML = '<tr><td colspan="6" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>';

        const params = {
            SinceDate: sinceDate.value,
            ToDate: toDate.value
        };

        if (projectSelect.value) params.ProjectId = projectSelect.value;
        if (categorySelect.value) params.CategoryId = categorySelect.value;
        if (accountSelect.value) params.AccountId = accountSelect.value;

        PureLedger.get('/DeliveryChallan/GetFilteredEntries', params)
            .done(function (data) {
                renderResults(data);
                initializeDataTable();
            })
            .fail(function (xhr, status, error) {
                let errorMessage = 'Error loading data';
                try {
                    errorMessage += ': ' + xhr.responseText;
                } catch (e) {
                    errorMessage += ': ' + error;
                }
                resultsTable.innerHTML = `<tr><td colspan="6" class="text-center text-danger">${errorMessage}</td></tr>`;
                showAlert('danger', 'Error loading entries: ' + error);
            });
    }

    function renderResults(data) {
        // Clear table body before rendering
        resultsTable.innerHTML = '';

        if (!data || !data.vouchers || data.vouchers.length === 0) {
            resultsTable.innerHTML = `<tr><td colspan="6" class="text-center">No entries found matching the criteria</td></tr>`;
            return;
        }

        data.vouchers.forEach(voucher => {
            if (!voucher.entries || voucher.entries.length === 0) {
                return;
            }
            voucher.entries.forEach(entry => {
                const row = document.createElement('tr');
                row.dataset.voucherId = voucher.jvId;
                row.dataset.entryId = entry.entryId;

                if (entry.isProcessed) {
                    row.classList.add('table-success');
                }

                row.innerHTML = `
                    <td>
                        <span class="badge bg-secondary mb-1">JV-${voucher.jvId}</span>
                        <br />
                        <small>E-Id: ${entry.entryId}</small>
                    </td>
                    <td>${formatDate(voucher.entryDate)}</td>
                    <td>${entry.description || '-'}</td>
                    <td>${entry.accountName || '-'}</td>
                    <td>${entry.quantity}</td>
                    <td class="text-center">
                        <button class="btn btn-sm ${entry.isProcessed ? 'btn-success' : 'btn-primary'} create-dc-btn" 
                                data-voucher-id="${voucher.jvId}" 
                                data-entry-id="${entry.entryId}">
                            <i class="fas fa-${entry.isProcessed ? 'check-circle' : 'pencil'}"></i> 
                            ${entry.isProcessed ? 'Processed' : 'Process'}
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

    function initializeDataTable() {
        // Destroy existing DataTable if it exists
        if ($.fn.DataTable.isDataTable('#entriesTable')) {
            $('#entriesTable').DataTable().destroy();
        }

        // Only initialize DataTable if there is at least one real data row
        // Exclude rows with the "No entries found" or error message
        const dataRows = Array.from(resultsTable.querySelectorAll('tr')).filter(tr =>
            !tr.textContent.includes('No entries found') &&
            !tr.textContent.includes('Error loading data') &&
            !tr.textContent.includes('No entries available')
        );

        if (dataRows.length === 0) {
            return; // Do not initialize DataTable if no real data rows
        }

        $('#entriesTable').DataTable({
            "pageLength": 10,
            "lengthMenu": [[5, 10, 25, 50, -1], [5, 10, 25, 50, "All"]],
            "language": {
                "emptyTable": "No entries available",
                "search": "Search entries:",
                "info": "Showing _START_ to _END_ of _TOTAL_ entries",
                "lengthMenu": "Show _MENU_ entries"
            },
            "columnDefs": [
                { "orderable": false, "targets": 5 } // Disable sorting on the Actions column
            ],
            "order": [[1, "desc"]] // Sort by date descending
        });
    }

    function filterResults(searchTerm) {
        if ($.fn.DataTable.isDataTable('#entriesTable')) {
            $('#entriesTable').DataTable().search(searchTerm).draw();
        } else {
            // Fallback to manual filtering if DataTable isn't initialized
            const rows = resultsTable.querySelectorAll('tr');
            searchTerm = searchTerm.toLowerCase();
            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                row.style.display = text.includes(searchTerm) ? '' : 'none';
            });
        }
    }

    function formatDate(dateString) {
        if (!dateString) return '-';

        try {
            const parts = dateString.split('-');
            if (parts.length === 3 && parts[0].length === 4) {
                return `${parts[2]}-${parts[1]}-${parts[0]}`;
            }
            return dateString;
        } catch (error) {
            console.error('Error formatting date:', error, dateString);
            return dateString || '-';
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

        // Remove available entries view completely
        const allEntriesView = document.getElementById('allEntriesView');
        if (allEntriesView) {
            allEntriesView.innerHTML = '';
            allEntriesView.classList.add('d-none');
        }

        loadLocations();

        // Check for existing challan
        PureLedger.get('/DeliveryChallan/CheckExistingChallan', { voucherId: voucherId })
            .done(function (data) {
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
            .fail(function (xhr, status, error) {
                console.error('Error checking existing challan:', error);
                showAlert('danger', 'Error checking existing challan: ' + error);
            });

        // Add cartage button to modal footer
        const modalFooter = document.querySelector('#deliveryChallanModal .modal-footer');
        if (!document.getElementById('openCartageBtn')) {
            const cartageBtn = document.createElement('button');
            cartageBtn.id = 'openCartageBtn';
            cartageBtn.className = 'btn btn-info';
            cartageBtn.innerHTML = '<i class="fas fa-shipping-fast me-1"></i> Add Cartage';
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

        // Get the transporter category ID first
        PureLedger.get('/DeliveryChallan/GetTransporterCategoryId')
            .done(function (data) {
                const transporterCategoryId = data.value;

                // Then fetch accounts with that category ID
                PureLedger.get('/DeliveryChallan/GetAccountsByCategory', { categoryId: transporterCategoryId })
                    .done(function (transporters) {
                        transporters.forEach(t => {
                            const option = document.createElement('option');
                            option.value = t.id;
                            option.textContent = t.name;
                            transporterSelect.appendChild(option);
                        });
                    })
                    .fail(function (xhr, status, error) {
                        console.error('Error loading transporters:', error);
                        showAlert('danger', 'Error loading transporters: ' + error);
                    });
            })
            .fail(function (xhr, status, error) {
                console.error('Error getting transporter category ID:', error);
                showAlert('danger', 'Error loading transporters: ' + error);
            });
    }

    function loadLocations() {
        PureLedger.get('/DeliveryChallan/GetLocations')
            .done(function (locations) {
                const locationSelect = document.getElementById('dcLocation');
                locationSelect.innerHTML = '<option value="">Select Location</option>';
                locations.forEach(loc => {
                    const opt = document.createElement('option');
                    opt.value = loc.locationId;
                    opt.textContent = loc.locationName;
                    locationSelect.appendChild(opt);
                });
            })
            .fail(function (xhr, status, error) {
                console.error('Error loading locations:', error);
                showAlert('danger', 'Error loading locations: ' + error);
            });
    }

    // Add event listener for FOR charges checkbox
    const includeForChargesCheckbox = document.getElementById('includeForCharges');
    const forChargesContainer = document.getElementById('forChargesContainer');

    if (includeForChargesCheckbox && forChargesContainer) {
        includeForChargesCheckbox.addEventListener('change', function () {
            if (this.checked) {
                forChargesContainer.classList.remove('d-none');
            } else {
                forChargesContainer.classList.add('d-none');
                document.getElementById('forChargesAmount').value = "0";
            }
        });
    }

    // Utility to show loading state on a button and disable others
    function setButtonLoadingExclusive(activeBtn, loadingText, allBtns) {
        allBtns.forEach(btn => {
            if (btn && btn !== activeBtn) {
                btn.disabled = true;
            }
        });
        if (activeBtn) {
            activeBtn.disabled = true;
            activeBtn.dataset.originalHtml = activeBtn.innerHTML;
            activeBtn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${loadingText}`;
        }
    }
    function resetButtonLoadingExclusive(activeBtn, allBtns) {
        allBtns.forEach(btn => {
            if (btn) btn.disabled = false;
        });
        if (activeBtn && activeBtn.dataset.originalHtml) {
            activeBtn.innerHTML = activeBtn.dataset.originalHtml;
            delete activeBtn.dataset.originalHtml;
        }
    }

    // Get references to all action buttons
    const postSingleBtn = document.getElementById('postSingleBtn');
    const postAllBtn = document.getElementById('postAllBtn');
    const addCartageBtn = document.getElementById('addCartageBtn');
    const actionBtns = [postSingleBtn, postAllBtn, addCartageBtn];

    // Post Single button handler
    if (postSingleBtn) {
        postSingleBtn.addEventListener('click', function () {
            if (postSingleBtn.disabled) return; // Prevent double submit
            setButtonLoadingExclusive(postSingleBtn, 'Posting...', actionBtns);

            // Get the current voucher ID
            const voucherId = document.getElementById('dcJournalVoucherId').value;

            // Get the specific entry ID
            let entryId = document.getElementById('dcJournalEntryId').value;
            if (!entryId) {
                showAlert('warning', 'No entry selected to process');
                resetButtonLoadingExclusive(postSingleBtn, actionBtns);
                return;
            }

            const locationId = document.getElementById('dcLocation').value;
            const dcDate = document.getElementById('dcDate').value;
            const remarks = document.getElementById('dcRemarks').value;
            const otherRef = document.getElementById('dcOtherRef').value;
            if (!locationId) {
                showAlert('warning', 'Please select a location');
                resetButtonLoadingExclusive(postSingleBtn, actionBtns);
                return;
            }

            // Get FOR charges data
            const includeForCharges = document.getElementById('includeForCharges').checked;
            const forChargesAmount = includeForCharges ?
                parseInt(document.getElementById('forChargesAmount').value || "0") : 0;

            if (includeForCharges && forChargesAmount <= 0) {
                showAlert('warning', 'Please enter a valid amount for FOR charges');
                resetButtonLoadingExclusive(postSingleBtn, actionBtns);
                return;
            }

            const data = {
                JournalEntryId: parseInt(entryId),
                LocationId: parseInt(locationId),
                DCDate: dcDate,
                Remarks: remarks,
                OtherReference: otherRef,
                IncludeForCharges: includeForCharges,
                ForAmount: forChargesAmount
            };

            PureLedger.post('/DeliveryChallan/PostSingleEntry', data)
                .done(function (data) {
                    if (data.success) {
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        currentModalData = { voucherId: null, entryId: null };
                        loadFilteredEntries();
                        showAlert('success', 'Entry processed successfully');
                    } else {
                        showAlert('danger', data.message || 'Error processing entry');
                    }
                })
                .fail(function (xhr, status, error) {
                    console.error('Error processing entry:', error);
                    showAlert('danger', 'Error: ' + error);
                })
                .always(function () {
                    resetButtonLoadingExclusive(postSingleBtn, actionBtns);
                });
        });
    }

    // Post All button handler
    if (postAllBtn) {
        postAllBtn.addEventListener('click', function () {
            if (postAllBtn.disabled) return;
            setButtonLoadingExclusive(postAllBtn, 'Posting...', actionBtns);

            const voucherId = document.getElementById('dcJournalVoucherId').value;
            if (!voucherId) {
                showAlert('warning', 'No voucher selected');
                resetButtonLoadingExclusive(postAllBtn, actionBtns);
                return;
            }
            const locationId = document.getElementById('dcLocation').value;
            if (!locationId) {
                showAlert('warning', 'Please select a location');
                resetButtonLoadingExclusive(postAllBtn, actionBtns);
                return;
            }
            const dcDate = document.getElementById('dcDate').value;
            const remarks = document.getElementById('dcRemarks').value;
            const otherRef = document.getElementById('dcOtherRef').value;

            // Get FOR charges data
            const includeForCharges = document.getElementById('includeForCharges').checked;
            const forChargesAmount = includeForCharges ?
                parseInt(document.getElementById('forChargesAmount').value || "0") : 0;

            if (includeForCharges && forChargesAmount <= 0) {
                showAlert('warning', 'Please enter a valid amount for FOR charges');
                resetButtonLoadingExclusive(postAllBtn, actionBtns);
                return;
            }

            const data = {
                JournalVoucherId: parseInt(voucherId),
                LocationId: parseInt(locationId),
                DCDate: dcDate,
                Remarks: remarks,
                OtherReference: otherRef,
                IncludeForCharges: includeForCharges,
                ForAmount: forChargesAmount
            };

            PureLedger.post('/DeliveryChallan/ProcessAll', data)
                .done(function (data) {
                    if (data.success) {
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        currentModalData = { voucherId: null, entryId: null };
                        loadFilteredEntries();
                        showAlert('success', data.message || 'Entries processed successfully');
                    } else {
                        showAlert('danger', data.message || 'Error processing entries');
                    }
                })
                .fail(function (xhr, status, error) {
                    console.error('Error processing entries:', error);
                    showAlert('danger', 'Error processing entries: ' + error);
                })
                .always(function () {
                    resetButtonLoadingExclusive(postAllBtn, actionBtns);
                });
        });
    }

    // Add cartage button handler
    if (addCartageBtn) {
        addCartageBtn.addEventListener('click', function () {
            if (addCartageBtn.disabled) return;
            setButtonLoadingExclusive(addCartageBtn, 'Processing...', actionBtns);

            const voucherId = document.getElementById('cartageJournalVoucherId').value;
            const entryId = document.getElementById('cartageEntryId').value;
            const transporterId = document.getElementById('transporterAccount').value;
            const amount = document.getElementById('cartageAmount').value;
            const postToCustomer = document.getElementById('postToCustomer').checked;
            const dcDate = document.getElementById('dcDate').value;
            const dcRemarks = document.getElementById('dcRemarks').value;
            const dcStation = document.getElementById('dcStation')?.value || '';
            const dcOtherRef = document.getElementById('dcOtherRef').value;
            const dcLocation = document.getElementById('dcLocation').value;

            // Get FOR charges data
            const includeForCharges = document.getElementById('includeForCharges').checked;
            const forChargesAmount = includeForCharges ?
                parseInt(document.getElementById('forChargesAmount').value || "0") : 0;

            if (includeForCharges && forChargesAmount <= 0) {
                showAlert('warning', 'Please enter a valid amount for FOR charges');
                resetButtonLoadingExclusive(addCartageBtn, actionBtns);
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
                dcLocation: parseInt(dcLocation),
                includeForCharges: includeForCharges,
                forAmount: forChargesAmount
            };

            PureLedger.post('/DeliveryChallan/AddCartage', data)
                .done(function (result) {
                    if (result.success) {
                        bootstrap.Modal.getInstance(document.getElementById('cartageModal')).hide();
                        bootstrap.Modal.getInstance(document.getElementById('deliveryChallanModal')).hide();
                        document.getElementById('dcJournalEntryId').value = '';
                        document.getElementById('dcJournalVoucherId').value = '';
                        document.getElementById('cartageJournalVoucherId').value = '';
                        document.getElementById('cartageEntryId').value = '';
                        currentModalData = { voucherId: null, entryId: null };
                        loadFilteredEntries();
                        showAlert('success', 'Cartage added and delivery challan processed successfully');
                    } else {
                        showAlert('danger', result.message || 'Error processing cartage');
                    }
                })
                .fail(function (xhr, status, error) {
                    console.error('Error processing cartage:', error);
                    showAlert('danger', 'Error processing cartage: ' + error);
                })
                .always(function () {
                    resetButtonLoadingExclusive(addCartageBtn, actionBtns);
                });
        });
    }

    // Show alert function
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

        // Auto dismiss after 5 seconds
        setTimeout(function () {
            alert.alert('close');
        }, 5000);
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