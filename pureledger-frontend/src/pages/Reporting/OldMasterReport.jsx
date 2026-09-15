import React, { useEffect, useState, useRef } from 'react';
import { Alert, Button, Card, Form, Table, InputGroup } from 'react-bootstrap';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import apiClient from '../../services/api';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getDateRange, dateFilterOptions } from '../../utils/dateFilters';
import './MasterReport.css';

const OldMasterReport = () => {
    usePageTitle('Old Master Report');

    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [exportingCsv, setExportingCsv] = useState(false);
    const [exportingPdf, setExportingPdf] = useState(false);

    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accounts, setAccounts] = useState([]);
    const [bookers, setBookers] = useState([]);
    const [locations, setLocations] = useState([]);

    const [entries, setEntries] = useState([]);
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const [previousBalance, setPreviousBalance] = useState(0);
    const [searchTerm, setSearchTerm] = useState('');

    const [visibleColumns, setVisibleColumns] = useState({
        date: true,
        creditableDate: true,
        description: true,
        project: true,
        category: true,
        account: true,
        booker: true,
        location: true,
        quantity: true,
        qtyBalance: false,
        rate: true,
        debit: true,
        credit: true,
        balance: true
    });

    const [formData, setFormData] = useState({
        startDate: '2020-03-31',
        endDate: '2026-01-31',
        projectId: null,
        categoryName: null,
        accountName: null,
        bookerId: null,
        locationId: null,
        showQtyBalance: false
    });

    const [dateFilterType, setDateFilterType] = useState('custom');
    const pageSize = 2500;
    const searchTermRef = useRef('');

    useEffect(() => {
        loadInitData();
    }, []);

    useEffect(() => {
        searchTermRef.current = searchTerm.trim();
    }, [searchTerm]);

    const loadInitData = async () => {
        setLoading(true);
        setError('');

        try {
            const response = await apiClient.get('/ReportingApi/old-master-report-init');
            if (!response.data.success) {
                setError(response.data.message || 'Failed to load initial data');
                return;
            }

            const data = response.data.data || {};
            setProjects(data.projects || []);
            setBookers(data.bookers || []);
            setLocations(data.locations || []);
            // Initialize categories and accounts as empty for cascading
            setCategories([]);
            setAccounts([]);
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load initial data');
        } finally {
            setLoading(false);
        }
    };

    const loadCategoriesByProject = async (projectId) => {
        if (projectId === null || projectId === undefined || projectId === '') {
            setCategories([]);
            return;
        }
        try {
            const response = await apiClient.get(`/ReportingApi/old-master-report-categories-by-project/${projectId}`);
            if (response.data.success) {
                setCategories(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading categories:', err);
            setCategories([]);
        }
    };

    const loadAccountsByCategory = async (categoryName) => {
        if (categoryName === null || categoryName === undefined || categoryName === '') {
            setAccounts([]);
            return;
        }
        try {
            const response = await apiClient.get(`/ReportingApi/old-master-report-accounts-by-category/${encodeURIComponent(categoryName)}`);
            if (response.data.success) {
                setAccounts(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading accounts:', err);
            setAccounts([]);
        }
    };

    // Cascading dropdown effects
    useEffect(() => {
        if (formData.projectId) {
            loadCategoriesByProject(formData.projectId);
        } else {
            setCategories([]);
            setAccounts([]);
            setFormData(prev => ({ ...prev, categoryName: null, accountName: null }));
        }
    }, [formData.projectId]);

    useEffect(() => {
        if (formData.categoryName) {
            loadAccountsByCategory(formData.categoryName);
        } else {
            setAccounts([]);
            setFormData(prev => ({ ...prev, accountName: null }));
        }
    }, [formData.categoryName]);

    const generateReport = async (page = 0, append = false) => {
        if (page === 0) {
            setGenerating(true);
            setEntries([]);
            setCurrentPage(0);
            setHasMoreData(false);
            setPreviousBalance(0);
            setTotalRecords(0);
            setIsAutoLoading(false);
        } else {
            setIsLoadingMore(true);
        }

        setError('');

        try {
            const params = {
                startDate: formData.startDate || null,
                endDate: formData.endDate || null,
                projectId: formData.projectId ?? null,
                categoryName: formData.categoryName ?? null,
                accountName: formData.accountName ?? null,
                bookerId: formData.bookerId ?? null,
                locationId: formData.locationId ?? null,
                showQtyBalance: formData.showQtyBalance,
                page,
                pageSize
            };

            const response = await apiClient.get('/ReportingApi/old-master-report', { params });
            if (!response.data.success) {
                setError(response.data.message || 'Failed to generate report');
                setHasMoreData(false);
                return;
            }

            const data = response.data.data || {};
            const newEntries = data.entries || [];

            setEntries(prev => append ? [...prev, ...newEntries] : newEntries);
            setCurrentPage(page + 1);
            setHasMoreData(data.hasMoreData || false);

            if (!append) {
                setTotalRecords(data.totalRecords || 0);
                setPreviousBalance(data.previousBalance || 0);
            }

            if (data.hasMoreData) {
                setIsAutoLoading(true);
                setTimeout(() => {
                    generateReport(page + 1, true);
                }, 100);
            } else {
                setIsAutoLoading(false);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to generate report');
            setHasMoreData(false);
            setIsAutoLoading(false);
        } finally {
            if (page === 0) {
                setGenerating(false);
            } else {
                setIsLoadingMore(false);
            }
        }
    };

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

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value || 0);
    };

    const formatDate = (value) => {
        if (!value || typeof value !== 'string') {
            return '';
        }

        const parts = value.split('-');
        if (parts.length !== 3) {
            return value;
        }

        return `${parts[1]}/${parts[2]}/${parts[0].slice(-2)}`;
    };

    const filteredEntries = entries.filter(entry => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        const transactionDate = formatDate(entry.transactionDate).toLowerCase();
        const creditableDate = formatDate(entry.creditableDate).toLowerCase();
        return (
            (entry.description || '').toLowerCase().includes(search) ||
            (entry.accountName || '').toLowerCase().includes(search) ||
            (entry.categoryName || '').toLowerCase().includes(search) ||
            (entry.projectName || '').toLowerCase().includes(search) ||
            (entry.bookerName || '').toLowerCase().includes(search) ||
            (entry.locationName || '').toLowerCase().includes(search) ||
            (entry.formattedId || '').toLowerCase().includes(search) ||
            (entry.id?.toString() || '').includes(search) ||
            (entry.journalVoucherId?.toString() || '').includes(search) ||
            transactionDate.includes(search) ||
            creditableDate.includes(search) ||
            (entry.quantity?.toString() || '').includes(search) ||
            (entry.debit?.toString() || '').includes(search) ||
            (entry.credit?.toString() || '').includes(search) ||
            (entry.balance?.toString() || '').includes(search) ||
            ('opening'.includes(search) && (entry.formattedId === 'OPENING' || entry.description === 'Opening Balance'))
        );
    });

    const totals = filteredEntries.reduce((acc, entry) => ({
        quantity: acc.quantity + (entry.quantity || 0),
        debit: acc.debit + (entry.debit || 0),
        credit: acc.credit + (entry.credit || 0)
    }), { quantity: 0, debit: 0, credit: 0 });

    const closingBalance = entries.length > 0
        ? entries[entries.length - 1].balance || 0
        : previousBalance;

    const totalColumnCount = 1
        + (visibleColumns.date ? 1 : 0)
        + (visibleColumns.creditableDate ? 1 : 0)
        + (visibleColumns.project ? 1 : 0)
        + (visibleColumns.category ? 1 : 0)
        + (visibleColumns.account ? 1 : 0)
        + (visibleColumns.description ? 1 : 0)
        + (visibleColumns.booker ? 1 : 0)
        + (visibleColumns.location ? 1 : 0)
        + (visibleColumns.quantity ? 1 : 0)
        + (visibleColumns.qtyBalance ? 1 : 0)
        + (visibleColumns.rate ? 1 : 0)
        + (visibleColumns.debit ? 1 : 0)
        + (visibleColumns.credit ? 1 : 0)
        + (visibleColumns.balance ? 1 : 0);

    const handleExportCsv = async () => {
        setExportingCsv(true);
        try {
            const csvData = filteredEntries.map(entry => {
                const row = { ID: entry.formattedId || entry.id };
                if (visibleColumns.date) row['Entry Date'] = formatDate(entry.transactionDate);
                if (visibleColumns.creditableDate) row['Credit Date'] = formatDate(entry.creditableDate);
                if (visibleColumns.project) row['Project'] = entry.projectName || '';
                if (visibleColumns.category) row['Category'] = entry.categoryName || '';
                if (visibleColumns.account) row['Account'] = entry.accountName || '';
                if (visibleColumns.description) row['Description'] = entry.description || '';
                if (visibleColumns.booker) row['Booker'] = entry.bookerName || '';
                if (visibleColumns.location) row['Location'] = entry.locationName || '';
                if (visibleColumns.quantity) row['Quantity'] = entry.quantity || 0;
                if (visibleColumns.qtyBalance) row['Qty Balance'] = entry.qtyBalance || 0;
                if (visibleColumns.rate) row['Rate'] = entry.rate || 0;
                if (visibleColumns.debit) row['Debit'] = entry.debit || 0;
                if (visibleColumns.credit) row['Credit'] = entry.credit || 0;
                if (visibleColumns.balance) row['Balance'] = entry.balance || 0;
                return row;
            });
            await exportToCsvAsync(csvData, `OldMasterReport_${new Date().toISOString().split('T')[0]}.csv`);
        } finally {
            setExportingCsv(false);
        }
    };

    const handleExportPdf = async () => {
        setExportingPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);

            const columns = [{ header: 'ID', dataKey: 'id' }];
            if (visibleColumns.date) columns.push({ header: 'Entry Date', dataKey: 'date' });
            if (visibleColumns.creditableDate) columns.push({ header: 'Credit Date', dataKey: 'creditableDate' });
            if (visibleColumns.project) columns.push({ header: 'Project', dataKey: 'project' });
            if (visibleColumns.category) columns.push({ header: 'Category', dataKey: 'category' });
            if (visibleColumns.account) columns.push({ header: 'Account', dataKey: 'account' });
            if (visibleColumns.description) columns.push({ header: 'Description', dataKey: 'description' });
            if (visibleColumns.booker) columns.push({ header: 'Booker', dataKey: 'booker' });
            if (visibleColumns.location) columns.push({ header: 'Location', dataKey: 'location' });
            if (visibleColumns.quantity) columns.push({ header: 'Qty', dataKey: 'quantity' });
            if (visibleColumns.qtyBalance) columns.push({ header: 'Qty Bal', dataKey: 'qtyBalance' });
            if (visibleColumns.rate) columns.push({ header: 'Rate', dataKey: 'rate' });
            if (visibleColumns.debit) columns.push({ header: 'Debit', dataKey: 'debit' });
            if (visibleColumns.credit) columns.push({ header: 'Credit', dataKey: 'credit' });
            if (visibleColumns.balance) columns.push({ header: 'Balance', dataKey: 'balance' });

            const rows = filteredEntries.map(entry => {
                const row = { id: entry.formattedId || entry.id };
                if (visibleColumns.date) row.date = formatDate(entry.transactionDate);
                if (visibleColumns.creditableDate) row.creditableDate = formatDate(entry.creditableDate);
                if (visibleColumns.project) row.project = entry.projectName || '';
                if (visibleColumns.category) row.category = entry.categoryName || '';
                if (visibleColumns.account) row.account = entry.accountName || '';
                if (visibleColumns.description) row.description = entry.description || '';
                if (visibleColumns.booker) row.booker = entry.bookerName || '';
                if (visibleColumns.location) row.location = entry.locationName || '';
                if (visibleColumns.quantity) row.quantity = entry.quantity ?? '';
                if (visibleColumns.qtyBalance) row.qtyBalance = entry.qtyBalance ?? '';
                if (visibleColumns.rate) row.rate = entry.rate != null ? formatCurrency(entry.rate) : '';
                if (visibleColumns.debit) row.debit = entry.debit != null ? formatCurrency(entry.debit) : '';
                if (visibleColumns.credit) row.credit = entry.credit != null ? formatCurrency(entry.credit) : '';
                if (visibleColumns.balance) row.balance = entry.balance != null ? formatCurrency(entry.balance) : '';
                return row;
            });

            const dateRange = formData.startDate
                ? `${formatDate(formData.startDate)} to ${formatDate(formData.endDate || formData.startDate)}`
                : null;

            await generatePdfAsync({
                orientation: 'landscape',
                format: 'a4',
                title: 'Old Master Report',
                projectName: selectedProject?.name || 'All Projects',
                dateRange,
                generatedInfo: `Generated: ${new Date().toLocaleString()} | ${filteredEntries.length} of ${totalRecords} records | Previous Balance: ${formatCurrency(previousBalance)}`,
                columns,
                rows,
                rowsPerPage: 14,
                startY: 46,
                fontSize: 8,
                cellPadding: 2,
                filename: `OldMasterReport_${new Date().toISOString().split('T')[0]}.pdf`
            });
        } finally {
            setExportingPdf(false);
        }
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-end align-items-center mb-4">
                <small className="text-muted">Legacy journalentries report</small>
            </div>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            <Card className="shadow mb-4">
                <Card.Header className="py-3">
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
                                            const { value } = e.target;
                                            setFormData(prev => ({ ...prev, startDate: value }));
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
                                            const { value } = e.target;
                                            setFormData(prev => ({ ...prev, endDate: value }));
                                            setDateFilterType('custom');
                                        }}
                                        disabled={generating}
                                        style={{ fontSize: '0.875rem' }}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Project</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.projectId}
                                        onChange={(value) => setFormData(prev => ({
                                            ...prev,
                                            projectId: value ?? null,
                                            categoryName: null,
                                            accountName: null
                                        }))}
                                        options={projects}
                                        placeholder="All Projects"
                                        disabled={generating}
                                        onClear={() => {
                                            setCategories([]);
                                            setAccounts([]);
                                            setFormData(prev => ({
                                                ...prev,
                                                projectId: null,
                                                categoryName: null,
                                                accountName: null
                                            }));
                                        }}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Category</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.categoryName}
                                        onChange={(value) => setFormData(prev => ({
                                            ...prev,
                                            categoryName: value ?? null,
                                            accountName: null
                                        }))}
                                        options={[{ id: null, name: 'All Categories' }, ...categories]}
                                        placeholder="All Categories"
                                        disabled={(formData.projectId === null || formData.projectId === undefined || formData.projectId === '') || generating}
                                        onClear={() => {
                                            setAccounts([]);
                                            setFormData(prev => ({
                                                ...prev,
                                                categoryName: null,
                                                accountName: null
                                            }));
                                        }}
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
                                        value={formData.accountName}
                                        onChange={(value) => setFormData(prev => ({ ...prev, accountName: value ?? null }))}
                                        options={[{ id: null, name: 'All Accounts' }, ...accounts]}
                                        placeholder="All Accounts"
                                        disabled={(formData.categoryName === null || formData.categoryName === undefined || formData.categoryName === '') || generating}
                                        onClear={() => setFormData(prev => ({ ...prev, accountName: null }))}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Booker</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-user-tag"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.bookerId}
                                        onChange={(value) => setFormData(prev => ({ ...prev, bookerId: value ?? null }))}
                                        options={[{ id: null, name: 'All Bookers' }, ...bookers]}
                                        placeholder="All Bookers"
                                        disabled={generating}
                                        onClear={() => setFormData(prev => ({ ...prev, bookerId: null }))}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Location</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-map-marker-alt"></i></InputGroup.Text>
                                    <SearchableSelect
                                        value={formData.locationId}
                                        onChange={(value) => setFormData(prev => ({ ...prev, locationId: value ?? null }))}
                                        options={[{ id: null, name: 'All Locations' }, ...locations]}
                                        placeholder="All Locations"
                                        disabled={generating}
                                        onClear={() => setFormData(prev => ({ ...prev, locationId: null }))}
                                    />
                                </InputGroup>
                            </div>
                        </div>

                        <div className="row align-items-end">
                            <div className="col-md-3 mb-3 d-flex align-items-center">
                                <Form.Check
                                    type="checkbox"
                                    id="showLegacyQtyBalance"
                                    label="Show Qty Balance"
                                    checked={formData.showQtyBalance}
                                    onChange={(e) => {
                                        setFormData({
                                            ...formData,
                                            showQtyBalance: e.target.checked
                                        });
                                        setVisibleColumns(prev => ({ ...prev, qtyBalance: e.target.checked }));
                                    }}
                                    disabled={generating}
                                />
                            </div>
                            <div className="col-md-3 mb-3">
                                <div className="d-grid">
                                    <Button
                                        type="submit"
                                        variant="primary"
                                        disabled={generating}
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

                        {entries.length > 0 && (
                            <div className="row mb-3">
                                <div className="col-12">
                                    <Form.Label className="d-block">Column Visibility</Form.Label>
                                    <div className="btn-group column-toggles" role="group">
                                        {Object.entries({
                                            showDate: { key: 'date', label: 'Entry Date', icon: 'fa-calendar-day' },
                                            showCreditableDate: { key: 'creditableDate', label: 'Credit Date', icon: 'fa-calendar-check' },
                                            showProject: { key: 'project', label: 'Project', icon: 'fa-project-diagram' },
                                            showCategory: { key: 'category', label: 'Category', icon: 'fa-list' },
                                            showAccount: { key: 'account', label: 'Account', icon: 'fa-user' },
                                            showDescription: { key: 'description', label: 'Description', icon: 'fa-align-left' },
                                            showBooker: { key: 'booker', label: 'Booker', icon: 'fa-user-tag' },
                                            showLocation: { key: 'location', label: 'Location', icon: 'fa-map-marker-alt' },
                                            showQuantity: { key: 'quantity', label: 'Quantity', icon: 'fa-cubes' },
                                            showQtyBalance: { key: 'qtyBalance', label: 'Qty Balance', icon: 'fa-balance-scale-left' },
                                            showRate: { key: 'rate', label: 'Rate', icon: 'fa-tag' },
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

            {entries.length > 0 && (
                <Card className="shadow mb-4">
                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-table me-2"></i>Results ({filteredEntries.length} of {totalRecords} records)
                        </h6>
                        <div>
                            <small className="text-muted me-3">
                                Previous Balance: {formatCurrency(previousBalance)}
                            </small>
                            {isAutoLoading && (
                                <small className="text-primary me-3">
                                    Loading more records...
                                </small>
                            )}
                            <Button
                                variant="outline-success"
                                size="sm"
                                onClick={handleExportCsv}
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
                                className="ms-2"
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
                        </div>
                    </Card.Header>
                    <Card.Body>
                        <div className="master-report-container">
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
                                <Table bordered striped hover className="table-sm mb-0" id="masterReportTable">
                                    <thead>
                                        <tr>
                                            <th className="id-column text-center" style={{ minWidth: '130px', whiteSpace: 'nowrap' }}>ID</th>
                                            {visibleColumns.date && <th className="date-column" style={{ minWidth: '90px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Entry Date</th>}
                                            {visibleColumns.creditableDate && <th className="creditable-date-column" style={{ minWidth: '90px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Credit Date</th>}
                                            {visibleColumns.project && <th className="project-column" style={{ minWidth: '100px', maxWidth: '150px', whiteSpace: 'nowrap' }}>Project</th>}
                                            {visibleColumns.category && <th className="category-column" style={{ minWidth: '100px', maxWidth: '180px', whiteSpace: 'normal', wordWrap: 'break-word' }}>Category</th>}
                                            {visibleColumns.account && <th className="account-column" style={{ minWidth: '90px', maxWidth: '160px', wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>Account</th>}
                                            {visibleColumns.description && <th className="description-column" style={{ width: '280px', minWidth: '200px', maxWidth: '350px', wordWrap: 'break-word', overflowWrap: 'break-word', wordBreak: 'break-word', whiteSpace: 'normal' }}>Description</th>}
                                            {visibleColumns.booker && <th className="booker-column" style={{ minWidth: '110px', maxWidth: '180px', wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>Booker</th>}
                                            {visibleColumns.location && <th className="location-column" style={{ minWidth: '70px', maxWidth: '120px', wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal' }}>Location</th>}
                                            {visibleColumns.quantity && <th className="quantity-column text-end" style={{ minWidth: '80px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Qty</th>}
                                            {visibleColumns.qtyBalance && <th className="qtybalance-column text-end" style={{ minWidth: '80px', maxWidth: '120px', whiteSpace: 'nowrap' }}>Qty Balance</th>}
                                            {visibleColumns.rate && <th className="rate-column text-end" style={{ minWidth: '100px', whiteSpace: 'nowrap' }}>Rate</th>}
                                            {visibleColumns.debit && <th className="debit-column text-end" style={{ minWidth: '130px', maxWidth: '180px', whiteSpace: 'nowrap' }}>Debit</th>}
                                            {visibleColumns.credit && <th className="credit-column text-end" style={{ minWidth: '130px', maxWidth: '180px', whiteSpace: 'nowrap' }}>Credit</th>}
                                            {visibleColumns.balance && <th className="balance-column text-end" style={{ minWidth: '130px', maxWidth: '180px', whiteSpace: 'nowrap' }}>Balance</th>}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredEntries.length === 0 && entries.length > 0 ? (
                                            <tr>
                                                <td colSpan={totalColumnCount} className="text-center text-muted py-4">
                                                    {searchTerm ? 'No results found for your search' : 'No data available'}
                                                </td>
                                            </tr>
                                        ) : filteredEntries.length === 0 ? (
                                            <tr>
                                                <td colSpan={totalColumnCount} className="text-center text-muted py-4">
                                                    No data available
                                                </td>
                                            </tr>
                                        ) : (
                                            filteredEntries.map((entry, idx) => {
                                                const isOpening = entry.formattedId === 'OPENING' || entry.description === 'Opening Balance';
                                                return (
                                                    <tr key={`${entry.id}-${idx}`} className={isOpening ? 'table-info' : ''}>
                                                        <td className="id-column" style={{ whiteSpace: 'nowrap' }}>
                                                            {isOpening ? (
                                                                <span className="fw-bold text-primary">OPENING</span>
                                                            ) : (
                                                                <div>
                                                                    <div className="fw-semibold">{entry.formattedId || `M-${entry.masterId}`}</div>
                                                                    <div className="small text-muted">Entry: {entry.id}</div>
                                                                </div>
                                                            )}
                                                        </td>
                                                        {visibleColumns.date && <td className="date-column" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px' }}>{formatDate(entry.transactionDate)}</td>}
                                                        {visibleColumns.creditableDate && <td className="creditable-date-column" style={{ whiteSpace: 'nowrap', minWidth: '90px', maxWidth: '120px' }}>{formatDate(entry.creditableDate)}</td>}
                                                        {visibleColumns.project && <td className="project-column" style={{ whiteSpace: 'nowrap', minWidth: '100px', maxWidth: '150px' }}>{entry.projectName || ''}</td>}
                                                        {visibleColumns.category && <td className="category-column" style={{ whiteSpace: 'normal', wordWrap: 'break-word', minWidth: '100px', maxWidth: '180px' }}>{entry.categoryName || ''}</td>}
                                                        {visibleColumns.account && <td className="account-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal', maxWidth: '160px' }}>{entry.accountName || ''}</td>}
                                                        {visibleColumns.description && <td className="description-column" style={{ width: '280px', minWidth: '200px', maxWidth: '350px', wordWrap: 'break-word', overflowWrap: 'break-word', wordBreak: 'break-word', whiteSpace: 'normal' }}>{entry.description || ''}</td>}
                                                        {visibleColumns.booker && <td className="booker-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal', maxWidth: '180px' }}>{entry.bookerName || ''}</td>}
                                                        {visibleColumns.location && <td className="location-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal', maxWidth: '120px' }}>{entry.locationName || '-'}</td>}
                                                        {visibleColumns.quantity && <td className="quantity-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '80px', maxWidth: '120px' }}>{entry.quantity ?? ''}</td>}
                                                        {visibleColumns.qtyBalance && <td className="qtybalance-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '80px', maxWidth: '120px' }}>{entry.qtyBalance ?? ''}</td>}
                                                        {visibleColumns.rate && <td className="rate-column text-end" style={{ whiteSpace: 'nowrap' }}>{entry.rate ? formatCurrency(entry.rate) : '-'}</td>}
                                                        {visibleColumns.debit && <td className="debit-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(entry.debit)}</td>}
                                                        {visibleColumns.credit && <td className="credit-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(entry.credit)}</td>}
                                                        {visibleColumns.balance && <td className="balance-column text-end" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(entry.balance)}</td>}
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
                                            {visibleColumns.project && <td></td>}
                                            {visibleColumns.category && <td></td>}
                                            {visibleColumns.account && <td></td>}
                                            {visibleColumns.description && <td></td>}
                                            {visibleColumns.booker && <td></td>}
                                            {visibleColumns.location && <td></td>}
                                            {visibleColumns.quantity && <td className="quantity-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '80px', maxWidth: '120px' }}>{totals.quantity}</td>}
                                            {visibleColumns.qtyBalance && <td className="qtybalance-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '80px', maxWidth: '120px' }}></td>}
                                            {visibleColumns.rate && <td className="rate-column text-end fw-bold" style={{ whiteSpace: 'nowrap' }}>-</td>}
                                            {visibleColumns.debit && <td className="debit-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(totals.debit)}</td>}
                                            {visibleColumns.credit && <td className="credit-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(totals.credit)}</td>}
                                            {visibleColumns.balance && <td className="balance-column text-end fw-bold" style={{ whiteSpace: 'nowrap', minWidth: '130px', maxWidth: '180px' }}>{formatCurrency(closingBalance)}</td>}
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

export default OldMasterReport;
