import React, { useState, useEffect, useRef } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';

const AgingReport = () => {
  usePageTitle('Aging Report');
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [allEntries, setAllEntries] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [exportingCsv, setExportingCsv] = useState(false);
  const [exportingPdf, setExportingPdf] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [formData, setFormData] = useState({
    referenceDate: new Date().toISOString().split('T')[0],
    projectId: null,
    categoryId: null
  });
  const [currentPage, setCurrentPage] = useState(0);
  const [hasMoreData, setHasMoreData] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [totalRecords, setTotalRecords] = useState(0);
  const [isAutoLoading, setIsAutoLoading] = useState(false);
  const pageSize = 1000;
  const observerRef = useRef(null);
  const loadingRef = useRef(null);

  useEffect(() => {
    loadInitData();
  }, []);

  useEffect(() => {
    if (formData.projectId) {
      loadCategories(formData.projectId);
    } else {
      setCategories([]);
      setFormData(prev => ({ ...prev, categoryId: null }));
    }
  }, [formData.projectId]);

  // Auto-load more data when scrolling to bottom
  useEffect(() => {
    if (observerRef.current) {
      observerRef.current.disconnect();
    }

    if (hasMoreData && !isLoadingMore && !isAutoLoading && allEntries.length > 0) {
      observerRef.current = new IntersectionObserver(
        (entries) => {
          if (entries[0].isIntersecting && hasMoreData && !isLoadingMore && !isAutoLoading) {
            setIsAutoLoading(true);
            const nextPage = currentPage + 1;
            generateReport(nextPage, true);
          }
        },
        { threshold: 0.1 }
      );

      if (loadingRef.current) {
        observerRef.current.observe(loadingRef.current);
      }
    }

    return () => {
      if (observerRef.current) {
        observerRef.current.disconnect();
      }
    };
  }, [hasMoreData, isLoadingMore, isAutoLoading, allEntries.length, currentPage]);

  const loadInitData = async () => {
    try {
      const response = await apiClient.get('/ReportingApi/report-data');
      if (response.data.success) {
        // Ensure projects have consistent id format (convert string ids to numbers)
        const projectsData = (response.data.projects || []).map(p => ({
          id: typeof p.id === 'string' ? parseInt(p.id, 10) : p.id,
          name: p.name
        }));
        setProjects(projectsData);
      }
    } catch (error) {
      console.error('Error loading initial data:', error);
    }
  };

  const loadCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/ReportingApi/categories-by-project/${projectId}`);
      if (response.data.success) {
        // Ensure categories have consistent id format (convert string ids to numbers)
        const categoriesData = (response.data.data || []).map(c => ({
          id: typeof c.id === 'string' ? parseInt(c.id, 10) : (c.id || c.categoryId),
          name: c.name || c.categoryName
        }));
        setCategories(categoriesData);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  };

  const generateReport = async (page = 0, append = false) => {
    if (!formData.referenceDate) {
      setError('Please select a reference date');
      return;
    }

    if (!formData.categoryId) {
      setError('Please select a category');
      return;
    }

    if (page === 0) {
      setLoading(true);
      setAllEntries([]);
      setCurrentPage(0);
      setError('');
    } else {
      setIsLoadingMore(true);
    }

    try {
      const response = await apiClient.get('/ReportingApi/aging-report', {
        params: {
          referenceDate: formData.referenceDate,
          projectId: formData.projectId || null,
          categoryId: formData.categoryId,
          page: page,
          pageSize: pageSize
        }
      });

      if (response.data.success) {
        const reportData = response.data.data;
        if (reportData && reportData.entries && Array.isArray(reportData.entries)) {
          if (append) {
            setAllEntries(prev => [...prev, ...reportData.entries]);
          } else {
            setAllEntries(reportData.entries);
          }
          setHasMoreData(reportData.hasMoreData || false);
          setTotalRecords(reportData.totalRecords || reportData.entries.length);
          setCurrentPage(page + 1);

          // Auto-load next page if there's more data (not using scroll-based loading)
          if (reportData.hasMoreData && !isLoadingMore && !isAutoLoading) {
            setIsAutoLoading(true);
            setTimeout(() => {
              generateReport(page + 1, true);
            }, 100);
          } else {
            setIsAutoLoading(false);
          }
        } else if (Array.isArray(reportData)) {
          if (append) {
            setAllEntries(prev => [...prev, ...reportData]);
          } else {
            setAllEntries(reportData);
          }
          setHasMoreData(false);
          setTotalRecords(reportData.length);
          setCurrentPage(page + 1);
        } else {
          setAllEntries([]);
          setHasMoreData(false);
          setTotalRecords(0);
          if (!reportData || !reportData.entries || reportData.entries.length === 0) {
            setError('No data found for the selected criteria');
          }
        }
      } else {
        setError(response.data.message || 'Failed to generate report');
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to generate report');
    } finally {
      setLoading(false);
      setIsLoadingMore(false);
      // Only reset auto-loading if no more data
      if (!hasMoreData) {
        setIsAutoLoading(false);
      }
    }
  };

  const handleGenerate = () => {
    generateReport(0, false);
  };

  const formatCurrency = (value) => {
    return new Intl.NumberFormat('en-PK', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value || 0);
  };

  const filteredEntries = allEntries.filter(entry => {
    if (!searchTerm) return true;
    const search = searchTerm.toLowerCase();
    return (
      (entry.accountName || '').toLowerCase().includes(search) ||
      entry.accountId?.toString().includes(search) ||
      (entry.dueTillDate?.toString() || '').includes(search) ||
      (entry.totalDue?.toString() || '').includes(search)
    );
  });

  const totalDueTillDate = filteredEntries.reduce((sum, row) => sum + (parseFloat(row.dueTillDate) || 0), 0);
  const totalTotalDue = filteredEntries.reduce((sum, row) => sum + (parseFloat(row.totalDue) || 0), 0);

  const handleExportCsv = async () => {
    setExportingCsv(true);
    try {
      const csvData = filteredEntries.map(entry => ({
        Account: entry.accountName || entry.accountId || 'N/A',
        'Due Till Date': (entry.dueTillDate || 0).toFixed(2),
        'Total Due': (entry.totalDue || 0).toFixed(2)
      }));
      await exportToCsvAsync(csvData, `AgingReport_${formData.referenceDate || 'report'}.csv`);
    } finally {
      setExportingCsv(false);
    }
  };

  const handleExportPdf = async () => {
    setExportingPdf(true);
    try {
      const selectedProject = projects.find(p => p.id === formData.projectId);
      const selectedCategory = categories.find(c => c.id === formData.categoryId);

      const rows = filteredEntries.map(entry => ({
        account: entry.accountName || entry.accountId || 'N/A',
        dueTillDate: formatCurrency(entry.dueTillDate),
        totalDue: formatCurrency(entry.totalDue)
      }));

      rows.push({
        account: 'TOTAL',
        dueTillDate: formatCurrency(totalDueTillDate),
        totalDue: formatCurrency(totalTotalDue)
      });

      const columns = [
        { header: 'Account', dataKey: 'account' },
        { header: 'Due Till Date', dataKey: 'dueTillDate' },
        { header: 'Total Due', dataKey: 'totalDue' }
      ];

      let subtitle = `Reference Date: ${formData.referenceDate}`;
      if (selectedCategory) {
        subtitle += `\nCategory: ${selectedCategory.name}`;
      }

      await generatePdfAsync({
        orientation: 'portrait',
        format: 'a4',
        title: 'Aging Report',
        projectName: selectedProject?.name || null,
        subtitle,
        generatedInfo: `Generated: ${new Date().toLocaleString()} | Total Records: ${totalRecords}`,
        columns,
        rows,
        rowsPerPage: 20,
        startY: selectedCategory ? 52 : 46,
        fontSize: 9,
        cellPadding: 2,
        filename: `AgingReport_${formData.referenceDate || new Date().toISOString().split('T')[0]}.pdf`
      });
    } finally {
      setExportingPdf(false);
    }
  };

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2><i className="fas fa-hourglass-half me-2"></i>Aging Report</h2>
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
          <Form onSubmit={(e) => { e.preventDefault(); handleGenerate(); }}>
            <div className="row mb-3">
              <div className="col-md-3 mb-3">
                <Form.Label>Reference Date <span className="text-danger">*</span></Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                  <Form.Control
                    type="date"
                    value={formData.referenceDate}
                    onChange={(e) => setFormData({ ...formData, referenceDate: e.target.value })}
                    required
                    className="no-arrow"
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>Project</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                  <SearchableSelect
                    value={formData.projectId}
                    onChange={(value) => {
                      setFormData({ ...formData, projectId: value ? parseInt(value) : null, categoryId: null });
                      setCategories([]);
                    }}
                    options={projects.map(p => ({ id: p.id, name: p.name }))}
                    placeholder="Search project..."
                    disabled={loading || isLoadingMore}
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>Category <span className="text-danger">*</span></Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                  <SearchableSelect
                    value={formData.categoryId}
                    onChange={(value) => setFormData({ ...formData, categoryId: value ? parseInt(value) : null })}
                    options={categories.map(c => ({ id: c.id, name: c.name }))}
                    placeholder="Select category..."
                    disabled={!formData.projectId || loading || isLoadingMore}
                    required
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>&nbsp;</Form.Label>
                <div className="d-grid">
                  <Button
                    variant="primary"
                    type="submit"
                    disabled={loading || isLoadingMore || !formData.referenceDate || !formData.categoryId}
                    className="w-100"
                    title={!formData.referenceDate ? 'Please select a reference date' : !formData.categoryId ? 'Please select a category' : ''}
                  >
                    {loading ? (
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
              <i className="fas fa-table me-2"></i>Aging Report Results
            </h6>
            <div>
              {totalRecords > 0 && (
                <span className="badge bg-primary me-2">
                  {allEntries.length} of {totalRecords} accounts
                </span>
              )}
              {!hasMoreData && allEntries.length > 0 && (
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
                    disabled={loading || isLoadingMore || hasMoreData}
                  />
                </InputGroup>
              </div>
            </div>
            <div className="table-responsive" style={{ width: '100%', overflowX: 'auto' }}>
              <Table bordered striped hover className="table-sm mb-0 w-100" style={{ width: '100%', tableLayout: 'auto' }}>
                <thead className="table-light">
                  <tr>
                    <th>Account</th>
                    <th className="text-end">Due Till Date</th>
                    <th className="text-end">Total Due</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredEntries.length === 0 && allEntries.length > 0 ? (
                    <tr>
                      <td colSpan="3" className="text-center text-muted py-4">
                        {searchTerm ? 'No results found for your search' : 'No data available'}
                      </td>
                    </tr>
                  ) : filteredEntries.length === 0 && allEntries.length === 0 ? (
                    <tr>
                      <td colSpan="3" className="text-center text-muted py-4">
                        No data available
                      </td>
                    </tr>
                  ) : (
                    filteredEntries.map((row, index) => (
                      <tr key={index}>
                        <td>{row.accountName || row.accountId || 'N/A'}</td>
                        <td className={`text-end ${(row.dueTillDate || 0) < 0 ? 'text-danger' : ''}`}>
                          {formatCurrency(row.dueTillDate)}
                        </td>
                        <td className={`text-end ${(row.totalDue || 0) < 0 ? 'text-danger' : ''}`}>
                          {formatCurrency(row.totalDue)}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
                <tfoot className="table-secondary">
                  <tr className="fw-bold">
                    <td>TOTAL</td>
                    <td className="text-end">{formatCurrency(totalDueTillDate)}</td>
                    <td className="text-end">{formatCurrency(totalTotalDue)}</td>
                  </tr>
                </tfoot>
              </Table>
            </div>
            {(isLoadingMore || isAutoLoading) && hasMoreData && (
              <div className="text-center mt-3" ref={loadingRef}>
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

export default AgingReport;
