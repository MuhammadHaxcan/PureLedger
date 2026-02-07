import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getDateRange, dateFilterOptions } from '../../utils/dateFilters';

const buildVoucherUrl = (voucherId, type) => {
    if (!voucherId) {
        return '#';
    }
    return `/reporting/print-voucher?voucherId=${voucherId}&type=${type}`;
};

const UserReport = () => {
    usePageTitle('User Report');
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [bookers, setBookers] = useState([]);
    const [locations, setLocations] = useState([]);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [exportingMainCsv, setExportingMainCsv] = useState(false);
    const [exportingMainPdf, setExportingMainPdf] = useState(false);
    const [exportingAccountCsv, setExportingAccountCsv] = useState(false);
    const [exportingAccountPdf, setExportingAccountPdf] = useState(false);
    const [exportingCategoryCsv, setExportingCategoryCsv] = useState(false);
    const [exportingCategoryPdf, setExportingCategoryPdf] = useState(false);
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
        location: true,
        quantity: true,
        qtyBalance: false,
        rate: false,
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
        bookerId: null,
        locationId: null
    });
    const [dateFilterType, setDateFilterType] = useState('custom');
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [statusMessage, setStatusMessage] = useState('');
    const [previousBalance, setPreviousBalance] = useState(0);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 1000;
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
            const response = await apiClient.get('/ReportingApi/user-report-init');
            if (response.data.success) {
                setProjects(response.data.projects || []);
                setLocations(response.data.locations || []);
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
            const response = await apiClient.get(`/ReportingApi/user-report-categories-by-project/${projectId}`);
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

        if (!formData.categoryId) {
            setError('Please select a category');
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
                categoryId: formData.categoryId.toString(),
                accountId: formData.accountId ? formData.accountId.toString() : '',
                bookerId: formData.bookerId ? formData.bookerId.toString() : '',
                locationId: formData.locationId ? formData.locationId.toString() : '',
                page: page,
                pageSize: pageSize,
                showQtyBalance: visibleColumns.qtyBalance || false
            };

            const response = await apiClient.get('/ReportingApi/user-report', { params });
            
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

    const handleDateFilterChange = (filterType) => {
        setDateFilterType(filterType);
        if (filterType === 'custom') {
            return;
        }
        const dateRange = getDateRange(filterType);
        setFormData(prev => ({
            ...prev,
            startDate: dateRange.startDate,
            endDate: dateRange.endDate
        }));
    };

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
        // Format dates for searching
        const entryDateStr = entry.entryDate 
            ? new Date(entry.entryDate).toLocaleDateString().toLowerCase()
            : '';
        const creditableDateStr = entry.creditableDate 
            ? new Date(entry.creditableDate).toLocaleDateString().toLowerCase()
            : '';
        return (
            (entry.description || '').toLowerCase().includes(search) ||
            (entry.accountName || '').toLowerCase().includes(search) ||
            (entry.categoryName || '').toLowerCase().includes(search) ||
            (entry.bookerName || '').toLowerCase().includes(search) ||
            (entry.locationName || '').toLowerCase().includes(search) ||
            entry.journalVoucherId?.toString().includes(search) ||
            entry.id?.toString().includes(search) ||
            entryDateStr.includes(search) ||
            creditableDateStr.includes(search) ||
            (entry.quantity?.toString() || '').includes(search) ||
            (entry.debit?.toString() || '').includes(search) ||
            (entry.credit?.toString() || '').includes(search) ||
            (entry.balance?.toString() || '').includes(search) ||
            `jv-${entry.journalVoucherId || ''}`.includes(search) ||
            `inv-${entry.journalVoucherId || ''}`.includes(search) ||
            `entry: ${entry.id || ''}`.includes(search) ||
            entry.formattedId?.toLowerCase().includes(search) ||
            'opening'.includes(search) && (entry.formattedId === "OPENING" || entry.description === "Opening Balance")
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
            maximumFractionDigits: 2
        }).format(value || 0);
    };

    const formatDate = (dateStr) => {
        if (!dateStr) return '';
        try {
            const date = new Date(dateStr);
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const year = String(date.getFullYear()).slice(-2); // Get last 2 digits of year
            return `${month}/${day}/${year}`;
        } catch {
            return dateStr;
        }
    };

    const handleExportMainReport = async () => {
        setExportingMainCsv(true);
        try {
            const csvData = filteredEntries.map(entry => {
                const row = { ID: entry.formattedId || `JV-${entry.journalVoucherId}, Entry: ${entry.id}` };
                if (visibleColumns.date) row['Entry Date'] = formatDate(entry.transactionDate);
                if (visibleColumns.creditableDate) row['Credit Date'] = formatDate(entry.creditableDate);
                if (visibleColumns.description) row['Description'] = entry.description || '';
                if (visibleColumns.account) row['Account'] = entry.accountName || '';
                if (visibleColumns.booker) row['Booker'] = entry.bookerName || '';
                if (visibleColumns.location) row['Location'] = entry.locationName || '-';
                if (visibleColumns.quantity) row['Quantity'] = entry.quantity || '';
                if (visibleColumns.qtyBalance) row['Qty Balance'] = entry.qtyBalance || 0;
                if (visibleColumns.rate) row['Rate'] = entry.rate || 0;
                if (visibleColumns.debit) row['Debit'] = entry.debit || 0;
                if (visibleColumns.credit) row['Credit'] = entry.credit || 0;
                if (visibleColumns.balance) row['Balance'] = entry.balance || 0;
                return row;
            });
            await exportToCsvAsync(csvData, `UserReport_${new Date().toISOString().split('T')[0]}.csv`);
        } finally {
            setExportingMainCsv(false);
        }
    };

    const handleExportAccountSummary = async () => {
        setExportingAccountCsv(true);
        try {
            const csvData = accountSummary.map(item => ({
                ID: item.id,
                Account: item.name,
                Debit: item.debit,
                Credit: item.credit,
                Balance: item.debit - item.credit
            }));
            await exportToCsvAsync(csvData, `UserReportAccountSummary_${new Date().toISOString().split('T')[0]}.csv`);
        } finally {
            setExportingAccountCsv(false);
        }
    };

    const handleExportCategorySummary = async () => {
        setExportingCategoryCsv(true);
        try {
            const csvData = categorySummary.map(item => ({
                ID: item.id,
                Category: item.name,
                Debit: item.debit,
                Credit: item.credit,
                Balance: item.debit - item.credit
            }));
            await exportToCsvAsync(csvData, `UserReportCategorySummary_${new Date().toISOString().split('T')[0]}.csv`);
        } finally {
            setExportingCategoryCsv(false);
        }
    };

    const handleExportMainReportPdf = async () => {
        setExportingMainPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);
            
            const columns = [{ header: 'ID', dataKey: 'id' }];
            if (visibleColumns.date) columns.push({ header: 'Entry Date', dataKey: 'date' });
            if (visibleColumns.creditableDate) columns.push({ header: 'Credit Date', dataKey: 'creditableDate' });
            if (visibleColumns.description) columns.push({ header: 'Description', dataKey: 'description' });
            if (visibleColumns.account) columns.push({ header: 'Account', dataKey: 'account' });
            if (visibleColumns.booker) columns.push({ header: 'Booker', dataKey: 'booker' });
            if (visibleColumns.location) columns.push({ header: 'Location', dataKey: 'location' });
            if (visibleColumns.quantity) columns.push({ header: 'Qty', dataKey: 'quantity' });
            if (visibleColumns.qtyBalance) columns.push({ header: 'Qty Bal', dataKey: 'qtyBalance' });
            if (visibleColumns.rate) columns.push({ header: 'Rate', dataKey: 'rate' });
            if (visibleColumns.debit) columns.push({ header: 'Debit', dataKey: 'debit' });
            if (visibleColumns.credit) columns.push({ header: 'Credit', dataKey: 'credit' });
            if (visibleColumns.balance) columns.push({ header: 'Balance', dataKey: 'balance' });

            const rows = filteredEntries.map(entry => {
                const isOpening = entry.formattedId === "OPENING" || entry.description === "Opening Balance";
                return {
                    id: isOpening ? 'OPENING' : `JV-${entry.journalVoucherId}\nEntry: ${entry.id}`,
                    date: formatDate(entry.transactionDate),
                    creditableDate: formatDate(entry.creditableDate),
                    description: entry.description || '',
                    account: entry.accountName || '',
                    booker: entry.bookerName || '',
                    location: entry.locationName || '-',
                    quantity: entry.quantity ?? '',
                    qtyBalance: entry.qtyBalance ?? '',
                    rate: entry.rate ? formatCurrency(entry.rate) : '',
                    debit: formatCurrency(entry.debit),
                    credit: formatCurrency(entry.credit),
                    balance: formatCurrency(entry.balance)
                };
            });

            rows.push({
                id: 'TOTAL',
                date: '',
                creditableDate: '',
                description: '',
                account: '',
                booker: '',
                location: '',
                quantity: totals.quantity,
                qtyBalance: '',
                rate: '',
                debit: formatCurrency(totals.debit),
                credit: formatCurrency(totals.credit),
                balance: formatCurrency(totals.debit - totals.credit)
            });

            const dateRange = formData.startDate 
                ? `${formatDate(formData.startDate)} to ${formatDate(formData.endDate || formData.startDate)}`
                : null;

            await generatePdfAsync({
                orientation: 'landscape',
                format: 'a4',
                title: 'User Report',
                projectName: selectedProject?.name || 'All Projects',
                dateRange,
                generatedInfo: `Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords || filteredEntries.length}`,
                columns,
                rows,
                rowsPerPage: 14,
                startY: 46,
                fontSize: 9,
                cellPadding: 2,
                filename: `UserReport_${new Date().toISOString().split('T')[0]}.pdf`
            });
        } finally {
            setExportingMainPdf(false);
        }
    };

    const handleExportAccountSummaryPdf = async () => {
        setExportingAccountPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);
            
            const rows = accountSummary.map(item => ({
                id: item.id,
                account: item.name,
                debit: formatCurrency(item.debit),
                credit: formatCurrency(item.credit),
                balance: formatCurrency(item.debit - item.credit)
            }));

            const columns = [
                { header: 'ID', dataKey: 'id' },
                { header: 'Account', dataKey: 'account' },
                { header: 'Debit', dataKey: 'debit' },
                { header: 'Credit', dataKey: 'credit' },
                { header: 'Balance', dataKey: 'balance' }
            ];

            await generatePdfAsync({
                orientation: 'portrait',
                format: 'a4',
                title: 'User Report - Account Summary',
                projectName: selectedProject?.name || 'All Projects',
                generatedInfo: `Generated: ${new Date().toLocaleString()}`,
                columns,
                rows,
                rowsPerPage: 10,
                startY: 40,
                fontSize: 9,
                cellPadding: 2,
                filename: `UserReportAccountSummary_${new Date().toISOString().split('T')[0]}.pdf`
            });
        } finally {
            setExportingAccountPdf(false);
        }
    };

    const handleExportCategorySummaryPdf = async () => {
        setExportingCategoryPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);
            
            const rows = categorySummary.map(item => ({
                id: item.id,
                category: item.name,
                debit: formatCurrency(item.debit),
                credit: formatCurrency(item.credit),
                balance: formatCurrency(item.debit - item.credit)
            }));

            const columns = [
                { header: 'ID', dataKey: 'id' },
                { header: 'Category', dataKey: 'category' },
                { header: 'Debit', dataKey: 'debit' },
                { header: 'Credit', dataKey: 'credit' },
                { header: 'Balance', dataKey: 'balance' }
            ];

            await generatePdfAsync({
                orientation: 'portrait',
                format: 'a4',
                title: 'User Report - Category Summary',
                projectName: selectedProject?.name || 'All Projects',
                generatedInfo: `Generated: ${new Date().toLocaleString()}`,
                columns,
                rows,
                rowsPerPage: 10,
                startY: 40,
                fontSize: 9,
                cellPadding: 2,
                filename: `UserReportCategorySummary_${new Date().toISOString().split('T')[0]}.pdf`
            });
        } finally {
            setExportingCategoryPdf(false);
        }
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-file-alt me-2"></i>User Report</h2>
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
                            <div className="col-md-2 mb-3">
                                <Form.Label className="small text-muted">Quick Filter</Form.Label>
                                <Form.Select
                                    size="sm"
                                    value={dateFilterType}
                                    onChange={(e) => handleDateFilterChange(e.target.value)}
                                    disabled={generating}
                                    style={{ fontSize: '0.875rem' }}
                                >
                                    {dateFilterOptions.map(option => (
                                        <option key={option.value} value={option.value}>
                                            {option.label}
                                        </option>
                                    ))}
                                </Form.Select>
                            </div>
                            <div className="col-md-2 mb-3">
                                <Form.Label className="small">Since</Form.Label>
                                <InputGroup size="sm">
                                    <InputGroup.Text style={{ fontSize: '0.875rem' }}><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        size="sm"
                                        value={formData.startDate}
                                        onChange={(e) => {
                                            setFormData({ ...formData, startDate: e.target.value });
                                            setDateFilterType('custom');
                                        }}
                                        disabled={generating}
                                        style={{ fontSize: '0.875rem' }}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-2 mb-3">
                                <Form.Label className="small">To</Form.Label>
                                <InputGroup size="sm">
                                    <InputGroup.Text style={{ fontSize: '0.875rem' }}><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        size="sm"
                                        value={formData.endDate}
                                        onChange={(e) => {
                                            setFormData({ ...formData, endDate: e.target.value });
                                            setDateFilterType('custom');
                                        }}
                                        disabled={generating}
                                        style={{ fontSize: '0.875rem' }}
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
                                <Form.Label>Category <span className="text-danger">*</span></Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.categoryId}
                                        onChange={(value) => setFormData({ ...formData, categoryId: value ? parseInt(value) : null, accountId: null })}
                                        options={categories.map(c => ({ id: c.id, name: c.name }))}
                                        placeholder="Search category..."
                                        required
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
                                        options={[{ id: null, name: 'All Bookers' }, ...bookers.map(b => ({ id: b.id, name: b.name }))]}
                                        placeholder="Search booker..."
                                        disabled={generating}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Location</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-map-marker-alt"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.locationId}
                                        onChange={(value) => setFormData({ ...formData, locationId: value ? parseInt(value) : null })}
                                        options={[{ id: null, name: 'All Locations' }, ...locations.map(l => ({ id: l.id, name: l.name }))]}
                                        placeholder="Search location..."
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
                                        disabled={generating || !formData.projectId || !formData.categoryId}
                                        title={!formData.projectId ? 'Please select a project' : !formData.categoryId ? 'Please select a category' : ''}
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
                        {allEntries.length > 0 && (
                            <div className="row mb-3">
                                <div className="col-12">
                                    <Form.Label className="d-block">Column Visibility</Form.Label>
                                    <div className="btn-group column-toggles" role="group">
                                        {Object.entries({
                                            showDate: { key: 'date', label: 'Entry Date', icon: 'fa-calendar-day' },
                                            showCreditableDate: { key: 'creditableDate', label: 'Credit Date', icon: 'fa-calendar-check' },
                                            showDescription: { key: 'description', label: 'Description', icon: 'fa-align-left' },
                                            showAccount: { key: 'account', label: 'Account', icon: 'fa-user' },
                                            showBooker: { key: 'booker', label: 'Booker', icon: 'fa-user-tag' },
                                            showLocation: { key: 'location', label: 'Location', icon: 'fa-map-marker-alt' },
                                            showQuantity: { key: 'quantity', label: 'Quantity', icon: 'fa-cubes' },
                                            showQtyBalance: { key: 'qtyBalance', label: 'Qty Balance', icon: 'fa-balance-scale-right' },
                                            showRate: { key: 'rate', label: 'Rate', icon: 'fa-percentage' },
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
                                <i className="fas fa-table me-2"></i>User Report Results
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
                                            onClick={handleExportMainReport} 
                                            className="me-2"
                                            disabled={exportingMainCsv || exportingMainPdf}
                                        >
                                            {exportingMainCsv ? (
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
                                            onClick={handleExportMainReportPdf}
                                            disabled={exportingMainCsv || exportingMainPdf}
                                        >
                                            {exportingMainPdf ? (
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
                            <div className="report-table-container">
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
                                <div className="table-responsive">
                                    <Table bordered striped hover className="table-sm mb-0" id="userReportTable">
                                    <thead className="table-light">
                                        <tr>
                                            <th className="id-column" style={{ minWidth: '120px', whiteSpace: 'nowrap' }}>ID</th>
                                            {visibleColumns.date && <th className="date-column" style={{ minWidth: '85px', maxWidth: '95px', whiteSpace: 'nowrap' }}>Entry Date</th>}
                                            {visibleColumns.creditableDate && <th className="creditable-date-column" style={{ minWidth: '85px', maxWidth: '95px', whiteSpace: 'nowrap' }}>Credit Date</th>}
                                            {visibleColumns.description && <th className="description-column" style={{ minWidth: '170px' }}>Description</th>}
                                            {visibleColumns.account && <th className="account-column" style={{ minWidth: '85px', maxWidth: '120px' }}>Account</th>}
                                            {visibleColumns.booker && <th className="booker-column" style={{ minWidth: '100px', maxWidth: '140px' }}>Booker</th>}
                                            {visibleColumns.location && <th className="location-column" style={{ minWidth: '70px', maxWidth: '120px' }}>Location</th>}
                                            {visibleColumns.quantity && <th className="quantity-column text-end" style={{ minWidth: '90px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Quantity</th>}
                                            {visibleColumns.qtyBalance && <th className="qty-balance-column text-end" style={{ minWidth: '90px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Qty Balance</th>}
                                            {visibleColumns.rate && <th className="rate-column text-end" style={{ minWidth: '90px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Rate</th>}
                                            {visibleColumns.debit && <th className="debit-column text-end" style={{ minWidth: '130px', maxWidth: '160px', whiteSpace: 'nowrap' }}>Debit</th>}
                                            {visibleColumns.credit && <th className="credit-column text-end" style={{ minWidth: '130px', maxWidth: '160px', whiteSpace: 'nowrap' }}>Credit</th>}
                                            {visibleColumns.balance && <th className="balance-column text-end" style={{ minWidth: '130px', maxWidth: '160px', whiteSpace: 'nowrap' }}>Balance</th>}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredEntries.length === 0 && allEntries.length > 0 ? (
                                            <tr>
                                                <td colSpan="11" className="text-center text-muted py-4">
                                                    {searchTerm ? 'No results found for your search' : 'No data available'}
                                                </td>
                                            </tr>
                                        ) : filteredEntries.length === 0 && allEntries.length === 0 ? (
                                            <tr>
                                                <td colSpan="11" className="text-center text-muted py-4">
                                                    No data available
                                                </td>
                                            </tr>
                                        ) : (
                                            filteredEntries.map((entry, index) => {
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
                                                        {visibleColumns.date && <td className="date-column" style={{ whiteSpace: 'nowrap', minWidth: '85px', maxWidth: '95px' }}>{formatDate(entry.transactionDate)}</td>}
                                                        {visibleColumns.creditableDate && <td className="creditable-date-column" style={{ whiteSpace: 'nowrap', minWidth: '85px', maxWidth: '95px' }}>{formatDate(entry.creditableDate)}</td>}
                                                        {visibleColumns.description && <td className="description-column" style={{ wordWrap: 'break-word', whiteSpace: 'normal' }}>{entry.description || ''}</td>}
                                                        {visibleColumns.account && <td className="account-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>{entry.accountName || ''}</td>}
                                                        {visibleColumns.booker && <td className="booker-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>{entry.bookerName || ''}</td>}
                                                        {visibleColumns.location && <td className="location-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>{entry.locationName || '-'}</td>}
                                                        {visibleColumns.quantity && <td className="quantity-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px' }}>{entry.quantity ?? ''}</td>}
                                                        {visibleColumns.qtyBalance && <td className="qty-balance-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px' }}>{entry.qtyBalance ?? ''}</td>}
                                                        {visibleColumns.rate && <td className="rate-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px' }}>{entry.rate ? formatCurrency(entry.rate) : ''}</td>}
                                                        {visibleColumns.debit && <td className="debit-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(entry.debit)}</td>}
                                                        {visibleColumns.credit && <td className="credit-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(entry.credit)}</td>}
                                                        {visibleColumns.balance && <td className="balance-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(entry.balance)}</td>}
                                                    </tr>
                                                );
                                            })
                                        )}
                                    </tbody>
                                    <tfoot className="table-secondary">
                                        <tr>
                                            <td className="id-column text-end fw-bold">TOTAL</td>
                                            {visibleColumns.date && <td></td>}
                                            {visibleColumns.creditableDate && <td></td>}
                                            {visibleColumns.description && <td></td>}
                                            {visibleColumns.account && <td></td>}
                                            {visibleColumns.booker && <td></td>}
                                            {visibleColumns.location && <td></td>}
                                            {visibleColumns.quantity && <td className="quantity-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px', overflow: 'visible' }}>{totals.quantity}</td>}
                                            {visibleColumns.qtyBalance && <td></td>}
                                            {visibleColumns.rate && <td></td>}
                                            {visibleColumns.debit && <td className="debit-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(totals.debit)}</td>}
                                            {visibleColumns.credit && <td className="credit-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(totals.credit)}</td>}
                                            {visibleColumns.balance && <td className="balance-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '160px', overflow: 'visible' }}>{formatCurrency(totals.debit - totals.credit)}</td>}
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

                    <Card className="shadow mb-4">
                        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                            <h6 className="m-0 font-weight-bold text-primary">
                                <i className="fas fa-users me-2"></i>Account Summary
                            </h6>
                            <div>
                                {!hasMoreData && accountSummary.length > 0 && (
                                    <>
                                        <Button
                                            variant="outline-success"
                                            size="sm"
                                            onClick={handleExportAccountSummary}
                                            className="me-2"
                                            disabled={exportingAccountCsv || exportingAccountPdf}
                                        >
                                            {exportingAccountCsv ? (
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
                                            onClick={handleExportAccountSummaryPdf}
                                            disabled={exportingAccountCsv || exportingAccountPdf}
                                        >
                                            {exportingAccountPdf ? (
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
                            <div>
                                {!hasMoreData && categorySummary.length > 0 && (
                                    <>
                                        <Button
                                            variant="outline-success"
                                            size="sm"
                                            onClick={handleExportCategorySummary}
                                            className="me-2"
                                            disabled={exportingCategoryCsv || exportingCategoryPdf}
                                        >
                                            {exportingCategoryCsv ? (
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
                                            onClick={handleExportCategorySummaryPdf}
                                            disabled={exportingCategoryCsv || exportingCategoryPdf}
                                        >
                                            {exportingCategoryPdf ? (
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

export default UserReport;


