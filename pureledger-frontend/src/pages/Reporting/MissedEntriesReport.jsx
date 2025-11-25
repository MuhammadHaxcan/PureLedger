import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';

const MissedEntriesReport = () => {
    const [projects, setProjects] = useState([]);
    const [allEntries, setAllEntries] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searching, setSearching] = useState(false);
    const [error, setError] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        startDate: '',
        endDate: ''
    });
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 250;

    const observer = useRef();
    const lastEntryElementRef = useCallback(node => {
        if (isLoadingMore || searching) return;
        if (observer.current) observer.current.disconnect();
        observer.current = new IntersectionObserver(entries => {
            if (entries[0].isIntersecting && hasMoreData) {
                const nextPage = Math.floor(allEntries.length / pageSize);
                loadEntries(nextPage, true);
            }
        });
        if (node) observer.current.observe(node);
    }, [isLoadingMore, searching, hasMoreData, allEntries.length, pageSize]);

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
        setError('');
        setCurrentPage(0);
        await loadEntries(0, false);
    };

    const loadEntries = async (page, append = false) => {
        if (page === 0) {
            setSearching(true);
            setAllEntries([]);
        } else {
            setIsLoadingMore(true);
        }

        try {
            const params = {
                page: page,
                pageSize: pageSize,
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries', { params });
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                // API returns MissedEntriesPagedResultViewModel with Entries (capital E)
                const entries = data.entries || data.Entries || [];
                
                if (append) {
                    setAllEntries(prev => [...prev, ...entries]);
                } else {
                    setAllEntries(entries);
                }
                
                setHasMoreData(data.hasMoreData || data.HasMoreData || false);
                setTotalRecords(data.totalRecords || data.TotalRecords || entries.length);
                setCurrentPage(page + 1);

                // Auto-load next page if there's more data
                const hasMore = data.hasMoreData || data.HasMoreData || false;
                if (hasMore && !isLoadingMore) {
                    setTimeout(() => {
                        loadEntries(page + 1, true);
                    }, 100);
                }
            } else {
                setError('Failed to load missed entries');
            }
        } catch (err) {
            setError(err.response?.data?.error || 'Failed to load missed entries');
        } finally {
            setSearching(false);
            setIsLoadingMore(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2
        }).format(value || 0);
    };

    const filteredEntries = allEntries.filter(entry => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        const missedDate = entry.missedDate || entry.MissedDate || '';
        const customerName = entry.customerName || entry.customerAccountName || entry.CustomerName || '';
        const productName = entry.productName || entry.productAccountName || entry.ProductName || '';
        const description = entry.description || entry.Description || '';
        return (
            missedDate.toLowerCase().includes(search) ||
            customerName.toLowerCase().includes(search) ||
            productName.toLowerCase().includes(search) ||
            description.toLowerCase().includes(search) ||
            entry.quantity?.toString().includes(search) ||
            entry.Quantity?.toString().includes(search) ||
            entry.rate?.toString().includes(search) ||
            entry.Rate?.toString().includes(search)
        );
    });

    const exportToCsv = (data, filename) => {
        if (!data || data.length === 0) {
            alert('No data to export');
            return;
        }

        const headers = Object.keys(data[0]);
        const csvContent = [
            headers.join(','),
            ...data.map(row => headers.map(header => {
                const value = row[header];
                return value !== null && value !== undefined ? `"${String(value).replace(/"/g, '""')}"` : '';
            }).join(','))
        ].join('\n');

        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.click();
    };

    const handleExportCsv = () => {
        const csvData = filteredEntries.map(entry => {
            const missedDate = entry.missedDate || entry.MissedDate || '-';
            const customerName = entry.customerName || entry.customerAccountName || entry.CustomerName || 'N/A';
            const productName = entry.productName || entry.productAccountName || entry.ProductName || 'N/A';
            const quantity = entry.quantity || entry.Quantity || 0;
            const rate = entry.rate || entry.Rate || 0;
            const discount = entry.discount || entry.Discount || 0;
            const description = entry.description || entry.Description || '-';
            return {
                Date: missedDate,
                Customer: customerName,
                Product: productName,
                Quantity: quantity,
                Rate: rate,
                Discount: discount,
                Description: description
            };
        });
        const dateRange = formData.startDate && formData.endDate 
            ? `${formData.startDate}_to_${formData.endDate}` 
            : 'report';
        exportToCsv(csvData, `MissedEntriesReport_${dateRange}.csv`);
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
                                        options={[{ id: null, name: 'All Projects' }, ...projects.map(p => ({ id: p.id, name: p.name }))]}
                                        placeholder="All Projects"
                                        disabled={searching}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-2">
                                <button 
                                    type="submit" 
                                    className="btn btn-primary w-100" 
                                    disabled={searching || isLoadingMore}
                                    title={searching ? 'Searching...' : ''}
                                >
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

            {allEntries.length > 0 && (
                <div className="card shadow">
                    <div className="card-header py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">Missed Entries</h6>
                        <div>
                            {totalRecords > 0 && (
                                <span className="badge bg-primary me-2">{allEntries.length} of {totalRecords}</span>
                            )}
                            <Button variant="outline-success" size="sm" onClick={handleExportCsv} disabled={filteredEntries.length === 0}>
                                <i className="fas fa-file-csv me-1"></i> Export CSV
                            </Button>
                        </div>
                    </div>
                    <div className="card-body">
                        <div className="row mb-3">
                            <div className="col-12">
                                <Form.Label>Search</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-search"></i></InputGroup.Text>
                                    <Form.Control
                                        placeholder="Filter results..."
                                        value={searchTerm}
                                        onChange={(e) => setSearchTerm(e.target.value)}
                                    />
                                </InputGroup>
                            </div>
                        </div>
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
                                    {filteredEntries.length === 0 ? (
                                        <tr>
                                            <td colSpan="7" className="text-center text-muted py-4">
                                                {searchTerm ? 'No results found for your search' : 'No data available'}
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredEntries.map((entry, index) => {
                                            const isLastEntry = index === filteredEntries.length - 1;
                                            // Handle both camelCase and PascalCase property names
                                            const missedDate = entry.missedDate || entry.MissedDate || '-';
                                            const customerName = entry.customerName || entry.customerAccountName || entry.CustomerName || 'N/A';
                                            const productName = entry.productName || entry.productAccountName || entry.ProductName || 'N/A';
                                            const quantity = entry.quantity || entry.Quantity || 0;
                                            const rate = entry.rate || entry.Rate || 0;
                                            const discount = entry.discount || entry.Discount || 0;
                                            const description = entry.description || entry.Description || '-';
                                            return (
                                                <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                    <td>{missedDate}</td>
                                                    <td>{customerName}</td>
                                                    <td>{productName}</td>
                                                    <td>{quantity}</td>
                                                    <td>{formatCurrency(rate)}</td>
                                                    <td>{formatCurrency(discount)}</td>
                                                    <td>{description}</td>
                                                </tr>
                                            );
                                        })
                                    )}
                                </tbody>
                            </table>
                        </div>
                        {isLoadingMore && (
                            <div className="text-center my-3">
                                <LoadingSpinner />
                            </div>
                        )}
                        {!hasMoreData && allEntries.length > 0 && (
                            <div className="text-center my-3 text-muted">
                                End of report. Total records: {totalRecords}
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
};

export default MissedEntriesReport;

