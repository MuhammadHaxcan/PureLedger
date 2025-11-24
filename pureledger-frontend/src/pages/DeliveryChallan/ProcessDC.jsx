import { useState, useEffect } from 'react';
import { Card, Form, Button, Alert, Table, Badge } from 'react-bootstrap';
import apiClient from '../../services/api';

const ProcessDC = () => {
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [locations, setLocations] = useState([]);
  const [vouchers, setVouchers] = useState([]);
  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [voucherEntries, setVoucherEntries] = useState([]);
  const [filter, setFilter] = useState({
    projectId: '',
    categoryId: '',
    accountId: '',
    sinceDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
    toDate: new Date().toISOString().split('T')[0]
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    loadInitialData();
  }, []);

  useEffect(() => {
    if (filter.projectId) {
      loadCategories(parseInt(filter.projectId));
    }
  }, [filter.projectId]);

  useEffect(() => {
    if (filter.categoryId) {
      loadAccounts(parseInt(filter.categoryId));
    }
  }, [filter.categoryId]);

  const loadInitialData = async () => {
    try {
      const [projectsRes, locationsRes] = await Promise.all([
        apiClient.get('/DeliveryChallanApi/process-dc-data'),
        apiClient.get('/DeliveryChallanApi/locations')
      ]);

      if (projectsRes.data.success) {
        setProjects(projectsRes.data.projects);
      }
      if (locationsRes.data.success) {
        setLocations(locationsRes.data.data);
      }
    } catch (error) {
      setError('Failed to load initial data');
    }
  };

  const loadCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/categories-by-project/${projectId}`);
      if (response.data.success) {
        setCategories(response.data.data);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
    }
  };

  const loadAccounts = async (categoryId) => {
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/accounts-by-category/${categoryId}`);
      if (response.data.success) {
        setAccounts(response.data.data);
      }
    } catch (error) {
      console.error('Error loading accounts:', error);
    }
  };

  const handleSearch = async () => {
    if (!filter.projectId) {
      setError('Please select a project');
      return;
    }

    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get('/DeliveryChallanApi/filtered-entries', {
        params: {
          projectId: filter.projectId,
          categoryId: filter.categoryId || null,
          accountId: filter.accountId || null,
          sinceDate: filter.sinceDate,
          toDate: filter.toDate
        }
      });

      if (response.data.success) {
        setVouchers(response.data.data.vouchers || []);
      }
    } catch (error) {
      setError('Failed to load vouchers');
    } finally {
      setLoading(false);
    }
  };

  const handleSelectVoucher = async (voucher) => {
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/voucher-entries/${voucher.voucherId}`);
      if (response.data.success) {
        setSelectedVoucher(voucher);
        setVoucherEntries(response.data.data);
      }
    } catch (error) {
      setError('Failed to load voucher entries');
    }
  };

  const handleProcessEntry = async (entry, locationId, dcDate) => {
    try {
      const response = await apiClient.post('/DeliveryChallanApi/single-entry', {
        journalEntryId: entry.entryId,
        locationId: locationId,
        dcDate: dcDate,
        otherReference: '',
        remarks: '',
        includeForCharges: false,
        forAmount: 0
      });

      if (response.data.success) {
        setSuccess('Entry processed successfully');
        handleSelectVoucher(selectedVoucher); // Reload entries
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to process entry');
    }
  };

  return (
    <div>
      <h2 className="mb-4">
        <i className="fas fa-truck me-2"></i>Delivery Challan Processing
      </h2>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert variant="success" dismissible onClose={() => setSuccess('')}>
          {success}
        </Alert>
      )}

      <Card className="mb-4">
        <Card.Body>
          <h5 className="mb-3">Filter</h5>
          <Form>
            <div className="row">
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Project</Form.Label>
                  <Form.Select
                    value={filter.projectId}
                    onChange={(e) => {
                      setFilter({ ...filter, projectId: e.target.value, categoryId: '', accountId: '' });
                      setCategories([]);
                      setAccounts([]);
                    }}
                    required
                  >
                    <option value="">Select Project</option>
                    {projects.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Category</Form.Label>
                  <Form.Select
                    value={filter.categoryId}
                    onChange={(e) => {
                      setFilter({ ...filter, categoryId: e.target.value, accountId: '' });
                      setAccounts([]);
                    }}
                  >
                    <option value="">All Categories</option>
                    {categories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Account</Form.Label>
                  <Form.Select
                    value={filter.accountId}
                    onChange={(e) => setFilter({ ...filter, accountId: e.target.value })}
                  >
                    <option value="">All Accounts</option>
                    {accounts.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.name}
                      </option>
                    ))}
                  </Form.Select>
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Since Date</Form.Label>
                  <Form.Control
                    type="date"
                    value={filter.sinceDate}
                    onChange={(e) => setFilter({ ...filter, sinceDate: e.target.value })}
                  />
                </Form.Group>
              </div>
            </div>
            <div className="row">
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>To Date</Form.Label>
                  <Form.Control
                    type="date"
                    value={filter.toDate}
                    onChange={(e) => setFilter({ ...filter, toDate: e.target.value })}
                  />
                </Form.Group>
              </div>
              <div className="col-md-3 d-flex align-items-end">
                <Button variant="primary" onClick={handleSearch} disabled={loading}>
                  {loading ? (
                    <>
                      <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                      Searching...
                    </>
                  ) : (
                    <>
                      <i className="fas fa-search me-2"></i>Search
                    </>
                  )}
                </Button>
              </div>
            </div>
          </Form>
        </Card.Body>
      </Card>

      {vouchers.length > 0 && (
        <Card className="mb-4">
          <Card.Body>
            <h5 className="mb-3">Vouchers</h5>
            <Table responsive striped bordered hover>
              <thead>
                <tr>
                  <th>Voucher ID</th>
                  <th>Date</th>
                  <th>Entries Count</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {vouchers.map((voucher) => (
                  <tr key={voucher.voucherId}>
                    <td>{voucher.voucherId}</td>
                    <td>{voucher.voucherDate}</td>
                    <td>{voucher.entriesCount}</td>
                    <td>
                      <Button
                        variant="outline-primary"
                        size="sm"
                        onClick={() => handleSelectVoucher(voucher)}
                      >
                        <i className="fas fa-eye me-2"></i>View Entries
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </Card.Body>
        </Card>
      )}

      {selectedVoucher && voucherEntries.length > 0 && (
        <Card>
          <Card.Body>
            <div className="d-flex justify-content-between align-items-center mb-3">
              <h5>Voucher {selectedVoucher.voucherId} Entries</h5>
              <Button variant="secondary" size="sm" onClick={() => {
                setSelectedVoucher(null);
                setVoucherEntries([]);
              }}>
                Close
              </Button>
            </div>
            <Table responsive striped bordered hover>
              <thead>
                <tr>
                  <th>Entry ID</th>
                  <th>Description</th>
                  <th>Account</th>
                  <th>Quantity</th>
                  <th>Location</th>
                  <th>DC Date</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {voucherEntries.map((entry) => (
                  <tr key={entry.entryId}>
                    <td>{entry.entryId}</td>
                    <td>{entry.description}</td>
                    <td>{entry.accountName}</td>
                    <td>{entry.quantity}</td>
                    <td>
                      <Form.Select
                        size="sm"
                        defaultValue={entry.locationId || ''}
                        onChange={(e) => {
                          if (e.target.value) {
                            handleProcessEntry(entry, parseInt(e.target.value), filter.toDate);
                          }
                        }}
                      >
                        <option value="">Select Location</option>
                        {locations.map((l) => (
                          <option key={l.id} value={l.id}>
                            {l.name}
                          </option>
                        ))}
                      </Form.Select>
                    </td>
                    <td>
                      <Form.Control
                        type="date"
                        size="sm"
                        defaultValue={filter.toDate}
                      />
                    </td>
                    <td>
                      {entry.deliveryChallanEntryId ? (
                        <Badge bg="success">Processed</Badge>
                      ) : (
                        <Badge bg="warning">Pending</Badge>
                      )}
                    </td>
                    <td>
                      {entry.deliveryChallanEntryId && (
                        <Button variant="outline-info" size="sm">
                          <i className="fas fa-eye"></i>
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </Card.Body>
        </Card>
      )}
    </div>
  );
};

export default ProcessDC;

