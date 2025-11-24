import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';

const VoucherModifiedEntries = () => {
    const { jvId } = useParams();
    const [voucherData, setVoucherData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        if (jvId) {
            loadVoucherModifiedEntries();
        }
    }, [jvId]);

    const loadVoucherModifiedEntries = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get(`/HomeApi/voucher-modified-entries/${jvId}`);
            if (response.data.success) {
                setVoucherData(response.data.data);
            } else {
                setError(response.data.message || 'Failed to load voucher modified entries');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load voucher modified entries');
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2,
            maximumFractionDigits: 4
        }).format(value);
    };

    const formatDate = (dateString) => {
        if (!dateString) return 'N/A';
        try {
            const date = new Date(dateString);
            return date.toLocaleString('en-IN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        } catch {
            return dateString;
        }
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2 className="mb-0">
                    <i className="fas fa-history me-2"></i>Modified Entries - JV-{jvId}
                </h2>
                <Link to="/home/modified-entries" className="btn btn-secondary">
                    <i className="fas fa-arrow-left me-1"></i> Back to Modified Entries
                </Link>
            </div>

            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            {voucherData && (
                <div className="card shadow-sm">
                    <div className="card-header">
                        <h6 className="m-0 fw-bold text-primary">
                            <i className="fas fa-list-alt me-2"></i>Voucher Entry Details
                        </h6>
                    </div>
                    <div className="card-body">
                        <div className="table-responsive" style={{ overflow: 'visible' }}>
                            <table className="table table-striped table-hover align-middle">
                                <thead className="table-light">
                                    <tr>
                                        <th>Entry ID</th>
                                        <th>Category</th>
                                        <th>Account</th>
                                        <th>Quantity</th>
                                        <th>Rate</th>
                                        <th>Debit</th>
                                        <th>Credit</th>
                                        <th>Modified Time</th>
                                        <th>Status</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {voucherData.entries && voucherData.entries.length > 0 ? (
                                        voucherData.entries.map((entry) => (
                                            <tr key={entry.entryId}>
                                                <td>{entry.entryId}</td>
                                                <td>{entry.category || 'N/A'}</td>
                                                <td>{entry.account || 'N/A'}</td>
                                                <td className="text-end">{entry.quantity || 0}</td>
                                                <td className="text-end">{entry.rate ? entry.rate.toFixed(4) : '0.0000'}</td>
                                                <td className="text-end">{formatCurrency(entry.debit || 0)}</td>
                                                <td className="text-end">{formatCurrency(entry.credit || 0)}</td>
                                                <td>{formatDate(entry.modifyTime)}</td>
                                                <td>
                                                    {entry.isModified ? (
                                                        <span className="badge bg-warning">
                                                            <i className="fas fa-edit me-1"></i>Modified
                                                        </span>
                                                    ) : (
                                                        <span className="badge bg-success">
                                                            <i className="fas fa-check me-1"></i>Original
                                                        </span>
                                                    )}
                                                </td>
                                            </tr>
                                        ))
                                    ) : (
                                        <tr>
                                            <td colSpan="9" className="text-center text-muted py-4">
                                                <i className="fas fa-inbox fa-2x mb-2"></i>
                                                <p>No entries found for this voucher.</p>
                                            </td>
                                        </tr>
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default VoucherModifiedEntries;

