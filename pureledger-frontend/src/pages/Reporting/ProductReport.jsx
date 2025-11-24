import React, { useState, useEffect } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';

const ProductReport = () => {
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [locations, setLocations] = useState([]);
    const [reportData, setReportData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [error, setError] = useState('');
    const [formData, setFormData] = useState({
        projectId: null,
        categoryId: null,
        locationId: null,
        startDate: '',
        endDate: ''
    });

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

    const handleSubmit = async (e) => {
        e.preventDefault();
        setGenerating(true);
        setError('');
        setReportData(null);

        if (!formData.projectId) {
            setError('Please select a project');
            return;
        }

        try {
            const params = {
                projectId: formData.projectId.toString(),
                startDate: formData.startDate || null,
                endDate: formData.endDate || null,
                categoryId: formData.categoryId ? formData.categoryId.toString() : '',
                locationId: formData.locationId ? formData.locationId.toString() : ''
            };

            const response = await apiClient.get('/ReportingApi/product-report', { params });
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                // API returns ProductSummaries, not products
                setReportData({
                    products: data.productSummaries || [],
                    totals: data.totals || {}
                });
            } else {
                setError('Failed to generate report');
            }
        } catch (err) {
            setError(err.response?.data?.error || 'Failed to generate report');
        } finally {
            setGenerating(false);
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-PK', {
            style: 'currency',
            currency: 'PKR',
            minimumFractionDigits: 2
        }).format(value);
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
                        </div>
                        <div className="row">
                            <div className="col-12">
                                <button type="submit" className="btn btn-primary" disabled={generating}>
                                    {generating ? <LoadingSpinner /> : <><i className="fas fa-search me-1"></i> Generate Report</>}
                                </button>
                            </div>
                        </div>
                    </form>
                </div>
            </div>

            {reportData && (
                <div className="card shadow">
                    <div className="card-header py-3">
                        <h6 className="m-0 font-weight-bold text-primary">Report Results</h6>
                    </div>
                    <div className="card-body">
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
                                    {reportData.products && reportData.products.length > 0 ? (
                                        reportData.products.map((product, index) => (
                                            <tr key={index}>
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
                                        ))
                                    ) : (
                                        <tr>
                                            <td colSpan="13" className="text-center">No data found</td>
                                        </tr>
                                    )}
                                </tbody>
                                {reportData.totals && (
                                    <tfoot className="table-secondary">
                                        <tr className="fw-bold">
                                            <td colSpan="4" className="text-end">TOTAL</td>
                                            <td className="text-end">{reportData.totals.totalDebitQty || 0}</td>
                                            <td className="text-end">{reportData.totals.totalCreditQty || 0}</td>
                                            <td className="text-end">{reportData.totals.netQuantity || 0}</td>
                                            <td className="text-end">-</td>
                                            <td className="text-end">{formatCurrency(reportData.totals.totalDebit || 0)}</td>
                                            <td className="text-end">{formatCurrency(reportData.totals.totalCredit || 0)}</td>
                                            <td className="text-end">{formatCurrency(reportData.totals.totalCOGS || 0)}</td>
                                            <td className="text-end">{formatCurrency(reportData.totals.totalMargin || 0)}</td>
                                            <td className="text-end">{reportData.totals.marginPercentage?.toFixed(2) || '0.00'}%</td>
                                        </tr>
                                    </tfoot>
                                )}
                            </table>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default ProductReport;

