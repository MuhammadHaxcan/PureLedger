import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import Papa from 'papaparse';

const AccountReport = () => {
    usePageTitle('Account Report');
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
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
    const pageSize = 1000;

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

    const totals = filteredEntries.reduce((acc, entry) => ({
        debit: acc.debit + (entry.totalDebit || 0),
        credit: acc.credit + (entry.totalCredit || 0)
    }), { debit: 0, credit: 0 });

    const handleExportCsv = () => {
        const csvData = filteredEntries.map(entry => ({
            ID: entry.accountId ? `${entry.projectId}-${entry.categoryId}-${entry.accountId}` : '-',
            Category: entry.categoryName || '',
            'Account Name': entry.accountName || '',
            Debit: (entry.totalDebit || 0).toFixed(2),
            Credit: (entry.totalCredit || 0).toFixed(2),
            Balance: ((entry.totalDebit || 0) - (entry.totalCredit || 0)).toFixed(2)
        }));

        if (!csvData || csvData.length === 0) {
            alert('No data to export');
            return;
        }

        const csvContent = Papa.unparse(csvData);
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = `AccountReport_${new Date().toISOString().split('T')[0]}.csv`;
        link.click();
    };

    const handleExportPdf = () => {
        const doc = new jsPDF('portrait', 'mm', 'a4');
        const selectedProject = projects.find(p => p.id === formData.projectId);
        const selectedCategory = categories.find(c => c.id === formData.categoryId);

        // Header
        doc.setFontSize(18);
        doc.setTextColor(30, 64, 175);
        doc.text('Account Report', 14, 20);

        doc.setFontSize(10);
        doc.setTextColor(100);
        doc.text(`Project: ${selectedProject?.name || 'All Projects'}`, 14, 28);
        if (selectedCategory) {
            doc.text(`Category: ${selectedCategory.name}`, 14, 34);
        }
        doc.text(`Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords}`, 14, selectedCategory ? 40 : 34);

        const rows = filteredEntries.map(entry => {
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

        // Add totals row
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

        // Split rows into chunks per page
        const rowsPerPage = 20;
        let startY = selectedCategory ? 46 : 40;

        for (let i = 0; i < rows.length; i += rowsPerPage) {
            if (i > 0) {
                doc.addPage();
                startY = 20;
            }

            const chunk = rows.slice(i, i + rowsPerPage);

            autoTable(doc, {
                columns,
                body: chunk,
                startY: startY,
                styles: { fontSize: 8 },
                headStyles: { fillColor: [241, 245, 249], textColor: [30, 41, 59], fontStyle: 'bold' }
            });
        }

        // Add page numbers to all pages
        const pageCount = doc.internal.getNumberOfPages();
        for (let i = 1; i <= pageCount; i++) {
            doc.setPage(i);
            doc.setFontSize(8);
            doc.setTextColor(100);
            doc.text(
                `PureLedger - Account Report | Page ${i} of ${pageCount}`,
                14,
                doc.internal.pageSize.height - 10
            );
        }

        doc.save(`AccountReport_${new Date().toISOString().split('T')[0]}.pdf`);
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
                                    <Button variant="outline-success" size="sm" onClick={handleExportCsv} className="me-2">
                                        <i className="fas fa-file-csv me-1"></i> Export CSV
                                    </Button>
                                    <Button variant="outline-danger" size="sm" onClick={handleExportPdf}>
                                        <i className="fas fa-file-pdf me-1"></i> Export PDF
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
                        <div className="table-responsive" style={{ width: '100%', overflowX: 'auto' }}>
                            <Table bordered striped hover className="table-sm mb-0 w-100" style={{ width: '100%', tableLayout: 'auto' }}>
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
                                    {filteredEntries.length === 0 && allEntries.length > 0 ? (
                                        <tr>
                                            <td colSpan="6" className="text-center text-muted py-4">
                                                {searchTerm ? 'No results found for your search' : 'No data available'}
                                            </td>
                                        </tr>
                                    ) : filteredEntries.length === 0 && allEntries.length === 0 ? (
                                        <tr>
                                            <td colSpan="6" className="text-center text-muted py-4">
                                                No data available
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredEntries.map((entry, index) => {
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
