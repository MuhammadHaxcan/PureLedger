import React, { useState, useEffect } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';

const MissedEntriesReport = () => {
    const [projects, setProjects] = useState([]);
    const [entries, setEntries] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searching, setSearching] = useState(false);
    const [error, setError] = useState('');
    const [formData, setFormData] = useState({
        projectId: '',
        startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
        endDate: new Date().toISOString().split('T')[0]
    });
    const [page, setPage] = useState(0);
    const [pageSize] = useState(25);
    const [totalPages, setTotalPages] = useState(0);

    useEffect(() => {
        loadInitData();
    }, []);

    const loadInitData = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/ReportingApi/missed-entries-init');
            if (response.data.success) {
                setProjects(response.data.projects || []);
            } else {
                setError(response.data.message || 'Failed to load initial data');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load initial data');
        } finally {
            setLoading(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setSearching(true);
        setError('');
        setPage(0);
        await loadEntries(0);
    };

    const loadEntries = async (currentPage) => {
        try {
            const params = new URLSearchParams({
                page: currentPage.toString(),
                pageSize: pageSize.toString(),
                projectId: formData.projectId || '',
                startDate: formData.startDate || '',
                endDate: formData.endDate || ''
            });

            const response = await apiClient.get(`/ReportingApi/missed-entries?${params}`);
            if (response.data.success && response.data.data) {
                setEntries(response.data.data.entries || []);
                setTotalPages(response.data.data.totalPages || 0);
            } else {
                setError('Failed to load missed entries');
            }
        } catch (err) {
            setError(err.response?.data?.error || 'Failed to load missed entries');
        } finally {
            setSearching(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2
        }).format(value);
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2><i className="fas fa-exclamation-circle me-2"></i>Missed Entries</h2>
            </div>

            <div className="card shadow mb-4">
                <div className="card-header py-3">
                    <h6 className="m-0 font-weight-bold text-primary">Filter Missed Entries</h6>
                </div>
                <div className="card-body">
                    <form id="filterForm" onSubmit={handleSubmit}>
                        <div className="row g-3 align-items-end">
                            <div className="col-md-3">
                                <label className="form-label">Start Date</label>
                                <input
                                    type="date"
                                    className="form-control"
                                    id="startDate"
                                    value={formData.startDate}
                                    onChange={(e) => setFormData({ ...formData, startDate: e.target.value })}
                                />
                            </div>
                            <div className="col-md-3">
                                <label className="form-label">End Date</label>
                                <input
                                    type="date"
                                    className="form-control"
                                    id="endDate"
                                    value={formData.endDate}
                                    onChange={(e) => setFormData({ ...formData, endDate: e.target.value })}
                                />
                            </div>
                            <div className="col-md-4">
                                <Form.Label>Project</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.projectId}
                                        onChange={(value) => setFormData({ ...formData, projectId: value ? parseInt(value) : null })}
                                        options={projects.map(p => ({ id: p.id, name: p.name }))}
                                        placeholder="Search project..."
                                        disabled={searching}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-2">
                                <button type="submit" className="btn btn-primary w-100" disabled={searching}>
                                    {searching ? <LoadingSpinner /> : <><i className="fas fa-search me-1"></i> Search</>}
                                </button>
                            </div>
                        </div>
                    </form>
                </div>
            </div>

            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            {entries.length > 0 && (
                <div className="card shadow">
                    <div className="card-header py-3">
                        <h6 className="m-0 font-weight-bold text-primary">Missed Entries</h6>
                    </div>
                    <div className="card-body">
                        <div className="table-responsive">
                            <table className="table table-bordered table-striped">
                                <thead className="table-primary">
                                    <tr>
                                        <th>Date</th>
                                        <th>Customer</th>
                                        <th>Product</th>
                                        <th>Quantity</th>
                                        <th>Rate</th>
                                        <th>Discount</th>
                                        <th>Description</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {entries.map((entry, index) => (
                                        <tr key={index}>
                                            <td>{entry.missedDate}</td>
                                            <td>{entry.customerAccountName || 'N/A'}</td>
                                            <td>{entry.productAccountName || 'N/A'}</td>
                                            <td>{entry.quantity || 0}</td>
                                            <td>{formatCurrency(entry.rate || 0)}</td>
                                            <td>{formatCurrency(entry.discount || 0)}</td>
                                            <td>{entry.description || '-'}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                        {totalPages > 1 && (
                            <div className="d-flex justify-content-between align-items-center mt-3">
                                <button
                                    className="btn btn-sm btn-outline-primary"
                                    disabled={page === 0}
                                    onClick={() => {
                                        const newPage = page - 1;
                                        setPage(newPage);
                                        loadEntries(newPage);
                                    }}
                                >
                                    Previous
                                </button>
                                <span>Page {page + 1} of {totalPages}</span>
                                <button
                                    className="btn btn-sm btn-outline-primary"
                                    disabled={page >= totalPages - 1}
                                    onClick={() => {
                                        const newPage = page + 1;
                                        setPage(newPage);
                                        loadEntries(newPage);
                                    }}
                                >
                                    Next
                                </button>
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
};

export default MissedEntriesReport;

