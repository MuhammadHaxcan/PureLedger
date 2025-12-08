import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import Papa from 'papaparse';
import './ProductReport.css';

const buildVoucherUrl = (voucherId, type) => {
    if (!voucherId) {
        return '#';
    }
    return `/reporting/print-voucher?voucherId=${voucherId}&type=${type}`;
};

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
    const [searchTerm, setSearchTerm] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        categoryId: null,
        locationId: null,
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
            product.lastVoucherId?.toString().includes(search) ||
            `jv-${product.lastVoucherId || ''}`.includes(search) ||
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

    const exportToCsv = (data, filename) => {
        if (!data || data.length === 0) {
            alert('No data to export');
            return;
        }

        const csvContent = Papa.unparse(data);
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.click();
    };

    const handleExportCsv = () => {
        const csvData = filteredProducts.map(product => ({
            Product: product.productName || '',
            Voucher: `JV-${product.lastVoucherId || ''}`,
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
        exportToCsv(csvData, `ProductReport_${dateRange}.csv`);
    };

    const handleExportPdf = () => {
        const doc = new jsPDF('landscape', 'mm', 'a4');
        const selectedProject = projects.find(p => p.id === formData.projectId);
        const selectedCategory = categories.find(c => c.id === formData.categoryId);
        const selectedLocation = locations.find(l => l.id === formData.locationId);

        // Header
        doc.setFontSize(18);
        doc.setTextColor(30, 64, 175);
        doc.text('Product Stock & Margin Report', 14, 20);

        doc.setFontSize(10);
        doc.setTextColor(100);
        doc.text(`Project: ${selectedProject?.name || 'All Projects'}`, 14, 28);
        if (formData.startDate) {
            doc.text(`Date Range: ${formData.startDate} to ${formData.endDate || formData.startDate}`, 14, 34);
        }
        if (selectedCategory) {
            doc.text(`Category: ${selectedCategory.name}`, 14, 40);
        }
        if (selectedLocation) {
            doc.text(`Location: ${selectedLocation.name}`, 14, 46);
        }
        doc.text(`Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords}`, 14, selectedCategory || selectedLocation ? 52 : 40);

        const rows = filteredProducts.map(product => ({
            product: product.productName || '',
            voucher: `JV-${product.lastVoucherId || ''}`,
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

        // Add totals row
        if (totals) {
            rows.push({
                product: 'TOTAL',
                voucher: '',
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
            { header: 'Voucher', dataKey: 'voucher' },
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

        // Split rows into chunks of 10 per page
        const rowsPerPage = 20;
        let startY = selectedCategory || selectedLocation ? 56 : 46;
        
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
                styles: { fontSize: 6, cellPadding: 1.5 },
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
                `PureLedger - Product Report | Page ${i} of ${pageCount}`,
                14,
                doc.internal.pageSize.height - 10
            );
        }

        doc.save(`ProductReport_${new Date().toISOString().split('T')[0]}.pdf`);
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
                                    >
                                        <i className="fas fa-file-csv me-1"></i> Export CSV
                                    </Button>
                                    <Button 
                                        variant="outline-danger" 
                                        size="sm" 
                                        onClick={handleExportPdf}
                                    >
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
                        <div className="product-report-container">
                            <div className="table-responsive" style={{ width: '100%', overflowX: 'auto' }}>
                                <Table bordered striped hover className="table-sm mb-0 w-100" id="productReportTable" style={{ width: '100%', tableLayout: 'auto' }}>
                                    <thead className="table-light">
                                        <tr>
                                            <th className="product-column">Product</th>
                                            <th className="voucher-column">Voucher</th>
                                            <th className="qty-column text-end">Debit Qty</th>
                                            <th className="qty-column text-end">Credit Qty</th>
                                            <th className="qty-column text-end">Bal Qty</th>
                                            <th className="currency-column text-end">Avg Rate</th>
                                            <th className="currency-column text-end">Total Debit</th>
                                            <th className="currency-column text-end">Total Credit</th>
                                            <th className="currency-column text-end">COGS</th>
                                            <th className="currency-column text-end">Margin</th>
                                            <th className="percent-column text-end">Margin %</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredProducts.length === 0 && allProducts.length > 0 ? (
                                            <tr>
                                                <td colSpan="11" className="text-center text-muted py-4">
                                                    {searchTerm ? 'No results found for your search' : 'No data available'}
                                                </td>
                                            </tr>
                                        ) : filteredProducts.length === 0 && allProducts.length === 0 ? (
                                            <tr>
                                                <td colSpan="11" className="text-center text-muted py-4">
                                                    No data available
                                                </td>
                                            </tr>
                                        ) : (
                                            filteredProducts.map((product, index) => {
                                                const isLastEntry = index === filteredProducts.length - 1;
                                                return (
                                                    <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                        <td className="product-column" title={product.productName}>{product.productName}</td>
                                                        <td className="voucher-column">
                                                            <div className="d-flex flex-wrap gap-1 align-items-center">
                                                                <a
                                                                    href={buildVoucherUrl(product.lastVoucherId, 'JV')}
                                                                    target="_blank"
                                                                    rel="noopener noreferrer"
                                                                    className="voucher-link text-decoration-none badge bg-primary"
                                                                    style={{ fontSize: '0.75rem', cursor: 'pointer' }}
                                                                >
                                                                    JV-{product.lastVoucherId}
                                                                </a>
                                                            </div>
                                                        </td>
                                                        <td className="qty-column text-end">{formatNumber(product.totalDebitQty)}</td>
                                                        <td className="qty-column text-end">{formatNumber(product.totalCreditQty)}</td>
                                                        <td className="qty-column text-end">{formatNumber(product.qtyBalance)}</td>
                                                        <td className="currency-column text-end">{formatCurrency(product.averageRate || 0)}</td>
                                                        <td className="currency-column text-end">{formatCurrency(product.totalDebit || 0)}</td>
                                                        <td className="currency-column text-end">{formatCurrency(product.totalCredit || 0)}</td>
                                                        <td className="currency-column text-end">{formatCurrency(product.cogs || 0)}</td>
                                                        <td className="currency-column text-end">{formatCurrency(product.margin || 0)}</td>
                                                        <td className="percent-column text-end">{formatNumber(product.marginPercentage)}%</td>
                                                    </tr>
                                                );
                                            })
                                        )}
                                    </tbody>
                                    {totals && (
                                        <tfoot className="table-secondary">
                                            <tr className="fw-bold">
                                                <td colSpan="2" className="text-end">TOTAL</td>
                                                <td className="qty-column text-end">{formatNumber(totals.totalDebitQty)}</td>
                                                <td className="qty-column text-end">{formatNumber(totals.totalCreditQty)}</td>
                                                <td className="qty-column text-end">{formatNumber(totals.netQuantity)}</td>
                                                <td className="currency-column text-end">-</td>
                                                <td className="currency-column text-end">{formatCurrency(totals.totalDebit || 0)}</td>
                                                <td className="currency-column text-end">{formatCurrency(totals.totalCredit || 0)}</td>
                                                <td className="currency-column text-end">{formatCurrency(totals.totalCOGS || 0)}</td>
                                                <td className="currency-column text-end">{formatCurrency(totals.totalMargin || 0)}</td>
                                                <td className="percent-column text-end">{formatNumber(totals.marginPercentage)}%</td>
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

