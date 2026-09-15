import { useEffect, useState } from 'react';
import { Badge, Button, Card, Form, Modal, Table } from 'react-bootstrap';
import { useLocation } from 'react-router-dom';
import apiClient from '../../services/api';
import { usePageTitle } from '../../hooks/usePageTitle';
import ErrorAlert from '../../components/ErrorAlert';

const defaultFilters = {
  fromUtc: '',
  toUtc: '',
  level: '',
  statusCode: '',
  username: '',
  path: '',
  errorId: '',
  correlationId: '',
  source: '',
};

const formatDate = (value) => {
  if (!value) return '-';
  return new Date(value).toLocaleString();
};

const levelVariant = (level) => {
  if (level === 'Error') return 'danger';
  if (level === 'Warning') return 'warning';
  return 'secondary';
};

const tryFormatJson = (value) => {
  if (!value) return '-';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
};

const ErrorLogs = () => {
  usePageTitle('Error Logs');
  const location = useLocation();
  const initialErrorId = new URLSearchParams(location.search).get('errorId') || '';
  const [filters, setFilters] = useState({ ...defaultFilters, errorId: initialErrorId });
  const [logs, setLogs] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [selectedLog, setSelectedLog] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const pageSize = 50;

  useEffect(() => {
    loadLogs(1, { ...defaultFilters, errorId: initialErrorId });
  }, []);

  const buildParams = (targetPage, filterValues = filters) => {
    const params = {
      page: targetPage,
      pageSize,
    };

    Object.entries(filterValues).forEach(([key, value]) => {
      if (!value) return;
      params[key] = key.endsWith('Utc') ? new Date(value).toISOString() : value;
    });

    return params;
  };

  const loadLogs = async (targetPage = page, filterValues = filters) => {
    setLoading(true);
    setError('');
    try {
      const response = await apiClient.get('/AdminApi/logs/errors', {
        params: buildParams(targetPage, filterValues),
      });

      if (response.data.success) {
        setLogs(response.data.data || []);
        setTotalCount(response.data.totalCount || 0);
        setPage(targetPage);
      }
    } catch (err) {
      console.error('Error loading application logs:', err);
      setError(err.message || 'Failed to load error logs.');
    } finally {
      setLoading(false);
    }
  };

  const openDetail = async (errorId) => {
    setDetailLoading(true);
    setError('');
    try {
      const response = await apiClient.get(`/AdminApi/logs/errors/${encodeURIComponent(errorId)}`);
      if (response.data.success) {
        setSelectedLog(response.data.data);
      }
    } catch (err) {
      console.error('Error loading error detail:', err);
      setError(err.message || 'Failed to load error detail.');
    } finally {
      setDetailLoading(false);
    }
  };

  const resetFilters = () => {
    setFilters(defaultFilters);
    loadLogs(1, defaultFilters);
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-end align-items-center mb-4">
        <Button variant="outline-primary" onClick={() => loadLogs(page)} disabled={loading}>
          <i className="fas fa-sync-alt me-2"></i>Refresh
        </Button>
      </div>

      <ErrorAlert error={error} dismissible onDismiss={() => setError('')} />

      <Card className="shadow-sm mb-4">
        <Card.Header>
          <strong>Search Filters</strong>
        </Card.Header>
        <Card.Body>
          <div className="row g-3">
            <div className="col-md-3">
              <Form.Label>From</Form.Label>
              <Form.Control type="datetime-local" value={filters.fromUtc} onChange={(e) => setFilters({ ...filters, fromUtc: e.target.value })} />
            </div>
            <div className="col-md-3">
              <Form.Label>To</Form.Label>
              <Form.Control type="datetime-local" value={filters.toUtc} onChange={(e) => setFilters({ ...filters, toUtc: e.target.value })} />
            </div>
            <div className="col-md-2">
              <Form.Label>Level</Form.Label>
              <Form.Select value={filters.level} onChange={(e) => setFilters({ ...filters, level: e.target.value })}>
                <option value="">Any</option>
                <option value="Error">Error</option>
                <option value="Warning">Warning</option>
                <option value="Information">Information</option>
              </Form.Select>
            </div>
            <div className="col-md-2">
              <Form.Label>Source</Form.Label>
              <Form.Select value={filters.source} onChange={(e) => setFilters({ ...filters, source: e.target.value })}>
                <option value="">Any</option>
                <option value="Backend">Backend</option>
                <option value="Frontend">Frontend</option>
              </Form.Select>
            </div>
            <div className="col-md-2">
              <Form.Label>Status</Form.Label>
              <Form.Control value={filters.statusCode} onChange={(e) => setFilters({ ...filters, statusCode: e.target.value })} placeholder="500" />
            </div>
            <div className="col-md-3">
              <Form.Label>Error ID</Form.Label>
              <Form.Control value={filters.errorId} onChange={(e) => setFilters({ ...filters, errorId: e.target.value })} placeholder="ERR-..." />
            </div>
            <div className="col-md-3">
              <Form.Label>Correlation ID</Form.Label>
              <Form.Control value={filters.correlationId} onChange={(e) => setFilters({ ...filters, correlationId: e.target.value })} placeholder="corr_..." />
            </div>
            <div className="col-md-3">
              <Form.Label>Username</Form.Label>
              <Form.Control value={filters.username} onChange={(e) => setFilters({ ...filters, username: e.target.value })} />
            </div>
            <div className="col-md-3">
              <Form.Label>Path</Form.Label>
              <Form.Control value={filters.path} onChange={(e) => setFilters({ ...filters, path: e.target.value })} placeholder="/api/..." />
            </div>
          </div>
          <div className="mt-3">
            <Button className="me-2" onClick={() => loadLogs(1)} disabled={loading}>
              Search
            </Button>
            <Button variant="outline-secondary" onClick={resetFilters} disabled={loading}>
              Reset
            </Button>
          </div>
        </Card.Body>
      </Card>

      <Card>
        <Card.Body>
          <div className="d-flex justify-content-between align-items-center mb-2">
            <span className="text-muted">{totalCount} matching log(s)</span>
            <span className="text-muted">Page {page} of {totalPages}</span>
          </div>
          <Table responsive striped bordered hover>
            <thead className="table-light">
              <tr>
                <th>Time</th>
                <th>Level</th>
                <th>Source</th>
                <th>Error ID</th>
                <th>Status</th>
                <th>User</th>
                <th>Path</th>
                <th>Message</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="9" className="text-center py-4">
                    <span className="spinner-border spinner-border-sm me-2"></span>Loading logs...
                  </td>
                </tr>
              ) : logs.length === 0 ? (
                <tr>
                  <td colSpan="9" className="text-center text-muted py-4">No logs found</td>
                </tr>
              ) : logs.map((log) => (
                <tr key={log.errorId}>
                  <td style={{ whiteSpace: 'nowrap' }}>{formatDate(log.createdAtUtc)}</td>
                  <td><Badge bg={levelVariant(log.level)}>{log.level}</Badge></td>
                  <td>{log.source}</td>
                  <td><code>{log.errorId}</code></td>
                  <td>{log.statusCode || '-'}</td>
                  <td>{log.username || log.userId || '-'}</td>
                  <td className="text-truncate" style={{ maxWidth: 220 }}>{log.path || '-'}</td>
                  <td className="text-truncate" style={{ maxWidth: 320 }}>{log.message}</td>
                  <td>
                    <Button size="sm" variant="outline-primary" onClick={() => openDetail(log.errorId)} disabled={detailLoading}>
                      View
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
          <div className="d-flex justify-content-end gap-2">
            <Button variant="outline-secondary" disabled={loading || page <= 1} onClick={() => loadLogs(page - 1)}>
              Previous
            </Button>
            <Button variant="outline-secondary" disabled={loading || page >= totalPages} onClick={() => loadLogs(page + 1)}>
              Next
            </Button>
          </div>
        </Card.Body>
      </Card>

      <Modal show={!!selectedLog} onHide={() => setSelectedLog(null)} size="xl">
        <Modal.Header closeButton>
          <Modal.Title>Error Detail</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {selectedLog && (
            <>
              <div className="row g-3 mb-3">
                <div className="col-md-4"><strong>Error ID:</strong> <code>{selectedLog.errorId}</code></div>
                <div className="col-md-4"><strong>Correlation:</strong> <code>{selectedLog.correlationId}</code></div>
                <div className="col-md-4"><strong>Time:</strong> {formatDate(selectedLog.createdAtUtc)}</div>
                <div className="col-md-4"><strong>Level:</strong> <Badge bg={levelVariant(selectedLog.level)}>{selectedLog.level}</Badge></div>
                <div className="col-md-4"><strong>Source:</strong> {selectedLog.source}</div>
                <div className="col-md-4"><strong>Status:</strong> {selectedLog.statusCode || '-'}</div>
                <div className="col-md-4"><strong>User:</strong> {selectedLog.username || selectedLog.userId || '-'}</div>
                <div className="col-md-4"><strong>Method:</strong> {selectedLog.method || '-'}</div>
                <div className="col-md-4"><strong>IP:</strong> {selectedLog.ipAddress || '-'}</div>
                <div className="col-12"><strong>Path:</strong> <code>{selectedLog.path || '-'}</code></div>
                <div className="col-12"><strong>User Agent:</strong> {selectedLog.userAgent || '-'}</div>
              </div>

              <h6>Message</h6>
              <pre className="bg-light p-3 rounded">{selectedLog.message}</pre>

              <h6>Stack Trace</h6>
              <pre className="bg-light p-3 rounded" style={{ maxHeight: 320, overflow: 'auto' }}>{selectedLog.stackTrace || '-'}</pre>

              <h6>Request Body Summary</h6>
              <pre className="bg-light p-3 rounded" style={{ maxHeight: 240, overflow: 'auto' }}>{tryFormatJson(selectedLog.requestBodySummary)}</pre>

              <h6>Extra Properties</h6>
              <pre className="bg-light p-3 rounded" style={{ maxHeight: 240, overflow: 'auto' }}>{tryFormatJson(selectedLog.extraPropertiesJson)}</pre>
            </>
          )}
        </Modal.Body>
      </Modal>
    </div>
  );
};

export default ErrorLogs;
