import React, { useState, useEffect, useRef } from 'react';
import { Link } from 'react-router-dom';
import { Card, Form, InputGroup, Alert, Button } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import ConfirmModal from '../../components/ConfirmModal';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const BaseVouchers = () => {
    usePageTitle('Base Vouchers');
    
    // Ref to prevent duplicate submissions
    const isSubmittingRef = useRef(false);
    
    const [vouchers, setVouchers] = useState([]);
    const [projects, setProjects] = useState([]);
    const [loadingProjects, setLoadingProjects] = useState(true);
    const [loadingVouchers, setLoadingVouchers] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [projectId, setProjectId] = useState(null);
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const [showMissedModal, setShowMissedModal] = useState(false);
    const [selectedVoucher, setSelectedVoucher] = useState(null);
    const [summaryStats, setSummaryStats] = useState(null);
    const [summaryCollapsed, setSummaryCollapsed] = useState(true);
    const [loadingSummary, setLoadingSummary] = useState(false);
    const pageSize = 250;

    useEffect(() => {
        loadInitData();
    }, []);

    useEffect(() => {
        if (projectId !== null) {
            loadBaseVouchers(0, false);
            loadSummaryStats(projectId);
        } else {
            setVouchers([]);
            setLoadingVouchers(false);
            setCurrentPage(0);
            setHasMoreData(false);
            setTotalRecords(0);
            setSummaryStats(null);
        }
    }, [projectId]);

    const loadInitData = async () => {
        setLoadingProjects(true);
        setError('');
        try {
            let response;
            try {
                response = await apiClient.get('/HomeApi/base-vouchers-init');
            } catch (initError) {
                if (initError.response?.status === 404) {
                    response = await apiClient.get('/HomeApi/entry-data');
                    if (response.data.success && response.data.projects) {
                        response.data = {
                            success: true,
                            projects: response.data.projects
                        };
                    }
                } else {
                    throw initError;
                }
            }
            
            if (response.data.success) {
                setProjects(response.data.projects || []);
            } else {
                setError(response.data.message || 'Failed to load initial data');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load initial data');
        } finally {
            setLoadingProjects(false);
        }
    };

    const loadSummaryStats = async (currentProjectId) => {
        if (currentProjectId === null) {
            setSummaryStats(null);
            return;
        }
        setLoadingSummary(true);
        try {
            const response = await apiClient.get(`/HomeApi/base-vouchers-summary?projectId=${currentProjectId}`);
            if (response.data.success) {
                setSummaryStats(response.data.data);
            } else {
                setError(response.data.message || 'Failed to load summary data');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load summary data');
        } finally {
            setLoadingSummary(false);
        }
    };

    const loadBaseVouchers = async (page = 0, append = false) => {
        if (projectId === null) {
            setVouchers([]);
            setLoadingVouchers(false);
            return;
        }

        if (page === 0) {
            setLoadingVouchers(true);
            setVouchers([]);
            setCurrentPage(0);
        } else {
            setIsLoadingMore(true);
        }

        setError('');

        try {
            const params = {
                projectId: projectId,
                page: page,
                pageSize: pageSize
            };

            const response = await apiClient.get('/HomeApi/base-vouchers', { params });
            
            if (response.data.success && response.data.vouchers) {
                const newVouchers = response.data.vouchers || [];

                if (append) {
                    setVouchers(prev => [...prev, ...newVouchers]);
                } else {
                    setVouchers(newVouchers);
                }

                setHasMoreData(response.data.hasMoreData || false);
                setTotalRecords(response.data.totalRecords || 0);
                setCurrentPage(page + 1);

                if (response.data.hasMoreData && !searchTerm.trim() && !isLoadingMore && !isAutoLoading) {
                    setIsAutoLoading(true);
                    setTimeout(() => {
                        loadBaseVouchers(page + 1, true);
                    }, 100);
                } else {
                    setIsAutoLoading(false);
                }
            } else {
                setError('Failed to load base vouchers');
                setIsAutoLoading(false);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load base vouchers');
            setIsAutoLoading(false);
        } finally {
            setLoadingVouchers(false);
            setIsLoadingMore(false);
        }
    };

    const handleLoadMore = () => {
        if (!isLoadingMore && hasMoreData) {
            loadBaseVouchers(currentPage, true);
        }
    };

    const handleMarkMissed = (voucher) => {
        setSelectedVoucher(voucher);
        setShowMissedModal(true);
    };

    const confirmMarkMissed = async () => {
        if (!selectedVoucher || isSubmittingRef.current) return;
        isSubmittingRef.current = true;

        try {
            const response = await apiClient.post(`/HomeApi/mark-voucher-as-missed/${selectedVoucher.journalVoucherId}`);
            if (response.data.success) {
                setSuccess(response.data.message);
                loadBaseVouchers(0, false);
            } else {
                setError(response.data.message || 'Failed to mark voucher as missed');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to mark voucher as missed');
        } finally {
            setShowMissedModal(false);
            setSelectedVoucher(null);
            isSubmittingRef.current = false;
        }
    };

    const getUnprocessedCount = (voucher) => {
        return voucher.unprocessedEntries || 0;
    };

    const getTotalCount = (voucher) => {
        return voucher.totalEntries || 0;
    };

    const formatDate = (dateStr) => {
        if (!dateStr) return '';
        try {
            const date = new Date(dateStr);
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const year = date.getFullYear();
            return `${month}/${day}/${year}`;
        } catch {
            return dateStr;
        }
    };

    const formatNumber = (value) => {
        return new Intl.NumberFormat('en-PK').format(value || 0);
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value || 0);
    };

    const filteredVouchers = vouchers.filter(voucher => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        return (
            `JV-${voucher.journalVoucherId}`.toLowerCase().includes(search) ||
            (voucher.projectName || '').toLowerCase().includes(search) ||
            (voucher.customerName || '').toLowerCase().includes(search) ||
            formatDate(voucher.entryDate).toLowerCase().includes(search) ||
            (voucher.unprocessedEntries || 0).toString().includes(search) ||
            (voucher.totalEntries || 0).toString().includes(search)
        );
    });

    if (loadingProjects) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2 className="mb-0">
                    <i className="fas fa-layer-group me-2"></i>Base Vouchers
                </h2>
                <Link to="/home/entry" className="btn btn-primary">
                    <i className="fas fa-plus me-1"></i> Create New Base Voucher
                </Link>
            </div>

            {success && (
                <Alert variant="success" dismissible onClose={() => setSuccess('')}>
                    <i className="fas fa-check-circle me-2"></i>{success}
                </Alert>
            )}

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            <Card className="shadow mb-4">
                <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-filter me-2"></i>Filter Options
                    </h6>
                </Card.Header>
                <Card.Body>
                    <Form>
                        <div className="row mb-3">
                            <div className="col-md-4 mb-3">
                                <Form.Label>Project</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={projectId}
                                        onChange={(value) => setProjectId(value ? parseInt(value) : null)}
                                        options={projects.map(p => ({ id: p.id, name: p.name }))}
                                        placeholder="Search project..."
                                        disabled={loadingProjects}
                                    />
                                </InputGroup>
                            </div>
                        </div>
                    </Form>
                </Card.Body>
            </Card>

            {projectId !== null && (
                <Card className="shadow mb-4">
                    <Card.Header
                        className="py-3 d-flex justify-content-between align-items-center"
                        style={{ cursor: 'pointer' }}
                        onClick={() => setSummaryCollapsed(!summaryCollapsed)}
                    >
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-chart-pie me-2"></i>Summary Statistics
                        </h6>
                        <i className={`fas fa-chevron-${summaryCollapsed ? 'down' : 'up'} text-muted`}></i>
                    </Card.Header>
                    {!summaryCollapsed && (
                        <Card.Body>
                            {loadingSummary ? (
                                <div className="text-center py-4">
                                    <div className="spinner-border text-primary" role="status">
                                        <span className="visually-hidden">Loading Summary...</span>
                                    </div>
                                </div>
                            ) : summaryStats ? (
                                <>
                                    <div className="row mb-4">
                                        <div className="col-md-3 mb-3">
                                            <div className="card bg-primary text-white">
                                                <div className="card-body">
                                                    <h6 className="card-subtitle mb-2">Total Base Vouchers</h6>
                                                    <h3 className="card-title mb-0">{formatNumber(summaryStats.totalBaseVouchers)}</h3>
                                                </div>
                                            </div>
                                        </div>
                                        <div className="col-md-3 mb-3">
                                            <div className="card bg-warning text-white">
                                                <div className="card-body">
                                                    <h6 className="card-subtitle mb-2">Unprocessed Entries</h6>
                                                    <h3 className="card-title mb-0">{formatNumber(summaryStats.totalUnprocessedEntries)}</h3>
                                                    <small className="text-white-50">{formatNumber(summaryStats.totalUnprocessedQuantity)} qty</small>
                                                </div>
                                            </div>
                                        </div>
                                        <div className="col-md-3 mb-3">
                                            <div className="card bg-success text-white">
                                                <div className="card-body">
                                                    <h6 className="card-subtitle mb-2">Processed Entries</h6>
                                                    <h3 className="card-title mb-0">{formatNumber(summaryStats.totalProcessedEntries)}</h3>
                                                    <small className="text-white-50">{formatNumber(summaryStats.totalProcessedQuantity)} qty</small>
                                                </div>
                                            </div>
                                        </div>
                                        <div className="col-md-3 mb-3">
                                            <div className="card bg-info text-white">
                                                <div className="card-body">
                                                    <h6 className="card-subtitle mb-2">Processing Rate</h6>
                                                    <h3 className="card-title mb-0">{formatNumber(summaryStats.processingRate)}%</h3>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="row">
                                        <div className="col-md-6 mb-3">
                                            <h6 className="mb-3"><i className="fas fa-shopping-cart me-2"></i>Most Ordered Products</h6>
                                            {summaryStats.mostOrderedProducts && summaryStats.mostOrderedProducts.length > 0 ? (
                                                <div className="table-responsive">
                                                    <table className="table table-sm table-bordered">
                                                        <thead className="table-light">
                                                            <tr>
                                                                <th>Product</th>
                                                                <th className="text-end">Total Quantity</th>
                                                                <th className="text-end">Entries</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {summaryStats.mostOrderedProducts.slice(0, 5).map((product, idx) => (
                                                                <tr key={idx}>
                                                                    <td>{product.productName}</td>
                                                                    <td className="text-end">{formatNumber(product.totalQuantity)}</td>
                                                                    <td className="text-end">{formatNumber(product.entryCount)}</td>
                                                                </tr>
                                                            ))}
                                                        </tbody>
                                                    </table>
                                                </div>
                                            ) : (
                                                <p className="text-muted">No ordered products data available</p>
                                            )}
                                        </div>
                                        <div className="col-md-6 mb-3">
                                            <h6 className="mb-3"><i className="fas fa-clock me-2"></i>Most Pending Products</h6>
                                            {summaryStats.mostPendingProducts && summaryStats.mostPendingProducts.length > 0 ? (
                                                <div className="table-responsive">
                                                    <table className="table table-sm table-bordered">
                                                        <thead className="table-light">
                                                            <tr>
                                                                <th>Product</th>
                                                                <th className="text-end">Unprocessed Qty</th>
                                                                <th className="text-end">Processed Qty</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {summaryStats.mostPendingProducts.slice(0, 5).map((product, idx) => (
                                                                <tr key={idx}>
                                                                    <td>{product.productName}</td>
                                                                    <td className="text-end text-warning">{formatNumber(product.unprocessedQuantity)}</td>
                                                                    <td className="text-end text-success">{formatNumber(product.processedQuantity)}</td>
                                                                </tr>
                                                            ))}
                                                        </tbody>
                                                    </table>
                                                </div>
                                            ) : (
                                                <p className="text-muted">No pending products data available</p>
                                            )}
                                        </div>
                                    </div>

                                    <div className="row mt-3">
                                        <div className="col-md-6 mb-3">
                                            {summaryStats.topCustomer && (
                                                <div className="card">
                                                    <div className="card-body">
                                                        <h6 className="card-title"><i className="fas fa-user-tie me-2"></i>Top Customer</h6>
                                                        <p className="mb-1"><strong>{summaryStats.topCustomer.customerName}</strong></p>
                                                        <p className="mb-0 text-muted">
                                                            {formatNumber(summaryStats.topCustomer.voucherCount)} vouchers | {formatNumber(summaryStats.topCustomer.totalEntries)} entries
                                                        </p>
                                                    </div>
                                                </div>
                                            )}
                                        </div>
                                        <div className="col-md-6 mb-3">
                                            {summaryStats.oldestPendingVoucher && (
                                                <div className="card">
                                                    <div className="card-body">
                                                        <h6 className="card-title"><i className="fas fa-calendar-times me-2"></i>Oldest Pending Voucher</h6>
                                                        <p className="mb-1"><strong>JV-{summaryStats.oldestPendingVoucher.voucherId}</strong></p>
                                                        <p className="mb-0 text-muted">
                                                            {formatDate(summaryStats.oldestPendingVoucher.entryDate)} | {formatNumber(summaryStats.oldestPendingVoucher.daysPending)} days pending | {formatNumber(summaryStats.oldestPendingVoucher.unprocessedCount)} unprocessed
                                                        </p>
                                                    </div>
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                </>
                            ) : (
                                <div className="text-center py-4 text-muted">No summary data available for this project.</div>
                            )}
                        </Card.Body>
                    )}
                </Card>
            )}

            <Card className="shadow-sm">
                <Card.Header className="d-flex justify-content-between align-items-center">
                    <h6 className="m-0 fw-bold text-primary">
                        <i className="fas fa-list-alt me-2"></i>Available Base Vouchers
                    </h6>
                    <div className="d-flex align-items-center gap-3">
                        <span className="badge bg-primary">
                            {filteredVouchers.length} {totalRecords > 0 ? `of ${totalRecords}` : ''} vouchers
                        </span>
                        <div className="input-group" style={{ width: '250px' }}>
                            <InputGroup.Text><i className="fas fa-search"></i></InputGroup.Text>
                            <Form.Control
                                type="text"
                                placeholder="Search vouchers..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                            />
                        </div>
                    </div>
                </Card.Header>
                <Card.Body>
                    {loadingVouchers ? (
                        <div className="text-center py-4">
                            <div className="spinner-border text-primary" role="status">
                                <span className="visually-hidden">Loading...</span>
                            </div>
                        </div>
                    ) : filteredVouchers.length === 0 ? (
                        <div className="text-center py-4">
                            <i className="fas fa-inbox fa-3x text-muted mb-3"></i>
                            <p className="text-muted">
                                {projectId === null 
                                    ? 'Please select a project to view base vouchers.' 
                                    : searchTerm 
                                        ? 'No vouchers match your search criteria.' 
                                        : 'No base vouchers found for the selected project.'}
                            </p>
                        </div>
                    ) : (
                        <div className="table-responsive">
                            <table className="table table-bordered table-hover align-middle" width="100%">
                                <thead className="table-primary">
                                    <tr>
                                        <th style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Voucher ID</th>
                                        <th style={{ minWidth: '150px' }}>Project</th>
                                        <th style={{ minWidth: '150px' }}>Customer</th>
                                        <th style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Entry Date</th>
                                        <th className="text-end" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Available Items</th>
                                        <th className="text-end" style={{ minWidth: '100px', whiteSpace: 'nowrap' }}>Total Items</th>
                                        <th style={{ minWidth: '150px', whiteSpace: 'nowrap' }}>Status</th>
                                        <th className="text-center" style={{ minWidth: '250px', whiteSpace: 'nowrap' }}>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredVouchers.map((voucher) => {
                                        const unprocessed = getUnprocessedCount(voucher);
                                        const total = getTotalCount(voucher);
                                        const isProcessable = unprocessed > 0;
                                        const statusClass = isProcessable ? 'badge bg-success' : 'badge bg-secondary';
                                        const statusText = isProcessable ? 'Ready for Processing' : 'Fully Processed';

                                        return (
                                            <tr key={voucher.journalVoucherId}>
                                                <td style={{ whiteSpace: 'nowrap' }}>
                                                    <span className="fw-bold">JV-{voucher.journalVoucherId}</span>
                                                </td>
                                                <td>{voucher.projectName || 'Unknown Project'}</td>
                                                <td>{voucher.customerName || 'N/A'}</td>
                                                <td style={{ whiteSpace: 'nowrap' }}>{formatDate(voucher.entryDate)}</td>
                                                <td className="text-end" style={{ whiteSpace: 'nowrap' }}>{unprocessed}</td>
                                                <td className="text-end" style={{ whiteSpace: 'nowrap' }}>{total}</td>
                                                <td style={{ whiteSpace: 'nowrap' }}>
                                                    <span className={statusClass}>{statusText}</span>
                                                </td>
                                                <td className="text-center" style={{ whiteSpace: 'nowrap' }}>
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
                    {hasMoreData && (
                        <div className="text-center mt-3">
                            <div className="text-muted mb-2">
                                Loaded {vouchers.length} of {totalRecords} records
                            </div>
                            {isLoadingMore && (
                                <div className="spinner-border spinner-border-sm text-primary" role="status">
                                    <span className="visually-hidden">Loading more records...</span>
                                </div>
                            )}
                            {!isLoadingMore && (
                                <Button
                                    variant="outline-primary"
                                    size="sm"
                                    onClick={handleLoadMore}
                                >
                                    <i className="fas fa-arrow-down me-1"></i>Load More
                                </Button>
                            )}
                        </div>
                    )}
                    {!hasMoreData && vouchers.length > 0 && (
                        <div className="text-center mt-3 text-muted">
                            All {vouchers.length} records loaded
                        </div>
                    )}
                </Card.Body>
            </Card>

            <ConfirmModal
                show={showMissedModal}
                onHide={() => setShowMissedModal(false)}
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

