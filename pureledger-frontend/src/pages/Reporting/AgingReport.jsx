import React, { useState, useEffect } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import LoadingSpinner from '../../components/LoadingSpinner';

const AgingReport = () => {
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [reportData, setReportData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [formData, setFormData] = useState({
    referenceDate: new Date().toISOString().split('T')[0],
    projectId: null,
    categoryId: null,
    page: 0,
    pageSize: 100
  });
  const [allEntries, setAllEntries] = useState([]);
  const [hasMoreData, setHasMoreData] = useState(false);
  const [totalRecords, setTotalRecords] = useState(0);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  useEffect(() => {
    loadReportData();
  }, []);

  useEffect(() => {
    if (formData.projectId) {
      loadCategories(formData.projectId);
    } else {
      setCategories([]);
      setFormData(prev => ({ ...prev, categoryId: null }));
    }
  }, [formData.projectId]);

  const loadReportData = async () => {
    try {
      const response = await apiClient.get('/ReportingApi/report-data');
      if (response.data.success) {
        setProjects(response.data.projects);
        setCategories(response.data.categories);
      }
    } catch (error) {
      setError('Failed to load report data');
    }
  };

  const loadCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/ReportingApi/categories-by-project/${projectId}`);
      if (response.data.success) {
        setCategories(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  };

  const handleGenerate = async (page = 0, append = false) => {
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
          pageSize: formData.pageSize
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
        } else if (Array.isArray(reportData)) {
          if (append) {
            setAllEntries(prev => [...prev, ...reportData]);
          } else {
            setAllEntries(reportData);
          }
          setHasMoreData(false);
          setTotalRecords(reportData.length);
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
    }
  };

  const handleLoadMore = () => {
    if (!isLoadingMore && hasMoreData) {
      const nextPage = Math.floor(allEntries.length / formData.pageSize);
      handleGenerate(nextPage, true);
    }
  };

  const formatCurrency = (value) => {
    return new Intl.NumberFormat('en-PK', {
      style: 'currency',
      currency: 'PKR',
      minimumFractionDigits: 2
    }).format(value);
  };

  return (
    <div>
      <h2 className="mb-4">
        <i className="fas fa-calendar-alt me-2"></i>Aging Report
      </h2>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          {error}
        </Alert>
      )}

      <Card className="mb-4">
        <Card.Body>
          <h5 className="mb-3">Report Parameters</h5>
          <Form>
            <div className="row">
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Reference Date</Form.Label>
                  <Form.Control
                    type="date"
                    value={formData.referenceDate}
                    onChange={(e) => setFormData({ ...formData, referenceDate: e.target.value })}
                    required
                  />
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
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
                      disabled={loading}
                    />
                  </InputGroup>
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Category <span className="text-danger">*</span></Form.Label>
                  <InputGroup>
                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                    <SearchableSelect
                      value={formData.categoryId}
                      onChange={(value) => setFormData({ ...formData, categoryId: value ? parseInt(value) : null })}
                      options={categories.map(c => ({ id: c.id, name: c.name }))}
                      placeholder="Select category..."
                      disabled={!formData.projectId || loading}
                      required
                    />
                  </InputGroup>
                </Form.Group>
              </div>
              <div className="col-md-3 d-flex align-items-end">
                <Button
                  variant="primary"
                  onClick={() => handleGenerate(0, false)}
                  disabled={loading || isLoadingMore}
                  className="w-100"
                >
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                      Generating...
                    </>
                  ) : (
                    <>
                      <i className="fas fa-chart-bar me-2"></i>Generate Report
                    </>
                  )}
                </Button>
              </div>
            </div>
          </Form>
        </Card.Body>
      </Card>

      {allEntries.length > 0 && (
        <Card>
          <Card.Body>
            <div className="d-flex justify-content-between align-items-center mb-3">
              <h5>Report Results {totalRecords > 0 && <span className="badge bg-primary ms-2">{allEntries.length} of {totalRecords}</span>}</h5>
              <Button variant="success" onClick={() => window.print()}>
                <i className="fas fa-print me-2"></i>Print
              </Button>
            </div>
            <div className="table-responsive" style={{ overflow: 'visible' }}>
              <Table responsive striped bordered hover style={{ width: '100%', marginBottom: 0 }}>
                <thead>
                  <tr>
                    <th>Account ID</th>
                    <th>Account Name</th>
                    <th className="text-end">Total Debit</th>
                    <th className="text-end">Total Credit</th>
                    <th className="text-end">Net Balance</th>
                  </tr>
                </thead>
                <tbody>
                  {allEntries.map((row, index) => (
                    <tr key={index}>
                      <td>{row.accountId}</td>
                      <td>{row.accountName}</td>
                      <td className="text-end">{formatCurrency(row.totalDebit || 0)}</td>
                      <td className="text-end">{formatCurrency(row.totalCredit || 0)}</td>
                      <td className={`text-end ${(row.netBalance || 0) < 0 ? 'text-danger' : ''}`}>
                        {formatCurrency(row.netBalance || 0)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="table-secondary">
                  <tr>
                    <td colSpan="2" className="text-end fw-bold">TOTAL</td>
                    <td className="text-end fw-bold">{formatCurrency(allEntries.reduce((sum, row) => sum + (row.totalDebit || 0), 0))}</td>
                    <td className="text-end fw-bold">{formatCurrency(allEntries.reduce((sum, row) => sum + (row.totalCredit || 0), 0))}</td>
                    <td className="text-end fw-bold">{formatCurrency(allEntries.reduce((sum, row) => sum + (row.netBalance || 0), 0))}</td>
                  </tr>
                </tfoot>
              </Table>
            </div>
            {isLoadingMore && (
              <div className="text-center my-3">
                <LoadingSpinner />
              </div>
            )}
            {hasMoreData && !isLoadingMore && (
              <div className="text-center my-3">
                <Button variant="outline-primary" onClick={handleLoadMore}>
                  <i className="fas fa-arrow-down me-1"></i> Load More ({totalRecords - allEntries.length} remaining)
                </Button>
              </div>
            )}
            {!hasMoreData && allEntries.length > 0 && (
              <div className="text-center my-3 text-muted">
                End of report. Total records: {totalRecords}
              </div>
            )}
          </Card.Body>
        </Card>
      )}
    </div>
  );
};

export default AgingReport;

