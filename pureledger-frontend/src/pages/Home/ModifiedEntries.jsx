import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Card, Form, InputGroup, Alert, Button } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const ModifiedEntries = () => {
    usePageTitle('Modified Entries');
    const [entries, setEntries] = useState([]);
    const [projects, setProjects] = useState([]);
    const [loadingProjects, setLoadingProjects] = useState(true);
    const [loadingEntries, setLoadingEntries] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [error, setError] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [projectId, setProjectId] = useState(null);
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 250;

    useEffect(() => {
        loadInitData();
    }, []);

    useEffect(() => {
        if (projectId !== null) {
            loadModifiedEntries(0, false);
        } else {
            // Clear entries when no project is selected
            setEntries([]);
            setLoadingEntries(false);
            setCurrentPage(0);
            setHasMoreData(false);
            setTotalRecords(0);
        }
    }, [projectId]);

    const loadInitData = async () => {
        setLoadingProjects(true);
        setError('');
        try {
            // Try the new endpoint first, fallback to entry-data if it doesn't exist
            let response;
            try {
                response = await apiClient.get('/HomeApi/modified-entries-init');
            } catch (initError) {
                // If the new endpoint doesn't exist (404), use the existing entry-data endpoint
                if (initError.response?.status === 404) {
                    response = await apiClient.get('/HomeApi/entry-data');
                    // Transform the response to match expected format
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
            
            console.log('Modified entries init response:', response.data);
            if (response.data.success) {
                setProjects(response.data.projects || []);
                console.log('Projects loaded:', response.data.projects?.length || 0);
            } else {
                setError(response.data.message || 'Failed to load initial data');
            }
        } catch (err) {
            console.error('Error loading projects:', err);
            setError(err.response?.data?.message || err.message || 'Failed to load initial data');
        } finally {
            setLoadingProjects(false);
        }
    };

    const loadModifiedEntries = async (page = 0, append = false) => {
        if (projectId === null) {
            setEntries([]);
            setLoadingEntries(false);
            return;
        }

        if (page === 0) {
            setLoadingEntries(true);
            setEntries([]);
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

            const response = await apiClient.get('/HomeApi/modified-entries', { params });
            
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const newEntries = data || [];

                if (append) {
                    setEntries(prev => [...prev, ...newEntries]);
                } else {
                    setEntries(newEntries);
                }

                setHasMoreData(response.data.hasMoreData || false);
                setTotalRecords(response.data.totalRecords || 0);
                setCurrentPage(page + 1);

                // Auto-load next page if there's more data and not searching
                if (response.data.hasMoreData && !searchTerm.trim() && !isLoadingMore && !isAutoLoading) {
                    setIsAutoLoading(true);
                    setTimeout(() => {
                        loadModifiedEntries(page + 1, true);
                    }, 100);
                } else {
                    setIsAutoLoading(false);
                }
            } else {
                setError('Failed to load modified entries');
                setIsAutoLoading(false);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load modified entries');
            setIsAutoLoading(false);
        } finally {
            setLoadingEntries(false);
            setIsLoadingMore(false);
        }
    };

    const handleLoadMore = () => {
        if (!isLoadingMore && hasMoreData) {
            loadModifiedEntries(currentPage, true);
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

    if (loadingProjects) {
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

            <Card className="shadow-sm">
                <Card.Header className="d-flex justify-content-between align-items-center">
                    <h6 className="m-0 fw-bold text-primary">
                        <i className="fas fa-list-alt me-2"></i>Modified Voucher Entries
                    </h6>
                    <div className="d-flex align-items-center gap-3">
                        <span className="badge bg-primary">
                            {filteredEntries.length} {totalRecords > 0 ? `of ${totalRecords}` : ''} entries
                        </span>
                        <div className="input-group" style={{ width: '250px' }}>
                            <InputGroup.Text><i className="fas fa-search"></i></InputGroup.Text>
                            <Form.Control
                                type="text"
                                placeholder="Search entries..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                            />
                        </div>
                    </div>
                </Card.Header>

                <Card.Body>
                    {loadingEntries ? (
                        <div className="text-center py-4">
                            <div className="spinner-border text-primary" role="status">
                                <span className="visually-hidden">Loading...</span>
                            </div>
                        </div>
                    ) : filteredEntries.length === 0 ? (
                        <div className="text-center py-4">
                            <i className="fas fa-inbox fa-3x text-muted mb-3"></i>
                            <p className="text-muted">
                                {projectId === null 
                                    ? 'Please select a project to view modified entries.' 
                                    : searchTerm 
                                        ? 'No entries match your search criteria.' 
                                        : 'No modified entries found for the selected project.'}
                            </p>
                        </div>
                    ) : (
                        <div className="table-responsive">
                            <table className="table table-striped table-hover align-middle">
                                <thead className="table-light">
                                    <tr>
                                        <th style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Voucher ID</th>
                                        <th className="text-end" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Debit</th>
                                        <th className="text-end" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Credit</th>
                                        <th className="text-center" style={{ minWidth: '150px', whiteSpace: 'nowrap' }}>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredEntries.map((entry) => (
                                        <tr key={entry.jvId}>
                                            <td style={{ whiteSpace: 'nowrap' }}>
                                                <span className="fw-bold">JV-{entry.jvId}</span>
                                            </td>
                                            <td className="text-end" style={{ whiteSpace: 'nowrap' }}>
                                                {formatCurrency(entry.totalDebit || 0)}
                                            </td>
                                            <td className="text-end" style={{ whiteSpace: 'nowrap' }}>
                                                {formatCurrency(entry.totalCredit || 0)}
                                            </td>
                                            <td className="text-center" style={{ whiteSpace: 'nowrap' }}>
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
                    {hasMoreData && (
                        <div className="text-center mt-3">
                            <div className="text-muted mb-2">
                                Loaded {entries.length} of {totalRecords} records
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
                    {!hasMoreData && entries.length > 0 && (
                        <div className="text-center mt-3 text-muted">
                            All {entries.length} records loaded
                        </div>
                    )}
                </Card.Body>
            </Card>
        </div>
    );
};

export default ModifiedEntries;

