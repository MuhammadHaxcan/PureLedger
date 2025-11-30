import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Card, Form, Button, Alert, Table } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import { usePageTitle } from '../../hooks/usePageTitle';

const ProcessBaseVoucher = () => {
    usePageTitle('Process Base Voucher');
    const { id } = useParams();
    const navigate = useNavigate();
    const [voucherData, setVoucherData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [entries, setEntries] = useState([]);
    const [accountEntryAmount, setAccountEntryAmount] = useState(0);
    const [accountEntryIsDebit, setAccountEntryIsDebit] = useState(false);

    useEffect(() => {
        if (id) {
            loadBaseVoucher();
        }
    }, [id]);

    const loadBaseVoucher = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get(`/HomeApi/process-base-voucher/${id}`);
            if (response.data.success) {
                const data = response.data.data;
                setVoucherData(data);
                setEntries(data.entries || []);
            } else {
                setError(response.data.message || 'Failed to load base voucher');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load base voucher');
        } finally {
            setLoading(false);
        }
    };

    const handleQuantityChange = (index, value) => {
        const updatedEntries = [...entries];
        const entry = updatedEntries[index];
        const qty = parseInt(value) || 0;
        
        if (qty < 0) {
            entry.processQuantity = 0;
        } else if (qty > entry.availableQuantity) {
            entry.processQuantity = entry.availableQuantity;
        } else {
            entry.processQuantity = qty;
        }
        
        setEntries(updatedEntries);
    };

    const handleSelectAll = () => {
        const updatedEntries = entries.map(entry => ({
            ...entry,
            processQuantity: entry.isProcessed ? entry.processQuantity : entry.availableQuantity
        }));
        setEntries(updatedEntries);
    };

    const handleDeselectAll = () => {
        const updatedEntries = entries.map(entry => ({
            ...entry,
            processQuantity: entry.accountGroupId === 1 ? entry.availableQuantity : 0
        }));
        setEntries(updatedEntries);
    };

    const handleMaxQuantities = () => {
        const updatedEntries = entries.map(entry => ({
            ...entry,
            processQuantity: entry.isProcessed ? entry.processQuantity : entry.availableQuantity
        }));
        setEntries(updatedEntries);
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setSuccess('');
        setGenerating(true);

        try {
            const entriesToProcess = entries.filter(e => e.processQuantity > 0 || e.accountGroupId === 1);
            
            if (entriesToProcess.length === 0) {
                setError('Please select at least one entry to process');
                setGenerating(false);
                return;
            }

            const model = {
                baseVoucherId: voucherData.baseVoucherId,
                projectId: voucherData.projectId,
                entries: entries.map(entry => ({
                    entryId: entry.entryId,
                    description: entry.description,
                    accountName: entry.accountName,
                    accountId: entry.accountId,
                    categoryId: entry.categoryId,
                    availableQuantity: entry.availableQuantity,
                    processQuantity: entry.processQuantity,
                    rate: entry.rate,
                    debit: entry.debit,
                    credit: entry.credit,
                    percent: entry.percent,
                    disc: entry.disc,
                    rbp: entry.rbp,
                    accountGroupId: entry.accountGroupId
                })),
                accountEntryAmount: accountEntryAmount,
                accountEntryIsDebit: accountEntryIsDebit,
                accountEntryIncluded: true,
                creditableDate: voucherData.creditableDate || voucherData.entryDate || new Date().toISOString().split('T')[0]
            };

            const response = await apiClient.post('/HomeApi/create-derived-voucher', model);
            
            if (response.data.success) {
                const voucherId = response.data.voucherId;
                setSuccess(response.data.message || `Derived voucher JV-${voucherId} created successfully`);
                setTimeout(() => {
                    if (voucherId) {
                        navigate(`/home/voucher-entry?jvId=${voucherId}`);
                    } else {
                        navigate('/home/base-vouchers');
                    }
                }, 2000);
            } else {
                setError(response.data.message || 'Failed to create derived voucher');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to create derived voucher');
        } finally {
            setGenerating(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2,
            maximumFractionDigits: 4
        }).format(value || 0);
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    if (!voucherData) {
        return (
            <div className="container-fluid">
                <Alert variant="danger">
                    <i className="fas fa-exclamation-circle me-2"></i>Base voucher not found
                </Alert>
                <Link to="/home/base-vouchers" className="btn btn-secondary">
                    <i className="fas fa-arrow-left me-1"></i> Back to Base Vouchers
                </Link>
            </div>
        );
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2>
                    <i className="fas fa-cogs me-2"></i>Process Base Voucher JV-{voucherData.baseVoucherId}
                </h2>
                <Link to="/home/base-vouchers" className="btn btn-secondary">
                    <i className="fas fa-arrow-left me-1"></i> Back to Base Vouchers
                </Link>
            </div>

            <Alert variant="info">
                <i className="fas fa-info-circle me-2"></i>
                Select items to include in the new derived voucher. You can process all or part of each entry.
            </Alert>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            {success && (
                <Alert variant="success" dismissible onClose={() => setSuccess('')}>
                    <i className="fas fa-check-circle me-2"></i>{success}
                </Alert>
            )}

            <Card className="shadow mb-4">
                <Card.Header className="d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">Available Entries</h6>
                    <div>
                        <Button variant="outline-primary" size="sm" className="me-1" onClick={handleSelectAll}>
                            <i className="fas fa-check-square me-1"></i> Select All
                        </Button>
                        <Button variant="outline-secondary" size="sm" className="me-1" onClick={handleDeselectAll}>
                            <i className="fas fa-square me-1"></i> Deselect All
                        </Button>
                        <Button variant="outline-info" size="sm" onClick={handleMaxQuantities}>
                            <i className="fas fa-fill me-1"></i> Max Quantities
                        </Button>
                    </div>
                </Card.Header>
                <Card.Body>
                    <Form onSubmit={handleSubmit}>
                        <div className="table-responsive" style={{ overflow: 'visible' }}>
                            <Table bordered hover>
                                <thead className="table-primary">
                                    <tr>
                                        <th width="5%">Select</th>
                                        <th width="8%">Entry ID</th>
                                        <th width="15%">Category</th>
                                        <th width="20%">Account</th>
                                        <th width="22%">Description</th>
                                        <th width="8%">Available</th>
                                        <th width="10%">Process Qty</th>
                                        <th width="7%">Rate</th>
                                        <th width="10%">Debit</th>
                                        <th width="10%">Credit</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries.map((entry, index) => {
                                        const isProcessed = entry.isProcessed;
                                        const isAccountEntry = entry.accountGroupId === 1;
                                        const rowClass = isProcessed ? 'bg-light' : (isAccountEntry ? 'table-info' : '');

                                        return (
                                            <tr key={entry.entryId} className={rowClass}>
                                                <td className="text-center">
                                                    <Form.Check
                                                        type="checkbox"
                                                        checked={entry.processQuantity > 0 || (isAccountEntry && !isProcessed)}
                                                        disabled={isProcessed && !isAccountEntry}
                                                        onChange={(e) => {
                                                            if (e.target.checked) {
                                                                handleQuantityChange(index, isAccountEntry ? entry.availableQuantity : entry.availableQuantity);
                                                            } else {
                                                                handleQuantityChange(index, isAccountEntry ? entry.availableQuantity : 0);
                                                            }
                                                        }}
                                                    />
                                                </td>
                                                <td>{entry.entryId}</td>
                                                <td>{entry.categoryName || 'N/A'}</td>
                                                <td>
                                                    {entry.accountName || 'N/A'}
                                                    {isAccountEntry && (
                                                        <span className="badge bg-info ms-1">Account</span>
                                                    )}
                                                </td>
                                                <td>{entry.description || '-'}</td>
                                                <td className="text-end">{entry.availableQuantity}</td>
                                                <td>
                                                    <Form.Control
                                                        type="number"
                                                        size="sm"
                                                        min="0"
                                                        max={entry.availableQuantity}
                                                        value={entry.processQuantity || 0}
                                                        onChange={(e) => handleQuantityChange(index, e.target.value)}
                                                        disabled={isProcessed && !isAccountEntry}
                                                    />
                                                </td>
                                                <td className="text-end">{entry.rate ? entry.rate.toFixed(4) : '0.0000'}</td>
                                                <td className="text-end">{formatCurrency(entry.debit)}</td>
                                                <td className="text-end">{formatCurrency(entry.credit)}</td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot className="table-secondary">
                                    <tr>
                                        <td colSpan="6" className="text-end fw-bold">TOTAL</td>
                                        <td className="fw-bold text-end">
                                            {entries.reduce((sum, e) => sum + (e.processQuantity || 0), 0)}
                                        </td>
                                        <td colSpan="3"></td>
                                    </tr>
                                </tfoot>
                            </Table>
                        </div>

                        <div className="d-flex justify-content-between mt-4">
                            <Link to="/home/base-vouchers" className="btn btn-secondary">
                                <i className="fas fa-times me-1"></i> Cancel
                            </Link>
                            <Button type="submit" variant="primary" disabled={generating}>
                                {generating ? (
                                    <>
                                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                        Processing...
                                    </>
                                ) : (
                                    <>
                                        <i className="fas fa-check me-1"></i> Create Derived Voucher
                                    </>
                                )}
                            </Button>
                        </div>
                    </Form>
                </Card.Body>
            </Card>
        </div>
    );
};

export default ProcessBaseVoucher;

