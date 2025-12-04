import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup, Nav, Tab } from 'react-bootstrap';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const MissedEntriesReport = () => {
    usePageTitle('Missed Entries');
    const [projects, setProjects] = useState([]);
    const [allEntries, setAllEntries] = useState([]);
    const [byCustomerData, setByCustomerData] = useState([]);
    const [byProductData, setByProductData] = useState([]);
    const [customerDetails, setCustomerDetails] = useState([]);
    const [selectedCustomer, setSelectedCustomer] = useState(null);
    const [summaryStats, setSummaryStats] = useState({
        totalMissedEntries: 0,
        totalMissedQuantity: 0,
        uniqueCustomers: 0,
        uniqueProducts: 0,
        topCustomer: null,
        topCustomerMissedQuantity: 0,
        topMissedProduct: null,
        topMissedProductQuantity: 0
    });
    const [loading, setLoading] = useState(true);
    const [searching, setSearching] = useState(false);
    const [error, setError] = useState('');
    const [activeTab, setActiveTab] = useState('details');
    const [formData, setFormData] = useState({
        projectId: null,
        startDate: '',
        endDate: ''
    });
    const [currentPage, setCurrentPage] = useState(0);
    const [hasMoreData, setHasMoreData] = useState(false);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [isAutoLoading, setIsAutoLoading] = useState(false);
    const [totalRecords, setTotalRecords] = useState(0);
    const [loadedCount, setLoadedCount] = useState(0);
    const [tabsDisabled, setTabsDisabled] = useState(false);
    const pageSize = 500;
    const isLoadingRef = useRef(false);

    // Set default dates (1 month ago to today)
    useEffect(() => {
        const today = new Date();
        const oneMonthAgo = new Date();
        oneMonthAgo.setMonth(today.getMonth() - 1);
        
        setFormData(prev => ({
            ...prev,
            startDate: prev.startDate || oneMonthAgo.toISOString().split('T')[0],
            endDate: prev.endDate || today.toISOString().split('T')[0]
        }));
    }, []);

    useEffect(() => {
        loadInitData();
    }, []);

    const loadInitData = async () => {
        setLoading(true);
        setError('');
        try {
            const response = await apiClient.get('/ReportingApi/missed-entries-init');
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

    const validateDates = () => {
        if (formData.startDate && formData.endDate && formData.startDate > formData.endDate) {
            setError('Start date must be before end date');
            const today = new Date();
            const oneMonthAgo = new Date();
            oneMonthAgo.setMonth(today.getMonth() - 1);
            setFormData(prev => ({
                ...prev,
                startDate: oneMonthAgo.toISOString().split('T')[0],
                endDate: today.toISOString().split('T')[0]
            }));
            return false;
        }
        return true;
    };

    const resetReportState = () => {
        setAllEntries([]);
        setByCustomerData([]);
        setByProductData([]);
        setCustomerDetails([]);
        setSelectedCustomer(null);
        setCurrentPage(0);
        setHasMoreData(false);
        setTotalRecords(0);
        setLoadedCount(0);
        setIsLoadingMore(false);
        setIsAutoLoading(false);
        isLoadingRef.current = false;
        setSummaryStats({
            totalMissedEntries: 0,
            totalMissedQuantity: 0,
            uniqueCustomers: 0,
            uniqueProducts: 0,
            topCustomer: null,
            topCustomerMissedQuantity: 0,
            topMissedProduct: null,
            topMissedProductQuantity: 0
        });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');

        if (!formData.projectId) {
            setError('Please select a project first');
            return;
        }

        if (!validateDates()) {
            return;
        }

        resetReportState();
        await loadAllData();
    };

    const loadAllData = async () => {
        setSearching(true);
        setTabsDisabled(true);
        try {
            // Load summary, by-customer, and by-product data in parallel
            await Promise.all([
                loadSummaryStats(),
                loadByCustomerData(),
                loadByProductData()
            ]);

            // Start loading detailed entries with auto-pagination
            await loadEntries(0, false);
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load missed entries data');
        } finally {
            setSearching(false);
        }
    };

    const loadEntries = async (page, append = false) => {
        if (isLoadingRef.current) {
            return;
        }

        isLoadingRef.current = true;

        if (page > 0) {
            setIsLoadingMore(true);
        }

        try {
            const params = {
                page: page,
                pageSize: pageSize,
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries', { params });
            if (response.data.success && response.data.data) {
                const data = response.data.data;
                const entries = data.entries || data.Entries || [];
                
                if (append) {
                    setAllEntries(prev => [...prev, ...entries]);
                } else {
                    setAllEntries(entries);
                }
                
                const newLoadedCount = append ? loadedCount + entries.length : entries.length;
                setLoadedCount(newLoadedCount);
                setHasMoreData(data.hasMoreData || data.HasMoreData || false);
                setTotalRecords(data.totalRecords || data.TotalRecords || entries.length);
                setCurrentPage(page + 1);

                // Auto-load next page if there's more data
                const hasMore = data.hasMoreData || data.HasMoreData || false;
                if (hasMore) {
                    setIsAutoLoading(true);
                    setTimeout(() => {
                        isLoadingRef.current = false;
                        loadEntries(page + 1, true);
                    }, 150);
                } else {
                    setIsAutoLoading(false);
                    setTabsDisabled(false);
                }
            } else {
                setError('Failed to load missed entries');
                setTabsDisabled(false);
            }
        } catch (err) {
            setError(err.response?.data?.message || 'Failed to load missed entries');
            setTabsDisabled(false);
        } finally {
            setIsLoadingMore(false);
            if (!hasMoreData) {
                isLoadingRef.current = false;
            }
        }
    };

    const loadSummaryStats = async () => {
        try {
            const params = {
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries-summary', { params });
            if (response.data.success && response.data.data) {
                setSummaryStats(response.data.data);
            }
        } catch (err) {
            console.error('Error loading summary stats:', err);
        }
    };

    const loadByCustomerData = async () => {
        try {
            const params = {
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries-by-customer', { params });
            if (response.data.success && response.data.data) {
                setByCustomerData(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading by-customer data:', err);
            setByCustomerData([]);
        }
    };

    const loadByProductData = async () => {
        try {
            const params = {
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries-by-product', { params });
            if (response.data.success && response.data.data) {
                setByProductData(response.data.data || []);
            }
        } catch (err) {
            console.error('Error loading by-product data:', err);
            setByProductData([]);
        }
    };

    const showCustomerDetails = async (customerId, customerName) => {
        setSelectedCustomer({ id: customerId, name: customerName, loading: true });
        try {
            const params = {
                customerAccountId: customerId,
                projectId: formData.projectId || null,
                startDate: formData.startDate || null,
                endDate: formData.endDate || null
            };

            const response = await apiClient.get('/ReportingApi/missed-entries-data', { params });
            if (response.data.success && response.data.data) {
                setCustomerDetails(response.data.data.entries || []);
                setSelectedCustomer({ id: customerId, name: customerName, loading: false });
            }
        } catch (err) {
            console.error('Error loading customer details:', err);
            setCustomerDetails([]);
            setSelectedCustomer({ id: customerId, name: customerName, loading: false, error: true });
        }
    };

    const hideCustomerDetails = () => {
        setSelectedCustomer(null);
        setCustomerDetails([]);
    };

    const handleTabSelect = (k) => {
        if (tabsDisabled || isAutoLoading || searching) {
            return;
        }
        if (k) {
            setActiveTab(k);
            hideCustomerDetails();
        }
    };

    const formatCurrency = (value) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value || 0);
    };

    const formatDate = (dateString) => {
        if (!dateString) return '';
        try {
            const date = new Date(dateString);
            return date.toLocaleDateString();
        } catch {
            return dateString;
        }
    };

    const formatNumber = (value) => {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(value || 0);
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

    const handleExportDetails = () => {
        const csvData = allEntries.map(entry => ({
            Date: formatDate(entry.missedDate || entry.MissedDate),
            Project: entry.projectName || entry.ProjectName || '',
            Customer: entry.customerName || entry.customerAccountName || entry.CustomerName || 'N/A',
            Product: entry.productName || entry.productAccountName || entry.ProductName || 'N/A',
            Quantity: entry.quantity || entry.Quantity || 0,
            Rate: entry.rate || entry.Rate || 0,
            Discount: entry.discount || entry.Discount || 0,
            'Total Value': entry.totalValue || entry.TotalValue || 0,
            Description: entry.description || entry.Description || ''
        }));
        exportToCsv(csvData, `MissedEntries_Details_${new Date().toISOString().split('T')[0]}.csv`);
    };

    const handleExportByCustomer = () => {
        const csvData = byCustomerData.map(item => ({
            Customer: item.customerName || 'Unknown',
            'Entry Count': item.entryCount || 0,
            'Total Quantity': item.totalQuantity || 0,
            'Total Value': item.totalValue || 0,
            'Unique Products': item.uniqueProductCount || 0,
            'Avg Qty/Entry': item.averageQuantityPerEntry || 0
        }));
        exportToCsv(csvData, `MissedEntries_ByCustomer_${new Date().toISOString().split('T')[0]}.csv`);
    };

    const handleExportByProduct = () => {
        const csvData = byProductData.map(item => ({
            Product: item.productName || 'Unknown',
            'Entry Count': item.entryCount || 0,
            'Total Quantity': item.totalQuantity || 0,
            'Total Value': item.totalValue || 0,
            'Unique Customers': item.uniqueCustomerCount || 0,
            'Avg Rate': item.averageRate || 0,
            'Avg Discount': item.averageDiscount || 0
        }));
        exportToCsv(csvData, `MissedEntries_ByProduct_${new Date().toISOString().split('T')[0]}.csv`);
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    const hasData = allEntries.length > 0 || byCustomerData.length > 0 || byProductData.length > 0;

    return (
        <div className="container-fluid">
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2><i className="fas fa-exclamation-circle me-2"></i>Missed Entries</h2>
            </div>

            {/* Filter Card */}
            <Card className="shadow mb-4">
                <Card.Header className="py-3">
                    <h6 className="m-0 font-weight-bold text-primary">Filter Missed Entries</h6>
                </Card.Header>
                <Card.Body>
                    <Form id="filterForm" onSubmit={handleSubmit}>
                        <div className="row g-3 align-items-end">
                            <div className="col-md-3">
                                <Form.Label>Start Date</Form.Label>
                                <Form.Control
                                    type="date"
                                    id="startDate"
                                    value={formData.startDate}
                                    onChange={(e) => {
                                        setFormData({ ...formData, startDate: e.target.value });
                                    }}
                                    disabled={searching || isAutoLoading}
                                />
                            </div>
                            <div className="col-md-3">
                                <Form.Label>End Date</Form.Label>
                                <Form.Control
                                    type="date"
                                    id="endDate"
                                    value={formData.endDate}
                                    onChange={(e) => {
                                        setFormData({ ...formData, endDate: e.target.value });
                                    }}
                                    disabled={searching || isAutoLoading}
                                />
                            </div>
                            <div className="col-md-4">
                                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                                <SearchableSelect
                                    value={formData.projectId}
                                    onChange={(value) => setFormData({ ...formData, projectId: value ? parseInt(value) : null })}
                                    options={projects.map(p => ({ id: p.id, name: p.name }))}
                                    placeholder="Select Project"
                                    disabled={searching || isAutoLoading}
                                />
                            </div>
                            <div className="col-md-2">
                                <Button 
                                    type="submit" 
                                    variant="primary" 
                                    className="w-100"
                                    disabled={searching || isAutoLoading}
                                >
                                    {searching ? (
                                        <>
                                            <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                            Loading...
                                        </>
                                    ) : (
                                        <>
                                            <i className="fas fa-search me-1"></i> Search
                                        </>
                                    )}
                                </Button>
                            </div>
                        </div>
                    </Form>
                </Card.Body>
            </Card>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            {/* Summary Statistics Card */}
            {hasData && (
                <Card className="shadow mb-4">
                    <Card.Header className="py-3">
                        <h6 className="m-0 font-weight-bold text-primary">Summary Statistics</h6>
                    </Card.Header>
                    <Card.Body>
                        <div className="row" id="summaryStats">
                            <div className="col-md-3">
                                <div className="card bg-primary text-white mb-4">
                                    <div className="card-body">
                                        <h5 className="mb-0" id="totalEntries">{summaryStats.totalMissedEntries.toLocaleString()}</h5>
                                        <div>Total Missed Entries</div>
                                    </div>
                                </div>
                            </div>
                            <div className="col-md-3">
                                <div className="card bg-success text-white mb-4">
                                    <div className="card-body">
                                        <h5 className="mb-0" id="totalQuantity">{summaryStats.totalMissedQuantity.toLocaleString()}</h5>
                                        <div>Total Missed Quantity</div>
                                    </div>
                                </div>
                            </div>
                            <div className="col-md-3">
                                <div className="card bg-warning text-white mb-4">
                                    <div className="card-body">
                                        <h5 className="mb-0" id="uniqueCustomers">{summaryStats.uniqueCustomers.toLocaleString()}</h5>
                                        <div>Unique Customers</div>
                                    </div>
                                </div>
                            </div>
                            <div className="col-md-3">
                                <div className="card bg-info text-white mb-4">
                                    <div className="card-body">
                                        <h5 className="mb-0" id="uniqueProducts">{summaryStats.uniqueProducts.toLocaleString()}</h5>
                                        <div>Unique Products</div>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div className="row" id="additionalStats">
                            <div className="col-md-6">
                                <Card className="mb-4">
                                    <Card.Header>
                                        <i className="fas fa-user me-1"></i> Top Customer
                                    </Card.Header>
                                    <Card.Body>
                                        {summaryStats.topCustomer ? (
                                            <>
                                                <strong>{summaryStats.topCustomer}</strong>
                                                <div className="text-muted small">
                                                    Missed Quantity: {summaryStats.topCustomerMissedQuantity.toLocaleString()}
                                                </div>
                                            </>
                                        ) : (
                                            <span className="text-muted">No data available</span>
                                        )}
                                    </Card.Body>
                                </Card>
                            </div>
                            <div className="col-md-6">
                                <Card className="mb-4">
                                    <Card.Header>
                                        <i className="fas fa-box me-1"></i> Top Missed Product
                                    </Card.Header>
                                    <Card.Body>
                                        {summaryStats.topMissedProduct ? (
                                            <>
                                                <strong>{summaryStats.topMissedProduct}</strong>
                                                <div className="text-muted small">
                                                    Missed Quantity: {summaryStats.topMissedProductQuantity.toLocaleString()}
                                                </div>
                                            </>
                                        ) : (
                                            <span className="text-muted">No data available</span>
                                        )}
                                    </Card.Body>
                                </Card>
                            </div>
                        </div>
                    </Card.Body>
                </Card>
            )}

            {/* Tabs for different views */}
            {hasData && (
                <>
                    <Nav variant="tabs" className="mb-3" activeKey={activeTab} onSelect={handleTabSelect}>
                        <Nav.Item>
                            <Nav.Link 
                                eventKey="details" 
                                disabled={tabsDisabled || isAutoLoading}
                                style={tabsDisabled || isAutoLoading ? { cursor: 'not-allowed', opacity: 0.6 } : {}}
                            >
                                <i className="fas fa-list me-1"></i> Details
                                {isAutoLoading && activeTab === 'details' && (
                                    <span className="spinner-border spinner-border-sm ms-2" role="status"></span>
                                )}
                            </Nav.Link>
                        </Nav.Item>
                        <Nav.Item>
                            <Nav.Link 
                                eventKey="by-customer" 
                                disabled={tabsDisabled || isAutoLoading}
                                style={tabsDisabled || isAutoLoading ? { cursor: 'not-allowed', opacity: 0.6 } : {}}
                            >
                                <i className="fas fa-users me-1"></i> By Customer
                            </Nav.Link>
                        </Nav.Item>
                        <Nav.Item>
                            <Nav.Link 
                                eventKey="by-product" 
                                disabled={tabsDisabled || isAutoLoading}
                                style={tabsDisabled || isAutoLoading ? { cursor: 'not-allowed', opacity: 0.6 } : {}}
                            >
                                <i className="fas fa-boxes me-1"></i> By Product
                            </Nav.Link>
                        </Nav.Item>
                    </Nav>

                    <Tab.Container activeKey={activeTab}>
                        <Tab.Content>
                            {/* Details Tab */}
                            <Tab.Pane eventKey="details">
                                <Card className="shadow">
                                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                                        <h6 className="m-0 font-weight-bold text-primary">
                                            <i className="fas fa-table me-2"></i>Missed Entries Details
                                        </h6>
                                        <div className="d-flex align-items-center">
                                            {totalRecords > 0 && (
                                                <span className="badge bg-primary me-2">
                                                    {isAutoLoading ? (
                                                        <>
                                                            <span className="spinner-border spinner-border-sm me-1" role="status"></span>
                                                            {allEntries.length.toLocaleString()} of {totalRecords.toLocaleString()} entries
                                                        </>
                                                    ) : (
                                                        `${allEntries.length.toLocaleString()} entries`
                                                    )}
                                                </span>
                                            )}
                                            <Button 
                                                variant="outline-success" 
                                                size="sm" 
                                                onClick={handleExportDetails}
                                                disabled={allEntries.length === 0}
                                            >
                                                <i className="fas fa-file-csv me-1"></i> Export CSV
                                            </Button>
                                        </div>
                                    </Card.Header>
                                    <Card.Body>
                                        <div className="table-responsive">
                                            <Table bordered hover className="table-sm" id="missedEntriesTable">
                                                <thead className="table-primary">
                                                    <tr>
                                                        <th>Date</th>
                                                        <th>Project</th>
                                                        <th>Customer</th>
                                                        <th>Product</th>
                                                        <th className="text-end">Quantity</th>
                                                        <th className="text-end">Rate</th>
                                                        <th className="text-end">Discount</th>
                                                        <th className="text-end">Total Value</th>
                                                        <th>Description</th>
                                                    </tr>
                                                </thead>
                                                <tbody id="entriesTableBody">
                                                    {allEntries.length === 0 && !isAutoLoading ? (
                                                        <tr>
                                                            <td colSpan="9" className="text-center text-muted py-4">
                                                                No data available
                                                            </td>
                                                        </tr>
                                                    ) : (
                                                        allEntries.map((entry, index) => {
                                                            const missedDate = entry.missedDate || entry.MissedDate || '';
                                                            const projectName = entry.projectName || entry.ProjectName || '';
                                                            const customerName = entry.customerName || entry.customerAccountName || entry.CustomerName || 'N/A';
                                                            const productName = entry.productName || entry.productAccountName || entry.ProductName || 'N/A';
                                                            const quantity = entry.quantity || entry.Quantity || 0;
                                                            const rate = entry.rate || entry.Rate || 0;
                                                            const discount = entry.discount || entry.Discount || 0;
                                                            const totalValue = entry.totalValue || entry.TotalValue || (quantity * rate - discount);
                                                            const description = entry.description || entry.Description || '';
                                                            return (
                                                                <tr key={index}>
                                                                    <td>{formatDate(missedDate)}</td>
                                                                    <td>{projectName}</td>
                                                                    <td>{customerName}</td>
                                                                    <td>{productName}</td>
                                                                    <td className="text-end">{quantity.toLocaleString()}</td>
                                                                    <td className="text-end">{formatCurrency(rate)}</td>
                                                                    <td className="text-end">{formatCurrency(discount)}</td>
                                                                    <td className="text-end">{formatCurrency(totalValue)}</td>
                                                                    <td>{description}</td>
                                                                </tr>
                                                            );
                                                        })
                                                    )}
                                                </tbody>
                                            </Table>
                                        </div>
                                        {isAutoLoading && (
                                            <div className="text-center my-3">
                                                <div className="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                                                <span className="text-muted">Loading more entries... ({allEntries.length.toLocaleString()} of {totalRecords.toLocaleString()})</span>
                                            </div>
                                        )}
                                    </Card.Body>
                                </Card>
                            </Tab.Pane>

                            {/* By Customer Tab */}
                            <Tab.Pane eventKey="by-customer">
                                <Card className="shadow">
                                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                                        <h6 className="m-0 font-weight-bold text-primary">
                                            <i className="fas fa-users me-2"></i>Missed Entries by Customer
                                        </h6>
                                        <Button 
                                            variant="outline-success" 
                                            size="sm" 
                                            onClick={handleExportByCustomer}
                                            disabled={byCustomerData.length === 0}
                                        >
                                            <i className="fas fa-file-csv me-1"></i> Export CSV
                                        </Button>
                                    </Card.Header>
                                    <Card.Body>
                                        <div className="table-responsive">
                                            <Table bordered hover className="table-sm" id="byCustomerTable">
                                                <thead className="table-primary">
                                                    <tr>
                                                        <th>Customer</th>
                                                        <th className="text-end">Entry Count</th>
                                                        <th className="text-end">Total Quantity</th>
                                                        <th className="text-end">Total Value</th>
                                                        <th className="text-end">Unique Products</th>
                                                        <th className="text-end">Avg Qty/Entry</th>
                                                        <th className="text-center">Actions</th>
                                                    </tr>
                                                </thead>
                                                <tbody id="byCustomerTableBody">
                                                    {byCustomerData.length === 0 ? (
                                                        <tr>
                                                            <td colSpan="7" className="text-center text-muted py-4">
                                                                No customer data available
                                                            </td>
                                                        </tr>
                                                    ) : (
                                                        byCustomerData.map((item, index) => (
                                                            <tr key={index}>
                                                                <td>{item.customerName || 'Unknown'}</td>
                                                                <td className="text-end">{(item.entryCount || 0).toLocaleString()}</td>
                                                                <td className="text-end">{(item.totalQuantity || 0).toLocaleString()}</td>
                                                                <td className="text-end">{formatNumber(item.totalValue || 0)}</td>
                                                                <td className="text-end">{(item.uniqueProductCount || 0).toLocaleString()}</td>
                                                                <td className="text-end">{formatNumber(item.averageQuantityPerEntry || 0)}</td>
                                                                <td className="text-center">
                                                                    <Button
                                                                        variant="primary"
                                                                        size="sm"
                                                                        className="view-customer-details"
                                                                        onClick={() => showCustomerDetails(item.customerAccountId, item.customerName)}
                                                                    >
                                                                        <i className="fas fa-search-plus me-1"></i> Details
                                                                    </Button>
                                                                </td>
                                                            </tr>
                                                        ))
                                                    )}
                                                </tbody>
                                            </Table>
                                        </div>
                                    </Card.Body>
                                </Card>

                                {/* Customer Details Section */}
                                {selectedCustomer && (
                                    <Card className="shadow mt-4" id="customerDetailSection">
                                        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                                            <h6 className="m-0 font-weight-bold text-primary" id="selectedCustomerName">
                                                {selectedCustomer.name} - {selectedCustomer.loading ? 'Loading...' : 'Details'}
                                            </h6>
                                            <Button 
                                                variant="outline-secondary" 
                                                size="sm" 
                                                id="closeCustomerDetails"
                                                onClick={hideCustomerDetails}
                                            >
                                                <i className="fas fa-times"></i>
                                            </Button>
                                        </Card.Header>
                                        <Card.Body>
                                            {selectedCustomer.loading ? (
                                                <div className="text-center py-4">
                                                    <div className="spinner-border text-primary" role="status">
                                                        <span className="visually-hidden">Loading...</span>
                                                    </div>
                                                </div>
                                            ) : (
                                                <div className="table-responsive">
                                                    <Table bordered className="table-sm" id="customerDetailsTable">
                                                        <thead>
                                                            <tr>
                                                                <th>Date</th>
                                                                <th>Product</th>
                                                                <th className="text-end">Quantity</th>
                                                                <th className="text-end">Rate</th>
                                                                <th className="text-end">Discount</th>
                                                                <th className="text-end">Total Value</th>
                                                                <th>Description</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {customerDetails.length === 0 ? (
                                                                <tr>
                                                                    <td colSpan="7" className="text-center text-muted py-4">
                                                                        No details available
                                                                    </td>
                                                                </tr>
                                                            ) : (
                                                                customerDetails.map((entry, index) => {
                                                                    const missedDate = entry.missedDate || entry.MissedDate || '';
                                                                    const productName = entry.productName || entry.productAccountName || entry.ProductName || 'N/A';
                                                                    const quantity = entry.quantity || entry.Quantity || 0;
                                                                    const rate = entry.rate || entry.Rate || 0;
                                                                    const discount = entry.discount || entry.Discount || 0;
                                                                    const totalValue = entry.totalValue || entry.TotalValue || (quantity * rate - discount);
                                                                    const description = entry.description || entry.Description || '';
                                                                    return (
                                                                        <tr key={index}>
                                                                            <td>{formatDate(missedDate)}</td>
                                                                            <td>{productName}</td>
                                                                            <td className="text-end">{quantity.toLocaleString()}</td>
                                                                            <td className="text-end">{formatNumber(rate)}</td>
                                                                            <td className="text-end">{formatNumber(discount)}</td>
                                                                            <td className="text-end">{formatNumber(totalValue)}</td>
                                                                            <td>{description}</td>
                                                                        </tr>
                                                                    );
                                                                })
                                                            )}
                                                        </tbody>
                                                    </Table>
                                                </div>
                                            )}
                                        </Card.Body>
                                    </Card>
                                )}
                            </Tab.Pane>

                            {/* By Product Tab */}
                            <Tab.Pane eventKey="by-product">
                                <Card className="shadow">
                                    <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                                        <h6 className="m-0 font-weight-bold text-primary">
                                            <i className="fas fa-boxes me-2"></i>Missed Entries by Product
                                        </h6>
                                        <Button 
                                            variant="outline-success" 
                                            size="sm" 
                                            onClick={handleExportByProduct}
                                            disabled={byProductData.length === 0}
                                        >
                                            <i className="fas fa-file-csv me-1"></i> Export CSV
                                        </Button>
                                    </Card.Header>
                                    <Card.Body>
                                        <div className="table-responsive">
                                            <Table bordered hover className="table-sm" id="byProductTable">
                                                <thead className="table-primary">
                                                    <tr>
                                                        <th>Product</th>
                                                        <th className="text-end">Entry Count</th>
                                                        <th className="text-end">Total Quantity</th>
                                                        <th className="text-end">Total Value</th>
                                                        <th className="text-end">Unique Customers</th>
                                                        <th className="text-end">Avg Rate</th>
                                                        <th className="text-end">Avg Discount</th>
                                                    </tr>
                                                </thead>
                                                <tbody id="byProductTableBody">
                                                    {byProductData.length === 0 ? (
                                                        <tr>
                                                            <td colSpan="7" className="text-center text-muted py-4">
                                                                No product data available
                                                            </td>
                                                        </tr>
                                                    ) : (
                                                        byProductData.map((item, index) => (
                                                            <tr key={index}>
                                                                <td>{item.productName || 'Unknown'}</td>
                                                                <td className="text-end">{(item.entryCount || 0).toLocaleString()}</td>
                                                                <td className="text-end">{(item.totalQuantity || 0).toLocaleString()}</td>
                                                                <td className="text-end">{formatNumber(item.totalValue || 0)}</td>
                                                                <td className="text-end">{(item.uniqueCustomerCount || 0).toLocaleString()}</td>
                                                                <td className="text-end">{formatNumber(item.averageRate || 0)}</td>
                                                                <td className="text-end">{formatNumber(item.averageDiscount || 0)}</td>
                                                            </tr>
                                                        ))
                                                    )}
                                                </tbody>
                                            </Table>
                                        </div>
                                    </Card.Body>
                                </Card>
                            </Tab.Pane>
                        </Tab.Content>
                    </Tab.Container>
                </>
            )}
        </div>
    );
};

export default MissedEntriesReport;
