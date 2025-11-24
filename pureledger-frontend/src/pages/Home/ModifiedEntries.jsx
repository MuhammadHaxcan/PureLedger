import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';

const ModifiedEntries = () => {
    const [entries, setEntries] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [searchTerm, setSearchTerm] = useState('');

    useEffect(() => {
        loadModifiedEntries();
    }, []);

    const loadModifiedEntries = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/HomeApi/modified-entries');
            if (response.data.success) {
                setEntries(response.data.data || []);
            } else {
                setError('Failed to load modified entries');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load modified entries');
        } finally {
            setLoading(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2
        }).format(value);
    };

    const filteredEntries = entries.filter(entry => {
        const search = searchTerm.toLowerCase();
        return (
            `JV-${entry.jvId}`.toLowerCase().includes(search) ||
            formatCurrency(entry.totalDebit || 0).toLowerCase().includes(search) ||
            formatCurrency(entry.totalCredit || 0).toLowerCase().includes(search)
        );
    });

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2 className="mb-0">
                    <i className="fas fa-history me-2"></i>Modified Entries
                </h2>
            </div>

            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            <div className="card shadow-sm">
                <div className="card-header d-flex justify-content-between align-items-center">
                    <h6 className="m-0 fw-bold text-primary">
                        <i className="fas fa-list-alt me-2"></i>Modified Voucher Entries
                    </h6>
                    <div className="d-flex align-items-center gap-3">
                        <span className="badge bg-primary">{entries.length} entries</span>
                        <div className="input-group" style={{ width: '250px' }}>
                            <span className="input-group-text"><i className="fas fa-search"></i></span>
                            <input
                                type="text"
                                className="form-control"
                                placeholder="Search entries..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                            />
                        </div>
                    </div>
                </div>

                <div className="card-body">
                    {filteredEntries.length === 0 ? (
                        <div className="text-center py-4">
                            <i className="fas fa-inbox fa-3x text-muted mb-3"></i>
                            <p className="text-muted">
                                {searchTerm ? 'No entries match your search criteria.' : 'No modified entries found.'}
                            </p>
                        </div>
                    ) : (
                        <div className="table-responsive">
                            <table className="table table-striped table-hover align-middle">
                                <thead className="table-light">
                                    <tr>
                                        <th>Voucher ID</th>
                                        <th>Debit</th>
                                        <th>Credit</th>
                                        <th className="text-center" style={{ width: '150px' }}>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredEntries.map((entry) => (
                                        <tr key={entry.jvId}>
                                            <td><span className="fw-bold">JV-{entry.jvId}</span></td>
                                            <td>{formatCurrency(entry.totalDebit || 0)}</td>
                                            <td>{formatCurrency(entry.totalCredit || 0)}</td>
                                            <td className="text-center">
                                                <Link
                                                    to={`/home/voucher-modified-entries/${entry.jvId}`}
                                                    className="btn btn-sm btn-outline-primary"
                                                >
                                                    <i className="fas fa-eye me-1"></i>View Details
                                                </Link>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default ModifiedEntries;

