document.addEventListener('DOMContentLoaded', function () {
    initializeEventListeners();
    setDefaultDates();
});

// Track the currently open modal data
let currentModalData = {
    voucherId: null,
    entryId: null
};

// Track FOR charges availability
let forChargesAvailable = false;

// ==================== INITIALIZATION ====================

function initializeEventListeners() {
    const form = document.getElementById('deliveryChallanFilterForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            loadFilteredEntries();
        });
    }

    const projectSelect = document.getElementById('projectSelect');
    const categorySelect = document.getElementById('categorySelect');

    if (projectSelect) {
        projectSelect.addEventListener('change', handleProjectChange);
    }

    if (categorySelect) {
        categorySelect.addEventListener('change', handleCategoryChange);
    }

    initSearchFunctionality();
    setupModalListeners();
    setupActionButtons();

    // Check FOR charges if project already selected
    if (projectSelect && projectSelect.value) {
        checkForChargesAvailability(projectSelect.value);
    } else {
        updateForChargesState(false);
    }

    // Initialize tooltips
    initializeTooltips();
}

function setDefaultDates() {
    const sinceDate = document.getElementById('sinceDate');
    const toDate = document.getElementById('toDate');

    if (!sinceDate.value) {
        const thirtyDaysAgo = new Date();
        thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
        sinceDate.value = thirtyDaysAgo.toISOString().split('T')[0];
    }

    if (!toDate.value) {
        const today = new Date();
        toDate.value = today.toISOString().split('T')[0];
    }
}

function initializeTooltips() {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
}

// ==================== EVENT HANDLERS ====================

function handleProjectChange() {
    const projectSelect = document.getElementById('projectSelect');
    const categorySelect = document.getElementById('categorySelect');
    const accountSelect = document.getElementById('accountSelect');

    if (!projectSelect || !categorySelect) return;

    if (projectSelect.value) {
        resetSelect(categorySelect, 'All Categories', false);
        resetSelect(accountSelect, 'All Accounts', true);
        fetchCategories(projectSelect.value);
        checkForChargesAvailability(projectSelect.value);
    } else {
        resetSelect(categorySelect, 'All Categories', true);
        resetSelect(accountSelect, 'All Accounts', true);
        updateForChargesState(false);
    }
}

function handleCategoryChange() {
    const categorySelect = document.getElementById('categorySelect');
    const accountSelect = document.getElementById('accountSelect');

    if (!categorySelect || !accountSelect) return;

    if (categorySelect.value) {
        resetSelect(accountSelect, 'All Accounts', false);
        fetchAccounts(categorySelect.value);
    } else {
        resetSelect(accountSelect, 'All Accounts', true);
    }
}

// ==================== DATA LOADING ====================

function fetchCategories(projectId) {
    const categorySelect = document.getElementById('categorySelect');

    PureLedger.get('/DeliveryChallan/GetCategoriesByProject', { projectId: projectId })
        .done(function (data) {
            categorySelect.disabled = false;
            categorySelect.innerHTML = '<option value="">All Categories</option>';

            if (data && data.length > 0) {
                data.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.id;
                    option.textContent = category.name;
                    categorySelect.appendChild(option);
                });
            }
        })
        .fail(function (xhr, status, error) {
            console.error('Error loading categories:', error);
            showAlert('danger', 'Error loading categories. Please try again.');
            resetSelect(categorySelect, 'All Categories', true);
        });
}

function fetchAccounts(categoryId) {
    const accountSelect = document.getElementById('accountSelect');

    PureLedger.get('/DeliveryChallan/GetAccountsByCategory', { categoryId: categoryId })
        .done(function (data) {
            accountSelect.disabled = false;
            accountSelect.innerHTML = '<option value="">All Accounts</option>';

            if (data && data.length > 0) {
                data.forEach(account => {
                    const option = document.createElement('option');
                    option.value = account.id;
                    option.textContent = account.name;
                    accountSelect.appendChild(option);
                });
            }
        })
        .fail(function (xhr, status, error) {
            console.error('Error loading accounts:', error);
            showAlert('danger', 'Error loading accounts. Please try again.');
            resetSelect(accountSelect, 'All Accounts', true);
        });
}

