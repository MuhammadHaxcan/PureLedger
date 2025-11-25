import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import axios from 'axios';
import apiClient, { API_BASE_URL } from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import './MasterReport.css';

const buildVoucherUrl = (voucherId, type) => {
    if (!voucherId) {
        return '#';
    }
    return `/reporting/print-voucher?voucherId=${voucherId}&type=${type}`;
};

const MasterReport = () => {
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [bookers, setBookers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [allEntries, setAllEntries] = useState([]);
    const [accountSummary, setAccountSummary] = useState([]);
    const [categorySummary, setCategorySummary] = useState([]);
    const [searchTerm, setSearchTerm] = useState('');
    const [loadedRecords, setLoadedRecords] = useState(0);
    const [visibleColumns, setVisibleColumns] = useState({
        date: true,
        creditableDate: true,
        description: true,
        account: true,
        booker: true,
        quantity: true,
        debit: true,
        credit: true,
        balance: true
    });
    const [formData, setFormData] = useState({
        startDate: '',
        endDate: '',
        projectId: null,
        categoryId: null,
        accountId: null,
        bookerId: null
    });
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [statusMessage, setStatusMessage] = useState('');
    const [previousBalance, setPreviousBalance] = useState(0);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 250;
    const searchTermRef = useRef('');
    const abortControllerRef = useRef(null);
    const activeFiltersRef = useRef(null);

    useEffect(() => {
        loadInitData();
        loadBookers();
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

    useEffect(() => {
        searchTermRef.current = searchTerm.trim();
    }, [searchTerm]);

    useEffect(() => {
        return () => {
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }
        };
    }, []);

    const loadInitData = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/ReportingApi/master-report-init');
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

    const loadBookers = async () => {
        try {
            const response = await apiClient.get('/ReportingApi/bookers');
            if (response.data.success) {
                setBookers(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading bookers:', err);
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

    const resetReportState = () => {
        setAllEntries([]);
        setAccountSummary([]);
        setCategorySummary([]);
        setLoadedRecords(0);
        setTotalRecords(0);
        setStatusMessage('');
        setCurrentPage(0);
        setHasMoreData(false);
        setPreviousBalance(0);
        setIsLoadingMore(false);
        setIsAutoLoading(false);
    };

    const ensureAbortController = () => {
        if (!abortControllerRef.current || abortControllerRef.current.signal.aborted) {
            abortControllerRef.current = new AbortController();
        }
        return abortControllerRef.current;
    };

    const generateReport = async (page = 0, append = false) => {
        if (!formData.projectId) {
            setError('Please select a project');
            return;
        }

        if (page === 0) {
            setGenerating(true);
            setAllEntries([]);
            setAccountSummary([]);
            setCategorySummary([]);
            setSearchTerm('');
            setCurrentPage(0);
        } else {
            setIsLoadingMore(true);
        }

        setError('');

        try {
            const params = {
                startDate: formData.startDate || null,
                endDate: formData.endDate || null,
                projectId: formData.projectId.toString(),
                categoryId: formData.categoryId ? formData.categoryId.toString() : '',
                accountId: formData.accountId ? formData.accountId.toString() : '',
                bookerId: formData.bookerId ? formData.bookerId.toString() : '',
                page: page,
                pageSize: pageSize
            };

            const response = await apiClient.get('/ReportingApi/master-report', { params });
            
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const entries = data.entries || [];

                if (append) {
                    setAllEntries(prev => [...prev, ...entries]);
                } else {
                    setAllEntries(entries);
                    setAccountSummary([]);
                    setCategorySummary([]);
                }

                setHasMoreData(data.hasMoreData || false);
                setTotalRecords(data.totalRecords || 0);
                setPreviousBalance(data.previousBalance || 0);
                setCurrentPage(page + 1);

                // Calculate summaries
                const allEntriesForSummary = append ? [...allEntries, ...entries] : entries;
                calculateSummaries(allEntriesForSummary);

                // Auto-load next page if there's more data and not searching
                // Remove the append check so it auto-loads after first page too
                if (data.hasMoreData && !searchTerm.trim() && !isLoadingMore && !isAutoLoading) {
                    setIsAutoLoading(true);
                    setTimeout(() => {
                        generateReport(page + 1, true);
                    }, 100);
                } else {
                    setIsAutoLoading(false);
                }
            } else {
                setError('Failed to generate report');
                setIsAutoLoading(false);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to generate report');
            setIsAutoLoading(false);
        } finally {
            setGenerating(false);
            setIsLoadingMore(false);
        }
    };

    const calculateSummaries = useCallback((entries) => {
        const accountMap = {};
        const categoryMap = {};

        entries.forEach(entry => {
            // Account summary
            if (entry.accountId) {
                const key = entry.accountId.toString();
                if (!accountMap[key]) {
                    accountMap[key] = {
                        id: `${entry.projectId}-${entry.categoryId}-${entry.accountId}`,
                        name: entry.accountName || '',
                        debit: 0,
                        credit: 0
                    };
                }
                accountMap[key].debit += entry.debit || 0;
                accountMap[key].credit += entry.credit || 0;
            }

            // Category summary
            if (entry.categoryId) {
                const key = entry.categoryId.toString();
                if (!categoryMap[key]) {
                    categoryMap[key] = {
                        id: `${entry.projectId}-${entry.categoryId}`,
                        name: entry.categoryName || '',
                        debit: 0,
                        credit: 0
                    };
                }
                categoryMap[key].debit += entry.debit || 0;
                categoryMap[key].credit += entry.credit || 0;
            }
        });

        setAccountSummary(Object.values(accountMap));
        setCategorySummary(Object.values(categoryMap));
    }, []);

    const handleSubmit = (e) => {
        e.preventDefault();
        generateReport(0, false);
    };

    const handleLoadMore = () => {
        if (!isLoadingMore && hasMoreData) {
            generateReport(currentPage, true);
        }
    };

    const filteredEntries = allEntries.filter(entry => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        return (
            (entry.description || '').toLowerCase().includes(search) ||
            (entry.accountName || '').toLowerCase().includes(search) ||
            (entry.categoryName || '').toLowerCase().includes(search) ||
            (entry.bookerName || '').toLowerCase().includes(search) ||
            entry.journalVoucherId?.toString().includes(search) ||
            entry.id?.toString().includes(search)
        );
    });

    const totals = filteredEntries.reduce((acc, entry) => ({
        quantity: acc.quantity + (entry.quantity || 0),
        debit: acc.debit + (entry.debit || 0),
        credit: acc.credit + (entry.credit || 0)
    }), { quantity: 0, debit: 0, credit: 0 });

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
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const year = date.getFullYear();
            return `${month}/${day}/${year}`;
        } catch {
            return dateStr;
        }
    };

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

    const handleExportMainReport = () => {
        const csvData = filteredEntries.map(entry => ({
            ID: entry.formattedId || `JV-${entry.journalVoucherId}, Entry: ${entry.id}`,
            'Entry Date': formatDate(entry.transactionDate),
            'Creditable Date': formatDate(entry.creditableDate),
            Description: entry.description || '',
            Account: entry.accountName || '',
            Booker: entry.bookerName || '',
            Quantity: entry.quantity || '',
            Debit: entry.debit || 0,
            Credit: entry.credit || 0,
            Balance: entry.balance || 0
        }));
        exportToCsv(csvData, `MasterReport_${new Date().toISOString().split('T')[0]}.csv`);
    };

    const handleExportAccountSummary = () => {
        const csvData = accountSummary.map(item => ({
            ID: item.id,
            Account: item.name,
            Debit: item.debit,
            Credit: item.credit,
            Balance: item.debit - item.credit
        }));
        exportToCsv(csvData, `AccountSummary_${new Date().toISOString().split('T')[0]}.csv`);
    };

    const handleExportCategorySummary = () => {
        const csvData = categorySummary.map(item => ({
            ID: item.id,
            Category: item.name,
            Debit: item.debit,
            Credit: item.credit,
            Balance: item.debit - item.credit
        }));
        exportToCsv(csvData, `CategorySummary_${new Date().toISOString().split('T')[0]}.csv`);
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-file-alt me-2"></i>Master Report</h2>
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
                                <Form.Label>Since</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        value={formData.startDate}
                                        onChange={(e) => setFormData({ ...formData, startDate: e.target.value })}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>To</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        value={formData.endDate}
                                        onChange={(e) => setFormData({ ...formData, endDate: e.target.value })}
                                    />
                                </InputGroup>
                            </div>
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
                        </div>
                        <div className="row mb-3">
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
                                <Form.Label>Booker</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-user-tag"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.bookerId}
                                        onChange={(value) => setFormData({ ...formData, bookerId: value ? parseInt(value) : null })}
                                        options={bookers.map(b => ({ id: b.id, name: b.name }))}
                                        placeholder="Search booker..."
                                        disabled={generating}
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
                        {allEntries.length > 0 && (
                            <div className="row mb-3">
                                <div className="col-12">
                                    <Form.Label className="d-block">Column Visibility</Form.Label>
                                    <div className="btn-group column-toggles" role="group">
                                        {Object.entries({
                                            showDate: { key: 'date', label: 'Entry Date', icon: 'fa-calendar-day' },
                                            showCreditableDate: { key: 'creditableDate', label: 'Creditable Date', icon: 'fa-calendar-check' },
                                            showDescription: { key: 'description', label: 'Description', icon: 'fa-align-left' },
                                            showAccount: { key: 'account', label: 'Account', icon: 'fa-user' },
                                            showBooker: { key: 'booker', label: 'Booker', icon: 'fa-user-tag' },
                                            showQuantity: { key: 'quantity', label: 'Quantity', icon: 'fa-cubes' },
                                            showDebit: { key: 'debit', label: 'Debit', icon: 'fa-plus' },
                                            showCredit: { key: 'credit', label: 'Credit', icon: 'fa-minus' },
                                            showBalance: { key: 'balance', label: 'Balance', icon: 'fa-balance-scale' }
                                        }).map(([id, config]) => (
                                            <React.Fragment key={id}>
                                                <input
                                                    type="checkbox"
                                                    className="btn-check"
                                                    id={id}
                                                    checked={visibleColumns[config.key]}
                                                    onChange={(e) => setVisibleColumns(prev => ({ ...prev, [config.key]: e.target.checked }))}
                                                    autoComplete="off"
                                                />
                                                <label className="btn btn-outline-primary btn-sm" htmlFor={id}>
                                                    <i className={`fas ${config.icon} me-1`}></i>{config.label}
                                                </label>
                                            </React.Fragment>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        )}
                    </Form>
                </Card.Body>
            </Card>

            {allEntries.length > 0 && (
                <>
                    <Card className="shadow mb-4">
                        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                            <h6 className="m-0 font-weight-bold text-primary">
                                <i className="fas fa-table me-2"></i>Master Report Results
                            </h6>
                        </Card.Header>
                        <Card.Body>
                            <div className="master-report-container">
                                <div className="row mb-3 align-items-center">
                                    <div className="col-md-9">
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
                                    <div className="col-md-3 text-end">
                                        <Form.Label className="d-block">&nbsp;</Form.Label>
                                        <Button
                                            variant="outline-success"
                                            onClick={handleExportMainReport}
                                            disabled={filteredEntries.length === 0}
                                        >
                                            <i className="fas fa-file-csv me-1"></i> Export CSV
                                        </Button>
                                    </div>
                                </div>
                                <div className="table-responsive" style={{ width: '100%', overflowX: 'auto' }}>
                                    <Table bordered striped hover className="table-sm mb-0 w-100" id="masterReportTable" style={{ width: '100%', tableLayout: 'auto' }}>
                                    <thead className="table-light">
                                        <tr>
                                            <th className="id-column" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>ID</th>
                                            {visibleColumns.date && <th className="date-column" style={{ minWidth: '100px', whiteSpace: 'nowrap' }}>Entry Date</th>}
                                            {visibleColumns.creditableDate && <th className="creditable-date-column" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Creditable Date</th>}
                                            {visibleColumns.description && <th className="description-column" style={{ minWidth: '200px' }}>Description</th>}
                                            {visibleColumns.account && <th className="account-column" style={{ minWidth: '150px' }}>Account</th>}
                                            {visibleColumns.booker && <th className="booker-column" style={{ minWidth: '120px' }}>Booker</th>}
                                            {visibleColumns.quantity && <th className="quantity-column text-end" style={{ minWidth: '80px', whiteSpace: 'nowrap' }}>Quantity</th>}
                                            {visibleColumns.debit && <th className="debit-column text-end" style={{ minWidth: '100px', whiteSpace: 'nowrap' }}>Debit</th>}
                                            {visibleColumns.credit && <th className="credit-column text-end" style={{ minWidth: '100px', whiteSpace: 'nowrap' }}>Credit</th>}
                                            {visibleColumns.balance && <th className="balance-column text-end" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>Balance</th>}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredEntries.map((entry, index) => {
                                            const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";
                                            return (
                                                <tr key={index} className={isOpening ? 'table-info' : ''}>
                                                    <td className="id-column" style={{ whiteSpace: 'nowrap' }}>
                                                        {isOpening ? (
                                                            <span className="fw-bold text-primary">OPENING</span>
                                                        ) : (
                                                            <div>
                                                                <div className="d-flex flex-wrap gap-1 align-items-center">
                                                                    <a
                                                                        href={buildVoucherUrl(entry.journalVoucherId, 'JV')}
                                                                        target="_blank"
                                                                        rel="noopener noreferrer"
                                                                        className="voucher-link text-decoration-none badge bg-primary me-1"
                                                                        style={{ fontSize: '0.75rem', cursor: 'pointer' }}
                                                                    >
                                                                        JV-{entry.journalVoucherId}
                                                                    </a>
                                                                    <a
                                                                        href={buildVoucherUrl(entry.journalVoucherId, 'INV')}
                                                                        target="_blank"
                                                                        rel="noopener noreferrer"
                                                                        className="voucher-link text-decoration-none badge bg-success"
                                                                        style={{ fontSize: '0.75rem', cursor: 'pointer' }}
                                                                    >
                                                                        INV-{entry.journalVoucherId}
                                                                    </a>
                                                                </div>
                                                                <div className="entry-id mt-1 small text-muted">Entry: {entry.id}</div>
                                                            </div>
                                                        )}
                                                    </td>
                                                    {visibleColumns.date && <td className="date-column">{formatDate(entry.transactionDate)}</td>}
                                                    {visibleColumns.creditableDate && <td className="creditable-date-column">{formatDate(entry.creditableDate)}</td>}
                                                    {visibleColumns.description && <td className="description-column" style={{ wordWrap: 'break-word', whiteSpace: 'normal' }}>{entry.description || ''}</td>}
                                                    {visibleColumns.account && <td className="account-column">{entry.accountName || ''}</td>}
                                                    {visibleColumns.booker && <td className="booker-column">{entry.bookerName || ''}</td>}
                                                    {visibleColumns.quantity && <td className="quantity-column text-end">{entry.quantity ?? ''}</td>}
                                                    {visibleColumns.debit && <td className="debit-column text-end">{formatCurrency(entry.debit)}</td>}
                                                    {visibleColumns.credit && <td className="credit-column text-end">{formatCurrency(entry.credit)}</td>}
                                                    {visibleColumns.balance && <td className="balance-column text-end">{formatCurrency(entry.balance)}</td>}
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                    <tfoot className="table-secondary">
                                        <tr>
                                            <td className="id-column text-end fw-bold">TOTAL</td>
                                            {visibleColumns.date && <td></td>}
                                            {visibleColumns.creditableDate && <td></td>}
                                            {visibleColumns.description && <td></td>}
                                            {visibleColumns.account && <td></td>}
                                            {visibleColumns.booker && <td></td>}
                                            {visibleColumns.quantity && <td className="quantity-column text-end fw-bold">{totals.quantity}</td>}
                                            {visibleColumns.debit && <td className="debit-column text-end fw-bold">{formatCurrency(totals.debit)}</td>}
                                            {visibleColumns.credit && <td className="credit-column text-end fw-bold">{formatCurrency(totals.credit)}</td>}
                                            {visibleColumns.balance && <td className="balance-column text-end fw-bold">{formatCurrency(totals.debit - totals.credit)}</td>}
                                        </tr>
                                    </tfoot>
                                </Table>
                                </div>
                            </div>
                            {hasMoreData && (
                                <div className="text-center mt-3">
                                    <div className="text-muted mb-2">
                                        Loaded {allEntries.length} of {totalRecords} records
                                    </div>
                                    {isLoadingMore && (
                                        <div className="spinner-border spinner-border-sm text-primary" role="status">
                                            <span className="visually-hidden">Loading more records...</span>
                                        </div>
                                    )}
                                </div>
                            )}
                            {!hasMoreData && allEntries.length > 0 && (
                                <div className="text-center mt-3 text-muted">
                                    All {allEntries.length} records loaded
                                </div>
                            )}
                        </Card.Body>
                    </Card>

                    <Card className="shadow mb-4">
                        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                            <h6 className="m-0 font-weight-bold text-primary">
                                <i className="fas fa-users me-2"></i>Account Summary
                            </h6>
                            <Button
                                variant="outline-success"
                                size="sm"
                                onClick={handleExportAccountSummary}
                                disabled={accountSummary.length === 0}
                            >
                                <i className="fas fa-file-csv me-1"></i> Export CSV
                            </Button>
                        </Card.Header>
                        <Card.Body>
                            <div className="table-responsive">
                                <Table bordered striped>
                                    <thead className="table-light">
                                        <tr>
                                            <th>ID</th>
                                            <th>Account</th>
                                            <th className="text-end">Debit</th>
                                            <th className="text-end">Credit</th>
                                            <th className="text-end">Balance</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {accountSummary.length > 0 ? (
                                            accountSummary.map((item, index) => {
                                                const balance = item.debit - item.credit;
                                                return (
                                                    <tr key={index}>
                                                        <td>{item.id}</td>
                                                        <td>{item.name}</td>
                                                        <td className="text-end">{formatCurrency(item.debit)}</td>
                                                        <td className="text-end">{formatCurrency(item.credit)}</td>
                                                        <td className={`text-end ${balance < 0 ? 'text-danger' : 'text-success'}`}>
                                                            {formatCurrency(balance)}
                                                        </td>
                                                    </tr>
                                                );
                                            })
                                        ) : (
                                            <tr>
                                                <td colSpan="5" className="text-center">No data available</td>
                                            </tr>
                                        )}
                                    </tbody>
                                </Table>
                            </div>
                        </Card.Body>
                    </Card>

                    <Card className="shadow mb-4">
                        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                            <h6 className="m-0 font-weight-bold text-primary">
                                <i className="fas fa-tags me-2"></i>Category Summary
                            </h6>
                            <Button
                                variant="outline-success"
                                size="sm"
                                onClick={handleExportCategorySummary}
                                disabled={categorySummary.length === 0}
                            >
                                <i className="fas fa-file-csv me-1"></i> Export CSV
                            </Button>
                        </Card.Header>
                        <Card.Body>
                            <div className="table-responsive">
                                <Table bordered striped>
                                    <thead className="table-light">
                                        <tr>
                                            <th>ID</th>
                                            <th>Category</th>
                                            <th className="text-end">Debit</th>
                                            <th className="text-end">Credit</th>
                                            <th className="text-end">Balance</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {categorySummary.length > 0 ? (
                                            categorySummary.map((item, index) => {
                                                const balance = item.debit - item.credit;
                                                return (
                                                    <tr key={index}>
                                                        <td>{item.id}</td>
                                                        <td>{item.name}</td>
                                                        <td className="text-end">{formatCurrency(item.debit)}</td>
                                                        <td className="text-end">{formatCurrency(item.credit)}</td>
                                                        <td className={`text-end ${balance < 0 ? 'text-danger' : 'text-success'}`}>
                                                            {formatCurrency(balance)}
                                                        </td>
                                                    </tr>
                                                );
                                            })
                                        ) : (
                                            <tr>
                                                <td colSpan="5" className="text-center">No data available</td>
                                            </tr>
                                        )}
                                    </tbody>
                                </Table>
                            </div>
                        </Card.Body>
                    </Card>
                </>
            )}
        </div>
    );
};

export default MasterReport;
