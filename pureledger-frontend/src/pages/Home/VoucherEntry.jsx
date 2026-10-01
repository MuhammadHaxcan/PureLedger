import React, { useState, useEffect, useRef } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import { Modal, Form, Button, Alert, Badge, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';
import paymentReceiptVoucherApi from '../../services/paymentReceiptVoucherApi';
import { usePermissions } from '../../hooks/usePermissions';
import { normalizePaymentInstrumentType, paymentInstrumentLabel } from '../../constants/paymentInstruments';

const VoucherEntry = () => {
    usePageTitle('Voucher Entry', 'Voucher Entry & Summary');
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const { canAccessReporting } = usePermissions();
    
    // Ref to prevent duplicate submissions
    const isSubmittingRef = useRef(false);
    
    const [voucherId, setVoucherId] = useState(searchParams.get('jvId') || '');
    const [voucher, setVoucher] = useState(null);
    const [paymentReceiptData, setPaymentReceiptData] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [hasSearched, setHasSearched] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [editingEntry, setEditingEntry] = useState(null);
    const [editFormData, setEditFormData] = useState({
        entryId: null,
        projectId: null,
        categoryId: null,
        accountId: null,
        description: '',
        quantity: 0,
        rate: 0,
        discount: 0,
        discountPercent: 0,
        debit: 0,
        credit: 0,
        accountGroupId: 0
    });
    const [editCategories, setEditCategories] = useState([]);
    const [editAccounts, setEditAccounts] = useState([]);
    const [editLocations, setEditLocations] = useState([]);
    const [focusedFields, setFocusedFields] = useState({});
    const [showLocationGroup, setShowLocationGroup] = useState(false);
    const [locationId, setLocationId] = useState(null);

    useEffect(() => {
        const urlVoucherId = searchParams.get('jvId');
        if (urlVoucherId) {
            setVoucherId(urlVoucherId);
            fetchVoucher(urlVoucherId);
        }
    }, [searchParams]);

    useEffect(() => {
        if (editFormData.projectId) {
            loadEditCategories(editFormData.projectId);
        }
    }, [editFormData.projectId]);

    useEffect(() => {
        if (editFormData.categoryId) {
            loadEditAccounts(editFormData.categoryId);
        }
    }, [editFormData.categoryId]);

    const fetchVoucher = async (id) => {
        if (!id || id <= 0) {
            setError('Please enter a valid voucher ID');
            setHasSearched(false);
            return;
        }

        setLoading(true);
        setError('');
        setSuccess('');
        setPaymentReceiptData(null);
        setHasSearched(true);

        try {
            const response = await apiClient.get(`/HomeApi/voucher/${id}`);
            if (response.data.success) {
                const loadedVoucher = response.data.voucher;
                setVoucher(loadedVoucher);
                if (loadedVoucher.hasPaymentReceiptDetails && loadedVoucher.isPaymentReceipt) {
                    const paymentReceiptResponse = await paymentReceiptVoucherApi.getPostedVoucher(id);
                    setPaymentReceiptData(paymentReceiptResponse.data?.data ?? paymentReceiptResponse.data);
                }
                setSuccess('Voucher loaded successfully');
            } else {
                setError(response.data.message || 'Failed to load voucher');
                setVoucher(null);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load voucher');
            setVoucher(null);
            setPaymentReceiptData(null);
        } finally {
            setLoading(false);
        }
    };

    const handleSearch = (e) => {
        e.preventDefault();
        fetchVoucher(voucherId);
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2
        }).format(value);
    };

    const formatDate = value => value ? String(value).slice(0, 10) : 'N/A';

    const handleEditEntry = async (entry) => {
        console.log('handleEditEntry called with entry:', entry);
        const entryId = entry.entryId || entry.journalEntryId;
        console.log('Extracted entryId:', entryId);
        
        if (!entryId) {
            console.error('Entry ID is missing:', entry);
            setError('Entry ID is missing. Cannot edit this entry.');
            return;
        }

        try {
            setLoading(true);
            setError('');
            console.log('Fetching entry details for ID:', entryId);
            const response = await apiClient.get(`/HomeApi/journal-entry/${entryId}`);
            console.log('API response:', response.data);
            
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                console.log('Entry data loaded:', data);
                setEditingEntry(entry);
                setEditFormData({
                    entryId: data.entryId,
                    projectId: data.projectId,
                    categoryId: data.categoryId,
                    accountId: data.accountId,
                    description: data.description || '',
                    quantity: data.quantity || 0,
                    rate: data.rate || 0,
                    discount: data.discount || 0,
                    discountPercent: data.discountPercent || 0,
                    debit: data.debit || 0,
                    credit: data.credit || 0,
                    accountGroupId: data.accountGroupId || 0,
                    deliveryChallanEntryId: data.deliveryChallanEntryId || null
                });
                
                // Show location group if entry has delivery challan
                const hasDeliveryChallan = data.deliveryChallanEntryId != null || entry.hasDeliveryChallan;
                setShowLocationGroup(hasDeliveryChallan);
                setLocationId(data.locationId || entry.locationId || null);
                
                // Load locations if delivery challan exists or if locations are provided
                if (hasDeliveryChallan) {
                    if (data.locations && data.locations.length > 0) {
                        setEditLocations(data.locations);
                    } else {
                        // Load all locations from API
                        await loadEditLocations();
                    }
                }
                
                // Load categories and accounts for the edit form
                if (data.projectId) {
                    await loadEditCategories(data.projectId);
                }
                if (data.categoryId) {
                    await loadEditAccounts(data.categoryId);
                }
                console.log('Opening edit modal');
                setShowEditModal(true);
            } else {
                console.error('Failed to load entry details:', response.data);
                setError('Failed to load entry details');
            }
        } catch (err) {
            console.error('Error loading entry:', err);
            setError(err.response?.data?.message || 'Failed to load entry details');
        } finally {
            setLoading(false);
        }
    };

    const handleDeleteEntry = (entry) => {
        setEditingEntry(entry);
        setShowDeleteModal(true);
    };

    const confirmDeleteEntry = async () => {
        if (!editingEntry) return;

        // Prevent duplicate submissions
        if (isSubmittingRef.current) return;
        isSubmittingRef.current = true;

        try {
            setLoading(true);
            const entryId = editingEntry.entryId || editingEntry.journalEntryId;
            const response = await apiClient.delete(`/HomeApi/journal-entry/${entryId}`);
            if (response.data.success) {
                setSuccess('Entry deleted successfully');
                setShowDeleteModal(false);
                fetchVoucher(voucherId);
            } else {
                setError(response.data.message || 'Failed to delete entry');
            }
        } catch (err) {
            const errorData = err.response?.data;
            setError(errorData?.message || 'Failed to delete entry');
        } finally {
            setLoading(false);
            isSubmittingRef.current = false;
        }
    };

    const loadEditCategories = async (projectId) => {
        try {
            const response = await apiClient.get(`/HomeApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                setEditCategories(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading categories:', err);
        }
    };

    const loadEditAccounts = async (categoryId) => {
        try {
            const response = await apiClient.get(`/HomeApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                setEditAccounts(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading accounts:', err);
        }
    };

    const loadEditLocations = async () => {
        try {
            const response = await apiClient.get('/HomeApi/locations');
            if (response.data.success) {
                setEditLocations(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading locations:', err);
        }
    };

    const handleEditFormSubmit = async (e) => {
        e.preventDefault();
        
        // Prevent duplicate submissions
        if (isSubmittingRef.current) return;
        isSubmittingRef.current = true;
        
        try {
            setLoading(true);
            const submitData = {
                ...editFormData,
                locationId: showLocationGroup ? locationId : null,
                deliveryChallanEntryId: editFormData.deliveryChallanEntryId || null
            };
            const response = await apiClient.put('/HomeApi/journal-entry', submitData);
            if (response.data.success) {
                setSuccess('Entry updated successfully');
                setShowEditModal(false);
                fetchVoucher(voucherId);
            } else {
                setError(response.data.message || 'Failed to update entry');
            }
        } catch (err) {
            const errors = err.response?.data?.errors || [];
            setError(errors.length > 0 ? errors.join(', ') : (err.response?.data?.message || 'Failed to update entry'));
        } finally {
            setLoading(false);
            isSubmittingRef.current = false;
        }
    };

    const handleEditVoucher = () => {
        if (voucher?.isPaymentReceipt && voucher?.hasPaymentReceiptDetails) {
            navigate(`/home/payment-and-receipt?voucherId=${voucher.journalVoucherId}`);
            return;
        }
        if (voucher?.isStockTransferGenerated || voucher?.isStockTransfer) {
            navigate(`/stock-transfers?transferId=${voucher.journalVoucherId}`);
            return;
        }
        navigate(`/home/entry?voucherId=${voucherId}`);
    };

    const isStockTransferVoucher = Boolean(
        voucher?.isStockTransferGenerated || voucher?.isStockTransfer
    );
    const isManualExpenseVoucher = Boolean(voucher?.isManualExpenseVoucher);
    const isPaymentReceiptVoucher = Boolean(voucher?.hasPaymentReceiptDetails && voucher?.isPaymentReceipt);
    const showIndividualEntryActions = !isStockTransferVoucher && !isManualExpenseVoucher && !isPaymentReceiptVoucher;

    const paymentReceiptIntegrityError = voucher?.hasPaymentReceiptDetails && !voucher?.isPaymentReceipt;

    return (
        <div className="container-fluid">
            {success && (
                <div className="alert alert-success alert-dismissible fade show" role="alert">
                    <i className="fas fa-check-circle me-2"></i>{success}
                    <button type="button" className="btn-close" onClick={() => setSuccess('')}></button>
                </div>
            )}

            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            <div className="card shadow mb-3">
                <div className="card-header py-2 d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-search me-2"></i>Voucher Lookup
                    </h6>
                </div>
                <div className="card-body py-2">
                    <form onSubmit={handleSearch}>
                        <div className="row">
                            <div className="col-md-5 mb-3">
                                <Form.Label htmlFor="voucherIdInput">Enter Voucher ID</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-hashtag"></i></InputGroup.Text>
                                    <Form.Control
                                        type="number"
                                        id="voucherIdInput"
                                        className="no-arrow"
                                        min="1"
                                        placeholder="Enter voucher ID to search"
                                        value={voucherId}
                                        onChange={(e) => {
                                            setVoucherId(e.target.value);
                                        }}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>&nbsp;</Form.Label>
                                <div className="d-grid">
                                    <Button type="submit" variant="primary" disabled={loading}>
                                        {loading ? (
                                            <>
                                                <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                                                Loading...
                                            </>
                                        ) : (
                                            <>
                                                <i className="fas fa-search me-1"></i> Fetch Voucher
                                            </>
                                        )}
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </form>
                </div>
            </div>

            {!voucher && !loading && hasSearched && voucherId && (
                <div className="alert alert-warning">
                    <i className="fas fa-exclamation-triangle me-2"></i>No voucher found for the entered ID.
                </div>
            )}

            {paymentReceiptIntegrityError && (
                <Alert variant="danger">
                    <i className="fas fa-shield-alt me-2"></i>
                    <strong>Payment &amp; Receipt integrity error.</strong>{' '}
                    {voucher.paymentReceiptIntegrityError || 'This voucher has partial instrument linkage and cannot be edited through any voucher flow.'}
                </Alert>
            )}

            {isPaymentReceiptVoucher && paymentReceiptData && (
                <div className="card shadow mb-3">
                    <div className="card-header py-2 d-flex flex-wrap justify-content-between align-items-center gap-2">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-list me-2"></i>Voucher Summary - JV-{voucher.journalVoucherId}
                        </h6>
                        <div className="d-flex flex-wrap align-items-center gap-2">
                            <Badge bg="info">P&amp;R Voucher</Badge>
                            {canAccessReporting && paymentReceiptData.isCompleted && !paymentReceiptData.pendingApprovalId && (
                                <a
                                    className="btn btn-outline-primary btn-sm"
                                    href={`/reporting/print-voucher?voucherId=${voucher.journalVoucherId}`}
                                    target="_blank"
                                    rel="noreferrer"
                                >
                                    <i className="fas fa-print me-1"></i>Print P&amp;R
                                </a>
                            )}
                            <Button
                                variant="primary"
                                size="sm"
                                onClick={handleEditVoucher}
                                disabled={!paymentReceiptData.isCompleted || Boolean(paymentReceiptData.pendingApprovalId)}
                                title={paymentReceiptData.pendingApprovalId
                                    ? `Revision pending as approval #${paymentReceiptData.pendingApprovalId}`
                                    : ''}
                            >
                                <i className="fas fa-edit me-1"></i>Edit Entire Voucher
                            </Button>
                        </div>
                    </div>
                    <div className="card-body">
                        {paymentReceiptData.pendingApprovalId && (
                            <Alert variant="warning">
                                <Badge bg="warning" text="dark" className="me-2">P&amp;R Voucher</Badge>
                                This complete voucher is pending as approval #{paymentReceiptData.pendingApprovalId} and is read-only until that request is decided.
                            </Alert>
                        )}

                        <div className="row mb-2">
                            <div className="col-md-4">
                                <strong>Project:</strong> {paymentReceiptData.projectName || voucher.projectName || 'N/A'}
                            </div>
                            <div className="col-md-4">
                                <strong>Entry Date:</strong> {formatDate(paymentReceiptData.entryDate || voucher.entryDate)}
                            </div>
                            <div className="col-md-4">
                                <strong>Status:</strong> {paymentReceiptData.isCompleted ? 'Completed' : 'Pending Approval'}
                            </div>
                        </div>
                        <div className="row mb-3">
                            <div className="col-md-12">
                                <strong>Total Debit:</strong> {formatCurrency(paymentReceiptData.totalDebit || 0)}
                                <span className="mx-2">|</span>
                                <strong>Total Credit:</strong> {formatCurrency(paymentReceiptData.totalCredit || 0)}
                            </div>
                        </div>

                        <div className="table-responsive">
                            <table className="table table-bordered table-hover">
                                <thead className="table-primary">
                                    <tr>
                                        <th>Category</th>
                                        <th>Account</th>
                                        <th>Description</th>
                                        <th>Instrument</th>
                                        <th>Reference/Cheque No.</th>
                                        <th>Instrument Date</th>
                                        <th className="text-end">Debit</th>
                                        <th className="text-end">Credit</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {(paymentReceiptData.entries || []).map((entry, index) => {
                                        const instrumentType = normalizePaymentInstrumentType(entry.instrumentType);
                                        return (
                                            <tr key={entry.originalJournalEntryId ?? entry.journalEntryId ?? index}>
                                                <td>{entry.categoryName || 'N/A'}</td>
                                                <td>{entry.accountName || 'N/A'}</td>
                                                <td style={{ overflowWrap: 'anywhere', whiteSpace: 'normal', maxWidth: 260 }}>{entry.description || '-'}</td>
                                                <td>{paymentInstrumentLabel(instrumentType)}</td>
                                                <td>{instrumentType === 'Cash' ? '' : entry.referenceNumber || ''}</td>
                                                <td>{instrumentType === 'Cash' ? '' : formatDate(entry.instrumentDate)}</td>
                                                <td className="text-end">{formatCurrency(entry.debit || 0)}</td>
                                                <td className="text-end">{formatCurrency(entry.credit || 0)}</td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot className="table-secondary">
                                    <tr className="fw-bold">
                                        <td colSpan={6} className="text-end">Total</td>
                                        <td className="text-end">{formatCurrency(paymentReceiptData.totalDebit || 0)}</td>
                                        <td className="text-end">{formatCurrency(paymentReceiptData.totalCredit || 0)}</td>
                                    </tr>
                                </tfoot>
                            </table>
                        </div>
                    </div>
                </div>
            )}

            {voucher && !voucher.hasPaymentReceiptDetails && (
                <div className="card shadow mb-3">
                    <div className="card-header py-2 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-list me-2"></i>Voucher Summary - JV-{voucher.journalVoucherId}
                        </h6>
                        <Button
                            variant={isStockTransferVoucher ? 'outline-primary' : 'primary'}
                            size="sm"
                            onClick={handleEditVoucher}
                            disabled={isManualExpenseVoucher && voucher.isPendingApproval}
                        >
                            <i className={`fas ${isStockTransferVoucher ? 'fa-truck-loading' : 'fa-edit'} me-1`}></i>
                            {isStockTransferVoucher ? 'Open Stock Transfer' : 'Edit Entire Voucher'}
                        </Button>
                    </div>
                    <div className="card-body">
                        {isStockTransferVoucher && (
                            <Alert variant="info">
                                <i className="fas fa-lock me-2"></i>
                                This voucher belongs to a stock-transfer graph and is read-only here.
                                Use <Link className="alert-link ms-1" to={`/stock-transfers?transferId=${voucher.journalVoucherId}`}>
                                    Stock Transfers
                                </Link> to receive, close short, delete the complete transfer, or edit its pending dispatch.
                            </Alert>
                        )}
                        {isManualExpenseVoucher && (
                            <Alert variant="warning">
                                <div className="d-flex flex-wrap align-items-center gap-2">
                                    <Badge bg="warning" text="dark">Expense Voucher</Badge>
                                    <span>
                                        {voucher.isPendingApproval
                                            ? 'This complete Expense voucher already has a pending revision and is read-only until that request is decided.'
                                            : 'Expense vouchers are edited only as a complete balanced voucher. Individual line edit and delete actions are unavailable.'}
                                    </span>
                                </div>
                            </Alert>
                        )}
                        <div className="row mb-2">
                            <div className="col-md-3">
                                <strong>Project:</strong> {voucher.projectName || 'N/A'}
                            </div>
                            <div className="col-md-3">
                                <strong>Entry Date:</strong> {voucher.entryDate}
                            </div>
                            <div className="col-md-3">
                                <strong>Creditable Date:</strong> {voucher.creditableDate}
                            </div>
                        </div>
                        <div className="row mb-3">
                            <div className="col-md-12">
                                <strong>Total Debit:</strong> {formatCurrency(voucher.totalDebit || 0)}
                                <span className="ms-2 me-2">|</span>
                                <strong>Total Credit:</strong> {formatCurrency(voucher.totalCredit || 0)}
                                <span className="ms-2 me-2">|</span>
                                <strong>Balance:</strong> {formatCurrency(Number(voucher.totalDebit || 0) - Number(voucher.totalCredit || 0))}
                            </div>
                        </div>

                        <div className="table-responsive">
                            <table className="table table-bordered table-hover">
                                <thead className="table-primary">
                                    <tr>
                                        <th>Category</th>
                                        <th>Account</th>
                                        <th>Description</th>
                                        {!isManualExpenseVoucher && <th>Quantity</th>}
                                        {!isManualExpenseVoucher && <th>Rate</th>}
                                        {!isManualExpenseVoucher && <th>Discount</th>}
                                        <th>Debit</th>
                                        <th>Credit</th>
                                        {showIndividualEntryActions && <th className="text-center">Actions</th>}
                                    </tr>
                                </thead>
                                <tbody>
                                    {voucher.entries && voucher.entries.length > 0 ? (
                                        voucher.entries.map((entry, index) => {
                                            const entryId = entry.entryId || entry.journalEntryId;
                                            return (
                                                <tr key={entryId || index}>
                                                    <td>{entry.categoryName || 'N/A'}</td>
                                                    <td>{entry.accountName || 'N/A'}</td>
                                                    <td style={{ wordWrap: 'break-word', whiteSpace: 'normal', maxWidth: '200px' }}>{entry.description || '-'}</td>
                                                    {!isManualExpenseVoucher && <td>{entry.quantity || 0}</td>}
                                                    {!isManualExpenseVoucher && <td>{formatCurrency(entry.rate || 0)}</td>}
                                                    {!isManualExpenseVoucher && <td>{formatCurrency(entry.discount || 0)}</td>}
                                                    <td>{formatCurrency(entry.debit || 0)}</td>
                                                    <td>{formatCurrency(entry.credit || 0)}</td>
                                                    {showIndividualEntryActions && (
                                                        <td className="text-center">
                                                            <Button
                                                                variant="outline-primary"
                                                                size="sm"
                                                                className="me-1"
                                                                onClick={() => handleEditEntry(entry)}
                                                                title="Edit Entry"
                                                                disabled={!entryId}
                                                            >
                                                                <i className="fas fa-edit"></i>
                                                            </Button>
                                                            <Button
                                                                variant="outline-danger"
                                                                size="sm"
                                                                onClick={() => handleDeleteEntry(entry)}
                                                                title="Delete Entry"
                                                                disabled={!entryId}
                                                            >
                                                                <i className="fas fa-trash"></i>
                                                            </Button>
                                                        </td>
                                                    )}
                                                </tr>
                                            );
                                        })
                                    ) : (
                                        <tr>
                                            <td
                                                colSpan={isManualExpenseVoucher ? 5 : (showIndividualEntryActions ? 9 : 8)}
                                                className="text-center"
                                            >
                                                No entries found
                                            </td>
                                        </tr>
                                    )}
                                </tbody>
                                {isManualExpenseVoucher && voucher.entries?.length > 0 && (
                                    <tfoot className="table-secondary">
                                        <tr className="fw-bold">
                                            <td colSpan={3} className="text-end">Total</td>
                                            <td>{formatCurrency(voucher.totalDebit || 0)}</td>
                                            <td>{formatCurrency(voucher.totalCredit || 0)}</td>
                                        </tr>
                                    </tfoot>
                                )}
                            </table>
                        </div>
                    </div>
                </div>
            )}

            {/* Edit Entry Modal */}
            <Modal show={showEditModal} onHide={() => setShowEditModal(false)} size="lg">
                <Modal.Header closeButton>
                    <Modal.Title>
                        <i className="fas fa-edit me-2"></i>Edit Journal Entry
                    </Modal.Title>
                </Modal.Header>
                <Form onSubmit={handleEditFormSubmit}>
                    <Modal.Body>
                        <input type="hidden" value={editFormData.entryId || ''} />
                        <input type="hidden" value={editFormData.projectId || ''} />
                        
                        <div className="row mb-3">
                            <div className="col-md-6">
                                <Form.Label>Category</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={editFormData.categoryId}
                                        onChange={(value) => setEditFormData({ ...editFormData, categoryId: value ? parseInt(value) : null, accountId: null })}
                                        options={editCategories.map(c => ({ id: c.id, name: c.name }))}
                                        placeholder="Select Category"
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-6">
                                <Form.Label>Account</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-user"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={editFormData.accountId}
                                        onChange={(value) => setEditFormData({ ...editFormData, accountId: value ? parseInt(value) : null })}
                                        options={editAccounts.map(a => ({ id: a.id, name: a.name }))}
                                        placeholder="Select Account"
                                        disabled={!editFormData.categoryId}
                                    />
                                </InputGroup>
                            </div>
                        </div>

                        <div className="row mb-3">
                            <div className="col-md-12">
                                <Form.Label>Description</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editFormData.description}
                                    onChange={(e) => setEditFormData({ ...editFormData, description: e.target.value })}
                                    placeholder="Enter description"
                                />
                            </div>
                        </div>

                        <div className="row mb-3">
                            <div className="col-md-3">
                                <Form.Label>Quantity</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={editFormData.quantity}
                                        onChange={(e) => setEditFormData({ ...editFormData, quantity: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        step="0.01"
                                    />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>Rate</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={focusedFields.rate ? (editFormData.rate ?? '') : (editFormData.rate !== null && editFormData.rate !== undefined ? parseFloat(editFormData.rate).toFixed(4) : '')}
                                        onChange={(e) => {
                                            const inputValue = e.target.value;
                                            setEditFormData({ ...editFormData, rate: inputValue === '' ? 0 : parseFloat(inputValue) || 0 });
                                        }}
                                        min="0"
                                        step="0.0001"
                                        onFocus={() => setFocusedFields(prev => ({ ...prev, rate: true }))}
                                        onBlur={(e) => {
                                            setFocusedFields(prev => {
                                                const next = { ...prev };
                                                delete next.rate;
                                                return next;
                                            });
                                            if (e.target.value) {
                                                setEditFormData(prev => ({ ...prev, rate: parseFloat(parseFloat(e.target.value).toFixed(4)) }));
                                            }
                                        }}
                                    />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>Discount</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={focusedFields.discount ? (editFormData.discount ?? '') : (editFormData.discount !== null && editFormData.discount !== undefined ? parseFloat(editFormData.discount).toFixed(2) : '')}
                                        onChange={(e) => {
                                            const inputValue = e.target.value;
                                            setEditFormData({ ...editFormData, discount: inputValue === '' ? 0 : parseFloat(inputValue) || 0 });
                                        }}
                                        min="0"
                                        step="0.01"
                                        onFocus={() => setFocusedFields(prev => ({ ...prev, discount: true }))}
                                        onBlur={(e) => {
                                            setFocusedFields(prev => {
                                                const next = { ...prev };
                                                delete next.discount;
                                                return next;
                                            });
                                            if (e.target.value) {
                                                setEditFormData(prev => ({ ...prev, discount: parseFloat(parseFloat(e.target.value).toFixed(2)) }));
                                            }
                                        }}
                                    />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>Discount %</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={focusedFields.discountPercent ? (editFormData.discountPercent ?? '') : (editFormData.discountPercent !== null && editFormData.discountPercent !== undefined ? parseFloat(editFormData.discountPercent).toFixed(2) : '')}
                                        onChange={(e) => {
                                            const inputValue = e.target.value;
                                            setEditFormData({ ...editFormData, discountPercent: inputValue === '' ? 0 : parseFloat(inputValue) || 0 });
                                        }}
                                        min="0"
                                        max="100"
                                        step="0.01"
                                        onFocus={() => setFocusedFields(prev => ({ ...prev, discountPercent: true }))}
                                        onBlur={(e) => {
                                            setFocusedFields(prev => {
                                                const next = { ...prev };
                                                delete next.discountPercent;
                                                return next;
                                            });
                                            if (e.target.value) {
                                                setEditFormData(prev => ({ ...prev, discountPercent: parseFloat(parseFloat(e.target.value).toFixed(2)) }));
                                            }
                                        }}
                                    />
                            </div>
                        </div>

                        <div className="row mb-3">
                            <div className="col-md-6">
                                <Form.Label>Debit</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={focusedFields.debit ? (editFormData.debit ?? '') : (editFormData.debit !== null && editFormData.debit !== undefined ? parseFloat(editFormData.debit).toFixed(2) : '')}
                                        onChange={(e) => {
                                            const inputValue = e.target.value;
                                            setEditFormData({ ...editFormData, debit: inputValue === '' ? 0 : parseFloat(inputValue) || 0 });
                                        }}
                                        min="0"
                                        step="0.01"
                                        onFocus={() => setFocusedFields(prev => ({ ...prev, debit: true }))}
                                        onBlur={(e) => {
                                            setFocusedFields(prev => {
                                                const next = { ...prev };
                                                delete next.debit;
                                                return next;
                                            });
                                            if (e.target.value) {
                                                setEditFormData(prev => ({ ...prev, debit: parseFloat(parseFloat(e.target.value).toFixed(2)) }));
                                            }
                                        }}
                                    />
                            </div>
                            <div className="col-md-6">
                                <Form.Label>Credit</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={focusedFields.credit ? (editFormData.credit ?? '') : (editFormData.credit !== null && editFormData.credit !== undefined ? parseFloat(editFormData.credit).toFixed(2) : '')}
                                        onChange={(e) => {
                                            const inputValue = e.target.value;
                                            setEditFormData({ ...editFormData, credit: inputValue === '' ? 0 : parseFloat(inputValue) || 0 });
                                        }}
                                        min="0"
                                        step="0.01"
                                        onFocus={() => setFocusedFields(prev => ({ ...prev, credit: true }))}
                                        onBlur={(e) => {
                                            setFocusedFields(prev => {
                                                const next = { ...prev };
                                                delete next.credit;
                                                return next;
                                            });
                                            if (e.target.value) {
                                                setEditFormData(prev => ({ ...prev, credit: parseFloat(parseFloat(e.target.value).toFixed(2)) }));
                                            }
                                        }}
                                    />
                            </div>
                        </div>

                        {showLocationGroup && (
                            <div className="row mb-3">
                                <div className="col-md-12">
                                    <Form.Label>Location</Form.Label>
                                    <Form.Select
                                        value={locationId || ''}
                                        onChange={(e) => setLocationId(e.target.value ? parseInt(e.target.value) : null)}
                                    >
                                        <option value="">Select Location</option>
                                        {editLocations.map(loc => (
                                            <option key={loc.locationId} value={loc.locationId}>
                                                {loc.locationName}
                                            </option>
                                        ))}
                                    </Form.Select>
                                </div>
                            </div>
                        )}
                    </Modal.Body>
                    <Modal.Footer>
                        <Button variant="secondary" onClick={() => setShowEditModal(false)}>
                            <i className="fas fa-times me-1"></i> Cancel
                        </Button>
                        <Button variant="primary" type="submit" disabled={loading}>
                            {loading ? <LoadingSpinner /> : <><i className="fas fa-save me-1"></i> Save Changes</>}
                        </Button>
                    </Modal.Footer>
                </Form>
            </Modal>

            {/* Delete Confirmation Modal */}
            <ConfirmModal
                show={showDeleteModal}
                onHide={() => setShowDeleteModal(false)}
                onConfirm={confirmDeleteEntry}
                title="Delete Entry"
                message={`Are you sure you want to delete this entry? This action cannot be undone.`}
            />
        </div>
    );
};

export default VoucherEntry;

