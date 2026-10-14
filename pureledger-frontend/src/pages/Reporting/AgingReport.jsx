import { todayString } from '../../utils/businessDate';
import React, { useState, useEffect, useRef } from 'react';
import { Card, Form, Button, Alert, Table, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import SortableHeader from '../../components/SortableHeader';
import { usePageTitle } from '../../hooks/usePageTitle';
import useSortableData from '../../hooks/useSortableData';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import AgingFifoModal from './AgingFifoModal';

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
    referenceDate: todayString(),
    projectId: null,
    categoryId: null
  });
  const [hasMoreData, setHasMoreData] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [totalRecords, setTotalRecords] = useState(0);
  const [appliedFilters, setAppliedFilters] = useState(null);
  const [reportComplete, setReportComplete] = useState(false);
  const [fifoTarget, setFifoTarget] = useState(null);
  const pageSize = 2500;
  const reportRequest = useRef({ runId: 0, controller: null });
  const categoryRequest = useRef(null);
  const modalSequence = useRef(0);

  useEffect(() => {
    loadInitData();
    const request = reportRequest.current;
    return () => {
      request.runId += 1;
      request.controller?.abort();
      categoryRequest.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (formData.projectId) {
      loadCategories(formData.projectId);
    } else {
      categoryRequest.current?.abort();
      setCategories([]);
      setFormData(prev => ({ ...prev, categoryId: null }));
    }
  }, [formData.projectId]);

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
    categoryRequest.current?.abort();
    const controller = new AbortController();
    categoryRequest.current = controller;
    try {
      const response = await apiClient.get(`/ReportingApi/categories-by-project/${projectId}`, { signal: controller.signal });
      if (controller.signal.aborted) return;
      if (response.data.success) {
        // Ensure categories have consistent id format (convert string ids to numbers)
        const categoriesData = (response.data.data || []).map(c => ({
          id: typeof c.id === 'string' ? parseInt(c.id, 10) : (c.id || c.categoryId),
          name: c.name || c.categoryName
        }));
        setCategories(categoriesData);
      }
    } catch (error) {
      if (controller.signal.aborted) return;
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  };

  const generateReport = async (filters = formData) => {
    if (!filters.referenceDate) {
      setError('Please select a reference date');
      return;
    }

    if (!filters.projectId) {
      setError('Please select a project');
      return;
    }

    if (!filters.categoryId) {
      setError('Please select a category');
      return;
    }

    const request = reportRequest.current;
    request.controller?.abort();
    request.runId += 1;
    const runId = request.runId;
    const controller = new AbortController();
    request.controller = controller;
    const snapshot = {
      ...filters,
      projectName: filters.projectName ?? projects.find(p => p.id === filters.projectId)?.name ?? '',
      categoryName: filters.categoryName ?? categories.find(c => c.id === filters.categoryId)?.name ?? ''
    };
    setAppliedFilters(snapshot);
    setFifoTarget(null);
    setLoading(true); setIsLoadingMore(false); setAllEntries([]);
    setTotalRecords(0); setHasMoreData(false); setReportComplete(false); setError('');

    try {
      for (let page = 0; ; page += 1) {
        const response = await apiClient.get('/ReportingApi/aging-report', {
          params: { referenceDate: snapshot.referenceDate, projectId: snapshot.projectId,
            categoryId: snapshot.categoryId, page, pageSize }, signal: controller.signal
        });
        if (controller.signal.aborted || request.runId !== runId) return;
        if (!response.data.success) throw new Error(response.data.message || 'Failed to generate report');
        const raw = response.data.data;
        const report = Array.isArray(raw) ? { entries: raw, totalRecords: raw.length, hasMoreData: false } : raw;
        if (!Array.isArray(report?.entries)) throw new Error('Invalid aging report response');
        if (report.hasMoreData && report.entries.length === 0) throw new Error('No entries returned for an unfinished report');
        setAllEntries(prev => page === 0 ? report.entries : [...prev, ...report.entries]);
        setTotalRecords(report.totalRecords ?? report.entries.length);
        setHasMoreData(!!report.hasMoreData);
        setLoading(false);
        if (!report.hasMoreData) {
          setReportComplete(true);
          if (page === 0 && report.entries.length === 0) setError('No data found for the selected criteria');
          break;
        }
        setIsLoadingMore(true);
      }
    } catch (error) {
      if (controller.signal.aborted || request.runId !== runId) return;
      setError(`Report is incomplete. ${error.response?.data?.message || error.message || 'Failed to generate report'}. Generate again to retry.`);
      setHasMoreData(false);
    } finally {
      if (!controller.signal.aborted && request.runId === runId) {
        setLoading(false); setIsLoadingMore(false);
      }
    }
  };

  const handleGenerate = () => {
    generateReport();
  };

  const openFifo = (row, scope) => {
    if (appliedFilters) setFifoTarget({ ...row, accountId: Number(row.accountId), scope, filters: appliedFilters, openId: ++modalSequence.current });
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

  // Apply sorting to filtered entries
  const { sortedItems: sortedEntries, requestSort, getSortDirection } = useSortableData(filteredEntries);

  const totalDueTillDate = sortedEntries.reduce((sum, row) => sum + (parseFloat(row.dueTillDate) || 0), 0);
  const totalTotalDue = sortedEntries.reduce((sum, row) => sum + (parseFloat(row.totalDue) || 0), 0);

  const handleExportCsv = async () => {
    if (!reportComplete) return;
    setExportingCsv(true);
    try {
      const csvData = sortedEntries.map(entry => ({
        Account: entry.accountName || entry.accountId || 'N/A',
        'Due Till Date': (entry.dueTillDate || 0).toFixed(2),
        'Total Due': (entry.totalDue || 0).toFixed(2)
      }));
      await exportToCsvAsync(csvData, `AgingReport_${appliedFilters.referenceDate || 'report'}.csv`);
    } finally {
      setExportingCsv(false);
    }
  };

  const handleExportPdf = async () => {
    if (!reportComplete) return;
    setExportingPdf(true);
    try {
      const selectedProject = { name: appliedFilters.projectName };
      const selectedCategory = { name: appliedFilters.categoryName };

      const rows = sortedEntries.map(entry => ({
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

      let subtitle = `Reference Date: ${appliedFilters.referenceDate}`;
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
        filename: `AgingReport_${appliedFilters.referenceDate}.pdf`
      });
    } finally {
      setExportingPdf(false);
    }
  };

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
          <Form onSubmit={(e) => { e.preventDefault(); handleGenerate(); }}>
            <div className="row mb-3">
              <div className="col-md-3 mb-3">
                <Form.Label htmlFor="aging-reference-date">Reference Date <span className="text-danger">*</span></Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                  <Form.Control
                    type="date"
                    id="aging-reference-date"
                    aria-label="Reference Date"
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
                    ariaLabel="Project"
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
                    ariaLabel="Category"
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
                    disabled={loading || isLoadingMore || hasMoreData || !formData.referenceDate || !formData.categoryId}
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
              {reportComplete && allEntries.length > 0 && (
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
            <p className="text-muted small">{appliedFilters?.projectName} · {appliedFilters?.categoryName} · Reference date: {appliedFilters?.referenceDate}. Select an account or balance to view its FIFO breakdown.</p>
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
            <div className="report-table-container">
              <div className="table-responsive">
                <Table bordered striped hover className="table-sm mb-0" id="agingReportTable">
                <thead className="table-light">
                  <tr>
                    <SortableHeader label="Account" sortKey="accountName" sortDirection={getSortDirection('accountName')} onSort={requestSort} />
                    <SortableHeader label="Due Till Date" sortKey="dueTillDate" sortDirection={getSortDirection('dueTillDate')} onSort={requestSort} className="text-end" />
                    <SortableHeader label="Total Due" sortKey="totalDue" sortDirection={getSortDirection('totalDue')} onSort={requestSort} className="text-end" />
                  </tr>
                </thead>
                <tbody>
                  {sortedEntries.length === 0 && allEntries.length > 0 ? (
                    <tr>
                      <td colSpan="3" className="text-center text-muted py-4">
                        {searchTerm ? 'No results found for your search' : 'No data available'}
                      </td>
                    </tr>
                  ) : sortedEntries.length === 0 && allEntries.length === 0 ? (
                    <tr>
                      <td colSpan="3" className="text-center text-muted py-4">
                        No data available
                      </td>
                    </tr>
                  ) : (
                    sortedEntries.map(row => (
                      <tr key={row.accountId}>
                        <td><Button variant="link" size="sm" className="p-0 text-start" aria-label={`View full ledger FIFO for ${row.accountName}`} onClick={() => openFifo(row, 'full')}>{row.accountName || row.accountId || 'N/A'}</Button></td>
                        <td className={`text-end ${(row.dueTillDate || 0) < 0 ? 'text-danger' : ''}`}>
                          <Button variant="link" size="sm" className="p-0 text-reset" aria-label={`View due till date breakdown for ${row.accountName}`} onClick={() => openFifo(row, 'due')}>{formatCurrency(row.dueTillDate)}</Button>
                        </td>
                        <td className={`text-end ${(row.totalDue || 0) < 0 ? 'text-danger' : ''}`}>
                          <Button variant="link" size="sm" className="p-0 text-reset" aria-label={`View total due FIFO for ${row.accountName}`} onClick={() => openFifo(row, 'full')}>{formatCurrency(row.totalDue)}</Button>
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
            </div>
            {isLoadingMore && hasMoreData && (
              <div className="text-center mt-3">
                <div className="spinner-border spinner-border-sm text-primary" role="status">
                  <span className="visually-hidden">Loading more records...</span>
                </div>
              </div>
            )}
          </Card.Body>
        </Card>
      )}
      <AgingFifoModal target={fifoTarget} onHide={() => setFifoTarget(null)} onRefreshReport={() => generateReport(appliedFilters)} />
    </div>
  );
};

export default AgingReport;
