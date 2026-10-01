import React, { useState, useEffect, useRef } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup, Modal } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import SortableHeader from '../../components/SortableHeader';
import { usePageTitle } from '../../hooks/usePageTitle';
import useSortableData from '../../hooks/useSortableData';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getDateRange, dateFilterOptions } from '../../utils/dateFilters';
import './ProductReport.css';

const ProductReport = () => {
    usePageTitle('Product Report', 'Product Stock & Margin Report');
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
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const [appliedReportFilters, setAppliedReportFilters] = useState(null);
    const [breakdownTarget, setBreakdownTarget] = useState(null);
    const [stockBreakdown, setStockBreakdown] = useState(null);
    const [breakdownLoading, setBreakdownLoading] = useState(false);
    const [breakdownError, setBreakdownError] = useState('');
    const pageSize = 2500;
    const [visibleColumns, setVisibleColumns] = useState({
        debitQty: true,
        creditQty: true,
        balanceQty: true,
        avgRate: true,
        totalDebit: true,
        totalCredit: true,
        balance: true,
        cogs: true,
        margin: true,
        marginPct: true,
    });

    const reportRequest = useRef({
        runId: 0,
        controller: null,
        timer: null,
        requestedPages: new Set()
    });
    const breakdownAbortController = useRef(null);

    useEffect(() => {
        const request = reportRequest.current;
        loadInitData();
        return () => {
            request.runId += 1;
            clearTimeout(request.timer);
            request.controller?.abort();
            breakdownAbortController.current?.abort();
        };
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

    const generateReport = async (page = 0, append = false, runId = null) => {
        if (!formData.projectId) {
            setError('Please select a project');
            return;
        }

        const request = reportRequest.current;
        if (page === 0) {
            request.runId += 1;
            clearTimeout(request.timer);
            request.controller?.abort();
            request.controller = new AbortController();
            request.requestedPages = new Set();
            runId = request.runId;
            setGenerating(true);
            setAllProducts([]);
            setTotals(null);
            setAppliedReportFilters(null);
            setBreakdownTarget(null);
            setStockBreakdown(null);
            breakdownAbortController.current?.abort();
        } else {
            if (runId !== request.runId || request.requestedPages.has(page)) return;
            setIsLoadingMore(true);
        }
        request.requestedPages.add(page);
        const controller = request.controller;

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
            const requestedFilters = {
                projectId: Number(params.projectId),
                locationId: params.locationId ? Number(params.locationId) : null,
                endDate: params.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/product-report', {
                params,
                signal: controller.signal
            });
            if (runId !== request.runId || controller.signal.aborted) return;
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const products = data.productSummaries || [];
                
                if (append) {
                    setAllProducts(prev => [...prev, ...products]);
                } else {
                    setAllProducts(products);
                    setAppliedReportFilters(requestedFilters);
                }
                
                setTotals(data.totals || {});
                setHasMoreData(data.hasMoreData || false);
                setTotalRecords(data.totalRecords || products.length);

                // Auto-load next page if there's more data
                if (data.hasMoreData) {
                    request.timer = setTimeout(() => {
                        request.timer = null;
                        generateReport(page + 1, true, runId);
                    }, 100);
                }
            } else {
                setError('Failed to generate report');
                setHasMoreData(false);
            }
        } catch (err) {
            if (runId !== request.runId || controller.signal.aborted) return;
            setError(err.response?.data?.error || 'Failed to generate report');
            setHasMoreData(false);
        } finally {
            if (runId === request.runId) {
                setGenerating(false);
                setIsLoadingMore(false);
            }
        }
    };

    const closeStockBreakdown = () => {
        breakdownAbortController.current?.abort();
        breakdownAbortController.current = null;
        setBreakdownTarget(null);
        setStockBreakdown(null);
        setBreakdownError('');
        setBreakdownLoading(false);
    };

    const openStockBreakdown = async (product) => {
        if (!appliedReportFilters || appliedReportFilters.locationId) return;

        breakdownAbortController.current?.abort();
        const target = {
            productId: product.productId,
            productName: product.productName,
            reportQuantity: product.qtyBalance ?? product.reportQuantity,
            reportTransitQuantity: product.inTransitQuantity ?? product.reportTransitQuantity ?? 0
        };

        setBreakdownTarget(target);
        setBreakdownError('');
        setStockBreakdown(null);
        setBreakdownLoading(true);
        const controller = new AbortController();
        breakdownAbortController.current = controller;

        try {
            const params = {
                projectId: appliedReportFilters.projectId,
                productId: product.productId
            };
            if (appliedReportFilters.endDate) params.endDate = appliedReportFilters.endDate;
            const response = await apiClient.get('/ReportingApi/product-report-stock-breakdown', {
                params,
                signal: controller.signal
            });
            if (!response.data.success || !response.data.data) {
                throw new Error(response.data.message || 'Failed to load stock breakdown');
            }
            if (controller.signal.aborted) return;
            setStockBreakdown(response.data.data);
        } catch (err) {
            if (controller.signal.aborted) return;
            setBreakdownError(
                err.response?.data?.message || err.message || 'Failed to load stock breakdown'
            );
        } finally {
            if (!controller.signal.aborted) setBreakdownLoading(false);
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
            (product.balance?.toString() || '').includes(search) ||
            (product.cogs?.toString() || '').includes(search) ||
            (product.margin?.toString() || '').includes(search) ||
            (product.marginPercentage?.toString() || '').includes(search)
        );
    });

    // Apply sorting to filtered products
    const { sortedItems: sortedProducts, requestSort, getSortDirection } = useSortableData(filteredProducts);
    const totalBalanceQuantity = !hasMoreData && allProducts.length >= totalRecords
        ? allProducts.reduce((sum, product) => sum + (product.qtyBalance || 0), 0)
        : totals?.balanceQuantity ?? null;
    const showStockBreakdownActions = !!appliedReportFilters && !appliedReportFilters.locationId;
    const breakdownLocations = (() => {
        if (!stockBreakdown) return [];
        const rows = new Map((stockBreakdown.locations || []).map(location => [location.locationId, {
            locationId: location.locationId,
            locationName: location.locationName,
            availableQuantity: location.quantity,
            incomingQuantity: 0
        }]));
        for (const route of stockBreakdown.inTransitRoutes || []) {
            if (route.destinationLocationId == null) continue;
            const row = rows.get(route.destinationLocationId) || {
                locationId: route.destinationLocationId,
                locationName: route.destinationLocationName,
                availableQuantity: 0,
                incomingQuantity: 0
            };
            row.incomingQuantity += route.quantity;
            rows.set(route.destinationLocationId, row);
        }
        return [...rows.values()].sort((a, b) => a.locationName.localeCompare(b.locationName));
    })();
    const tableColumnCount = 1 + Object.values(visibleColumns).filter(Boolean).length +
        (showStockBreakdownActions ? 1 : 0);

    const handleExportCsv = async () => {
        setExportingCsv(true);
        try {
            const csvData = sortedProducts.map(product => {
                const row = { Product: product.productName || '' };
                if (visibleColumns.debitQty) row['Debit Qty'] = (product.totalDebitQty || 0).toFixed(2);
                if (visibleColumns.creditQty) row['Credit Qty'] = (product.totalCreditQty || 0).toFixed(2);
                if (visibleColumns.balanceQty) row['Balance Qty'] = (product.qtyBalance || 0).toFixed(2);
                if (visibleColumns.avgRate) row['Avg Rate'] = (product.averageRate || 0).toFixed(2);
                if (visibleColumns.totalDebit) row['Total Debit'] = (product.totalDebit || 0).toFixed(2);
                if (visibleColumns.totalCredit) row['Total Credit'] = (product.totalCredit || 0).toFixed(2);
                if (visibleColumns.balance) row['Balance'] = (product.balance || 0).toFixed(2);
                if (visibleColumns.cogs) row['COGS'] = (product.cogs || 0).toFixed(2);
                if (visibleColumns.margin) row['Margin'] = (product.margin || 0).toFixed(2);
                if (visibleColumns.marginPct) row['Margin %'] = `${(product.marginPercentage || 0).toFixed(2)}%`;
                return row;
            });
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

            const rows = sortedProducts.map(product => {
                const row = { product: product.productName || '' };
                if (visibleColumns.debitQty) row.debitQty = formatNumber(product.totalDebitQty);
                if (visibleColumns.creditQty) row.creditQty = formatNumber(product.totalCreditQty);
                if (visibleColumns.balanceQty) row.balanceQty = formatNumber(product.qtyBalance);
                if (visibleColumns.avgRate) row.avgRate = formatCurrency(product.averageRate || 0);
                if (visibleColumns.totalDebit) row.totalDebit = formatCurrency(product.totalDebit || 0);
                if (visibleColumns.totalCredit) row.totalCredit = formatCurrency(product.totalCredit || 0);
                if (visibleColumns.balance) row.balance = formatCurrency(product.balance || 0);
                if (visibleColumns.cogs) row.cogs = formatCurrency(product.cogs || 0);
                if (visibleColumns.margin) row.margin = formatCurrency(product.margin || 0);
                if (visibleColumns.marginPct) row.marginPct = `${formatNumber(product.marginPercentage)}%`;
                return row;
            });

            if (totals) {
                const totalRow = { product: 'TOTAL' };
                if (visibleColumns.debitQty) totalRow.debitQty = formatNumber(totals.totalDebitQty);
                if (visibleColumns.creditQty) totalRow.creditQty = formatNumber(totals.totalCreditQty);
                if (visibleColumns.balanceQty) totalRow.balanceQty = totalBalanceQuantity == null
                    ? '-' : formatNumber(totalBalanceQuantity);
                if (visibleColumns.avgRate) totalRow.avgRate = '-';
                if (visibleColumns.totalDebit) totalRow.totalDebit = formatCurrency(totals.totalDebit || 0);
                if (visibleColumns.totalCredit) totalRow.totalCredit = formatCurrency(totals.totalCredit || 0);
                if (visibleColumns.balance) totalRow.balance = formatCurrency(totals.balance || 0);
                if (visibleColumns.cogs) totalRow.cogs = formatCurrency(totals.totalCOGS || 0);
                if (visibleColumns.margin) totalRow.margin = formatCurrency(totals.totalMargin || 0);
                if (visibleColumns.marginPct) totalRow.marginPct = `${formatNumber(totals.marginPercentage)}%`;
                rows.push(totalRow);
            }

            const columns = [
                { header: 'Product', dataKey: 'product' },
                ...(visibleColumns.debitQty ? [{ header: 'Debit Qty', dataKey: 'debitQty' }] : []),
                ...(visibleColumns.creditQty ? [{ header: 'Credit Qty', dataKey: 'creditQty' }] : []),
                ...(visibleColumns.balanceQty ? [{ header: 'Bal Qty', dataKey: 'balanceQty' }] : []),
                ...(visibleColumns.avgRate ? [{ header: 'Avg Rate', dataKey: 'avgRate' }] : []),
                ...(visibleColumns.totalDebit ? [{ header: 'Total Debit', dataKey: 'totalDebit' }] : []),
                ...(visibleColumns.totalCredit ? [{ header: 'Total Credit', dataKey: 'totalCredit' }] : []),
                ...(visibleColumns.balance ? [{ header: 'Balance', dataKey: 'balance' }] : []),
                ...(visibleColumns.cogs ? [{ header: 'COGS', dataKey: 'cogs' }] : []),
                ...(visibleColumns.margin ? [{ header: 'Margin', dataKey: 'margin' }] : []),
                ...(visibleColumns.marginPct ? [{ header: 'Margin %', dataKey: 'marginPct' }] : [])
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
                fontSize: 9,
                cellPadding: 2,
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
                                        ariaLabel="Project"
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
                                        options={[{ id: null, name: 'All Locations' }, ...locations.map(l => ({ id: l.id, name: l.name }))]}
                                        placeholder="Search location..."
                                        disabled={generating}
                                        ariaLabel="Location"
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
                                            showBalance: { key: 'balance', label: 'Balance', icon: 'fa-equals' },
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
                        <div className="small text-muted px-3 pt-2">
                            Debit Qty and Credit Qty include stock transfers. Bal Qty is available stock for the selected location or all locations.
                            Total Debit and Total Credit exclude transfer cost; product receiving charges remain in Total Debit.
                        </div>
                        <div className="report-table-container">
                            <div className="table-responsive">
                                <Table bordered striped hover className="table-sm mb-0" id="productReportTable">
                                    <thead className="table-light">
                                        <tr>
                                            <SortableHeader label="Product" sortKey="productName" sortDirection={getSortDirection('productName')} onSort={requestSort} className="product-column" style={{ minWidth: '180px', maxWidth: '300px' }} />
                                            {visibleColumns.debitQty && <SortableHeader label="Debit Qty" sortKey="totalDebitQty" sortDirection={getSortDirection('totalDebitQty')} onSort={requestSort} className="qty-column text-end" />}
                                            {visibleColumns.creditQty && <SortableHeader label="Credit Qty" sortKey="totalCreditQty" sortDirection={getSortDirection('totalCreditQty')} onSort={requestSort} className="qty-column text-end" />}
                                            {visibleColumns.balanceQty && <SortableHeader label="Bal Qty" sortKey="qtyBalance" sortDirection={getSortDirection('qtyBalance')} onSort={requestSort} className="qty-column text-end" />}
                                            {visibleColumns.avgRate && <SortableHeader label="Avg Rate" sortKey="averageRate" sortDirection={getSortDirection('averageRate')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.totalDebit && <SortableHeader label="Total Debit" sortKey="totalDebit" sortDirection={getSortDirection('totalDebit')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.totalCredit && <SortableHeader label="Total Credit" sortKey="totalCredit" sortDirection={getSortDirection('totalCredit')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.balance && <SortableHeader label="Balance" sortKey="balance" sortDirection={getSortDirection('balance')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.cogs && <SortableHeader label="COGS" sortKey="cogs" sortDirection={getSortDirection('cogs')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.margin && <SortableHeader label="Margin" sortKey="margin" sortDirection={getSortDirection('margin')} onSort={requestSort} className="currency-column text-end" />}
                                            {visibleColumns.marginPct && <SortableHeader label="Margin %" sortKey="marginPercentage" sortDirection={getSortDirection('marginPercentage')} onSort={requestSort} className="percent-column text-end" />}
                                            {showStockBreakdownActions && <th className="stock-location-column text-center">Stock locations</th>}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {sortedProducts.length === 0 && allProducts.length > 0 ? (
                                            <tr>
                                                <td colSpan={tableColumnCount} className="text-center text-muted py-4">
                                                    {searchTerm ? 'No results found for your search' : 'No data available'}
                                                </td>
                                            </tr>
                                        ) : sortedProducts.length === 0 && allProducts.length === 0 ? (
                                            <tr>
                                                <td colSpan={tableColumnCount} className="text-center text-muted py-4">
                                                    No data available
                                                </td>
                                            </tr>
                                        ) : (
                                            sortedProducts.map((product, index) => {
                                                return (
                                                    <tr key={index}>
                                                        <td className="product-column" style={{ wordWrap: 'break-word', overflowWrap: 'break-word', whiteSpace: 'normal', minWidth: '180px', maxWidth: '300px' }} title={product.productName}>{product.productName}</td>
                                                        {visibleColumns.debitQty && <td className="qty-column text-end">{formatNumber(product.totalDebitQty)}</td>}
                                                        {visibleColumns.creditQty && <td className="qty-column text-end">{formatNumber(product.totalCreditQty)}</td>}
                                                        {visibleColumns.balanceQty && <td className="qty-column text-end">{formatNumber(product.qtyBalance)}</td>}
                                                        {visibleColumns.avgRate && <td className="currency-column text-end">{formatCurrency(product.averageRate || 0)}</td>}
                                                        {visibleColumns.totalDebit && <td className="currency-column text-end">{formatCurrency(product.totalDebit || 0)}</td>}
                                                        {visibleColumns.totalCredit && <td className="currency-column text-end">{formatCurrency(product.totalCredit || 0)}</td>}
                                                        {visibleColumns.balance && <td className="currency-column text-end">{formatCurrency(product.balance || 0)}</td>}
                                                        {visibleColumns.cogs && <td className="currency-column text-end">{formatCurrency(product.cogs || 0)}</td>}
                                                        {visibleColumns.margin && <td className="currency-column text-end">{formatCurrency(product.margin || 0)}</td>}
                                                        {visibleColumns.marginPct && <td className="percent-column text-end">{formatNumber(product.marginPercentage)}%</td>}
                                                        {showStockBreakdownActions && (
                                                            <td className="stock-location-column text-center">
                                                                <Button
                                                                    variant="outline-primary"
                                                                    size="sm"
                                                                    onClick={() => openStockBreakdown(product)}
                                                                    aria-label={`View stock locations for ${product.productName}`}
                                                                    title="View available stock by warehouse"
                                                                >
                                                                    <i className="fas fa-warehouse me-1" aria-hidden="true"></i>
                                                                    View
                                                                </Button>
                                                            </td>
                                                        )}
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
                                                {visibleColumns.balanceQty && <td className="qty-column text-end">{totalBalanceQuantity == null
                                                    ? '-' : formatNumber(totalBalanceQuantity)}</td>}
                                                {visibleColumns.avgRate && <td className="currency-column text-end">-</td>}
                                                {visibleColumns.totalDebit && <td className="currency-column text-end">{formatCurrency(totals.totalDebit || 0)}</td>}
                                                {visibleColumns.totalCredit && <td className="currency-column text-end">{formatCurrency(totals.totalCredit || 0)}</td>}
                                                {visibleColumns.balance && <td className="currency-column text-end">{formatCurrency(totals.balance || 0)}</td>}
                                                {visibleColumns.cogs && <td className="currency-column text-end">{formatCurrency(totals.totalCOGS || 0)}</td>}
                                                {visibleColumns.margin && <td className="currency-column text-end">{formatCurrency(totals.totalMargin || 0)}</td>}
                                                {visibleColumns.marginPct && <td className="percent-column text-end">{formatNumber(totals.marginPercentage)}%</td>}
                                                {showStockBreakdownActions && <td></td>}
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

            <Modal show={!!breakdownTarget} onHide={closeStockBreakdown} centered size="lg">
                <Modal.Header closeButton>
                    <Modal.Title>
                        <i className="fas fa-warehouse me-2" aria-hidden="true"></i>
                        {breakdownTarget?.productName} - Stock by warehouse
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="text-muted small mb-3">
                        Available quantity {appliedReportFilters?.endDate
                            ? `as of ${appliedReportFilters.endDate}`
                            : 'across all posted dates'}
                    </div>

                    {breakdownLoading && (
                        <div className="text-center py-4" role="status">
                            <span className="spinner-border text-primary" aria-hidden="true"></span>
                            <div className="mt-2">Loading stock locations...</div>
                        </div>
                    )}

                    {breakdownError && (
                        <Alert variant="danger">
                            <i className="fas fa-exclamation-circle me-2" aria-hidden="true"></i>
                            {breakdownError}
                            <div className="mt-2">
                                <Button variant="outline-danger" size="sm" onClick={() => openStockBreakdown(breakdownTarget)}>
                                    Retry
                                </Button>
                            </div>
                        </Alert>
                    )}

                    {stockBreakdown && (
                        <>
                            {(stockBreakdown.assignmentIssueCount > 0 || stockBreakdown.reconciliationDifference !== 0 || stockBreakdown.transitRouteIssueCount > 0) && (
                                <Alert variant="warning">
                                    Some stock movements or transfer routes do not have exactly one warehouse assignment.
                                    They are shown separately so the displayed totals remain auditable.
                                </Alert>
                            )}
                            {(Number(stockBreakdown.totalQuantity) !== Number(breakdownTarget?.reportQuantity) ||
                                Number(stockBreakdown.inTransitQuantity || 0) !== Number(breakdownTarget?.reportTransitQuantity || 0)) && (
                                <Alert variant="warning">
                                    The current stock position differs from the generated report row.
                                    Regenerate the report if stock changed after it was loaded.
                                </Alert>
                            )}
                            <div className="table-responsive">
                                <Table bordered hover size="sm" className="stock-breakdown-table mb-0">
                                    <thead className="table-light">
                                        <tr>
                                            <th>Warehouse</th>
                                            <th className="text-end">Available quantity</th>
                                            <th className="text-end">Incoming in transit</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {breakdownLocations.map(location => (
                                            <tr key={location.locationId}>
                                                <td>{location.locationName}</td>
                                                <td className={`text-end ${location.availableQuantity < 0 ? 'text-danger' : ''}`}>
                                                    {formatNumber(location.availableQuantity)}
                                                </td>
                                                <td className="text-end">{formatNumber(location.incomingQuantity)}</td>
                                            </tr>
                                        ))}
                                        {stockBreakdown.unassignedQuantity !== 0 && (
                                            <tr className="table-warning">
                                                <td>Unassigned / no warehouse</td>
                                                <td className="text-end">{formatNumber(stockBreakdown.unassignedQuantity)}</td>
                                                <td></td>
                                            </tr>
                                        )}
                                        {stockBreakdown.ambiguousQuantity !== 0 && (
                                            <tr className="table-warning">
                                                <td>Multiple warehouse assignments</td>
                                                <td className="text-end">{formatNumber(stockBreakdown.ambiguousQuantity)}</td>
                                                <td></td>
                                            </tr>
                                        )}
                                        {breakdownLocations.length === 0 &&
                                            stockBreakdown.unassignedQuantity === 0 &&
                                            stockBreakdown.ambiguousQuantity === 0 && (
                                                <tr>
                                                    <td colSpan="3" className="text-center text-muted py-3">
                                                        No available stock by warehouse
                                                    </td>
                                                </tr>
                                            )}
                                    </tbody>
                                    <tfoot className="table-secondary fw-bold">
                                        <tr>
                                            <td>Total available</td>
                                            <td className="text-end">{formatNumber(stockBreakdown.totalQuantity)}</td>
                                            <td className="text-end">{formatNumber(stockBreakdown.inTransitQuantity)}</td>
                                        </tr>
                                    </tfoot>
                                </Table>
                            </div>
                            <div className="mt-3 fw-semibold">
                                Total stock position: {formatNumber(stockBreakdown.totalStockPosition ??
                                    (Number(stockBreakdown.totalQuantity) + Number(stockBreakdown.inTransitQuantity || 0)))}
                            </div>
                            {(stockBreakdown.inTransitRoutes || []).length > 0 && (
                                <div className="mt-3">
                                    <div className="fw-semibold mb-2">Outstanding transfers</div>
                                    <Table bordered size="sm">
                                        <thead className="table-light"><tr>
                                            <th>Transfer</th><th>Route</th><th className="text-end">Quantity</th>
                                        </tr></thead>
                                        <tbody>{stockBreakdown.inTransitRoutes.map(route => (
                                            <tr key={route.dispatchJournalVoucherId}>
                                                <td>JV #{route.dispatchJournalVoucherId}</td>
                                                <td>{route.sourceLocationName} → {route.destinationLocationName}</td>
                                                <td className="text-end">{formatNumber(route.quantity)}</td>
                                            </tr>
                                        ))}</tbody>
                                    </Table>
                                </div>
                            )}
                        </>
                    )}
                </Modal.Body>
                <Modal.Footer>
                    <Button variant="secondary" onClick={closeStockBreakdown}>Close</Button>
                </Modal.Footer>
            </Modal>
        </div>
    );
};

export default ProductReport;

