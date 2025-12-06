import React, { useState, useEffect, useRef } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import { Modal, Form, Button, Alert, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';

const VoucherEntry = () => {
    usePageTitle('Voucher Entry');
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    
    // Ref to prevent duplicate submissions
    const isSubmittingRef = useRef(false);
    
    const [voucherId, setVoucherId] = useState(searchParams.get('jvId') || '');
    const [voucher, setVoucher] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
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
            return;
        }

        setLoading(true);
        setError('');
        setSuccess('');

        try {
            const response = await apiClient.get(`/HomeApi/voucher/${id}`);
            if (response.data.success) {
                setVoucher(response.data.voucher);
                setSuccess('Voucher loaded successfully');
            } else {
                setError(response.data.message || 'Failed to load voucher');
                setVoucher(null);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load voucher');
            setVoucher(null);
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
                    accountGroupId: data.accountGroupId || 0
                });
                setShowLocationGroup(data.accountGroupId === 2 && data.locationId != null);
                setLocationId(data.locationId || null);
                if (data.locations) {
                    setEditLocations(data.locations);
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
            setError(err.response?.data?.message || 'Failed to delete entry');
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

    const handleEditFormSubmit = async (e) => {
        e.preventDefault();
        
        // Prevent duplicate submissions
        if (isSubmittingRef.current) return;
        isSubmittingRef.current = true;
        
        try {
            setLoading(true);
            const submitData = {
                ...editFormData,
                locationId: showLocationGroup ? locationId : null
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
        navigate(`/home/entry?voucherId=${voucherId}`);
    };

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2><i className="fas fa-file-invoice me-2"></i>Voucher Entry & Summary</h2>
            </div>

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
                                        onChange={(e) => setVoucherId(e.target.value)}
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

            {!voucher && !loading && voucherId && (
                <div className="alert alert-warning">
                    <i className="fas fa-exclamation-triangle me-2"></i>No voucher found for the entered ID.
                </div>
            )}

            {voucher && (
                <div className="card shadow mb-3">
                    <div className="card-header py-2 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-list me-2"></i>Voucher Summary - JV-{voucher.journalVoucherId}
                        </h6>
                        <Button variant="primary" size="sm" onClick={handleEditVoucher}>
                            <i className="fas fa-edit me-1"></i> Edit Entire Voucher
                        </Button>
                    </div>
                    <div className="card-body">
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
                                <strong>Total Debit:</strong> {formatCurrency(voucher.totalDebit || 0)} <span className="ms-2 me-2">|</span> <strong>Total Credit:</strong> {formatCurrency(voucher.totalCredit || 0)}
                            </div>
                        </div>

                        <div className="table-responsive">
                            <table className="table table-bordered table-hover">
                                <thead className="table-primary">
                                    <tr>
                                        <th>Category</th>
                                        <th>Account</th>
                                        <th>Description</th>
                                        <th>Quantity</th>
                                        <th>Rate</th>
                                        <th>Discount</th>
                                        <th>Debit</th>
                                        <th>Credit</th>
                                        <th className="text-center">Actions</th>
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
                                                    <td>{entry.quantity || 0}</td>
                                                    <td>{formatCurrency(entry.rate || 0)}</td>
                                                    <td>{formatCurrency(entry.discount || 0)}</td>
                                                    <td>{formatCurrency(entry.debit || 0)}</td>
                                                    <td>{formatCurrency(entry.credit || 0)}</td>
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
                                                </tr>
                                            );
                                        })
                                    ) : (
                                        <tr>
                                            <td colSpan="9" className="text-center">No entries found</td>
                                        </tr>
                                    )}
                                </tbody>
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
                                        value={editFormData.rate}
                                        onChange={(e) => setEditFormData({ ...editFormData, rate: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        step="0.01"
                                    />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>Discount</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={editFormData.discount}
                                        onChange={(e) => setEditFormData({ ...editFormData, discount: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        step="0.01"
                                    />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>Discount %</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={editFormData.discountPercent}
                                        onChange={(e) => setEditFormData({ ...editFormData, discountPercent: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        max="100"
                                        step="0.01"
                                    />
                            </div>
                        </div>

                        <div className="row mb-3">
                            <div className="col-md-6">
                                <Form.Label>Debit</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={editFormData.debit}
                                        onChange={(e) => setEditFormData({ ...editFormData, debit: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        step="0.01"
                                    />
                            </div>
                            <div className="col-md-6">
                                <Form.Label>Credit</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="no-arrow"
                                        value={editFormData.credit}
                                        onChange={(e) => setEditFormData({ ...editFormData, credit: parseFloat(e.target.value) || 0 })}
                                        min="0"
                                        step="0.01"
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

