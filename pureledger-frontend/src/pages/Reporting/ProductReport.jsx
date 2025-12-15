import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getDateRange, dateFilterOptions } from '../../utils/dateFilters';
import './ProductReport.css';

const ProductReport = () => {
    usePageTitle('Product Report');
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [locations, setLocations] = useState([]);
    const [allProducts, setAllProducts] = useState([]);
    const [totals, setTotals] = useState(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [exportingCsv, setExportingCsv] = useState(false);
    const [exportingPdf, setExportingPdf] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        categoryId: null,
        locationId: null,
        startDate: '',
        endDate: ''
    });
    const [excludeNilProducts, setExcludeNilProducts] = useState(false);
    const [dateFilterType, setDateFilterType] = useState('custom');
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const pageSize = 1000;
    const [visibleColumns, setVisibleColumns] = useState({
        debitQty: true,
        creditQty: true,
        balanceQty: true,
        avgRate: true,
        totalDebit: true,
        totalCredit: true,
        cogs: true,
        margin: true,
        marginPct: true,
    });

    const observer = useRef();
    const lastEntryElementRef = useCallback(node => {
        if (isLoadingMore || generating) return;
        if (observer.current) observer.current.disconnect();
        observer.current = new IntersectionObserver(entries => {
            if (entries[0].isIntersecting && hasMoreData) {
                const nextPage = Math.floor(allProducts.length / pageSize);
                generateReport(nextPage, true);
            }
        });
        if (node) observer.current.observe(node);
    }, [isLoadingMore, generating, hasMoreData, allProducts.length, pageSize]);

    useEffect(() => {
        loadInitData();
    }, []);

    useEffect(() => {
        if (formData.projectId) {
            loadCategoriesByProject(formData.projectId);
        } else {
            setCategories([]);
            setFormData(prev => ({ ...prev, categoryId: null }));
        }
    }, [formData.projectId]);

    const loadCategoriesByProject = async (projectId) => {
        try {
            const response = await apiClient.get(`/ReportingApi/categories-by-project/${projectId}`);
            if (response.data.success) {
                setCategories(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading categories:', err);
            setCategories([]);
        }
    };

    const loadInitData = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/ReportingApi/product-report-init');
            if (response.data.success) {
                setProjects(response.data.projects || []);
                // Don't load all categories initially - load them when project is selected
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

    const generateReport = async (page = 0, append = false) => {
        if (!formData.projectId) {
            setError('Please select a project');
            return;
        }

        if (page === 0) {
            setGenerating(true);
            setAllProducts([]);
            setTotals(null);
        } else {
            setIsLoadingMore(true);
        }

        setError('');

        try {
            const params = {
                projectId: formData.projectId.toString(),
                startDate: formData.startDate || null,
                endDate: formData.endDate || null,
                categoryId: formData.categoryId ? formData.categoryId.toString() : '',
                locationId: formData.locationId ? formData.locationId.toString() : '',
                excludeNilProducts: excludeNilProducts,
                page: page,
                pageSize: pageSize
            };

            const response = await apiClient.get('/ReportingApi/product-report', { params });
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const products = data.productSummaries || [];
                
                if (append) {
                    setAllProducts(prev => [...prev, ...products]);
                } else {
                    setAllProducts(products);
                }
                
                setTotals(data.totals || {});
                setHasMoreData(data.hasMoreData || false);
                setTotalRecords(data.totalRecords || products.length);
                setCurrentPage(page + 1);

                // Auto-load next page if there's more data
                if (data.hasMoreData && !isLoadingMore) {
                    setTimeout(() => {
                        generateReport(page + 1, true);
                    }, 100);
                }
            } else {
                setError('Failed to generate report');
            }
        } catch (err) {
            setError(err.response?.data?.error || 'Failed to generate report');
        } finally {
            setGenerating(false);
            setIsLoadingMore(false);
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

    const handleSubmit = async (e) => {
        e.preventDefault();
        generateReport(0, false);
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value || 0);
    };

    const formatNumber = (value) => {
        return new Intl.NumberFormat('en-PK', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(value || 0);
    };

    const filteredProducts = allProducts.filter(product => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        return (
            (product.productName || '').toLowerCase().includes(search) ||
            (product.totalDebitQty?.toString() || '').includes(search) ||
            (product.totalCreditQty?.toString() || '').includes(search) ||
            (product.qtyBalance?.toString() || '').includes(search) ||
            (product.averageRate?.toString() || '').includes(search) ||
            (product.totalDebit?.toString() || '').includes(search) ||
            (product.totalCredit?.toString() || '').includes(search) ||
            (product.cogs?.toString() || '').includes(search) ||
            (product.margin?.toString() || '').includes(search) ||
            (product.marginPercentage?.toString() || '').includes(search)
        );
    });

    const handleExportCsv = async () => {
        setExportingCsv(true);
        try {
            const csvData = filteredProducts.map(product => ({
                Product: product.productName || '',
                'Debit Qty': (product.totalDebitQty || 0).toFixed(2),
                'Credit Qty': (product.totalCreditQty || 0).toFixed(2),
                'Balance Qty': (product.qtyBalance || 0).toFixed(2),
                'Avg Rate': (product.averageRate || 0).toFixed(2),
                'Total Debit': (product.totalDebit || 0).toFixed(2),
                'Total Credit': (product.totalCredit || 0).toFixed(2),
                COGS: (product.cogs || 0).toFixed(2),
                Margin: (product.margin || 0).toFixed(2),
                'Margin %': `${(product.marginPercentage || 0).toFixed(2)}%`
            }));
            const dateRange = formData.startDate && formData.endDate 
                ? `${formData.startDate}_to_${formData.endDate}` 
                : 'report';
            await exportToCsvAsync(csvData, `ProductReport_${dateRange}.csv`);
        } finally {
            setExportingCsv(false);
        }
    };

    const handleExportPdf = async () => {
        setExportingPdf(true);
        try {
            const selectedProject = projects.find(p => p.id === formData.projectId);
            const selectedCategory = categories.find(c => c.id === formData.categoryId);
            const selectedLocation = locations.find(l => l.id === formData.locationId);

            const rows = filteredProducts.map(product => ({
                product: product.productName || '',
                debitQty: formatNumber(product.totalDebitQty),
                creditQty: formatNumber(product.totalCreditQty),
                balanceQty: formatNumber(product.qtyBalance),
                avgRate: formatCurrency(product.averageRate || 0),
                totalDebit: formatCurrency(product.totalDebit || 0),
                totalCredit: formatCurrency(product.totalCredit || 0),
                cogs: formatCurrency(product.cogs || 0),
                margin: formatCurrency(product.margin || 0),
                marginPct: `${formatNumber(product.marginPercentage)}%`
            }));

            if (totals) {
                rows.push({
                    product: 'TOTAL',
                    debitQty: formatNumber(totals.totalDebitQty),
                    creditQty: formatNumber(totals.totalCreditQty),
                    balanceQty: formatNumber(totals.netQuantity),
                    avgRate: '-',
                    totalDebit: formatCurrency(totals.totalDebit || 0),
                    totalCredit: formatCurrency(totals.totalCredit || 0),
                    cogs: formatCurrency(totals.totalCOGS || 0),
                    margin: formatCurrency(totals.totalMargin || 0),
                    marginPct: `${formatNumber(totals.marginPercentage)}%`
                });
            }

            const columns = [
                { header: 'Product', dataKey: 'product' },
                { header: 'Debit Qty', dataKey: 'debitQty' },
                { header: 'Credit Qty', dataKey: 'creditQty' },
                { header: 'Bal Qty', dataKey: 'balanceQty' },
                { header: 'Avg Rate', dataKey: 'avgRate' },
                { header: 'Total Debit', dataKey: 'totalDebit' },
                { header: 'Total Credit', dataKey: 'totalCredit' },
                { header: 'COGS', dataKey: 'cogs' },
                { header: 'Margin', dataKey: 'margin' },
                { header: 'Margin %', dataKey: 'marginPct' }
            ];

            let subtitle = '';
            if (formData.startDate) {
                subtitle += `Date Range: ${formData.startDate} to ${formData.endDate || formData.startDate}`;
            }
            if (selectedCategory) {
                subtitle += (subtitle ? '\n' : '') + `Category: ${selectedCategory.name}`;
            }
            if (selectedLocation) {
                subtitle += (subtitle ? '\n' : '') + `Location: ${selectedLocation.name}`;
            }
            const generatedInfo = `Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords}`;

            await generatePdfAsync({
                orientation: 'landscape',
                format: 'a4',
                title: 'Product Stock & Margin Report',
                projectName: selectedProject?.name || 'All Projects',
                subtitle: subtitle || null,
                generatedInfo,
                columns,
                rows,
                rowsPerPage: 20,
                startY: selectedCategory || selectedLocation ? 56 : 46,
                fontSize: 6,
                cellPadding: 1.5,
                filename: `ProductReport_${new Date().toISOString().split('T')[0]}.pdf`
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
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-box me-2"></i>Product Stock & Margin Report</h2>
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
                                        onChange={(value) => setFormData({ ...formData, projectId: value ? parseInt(value) : null, categoryId: null })}
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
                                        onChange={(value) => setFormData({ ...formData, categoryId: value ? parseInt(value) : null })}
                                        options={[{ id: null, name: 'All Categories' }, ...categories.map(c => ({ id: c.id, name: c.name }))]}
                                        placeholder="Search category..."
                                        disabled={!formData.projectId || generating}
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
                                        options={locations.map(l => ({ id: l.id, name: l.name }))}
                                        placeholder="Search location..."
                                        disabled={generating}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-md-3 mb-3 d-flex align-items-center" style={{ paddingTop: '2rem' }}>
                                <Form.Check
                                    type="checkbox"
                                    id="excludeNilProducts"
                                    label="Nil Product"
                                    checked={excludeNilProducts}
                                    onChange={(e) => setExcludeNilProducts(e.target.checked)}
                                    disabled={generating}
                                />
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
                        {allProducts.length > 0 && (
                            <div className="row mb-3">
                                <div className="col-12">
                                    <Form.Label className="d-block">Column Visibility</Form.Label>
                                    <div className="btn-group column-toggles" role="group">
                                        {Object.entries({
                                            showDebitQty: { key: 'debitQty', label: 'Debit Qty', icon: 'fa-arrow-down' },
                                            showCreditQty: { key: 'creditQty', label: 'Credit Qty', icon: 'fa-arrow-up' },
                                            showBalanceQty: { key: 'balanceQty', label: 'Bal Qty', icon: 'fa-balance-scale' },
                                            showAvgRate: { key: 'avgRate', label: 'Avg Rate', icon: 'fa-tags' },
                                            showTotalDebit: { key: 'totalDebit', label: 'Total Debit', icon: 'fa-plus' },
                                            showTotalCredit: { key: 'totalCredit', label: 'Total Credit', icon: 'fa-minus' },
                                            showCOGS: { key: 'cogs', label: 'COGS', icon: 'fa-industry' },
                                            showMargin: { key: 'margin', label: 'Margin', icon: 'fa-chart-line' },
                                            showMarginPct: { key: 'marginPct', label: 'Margin %', icon: 'fa-percentage' },
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

            {allProducts.length > 0 && (
                <Card className="shadow mb-4">
                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-table me-2"></i>Product Report Results
                        </h6>
                        <div>
                            {totalRecords > 0 && (
                                <span className="badge bg-primary me-2">
                                    {allProducts.length} of {totalRecords} products
                                </span>
                            )}
                            {!hasMoreData && allProducts.length > 0 && (
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
                        <div className="product-report-container">
                            <div className="table-responsive" style={{ width: '100%', overflowX: 'auto' }}>
                                <Table bordered striped hover className="table-sm mb-0 w-100" id="productReportTable" style={{ width: '100%', tableLayout: 'auto' }}>
                                    <thead className="table-light">
                                        <tr>
                                            <th className="product-column" style={{ minWidth: '140px', maxWidth: '200px' }}>Product</th>
                                            {visibleColumns.debitQty && <th className="qty-column text-end">Debit Qty</th>}
                                            {visibleColumns.creditQty && <th className="qty-column text-end">Credit Qty</th>}
                                            {visibleColumns.balanceQty && <th className="qty-column text-end">Bal Qty</th>}
                                            {visibleColumns.avgRate && <th className="currency-column text-end">Avg Rate</th>}
                                            {visibleColumns.totalDebit && <th className="currency-column text-end">Total Debit</th>}
                                            {visibleColumns.totalCredit && <th className="currency-column text-end">Total Credit</th>}
                                            {visibleColumns.cogs && <th className="currency-column text-end">COGS</th>}
                                            {visibleColumns.margin && <th className="currency-column text-end">Margin</th>}
                                            {visibleColumns.marginPct && <th className="percent-column text-end">Margin %</th>}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredProducts.length === 0 && allProducts.length > 0 ? (
                                            <tr>
                                                <td colSpan="10" className="text-center text-muted py-4">
                                                    {searchTerm ? 'No results found for your search' : 'No data available'}
                                                </td>
                                            </tr>
                                        ) : filteredProducts.length === 0 && allProducts.length === 0 ? (
                                            <tr>
                                                <td colSpan="10" className="text-center text-muted py-4">
                                                    No data available
                                                </td>
                                            </tr>
                                        ) : (
                                            filteredProducts.map((product, index) => {
                                                const isLastEntry = index === filteredProducts.length - 1;
                                                return (
                                                    <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                        <td className="product-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal', minWidth: '140px', maxWidth: '200px' }} title={product.productName}>{product.productName}</td>
                                                        {visibleColumns.debitQty && <td className="qty-column text-end">{formatNumber(product.totalDebitQty)}</td>}
                                                        {visibleColumns.creditQty && <td className="qty-column text-end">{formatNumber(product.totalCreditQty)}</td>}
                                                        {visibleColumns.balanceQty && <td className="qty-column text-end">{formatNumber(product.qtyBalance)}</td>}
                                                        {visibleColumns.avgRate && <td className="currency-column text-end">{formatCurrency(product.averageRate || 0)}</td>}
                                                        {visibleColumns.totalDebit && <td className="currency-column text-end">{formatCurrency(product.totalDebit || 0)}</td>}
                                                        {visibleColumns.totalCredit && <td className="currency-column text-end">{formatCurrency(product.totalCredit || 0)}</td>}
                                                        {visibleColumns.cogs && <td className="currency-column text-end">{formatCurrency(product.cogs || 0)}</td>}
                                                        {visibleColumns.margin && <td className="currency-column text-end">{formatCurrency(product.margin || 0)}</td>}
                                                        {visibleColumns.marginPct && <td className="percent-column text-end">{formatNumber(product.marginPercentage)}%</td>}
                                                    </tr>
                                                );
                                            })
                                        )}
                                    </tbody>
                                    {totals && (
                                        <tfoot className="table-secondary">
                                            <tr className="fw-bold">
                                                <td className="text-end">TOTAL</td>
                                                {visibleColumns.debitQty && <td className="qty-column text-end">{formatNumber(totals.totalDebitQty)}</td>}
                                                {visibleColumns.creditQty && <td className="qty-column text-end">{formatNumber(totals.totalCreditQty)}</td>}
                                                {visibleColumns.balanceQty && <td className="qty-column text-end">{formatNumber(totals.netQuantity)}</td>}
                                                {visibleColumns.avgRate && <td className="currency-column text-end">-</td>}
                                                {visibleColumns.totalDebit && <td className="currency-column text-end">{formatCurrency(totals.totalDebit || 0)}</td>}
                                                {visibleColumns.totalCredit && <td className="currency-column text-end">{formatCurrency(totals.totalCredit || 0)}</td>}
                                                {visibleColumns.cogs && <td className="currency-column text-end">{formatCurrency(totals.totalCOGS || 0)}</td>}
                                                {visibleColumns.margin && <td className="currency-column text-end">{formatCurrency(totals.totalMargin || 0)}</td>}
                                                {visibleColumns.marginPct && <td className="percent-column text-end">{formatNumber(totals.marginPercentage)}%</td>}
                                            </tr>
                                        </tfoot>
                                    )}
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

export default ProductReport;