function loadFilteredEntries() {
    const projectSelect = document.getElementById('projectSelect');
    const resultsTable = document.getElementById('resultsTable');
    const reportResults = document.getElementById('reportResults');
    const searchInput = document.getElementById('searchInput');

    // Check if project is selected
    if (!projectSelect.value) {
        showAlert('warning', 'Please select a project to generate the report.');
        return;
    }

    // Clear search input
    if (searchInput) {
        searchInput.value = '';
    }

    // Show results section
    if (reportResults) {
        reportResults.style.display = 'block';
    }

    // Show loading state
    resultsTable.innerHTML = '<tr><td colspan="6" class="text-center"><div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div></td></tr>';

    const params = {
        SinceDate: document.getElementById('sinceDate').value,
        ToDate: document.getElementById('toDate').value,
        ProjectId: projectSelect.value
    };

    const categorySelect = document.getElementById('categorySelect');
    const accountSelect = document.getElementById('accountSelect');

    if (categorySelect && categorySelect.value) params.CategoryId = categorySelect.value;
    if (accountSelect && accountSelect.value) params.AccountId = accountSelect.value;

    PureLedger.get('/DeliveryChallan/GetFilteredEntries', params)
        .done(function (data) {
            renderResults(data);
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

// ==================== RENDERING ====================

function renderResults(data) {
    const resultsTable = document.getElementById('resultsTable');
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

// ==================== SEARCH FUNCTIONALITY ====================

function initSearchFunctionality() {
    const searchInput = document.getElementById('searchInput');
    if (searchInput) {
        searchInput.addEventListener('input', debounce(function () {
            filterResults(this.value.toLowerCase());
        }, 250));
    }
}

function filterResults(searchTerm) {
    const resultsTable = document.getElementById('resultsTable');
    if (!resultsTable) return;

    const rows = Array.from(resultsTable.getElementsByTagName('tr'));
    let hasVisibleRows = false;

    rows.forEach(row => {
        const isVisible = row.textContent.toLowerCase().includes(searchTerm);
        row.style.display = isVisible ? '' : 'none';
        if (isVisible) hasVisibleRows = true;
    });

    if (!hasVisibleRows && rows.length > 0 && searchTerm.length > 0) {
        const noResultsRow = document.createElement('tr');
        noResultsRow.id = 'noSearchResults';
        noResultsRow.innerHTML = '<td colspan="6" class="text-center">No matching entries found</td>';

        // Remove existing no results row if present
        const existing = document.getElementById('noSearchResults');
        if (existing) existing.remove();

        resultsTable.appendChild(noResultsRow);
    } else {
        const existing = document.getElementById('noSearchResults');
        if (existing) existing.remove();
    }
}

function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func.apply(this, args), wait);
    };
}

// ==================== MODAL HANDLERS ====================

function setupModalListeners() {
    const deliveryChallanModal = document.getElementById('deliveryChallanModal');
    if (deliveryChallanModal) {
        deliveryChallanModal.addEventListener('hidden.bs.modal', function () {
            document.getElementById('dcJournalVoucherId').value = '';
            document.getElementById('dcJournalEntryId').value = '';
            document.getElementById('dcChallanId').value = '';
            currentModalData = { voucherId: null, entryId: null };
        });
    }

    // FOR charges checkbox handler
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
}

window.openDeliveryChallanModal = function (voucherId, entryId) {
    console.log(`Opening modal for voucher: ${voucherId}, entry: ${entryId}`);

    currentModalData = {
        voucherId: voucherId,
        entryId: entryId
    };

    // Reset and set form values
    const voucherIdField = document.getElementById('dcJournalVoucherId');
    const entryIdField = document.getElementById('dcJournalEntryId');

    voucherIdField.value = '';
    entryIdField.value = '';

    voucherIdField.value = voucherId;
    if (entryId) {
        entryIdField.value = entryId;
    }

    const allEntriesView = document.getElementById('allEntriesView');
    if (allEntriesView) {
        allEntriesView.innerHTML = '';
        allEntriesView.classList.add('d-none');
    }

    loadLocations();

    const includeForChargesCheckbox = document.getElementById('includeForCharges');
    const forChargesContainer = document.getElementById('forChargesContainer');
    const forChargesAmount = document.getElementById('forChargesAmount');
    if (includeForChargesCheckbox) {
        includeForChargesCheckbox.checked = false;
        includeForChargesCheckbox.disabled = !forChargesAvailable; // enabled/disabled stays tied to project config
    }
    if (forChargesContainer) {
        forChargesContainer.classList.add('d-none');
    }
    if (forChargesAmount) {
        forChargesAmount.value = "0";
    }

    // Check for existing challan
    PureLedger.get('/DeliveryChallan/CheckExistingChallan', { voucherId: voucherId })
        .done(function (data) {
            const challanId = document.getElementById('dcChallanId');
            if (data.exists) {
                document.getElementById('existingChallanMessage').classList.remove('d-none');
                challanId.value = data.challanId;

                let challanDate = '';
                if (data.challan.challanDate) {
                    challanDate = formatDateForInput(data.challan.challanDate);
                }

                document.getElementById('dcDate').value = challanDate || new Date().toISOString().split('T')[0];
                // Do not pre-select FOR or driver when DC already exists
                document.getElementById('dcRemarks').value = '';
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
            openCartageModal(currentModalData.voucherId, currentModalData.entryId);
        };
        modalFooter.insertBefore(cartageBtn, modalFooter.firstChild);
    } else {
        document.getElementById('openCartageBtn').onclick = function () {
            openCartageModal(currentModalData.voucherId, currentModalData.entryId);
        };
    }

    const modal = new bootstrap.Modal(document.getElementById('deliveryChallanModal'));
    modal.show();
};

function openCartageModal(voucherId, entryId) {
    console.log(`Opening cartage modal for voucher: ${voucherId}, entry: ${entryId}`);

    const cartageVoucherId = document.getElementById('cartageJournalVoucherId');
    const cartageEntryId = document.getElementById('cartageEntryId');

    cartageVoucherId.value = '';
    cartageEntryId.value = '';

    cartageVoucherId.value = voucherId;

    const selectedEntryId = document.getElementById('dcJournalEntryId').value || entryId;
    if (selectedEntryId) {
        cartageEntryId.value = selectedEntryId;
    }

    // Explicitly reset transporter selection and amount so nothing is pre-selected
    const transporterSelect = document.getElementById('transporterAccount');
    if (transporterSelect) {
        transporterSelect.innerHTML = '<option value="">Select Transporter</option>';
        transporterSelect.value = '';
    }
    const cartageAmount = document.getElementById('cartageAmount');
    if (cartageAmount) {
        cartageAmount.value = '';
    }

    loadTransporters();

    const cartageModal = new bootstrap.Modal(document.getElementById('cartageModal'));
    cartageModal.show();
}

// ==================== ACTION BUTTONS ====================

function setupActionButtons() {
    const postSingleBtn = document.getElementById('postSingleBtn');
    const postAllBtn = document.getElementById('postAllBtn');
    const addCartageBtn = document.getElementById('addCartageBtn');
    const actionBtns = [postSingleBtn, postAllBtn, addCartageBtn];

    if (postSingleBtn) {
        postSingleBtn.addEventListener('click', function () {
            if (postSingleBtn.disabled) return;
            setButtonLoadingExclusive(postSingleBtn, 'Posting...', actionBtns);
            postSingleEntry(actionBtns);
        });
    }

    if (postAllBtn) {
        postAllBtn.addEventListener('click', function () {
            if (postAllBtn.disabled) return;
            setButtonLoadingExclusive(postAllBtn, 'Posting...', actionBtns);
            postAllEntries(actionBtns);
        });
    }

    if (addCartageBtn) {
        addCartageBtn.addEventListener('click', function () {
            if (addCartageBtn.disabled) return;
            setButtonLoadingExclusive(addCartageBtn, 'Processing...', actionBtns);
            addCartage(actionBtns);
        });
    }
}

function postSingleEntry(actionBtns) {
    const postSingleBtn = document.getElementById('postSingleBtn');
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

    const includeForCharges = document.getElementById('includeForCharges').checked && forChargesAvailable;
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
}

function postAllEntries(actionBtns) {
    const postAllBtn = document.getElementById('postAllBtn');
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

    const includeForCharges = document.getElementById('includeForCharges').checked && forChargesAvailable;
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
}

function addCartage(actionBtns) {
    const addCartageBtn = document.getElementById('addCartageBtn');
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

    const includeForCharges = document.getElementById('includeForCharges').checked && forChargesAvailable;
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
}

// ==================== FOR CHARGES ====================

function checkForChargesAvailability(projectId) {
    PureLedger.get('/DeliveryChallan/CheckForChargesAvailable', { projectId: projectId })
        .done(function (data) {
            updateForChargesState(data.available);
        })
        .fail(function (xhr, status, error) {
            console.error('Error checking FOR charges availability:', error);
            updateForChargesState(false);
        });
}

function updateForChargesState(isAvailable) {
    forChargesAvailable = isAvailable;

    const forChargesCheckboxes = document.querySelectorAll('#includeForCharges');
    forChargesCheckboxes.forEach(checkbox => {
        checkbox.disabled = !isAvailable;

        if (!isAvailable) {
            checkbox.checked = false;
            const forChargesContainer = document.getElementById('forChargesContainer');
            if (forChargesContainer) {
                forChargesContainer.classList.add('d-none');
            }
            const forChargesAmount = document.getElementById('forChargesAmount');
            if (forChargesAmount) {
                forChargesAmount.value = "0";
            }
        }

        if (!isAvailable) {
            checkbox.setAttribute('title', 'FOR charges are not available for this project. Check project settings.');

            if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                new bootstrap.Tooltip(checkbox);
            }
        } else {
            checkbox.setAttribute('title', '');
            if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                const tooltipInstance = bootstrap.Tooltip.getInstance(checkbox);
                if (tooltipInstance) {
                    tooltipInstance.dispose();
                }
            }
        }
    });
}

// ==================== UTILITY FUNCTIONS ====================

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

function loadTransporters() {
    const transporterSelect = document.getElementById('transporterAccount');
    transporterSelect.innerHTML = '<option value="">Select Transporter</option>';

    const projectSelect = document.getElementById('projectSelect');
    const projectId = projectSelect && projectSelect.value ? projectSelect.value : null;

    PureLedger.get('/DeliveryChallan/GetTransporterCategoryId', { projectId: projectId })
        .done(function (data) {
            const transporterCategoryId = data.value;

            PureLedger.get('/DeliveryChallan/GetAccountsByCategory', { categoryId: transporterCategoryId })
                .done(function (transporters) {
                    transporters.forEach(t => {
                        const option = document.createElement('option');
                        option.value = t.id;
                        option.textContent = t.name;
                        transporterSelect.appendChild(option);
                    });
                    // Ensure no transporter is pre-selected
                    transporterSelect.value = '';
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

function resetSelect(select, defaultText, disable = false) {
    if (!select) return;

    select.innerHTML = `<option value="">${defaultText}</option>`;
    select.disabled = disable;
}

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

    setTimeout(function () {
        alert.alert('close');
    }, 5000);
}