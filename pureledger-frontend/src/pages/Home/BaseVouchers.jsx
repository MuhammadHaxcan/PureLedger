import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import ConfirmModal from '../../components/ConfirmModal';

const BaseVouchers = () => {
    const [vouchers, setVouchers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [showMissedModal, setShowMissedModal] = useState(false);
    const [selectedVoucher, setSelectedVoucher] = useState(null);

    useEffect(() => {
        loadBaseVouchers();
    }, []);

    const loadBaseVouchers = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/HomeApi/base-vouchers');
            if (response.data.success) {
                setVouchers(response.data.vouchers || []);
            } else {
                setError('Failed to load base vouchers');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load base vouchers');
        } finally {
            setLoading(false);
        }
    };

    const handleMarkMissed = (voucher) => {
        setSelectedVoucher(voucher);
        setShowMissedModal(true);
    };

    const confirmMarkMissed = async () => {
        if (!selectedVoucher) return;

        try {
            const response = await apiClient.post(`/HomeApi/mark-voucher-as-missed/${selectedVoucher.journalVoucherId}`);
            if (response.data.success) {
                setSuccess(response.data.message);
                loadBaseVouchers();
            } else {
                setError(response.data.message || 'Failed to mark voucher as missed');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to mark voucher as missed');
        } finally {
            setShowMissedModal(false);
            setSelectedVoucher(null);
        }
    };

    const getUnprocessedCount = (voucher) => {
        return voucher.unprocessedEntries || 0;
    };

    const getTotalCount = (voucher) => {
        return voucher.totalEntries || 0;
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2><i className="fas fa-layer-group me-2"></i>Base Vouchers</h2>
                <Link to="/home/entry" className="btn btn-primary">
                    <i className="fas fa-plus me-1"></i> Create New Base Voucher
                </Link>
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

            <div className="card shadow mb-4">
                <div className="card-header py-3">
                    <h6 className="m-0 font-weight-bold text-primary">Available Base Vouchers</h6>
                </div>
                <div className="card-body">
                    {vouchers.length === 0 ? (
                        <div className="alert alert-info">
                            <i className="fas fa-info-circle me-2"></i> No base vouchers available for processing.
                        </div>
                    ) : (
                        <div className="table-responsive">
                            <table className="table table-bordered table-hover" width="100%">
                                <thead className="table-primary">
                                    <tr>
                                        <th>Voucher ID</th>
                                        <th>Project</th>
                                        <th>Entry Date</th>
                                        <th>Available Items</th>
                                        <th>Total Items</th>
                                        <th>Status</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {vouchers.map((voucher) => {
                                        const unprocessed = getUnprocessedCount(voucher);
                                        const total = getTotalCount(voucher);
                                        const isProcessable = unprocessed > 0;
                                        const statusClass = isProcessable ? 'badge bg-success' : 'badge bg-secondary';
                                        const statusText = isProcessable ? 'Ready for Processing' : 'Fully Processed';

                                        return (
                                            <tr key={voucher.journalVoucherId}>
                                                <td>JV-{voucher.journalVoucherId}</td>
                                                <td>{voucher.projectName || 'Unknown Project'}</td>
                                                <td>{voucher.entryDate}</td>
                                                <td>{unprocessed}</td>
                                                <td>{total}</td>
                                                <td><span className={statusClass}>{statusText}</span></td>
                                                <td>
                                                    {isProcessable ? (
                                                        <>
                                                            <Link
                                                                to={`/home/process-base-voucher/${voucher.journalVoucherId}`}
                                                                className="btn btn-sm btn-primary"
                                                            >
                                                                <i className="fas fa-cogs me-1"></i> Process
                                                            </Link>
                                                            <button
                                                                className="btn btn-sm btn-warning ms-1"
                                                                onClick={() => handleMarkMissed(voucher)}
                                                            >
                                                                <i className="fas fa-times-circle me-1"></i> Mark Missed
                                                            </button>
                                                        </>
                                                    ) : (
                                                        <button className="btn btn-sm btn-secondary" disabled>
                                                            <i className="fas fa-check-circle me-1"></i> Completed
                                                        </button>
                                                    )}
                                                    <Link
                                                        to={`/home/voucher-entry?jvId=${voucher.journalVoucherId}`}
                                                        className="btn btn-sm btn-info ms-1"
                                                    >
                                                        <i className="fas fa-eye me-1"></i> View
                                                    </Link>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </div>

            <ConfirmModal
                show={showMissedModal}
                onClose={() => setShowMissedModal(false)}
                onConfirm={confirmMarkMissed}
                title="Mark Entries as Missed"
                message={`Are you sure you want to mark voucher JV-${selectedVoucher?.journalVoucherId} as missed? These entries cannot be processed later.`}
                confirmText="Confirm Missed Entries"
                confirmClass="btn-warning"
            />
        </div>
    );
};

export default BaseVouchers;

