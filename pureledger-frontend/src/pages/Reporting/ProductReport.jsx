import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import { PDFDownloadLink } from '@react-pdf/renderer';
import ProductReportPDFDocument from './ProductReportPDF';

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
    const pageSize = 500;

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
            minimumFractionDigits: 2
        }).format(value || 0);
    };

    const filteredProducts = allProducts.filter(product => {
        if (!searchTerm) return true;
        const search = searchTerm.toLowerCase();
        return (
            (product.productName || '').toLowerCase().includes(search) ||
            product.lastVoucherId?.toString().includes(search) ||
            (product.entryDate || '').toLowerCase().includes(search) ||
            (product.creditableDate || '').toLowerCase().includes(search)
        );
    });

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
        const csvData = filteredProducts.map(product => ({
            Product: product.productName || '',
            Voucher: `JV-${product.lastVoucherId || ''}`,
            'Entry Date': product.entryDate || '-',
            'Due Date': product.creditableDate || '-',
            'Debit Qty': product.totalDebitQty || 0,
            'Credit Qty': product.totalCreditQty || 0,
            'Balance Qty': product.qtyBalance || 0,
            'Avg Rate': product.averageRate || 0,
            'Total Debit': product.totalDebit || 0,
            'Total Credit': product.totalCredit || 0,
            COGS: product.cogs || 0,
            Margin: product.margin || 0,
            'Margin %': `${product.marginPercentage?.toFixed(2) || '0.00'}%`
        }));
        const dateRange = formData.startDate && formData.endDate 
            ? `${formData.startDate}_to_${formData.endDate}` 
            : 'report';
        exportToCsv(csvData, `ProductReport_${dateRange}.csv`);
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
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            <div className="card shadow mb-4">
                <div className="card-header py-3 d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-filter me-2"></i>Report Parameters
                    </h6>
                </div>
                <div className="card-body">
                    <form id="productReportForm" onSubmit={handleSubmit}>
                        <div className="row mb-3">
                            <div className="col-md-3 mb-3">
                                <label className="form-label">Since</label>
                                <input
                                    type="date"
                                    className="form-control"
                                    id="startDate"
                                    value={formData.startDate}
                                    onChange={(e) => setFormData({ ...formData, startDate: e.target.value })}
                                />
                            </div>
                            <div className="col-md-3 mb-3">
                                <label className="form-label">To</label>
                                <input
                                    type="date"
                                    className="form-control"
                                    id="endDate"
                                    value={formData.endDate}
                                    onChange={(e) => setFormData({ ...formData, endDate: e.target.value })}
                                />
                            </div>
                            <div className="col-md-3 mb-3">
                                <Form.Label>Project</Form.Label>
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
                                                <i className="fas fa-chart-bar me-1"></i> Generate Report
                                            </>
                                        )}
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </form>
                </div>
            </div>

            {allProducts.length > 0 && (
                <div className="card shadow">
                    <div className="card-header py-3 d-flex justify-content-between align-items-center">
                        <h6 className="m-0 font-weight-bold text-primary">Report Results</h6>
                        <div>
                            {totalRecords > 0 && (
                                <span className="badge bg-primary me-2">
                                    {allProducts.length} of {totalRecords} products
                                </span>
                            )}
                            <Button 
                                variant="outline-success" 
                                size="sm" 
                                onClick={handleExportCsv} 
                                disabled={filteredProducts.length === 0}
                                className="me-2"
                            >
                                <i className="fas fa-file-csv me-1"></i> Export CSV
                            </Button>
                            {!hasMoreData && filteredProducts.length > 0 && (
                                <PDFDownloadLink
                                    document={
                                        <ProductReportPDFDocument
                                            products={filteredProducts}
                                            totals={totals}
                                            formData={formData}
                                            projects={projects}
                                            categories={categories}
                                            locations={locations}
                                            totalRecords={totalRecords}
                                        />
                                    }
                                    fileName={`ProductReport_${new Date().toISOString().split('T')[0]}.pdf`}
                                >
                                    {({ blob, url, loading, error }) => (
                                        <Button 
                                            variant="outline-danger" 
                                            size="sm" 
                                            disabled={loading || error}
                                        >
                                            {loading ? (
                                                <>
                                                    <span className="spinner-border spinner-border-sm me-1" role="status"></span>
                                                    Generating PDF...
                                                </>
                                            ) : (
                                                <>
                                                    <i className="fas fa-file-pdf me-1"></i> Export PDF
                                                </>
                                            )}
                                        </Button>
                                    )}
                                </PDFDownloadLink>
                            )}
                        </div>
                    </div>
                    <div className="card-body">
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
                            <table className="table table-bordered" style={{ width: '100%', marginBottom: 0 }}>
                                <thead className="table-light">
                                    <tr>
                                        <th>Product</th>
                                        <th>Voucher</th>
                                        <th>Entry Date</th>
                                        <th>Due Date</th>
                                        <th className="text-end">Debit Qty</th>
                                        <th className="text-end">Credit Qty</th>
                                        <th className="text-end">Balance Qty</th>
                                        <th className="text-end">Avg Rate</th>
                                        <th className="text-end">Total Debit</th>
                                        <th className="text-end">Total Credit</th>
                                        <th className="text-end">COGS</th>
                                        <th className="text-end">Margin</th>
                                        <th className="text-end">Margin %</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredProducts.length === 0 ? (
                                        <tr>
                                            <td colSpan="13" className="text-center text-muted py-4">
                                                {searchTerm ? 'No results found for your search' : 'No data available'}
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredProducts.map((product, index) => {
                                            const isLastEntry = index === filteredProducts.length - 1;
                                            return (
                                                <tr key={index} ref={isLastEntry ? lastEntryElementRef : null}>
                                                    <td>{product.productName}</td>
                                                    <td>JV-{product.lastVoucherId}</td>
                                                    <td>{product.entryDate || '-'}</td>
                                                    <td>{product.creditableDate || '-'}</td>
                                                    <td className="text-end">{product.totalDebitQty || 0}</td>
                                                    <td className="text-end">{product.totalCreditQty || 0}</td>
                                                    <td className="text-end">{product.qtyBalance || 0}</td>
                                                    <td className="text-end">{formatCurrency(product.averageRate || 0)}</td>
                                                    <td className="text-end">{formatCurrency(product.totalDebit || 0)}</td>
                                                    <td className="text-end">{formatCurrency(product.totalCredit || 0)}</td>
                                                    <td className="text-end">{formatCurrency(product.cogs || 0)}</td>
                                                    <td className="text-end">{formatCurrency(product.margin || 0)}</td>
                                                    <td className="text-end">{product.marginPercentage?.toFixed(2) || '0.00'}%</td>
                                                </tr>
                                            );
                                        })
                                    )}
                                </tbody>
                                {totals && (
                                    <tfoot className="table-secondary">
                                        <tr className="fw-bold">
                                            <td colSpan="4" className="text-end">TOTAL</td>
                                            <td className="text-end">{totals.totalDebitQty || 0}</td>
                                            <td className="text-end">{totals.totalCreditQty || 0}</td>
                                            <td className="text-end">{totals.netQuantity || 0}</td>
                                            <td className="text-end">-</td>
                                            <td className="text-end">{formatCurrency(totals.totalDebit || 0)}</td>
                                            <td className="text-end">{formatCurrency(totals.totalCredit || 0)}</td>
                                            <td className="text-end">{formatCurrency(totals.totalCOGS || 0)}</td>
                                            <td className="text-end">{formatCurrency(totals.totalMargin || 0)}</td>
                                            <td className="text-end">{totals.marginPercentage?.toFixed(2) || '0.00'}%</td>
                                        </tr>
                                    </tfoot>
                                )}
                            </table>
                        </div>
                        {isLoadingMore && (
                            <div className="text-center my-3">
                                <LoadingSpinner />
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
};

export default ProductReport;

