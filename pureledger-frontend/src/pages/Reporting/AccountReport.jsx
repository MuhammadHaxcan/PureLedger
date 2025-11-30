import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const AccountReport = () => {
    usePageTitle('Account Report');
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [reportData, setReportData] = useState(null);
    const [allEntries, setAllEntries] = useState([]);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        categoryId: null,
        accountId: null,
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
        if (isLoadingMore || generating) return;
        if (observer.current) observer.current.disconnect();
        observer.current = new IntersectionObserver(entries => {
            if (entries[0].isIntersecting && hasMoreData) {
                const nextPage = Math.floor(allEntries.length / pageSize);
                generateReport(nextPage, true);
            }
        });
        if (node) observer.current.observe(node);
    }, [isLoadingMore, generating, hasMoreData, allEntries.length, pageSize]);

    useEffect(() => {
        loadInitData();
    }, []);

    useEffect(() => {
        if (formData.projectId) {
            loadCategoriesByProject(formData.projectId);
        } else {
            setCategories([]);
            setAccounts([]);
            setFormData(prev => ({ ...prev, categoryId: null, accountId: null }));
        }
    }, [formData.projectId]);

    useEffect(() => {
        if (formData.categoryId) {
            loadAccountsByCategory(formData.categoryId);
        } else {
            setAccounts([]);
            setFormData(prev => ({ ...prev, accountId: null }));
        }
    }, [formData.categoryId]);

    const loadInitData = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/ReportingApi/account-report-init');
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

    const loadCategoriesByProject = async (projectId) => {
        try {
            const response = await apiClient.get(`/ReportingApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                setCategories(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading categories:', err);
        }
    };

    const loadAccountsByCategory = async (categoryId) => {
        try {
            const response = await apiClient.get(`/ReportingApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                setAccounts(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading accounts:', err);
        }
    };

    const generateReport = async (page = 0, append = false) => {
        if (!formData.projectId) {
            setError('Please select a project');
            return;
        }

        if (page === 0) {
            setGenerating(true);
            setAllEntries([]);
            setSearchTerm('');
            setCurrentPage(0);
        } else {
            setIsLoadingMore(true);
        }

        setError('');

        try {
            const params = {
                projectId: formData.projectId.toString(),
                categoryId: formData.categoryId ? formData.categoryId.toString() : null,
                accountId: formData.accountId ? formData.accountId.toString() : null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null,
                page: page,
                pageSize: pageSize
            };

            const response = await apiClient.get('/ReportingApi/account-report', { params });
            
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const entries = data.entries || [];

                if (append) {
                    setAllEntries(prev => [...prev, ...entries]);
                } else {
                    setAllEntries(entries);
                }

                setHasMoreData(data.hasMoreData || false);
                setTotalRecords(data.totalRecords || entries.length);
                setCurrentPage(page + 1);

                // Auto-load next page if there's more data and not searching
                if (data.hasMoreData && !searchTerm.trim() && !isLoadingMore) {
                    setTimeout(() => {
                        generateReport(page + 1, true);
                    }, 100);
                }
            } else {
                setError('Failed to generate report');
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to generate report');
        } finally {
            setGenerating(false);
            setIsLoadingMore(false);
        }
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        generateReport(0, false);
    };


    const filteredEntries = allEntries.filter(entry => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        return (
            (entry.categoryName || '').toLowerCase().includes(search) ||
            (entry.accountName || '').toLowerCase().includes(search) ||
            entry.accountId?.toString().includes(search) ||
            entry.projectId?.toString().includes(search) ||
            entry.categoryId?.toString().includes(search)
        );
    });

    const totals = filteredEntries.reduce((acc, entry) => ({
        debit: acc.debit + (entry.totalDebit || 0),
        credit: acc.credit + (entry.totalCredit || 0)
    }), { debit: 0, credit: 0 });

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
        const csvData = filteredEntries.map(entry => ({
            ID: entry.formattedId || entry.id || '',
            Category: entry.categoryName || '',
            'Account Name': entry.accountName || '',
            Debit: entry.totalDebit || 0,
            Credit: entry.totalCredit || 0,
            Balance: (entry.totalDebit || 0) - (entry.totalCredit || 0)
        }));
        const dateRange = formData.startDate && formData.endDate 
            ? `${formData.startDate}_to_${formData.endDate}` 
            : 'report';
        exportToCsv(csvData, `AccountReport_${dateRange}.csv`);
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 4
        }).format(value || 0);
    };

    const formatDate = (dateStr) => {
        if (!dateStr) return '';
        try {
            const date = new Date(dateStr);
            return date.toLocaleDateString('en-GB');
        } catch {
            return dateStr;
        }
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-chart-pie me-2"></i>Account Report</h2>
            </div>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            <Card className="shadow mb-4">
                <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-filter me-2"></i>Report Parameters
                    </h6>
                </Card.Header>
                <Card.Body>
                    <Form onSubmit={handleSubmit}>
                        <div className="row mb-3">
                            <div className="col-md-3 mb-3">
                                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.projectId}
                                        onChange={(value) => setFormData({ ...formData, projectId: value ? parseInt(value) : null, categoryId: null, accountId: null })}
                                        options={projects.map(p => ({ id: p.id, name: p.name }))}
                                        placeholder="Search project..."
                                        required
                                        disabled={generating}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Category</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.categoryId}
                                        onChange={(value) => setFormData({ ...formData, categoryId: value ? parseInt(value) : null, accountId: null })}
                                        options={[{ id: null, name: 'All Categories' }, ...categories.map(c => ({ id: c.id, name: c.name }))]}
                                        placeholder="Search category..."
                                        disabled={!formData.projectId || generating}
                                        onClear={() => setAccounts([])}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Account</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-user"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.accountId}
                                        onChange={(value) => setFormData({ ...formData, accountId: value ? parseInt(value) : null })}
                                        options={[{ id: null, name: 'All Accounts' }, ...accounts.map(a => ({ id: a.id, name: a.name }))]}
                                        placeholder="Search account..."
                                        disabled={!formData.categoryId || generating}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>&nbsp;</Form.Label>
                                <div className="d-grid">
                                    <Button 
                                        type="submit" 
                                        variant="primary" 
                                        disabled={generating || !formData.projectId}
                                        title={!formData.projectId ? 'Please select a project' : ''}
                                    >
                                        {generating ? (
                                            <>
                                                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                                Generating...
                                            </>
                                        ) : (
                                            <>
                                                <i className="fas fa-chart-bar me-1"></i> Generate Report
                                            </>
                                        )}
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </Form>
                </Card.Body>
            </Card>

            {allEntries.length > 0 && (
                <Card className="shadow mb-4">
                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-table me-2"></i>Account Report Results
                        </h6>
                        <div>
                            {totalRecords > 0 && (
                                <span className="badge bg-primary me-2">
                                    {allEntries.length} of {totalRecords} entries
                                </span>
                            )}
                            <Button variant="outline-success" size="sm" onClick={handleExportCsv} disabled={filteredEntries.length === 0}>
                                <i className="fas fa-file-csv me-1"></i> Export CSV
                            </Button>
                        </div>
                    </Card.Header>
                    <Card.Body>
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
                        <div className="table-responsive" style={{ overflow: 'visible' }}>
                            <Table bordered>
                                <thead className="table-light">
                                    <tr>
                                        <th>ID</th>
                                        <th>Category</th>
                                        <th>Account Name</th>
                                        <th className="text-end">Debit</th>
                                        <th className="text-end">Credit</th>
                                        <th className="text-end">Balance</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredEntries.map((entry, index) => {
                                        const balance = entry.netBalance || ((entry.totalDebit || 0) - (entry.totalCredit || 0));
                                        const formattedId = entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-';
                                        const isLastEntry = index === filteredEntries.length - 1;
                                        return (
                                            <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                <td>{formattedId}</td>
                                                <td>{entry.categoryName || '-'}</td>
                                                <td>{entry.accountName || '-'}</td>
                                                <td className="text-end">{formatCurrency(entry.totalDebit)}</td>
                                                <td className="text-end">{formatCurrency(entry.totalCredit)}</td>
                                                <td className={`text-end ${balance < 0 ? 'text-danger' : 'text-success'}`}>
                                                    {formatCurrency(balance)}
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                                <tfoot className="table-secondary">
                                    <tr className="fw-bold">
                                        <td colSpan="3" className="text-end">TOTAL</td>
                                        <td className="text-end">{formatCurrency(totals.debit)}</td>
                                        <td className="text-end">{formatCurrency(totals.credit)}</td>
                                        <td className={`text-end ${(totals.debit - totals.credit) < 0 ? 'text-danger' : 'text-success'}`}>
                                            {formatCurrency(totals.debit - totals.credit)}
                                        </td>
                                    </tr>
                                </tfoot>
                            </Table>
                        </div>
                        {isLoadingMore && (
                            <div className="text-center my-3">
                                <LoadingSpinner />
                            </div>
                        )}
                    </Card.Body>
                </Card>
            )}
        </div>
    );
};

export default AccountReport;
