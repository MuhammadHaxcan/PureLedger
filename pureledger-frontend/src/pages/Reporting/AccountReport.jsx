import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import SortableHeader from '../../components/SortableHeader';
import { usePageTitle } from '../../hooks/usePageTitle';
import useSortableData from '../../hooks/useSortableData';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';

const AccountReport = () => {
    usePageTitle('Account Report');
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [allEntries, setAllEntries] = useState([]);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [exportingCsv, setExportingCsv] = useState(false);
    const [exportingPdf, setExportingPdf] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        categoryId: null,
        accountId: null
    });
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 2500;

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
        }
    };

    const loadAccountsByCategory = async (categoryId) => {
        try {
            const response = await apiClient.get(`/ReportingApi/accounts-by-category/${categoryId}`);
            if (response.data.success) {
                setAccounts(response.data.data || []);
            }
        } catch (err) {
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
                if (data.hasMoreData && !searchTerm.trim()) {
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
        const formattedId = entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-';
        const balance = entry.netBalance || ((entry.totalDebit || 0) - (entry.totalCredit || 0));
        return (
            (entry.categoryName || '').toLowerCase().includes(search) ||
            (entry.accountName || '').toLowerCase().includes(search) ||
            entry.accountId?.toString().includes(search) ||
            entry.projectId?.toString().includes(search) ||
            entry.categoryId?.toString().includes(search) ||
            formattedId.toLowerCase().includes(search) ||
            (entry.totalDebit?.toString() || '').includes(search) ||
            (entry.totalCredit?.toString() || '').includes(search) ||
            balance?.toString().includes(search)
        );
    });

    // Add computed balance field for sorting, then apply sorting
    const entriesWithBalance = filteredEntries.map(entry => ({
        ...entry,
        balance: entry.netBalance || ((entry.totalDebit || 0) - (entry.totalCredit || 0))
    }));
    const { sortedItems: sortedEntries, requestSort, getSortDirection } = useSortableData(entriesWithBalance);

    const totals = sortedEntries.reduce((acc, entry) => ({
        debit: acc.debit + (entry.totalDebit || 0),
        credit: acc.credit + (entry.totalCredit || 0)
    }), { debit: 0, credit: 0 });

    const handleExportCsv = async () => {
        setExportingCsv(true);
        try {
            const csvData = sortedEntries.map(entry => ({
                ID: entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-',
                Category: entry.categoryName || '',
                'Account Name': entry.accountName || '',
                Debit: (entry.totalDebit || 0).toFixed(2),
                Credit: (entry.totalCredit || 0).toFixed(2),
                Balance: ((entry.totalDebit || 0) - (entry.totalCredit || 0)).toFixed(2)
            }));
            await exportToCsvAsync(csvData, `AccountReport_${new Date().toISOString().split('T')[0]}.csv`);
        } finally {
            setExportingCsv(false);
        }
    };

    const handleExportPdf = async () => {
        setExportingPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);
            const selectedCategory = categories.find(c => c.id === formData.categoryId);

            const rows = sortedEntries.map(entry => {
                const balance = entry.netBalance || ((entry.totalDebit || 0) - (entry.totalCredit || 0));
                return {
                    id: entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-',
                    category: entry.categoryName || '-',
                    account: entry.accountName || '-',
                    debit: formatCurrency(entry.totalDebit),
                    credit: formatCurrency(entry.totalCredit),
                    balance: formatCurrency(balance)
                };
            });

            rows.push({
                id: '',
                category: '',
                account: 'TOTAL',
                debit: formatCurrency(totals.debit),
                credit: formatCurrency(totals.credit),
                balance: formatCurrency(totals.debit - totals.credit)
            });

            const columns = [
                { header: 'ID', dataKey: 'id' },
                { header: 'Category', dataKey: 'category' },
                { header: 'Account', dataKey: 'account' },
                { header: 'Debit', dataKey: 'debit' },
                { header: 'Credit', dataKey: 'credit' },
                { header: 'Balance', dataKey: 'balance' }
            ];

            await generatePdfAsync({
                orientation: 'portrait',
                format: 'a4',
                title: 'Account Report',
                projectName: selectedProject?.name || 'All Projects',
                subtitle: selectedCategory ? `Category: ${selectedCategory.name}` : null,
                generatedInfo: `Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords}`,
                columns,
                rows,
                rowsPerPage: 20,
                startY: selectedCategory ? 46 : 40,
                fontSize: 9,
                cellPadding: 2,
                filename: `AccountReport_${new Date().toISOString().split('T')[0]}.pdf`
            });
        } finally {
            setExportingPdf(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value || 0);
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
                                        disabled={generating || isLoadingMore || hasMoreData || !formData.projectId}
                                        title={!formData.projectId ? 'Please select a project' : ''}
                                    >
                                        {generating ? (
                                            <>
                                                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                                Generating...
                                            </>
                                        ) : (
                                            <>
                                                <i className="fas fa-chart-bar me-2"></i> Generate Report
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
                            {!hasMoreData && allEntries.length > 0 && (
                                <>
                                    <Button 
                                        variant="outline-success" 
                                        size="sm" 
                                        onClick={handleExportCsv} 
                                        className="me-2"
                                        disabled={exportingCsv || exportingPdf}
                                    >
                                        {exportingCsv ? (
                                            <>
                                                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                                Generating...
                                            </>
                                        ) : (
                                            <>
                                                <i className="fas fa-file-csv me-1"></i> Export CSV
                                            </>
                                        )}
                                    </Button>
                                    <Button 
                                        variant="outline-danger" 
                                        size="sm" 
                                        onClick={handleExportPdf}
                                        disabled={exportingCsv || exportingPdf}
                                    >
                                        {exportingPdf ? (
                                            <>
                                                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                                Generating...
                                            </>
                                        ) : (
                                            <>
                                                <i className="fas fa-file-pdf me-1"></i> Export PDF
                                            </>
                                        )}
                                    </Button>
                                </>
                            )}
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
                                        disabled={generating || isLoadingMore || hasMoreData}
                                    />
                                </InputGroup>
                            </div>
                        </div>
                        <div className="report-table-container">
                            <div className="table-responsive">
                                <Table bordered striped hover className="table-sm mb-0" id="accountReportTable">
                                <thead className="table-light">
                                    <tr>
                                        <SortableHeader label="ID" sortKey="accountId" sortDirection={getSortDirection('accountId')} onSort={requestSort} />
                                        <SortableHeader label="Category" sortKey="categoryName" sortDirection={getSortDirection('categoryName')} onSort={requestSort} />
                                        <SortableHeader label="Account Name" sortKey="accountName" sortDirection={getSortDirection('accountName')} onSort={requestSort} />
                                        <SortableHeader label="Debit" sortKey="totalDebit" sortDirection={getSortDirection('totalDebit')} onSort={requestSort} className="text-end" />
                                        <SortableHeader label="Credit" sortKey="totalCredit" sortDirection={getSortDirection('totalCredit')} onSort={requestSort} className="text-end" />
                                        <SortableHeader label="Balance" sortKey="balance" sortDirection={getSortDirection('balance')} onSort={requestSort} className="text-end" />
                                    </tr>
                                </thead>
                                <tbody>
                                    {sortedEntries.length === 0 && allEntries.length > 0 ? (
                                        <tr>
                                            <td colSpan="6" className="text-center text-muted py-4">
                                                {searchTerm ? 'No results found for your search' : 'No data available'}
                                            </td>
                                        </tr>
                                    ) : sortedEntries.length === 0 && allEntries.length === 0 ? (
                                        <tr>
                                            <td colSpan="6" className="text-center text-muted py-4">
                                                No data available
                                            </td>
                                        </tr>
                                    ) : (
                                        sortedEntries.map((entry, index) => {
                                            const formattedId = entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-';
                                            const isLastEntry = index === sortedEntries.length - 1;
                                            return (
                                                <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                    <td>{formattedId}</td>
                                                    <td>{entry.categoryName || '-'}</td>
                                                    <td>{entry.accountName || '-'}</td>
                                                    <td className="text-end">{formatCurrency(entry.totalDebit)}</td>
                                                    <td className="text-end">{formatCurrency(entry.totalCredit)}</td>
                                                    <td className={`text-end ${entry.balance < 0 ? 'text-danger' : 'text-success'}`}>
                                                        {formatCurrency(entry.balance)}
                                                    </td>
                                                </tr>
                                            );
                                        })
                                    )}
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
                        </div>
                        {hasMoreData && isLoadingMore && (
                            <div className="text-center mt-3">
                                <div className="spinner-border spinner-border-sm text-primary" role="status">
                                    <span className="visually-hidden">Loading more records...</span>
                                </div>
                            </div>
                        )}
                    </Card.Body>
                </Card>
            )}
        </div>
    );
};

export default AccountReport;
