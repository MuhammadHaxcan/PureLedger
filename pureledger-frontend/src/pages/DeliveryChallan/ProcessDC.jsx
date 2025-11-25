import { useState, useEffect, useMemo } from 'react';
import { Card, Form, Button, Alert, Table, Badge, Modal, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';

const ProcessDC = () => {
  // State for filter form
  const [filter, setFilter] = useState({
    sinceDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
    toDate: new Date().toISOString().split('T')[0],
    projectId: null,
    categoryId: null,
    accountId: null
  });

  // State for data
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [locations, setLocations] = useState([]);
  const [transporters, setTransporters] = useState([]);
  const [vouchers, setVouchers] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');

  // State for modals
  const [showDCModal, setShowDCModal] = useState(false);
  const [showCartageModal, setShowCartageModal] = useState(false);
  const [currentEntry, setCurrentEntry] = useState(null);
  const [existingChallan, setExistingChallan] = useState(null);

  // State for DC form
  const [dcForm, setDcForm] = useState({
    journalVoucherId: null,
    journalEntryId: null,
    challanId: null,
    dcDate: new Date().toISOString().split('T')[0],
    locationId: null,
    station: '',
    otherRef: '',
    remarks: '',
    includeForCharges: false,
    forAmount: 0
  });

  // State for cartage form
  const [cartageForm, setCartageForm] = useState({
    journalVoucherId: null,
    entryId: null,
    transporterId: null,
    amount: '',
    postToCustomer: true
  });

  // State for UI
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [forChargesAvailable, setForChargesAvailable] = useState(false);
  const [processing, setProcessing] = useState(false);

  // Load initial data
  useEffect(() => {
    loadInitialData();
  }, []);

  // Load categories when project changes
  useEffect(() => {
    if (filter.projectId) {
      loadCategories(filter.projectId);
      checkForChargesAvailability(filter.projectId);
    } else {
      setCategories([]);
      setAccounts([]);
      setForChargesAvailable(false);
    }
  }, [filter.projectId]);

  // Load accounts when category changes
  useEffect(() => {
    if (filter.categoryId) {
      loadAccounts(filter.categoryId);
    } else {
      setAccounts([]);
    }
  }, [filter.categoryId]);

  // Load locations when DC modal opens
  useEffect(() => {
    if (showDCModal) {
      loadLocations();
    }
  }, [showDCModal]);

  // Load transporters when cartage modal opens
  useEffect(() => {
    if (showCartageModal && filter.projectId) {
      loadTransporters(filter.projectId);
    }
  }, [showCartageModal, filter.projectId]);

  const loadInitialData = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get('/DeliveryChallanApi/process-dc-data');
      if (response.data.success) {
        setProjects(response.data.projects || []);
      }
    } catch (error) {
      console.error('Error loading initial data:', error);
      setError('Failed to load initial data. Please refresh the page.');
    } finally {
      setLoading(false);
    }
  };

  const loadCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/categories-by-project/${projectId}`);
      if (response.data.success) {
        setCategories(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setError('Failed to load categories.');
    }
  };

  const loadAccounts = async (categoryId) => {
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/accounts-by-category/${categoryId}`);
      if (response.data.success) {
        setAccounts(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading accounts:', error);
      setError('Failed to load accounts.');
    }
  };

  const loadLocations = async () => {
    try {
      const response = await apiClient.get('/DeliveryChallanApi/locations');
      if (response.data.success) {
        setLocations(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading locations:', error);
      setError('Failed to load locations.');
    }
  };

  const loadTransporters = async (projectId) => {
    try {
      // First get transporter category ID
      const categoryResponse = await apiClient.get('/DeliveryChallanApi/transporter-category-id', {
        params: { projectId }
      });

      if (categoryResponse.data.success && categoryResponse.data.value) {
        // Then get accounts for that category
        const accountsResponse = await apiClient.get(
          `/DeliveryChallanApi/accounts-by-category/${categoryResponse.data.value}`
        );
        if (accountsResponse.data.success) {
          setTransporters(accountsResponse.data.data || []);
        }
      } else {
        setTransporters([]);
      }
    } catch (error) {
      console.error('Error loading transporters:', error);
      setTransporters([]);
    }
  };

  const checkForChargesAvailability = async (projectId) => {
    try {
      const response = await apiClient.get('/DeliveryChallanApi/check-for-charges-available', {
        params: { projectId }
      });
      if (response.data.success) {
        setForChargesAvailable(response.data.available || false);
      }
    } catch (error) {
      console.error('Error checking FOR charges:', error);
      setForChargesAvailable(false);
    }
  };

  const handleSearch = async (e) => {
    e?.preventDefault();
    
    if (!filter.projectId) {
      setError('Please select a project to generate the report.');
      return;
    }

    try {
      setLoading(true);
      setError('');
      setSuccess('');
      setSearchTerm('');

      const params = {
        SinceDate: filter.sinceDate,
        ToDate: filter.toDate,
        ProjectId: filter.projectId
      };

      if (filter.categoryId) params.CategoryId = filter.categoryId;
      if (filter.accountId) params.AccountId = filter.accountId;

      const response = await apiClient.get('/DeliveryChallanApi/filtered-entries', { params });

      if (response.data.success) {
        setVouchers(response.data.data?.vouchers || []);
        if (response.data.data?.vouchers?.length === 0) {
          setSuccess('No entries found matching the criteria.');
        }
      }
    } catch (error) {
      console.error('Error loading entries:', error);
      setError('Failed to load entries. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleOpenDCModal = async (voucherId, entryId) => {
    setCurrentEntry({ voucherId, entryId });
    setDcForm({
      ...dcForm,
      journalVoucherId: voucherId,
      journalEntryId: entryId,
      challanId: null,
      dcDate: new Date().toISOString().split('T')[0],
      locationId: null,
      station: '',
      otherRef: '',
      remarks: '',
      includeForCharges: false,
      forAmount: 0
    });
    setExistingChallan(null);
    setShowDCModal(true);

    // Check for existing challan
    try {
      const response = await apiClient.get(`/DeliveryChallanApi/check-existing-challan/${voucherId}`);
      if (response.data.success && response.data.exists) {
        setExistingChallan(response.data);
        setDcForm(prev => ({
          ...prev,
          challanId: response.data.challanId,
          dcDate: response.data.challan?.challanDate 
            ? new Date(response.data.challan.challanDate).toISOString().split('T')[0]
            : new Date().toISOString().split('T')[0],
          locationId: response.data.challan?.locationId || null,
          otherRef: response.data.challan?.otherRef || ''
        }));
      }
    } catch (error) {
      console.error('Error checking existing challan:', error);
    }
  };

  const handlePostSingle = async () => {
    if (!dcForm.journalEntryId) {
      setError('No entry selected to process.');
      return;
    }

    if (!dcForm.locationId) {
      setError('Please select a location.');
      return;
    }

    if (dcForm.includeForCharges && dcForm.forAmount <= 0) {
      setError('Please enter a valid amount for FOR charges.');
      return;
    }

    try {
      setProcessing(true);
      setError('');
      setSuccess('');

      const data = {
        JournalEntryId: dcForm.journalEntryId,
        LocationId: dcForm.locationId,
        DCDate: dcForm.dcDate,
        Remarks: dcForm.remarks,
        OtherReference: dcForm.otherRef,
        IncludeForCharges: dcForm.includeForCharges && forChargesAvailable,
        ForAmount: dcForm.includeForCharges ? dcForm.forAmount : 0
      };

      const response = await apiClient.post('/DeliveryChallanApi/single-entry', data);

      if (response.data.success) {
        setSuccess(response.data.message || 'Entry processed successfully.');
        setShowDCModal(false);
        await handleSearch();
      } else {
        setError(response.data.message || 'Failed to process entry.');
      }
    } catch (error) {
      console.error('Error processing entry:', error);
      setError(error.response?.data?.message || 'Failed to process entry. Please try again.');
    } finally {
      setProcessing(false);
    }
  };

  const handlePostAll = async () => {
    if (!dcForm.journalVoucherId) {
      setError('No voucher selected.');
      return;
    }

    if (!dcForm.locationId) {
      setError('Please select a location.');
      return;
    }

    if (dcForm.includeForCharges && dcForm.forAmount <= 0) {
      setError('Please enter a valid amount for FOR charges.');
      return;
    }

    try {
      setProcessing(true);
      setError('');
      setSuccess('');

      const data = {
        JournalVoucherId: dcForm.journalVoucherId,
        LocationId: dcForm.locationId,
        DCDate: dcForm.dcDate,
        Remarks: dcForm.remarks,
        OtherReference: dcForm.otherRef,
        IncludeForCharges: dcForm.includeForCharges && forChargesAvailable,
        ForAmount: dcForm.includeForCharges ? dcForm.forAmount : 0
      };

      const response = await apiClient.post('/DeliveryChallanApi/process-all', data);

      if (response.data.success) {
        setSuccess(response.data.message || 'Entries processed successfully.');
        setShowDCModal(false);
        await handleSearch();
      } else {
        setError(response.data.message || 'Failed to process entries.');
      }
    } catch (error) {
      console.error('Error processing entries:', error);
      setError(error.response?.data?.message || 'Failed to process entries. Please try again.');
    } finally {
      setProcessing(false);
    }
  };

  const handleOpenCartageModal = () => {
    if (!dcForm.locationId) {
      setError('Please select a location first.');
      return;
    }

    setCartageForm({
      journalVoucherId: dcForm.journalVoucherId,
      entryId: dcForm.journalEntryId,
      transporterId: null,
      amount: '',
      postToCustomer: true
    });
    setShowCartageModal(true);
  };

  const handleAddCartage = async () => {
    if (!cartageForm.transporterId) {
      setError('Please select a transporter.');
      return;
    }

    if (!cartageForm.amount || parseFloat(cartageForm.amount) <= 0) {
      setError('Please enter a valid cartage amount greater than zero.');
      return;
    }

    if (!dcForm.locationId) {
      setError('Please select a location before adding cartage.');
      return;
    }

    try {
      setProcessing(true);
      setError('');
      setSuccess('');

      const data = {
        journalVoucherId: cartageForm.journalVoucherId,
        entryId: cartageForm.entryId,
        transporterId: cartageForm.transporterId,
        amount: parseFloat(cartageForm.amount),
        postToCustomer: cartageForm.postToCustomer,
        dcDate: dcForm.dcDate,
        dcRemarks: dcForm.remarks,
        dcStation: dcForm.station,
        dcOtherRef: dcForm.otherRef,
        dcLocation: dcForm.locationId,
        includeForCharges: dcForm.includeForCharges && forChargesAvailable,
        forAmount: dcForm.includeForCharges ? dcForm.forAmount : 0
      };

      const response = await apiClient.post('/DeliveryChallanApi/add-cartage', data);

      if (response.data.success) {
        setSuccess(response.data.message || 'Cartage added and delivery challan processed successfully.');
        setShowCartageModal(false);
        setShowDCModal(false);
        await handleSearch();
      } else {
        setError(response.data.message || 'Failed to process cartage.');
      }
    } catch (error) {
      console.error('Error processing cartage:', error);
      setError(error.response?.data?.message || 'Failed to process cartage. Please try again.');
    } finally {
      setProcessing(false);
    }
  };

  // Filter vouchers by search term
  const filteredVouchers = useMemo(() => {
    if (!searchTerm) return vouchers;

    const term = searchTerm.toLowerCase();
    return vouchers.map(voucher => ({
      ...voucher,
      entries: voucher.entries?.filter(entry =>
        entry.description?.toLowerCase().includes(term) ||
        entry.accountName?.toLowerCase().includes(term) ||
        voucher.jvId?.toString().includes(term) ||
        entry.entryId?.toString().includes(term)
      ) || []
    })).filter(voucher => voucher.entries.length > 0);
  }, [vouchers, searchTerm]);

  // Flatten entries for display
  const displayEntries = useMemo(() => {
    const entries = [];
    filteredVouchers.forEach(voucher => {
      voucher.entries?.forEach(entry => {
        entries.push({
          ...entry,
          voucherId: voucher.jvId,
          entryDate: voucher.entryDate
        });
      });
    });
    return entries;
  }, [filteredVouchers]);

  return (
    <div className="container-fluid">
      {/* Header */}
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2><i className="fas fa-truck me-2"></i>Delivery Challan</h2>
      </div>

      {/* Alerts */}
      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          <i className="fas fa-exclamation-circle me-2"></i>{error}
        </Alert>
      )}

      {success && (
        <Alert variant="success" dismissible onClose={() => setSuccess('')}>
          <i className="fas fa-check-circle me-2"></i>{success}
        </Alert>
      )}

      {/* Filter Card */}
      <Card className="shadow mb-4">
        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
          <h6 className="m-0 font-weight-bold text-primary">
            <i className="fas fa-filter me-2"></i>Filter Parameters
          </h6>
        </Card.Header>
        <Card.Body>
          <Form onSubmit={handleSearch}>
            <div className="row mb-3">
              <div className="col-md-3 mb-3">
                <Form.Label>Since</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                  <Form.Control
                    type="date"
                    value={filter.sinceDate}
                    onChange={(e) => setFilter({ ...filter, sinceDate: e.target.value })}
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>To</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                  <Form.Control
                    type="date"
                    value={filter.toDate}
                    onChange={(e) => setFilter({ ...filter, toDate: e.target.value })}
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                  <SearchableSelect
                    value={filter.projectId}
                    onChange={(value) => setFilter({ 
                      ...filter, 
                      projectId: value ?? null, 
                      categoryId: null, 
                      accountId: null 
                    })}
                    options={projects.map(p => ({ id: p.id, name: p.name }))}
                    placeholder="Select Project"
                    required
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>Category</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                  <SearchableSelect
                    value={filter.categoryId}
                    onChange={(value) => setFilter({ ...filter, categoryId: value ?? null, accountId: null })}
                    options={categories.map(c => ({ id: c.id, name: c.name }))}
                    placeholder="All Categories"
                    disabled={!filter.projectId || categories.length === 0}
                  />
                </InputGroup>
              </div>
            </div>
            <div className="row mb-3">
              <div className="col-md-3 mb-3">
                <Form.Label>Account</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-file-invoice-dollar"></i></InputGroup.Text>
                  <SearchableSelect
                    value={filter.accountId}
                    onChange={(value) => setFilter({ ...filter, accountId: value ?? null })}
                    options={accounts.map(a => ({ id: a.id, name: a.name }))}
                    placeholder="All Accounts"
                    disabled={!filter.categoryId || accounts.length === 0}
                  />
                </InputGroup>
              </div>
              <div className="col-md-3 mb-3">
                <Form.Label>&nbsp;</Form.Label>
                <div className="d-grid">
                  <Button type="submit" variant="primary" disabled={loading || !filter.projectId}>
                    {loading ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                        Loading...
                      </>
                    ) : (
                      <>
                        <i className="fas fa-search me-1"></i> Generate Report
                      </>
                    )}
                  </Button>
                </div>
              </div>
            </div>
          </Form>
        </Card.Body>
      </Card>

      {/* Results Card */}
      {displayEntries.length > 0 && (
        <Card className="shadow mb-4">
          <Card.Header className="py-3 d-flex justify-content-between align-items-center">
            <h6 className="m-0 font-weight-bold text-primary">
              <i className="fas fa-list-alt me-2"></i>Available Entries
            </h6>
          </Card.Header>
          <Card.Body>
            <div className="row mb-3">
              <div className="col-12">
                <Form.Label>Search</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-search"></i></InputGroup.Text>
                  <Form.Control
                    type="text"
                    placeholder="Filter results..."
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                  />
                </InputGroup>
              </div>
            </div>
            <div className="table-responsive">
              <Table bordered striped hover>
                <thead className="table-light">
                  <tr>
                    <th>ID</th>
                    <th>Date</th>
                    <th>Description</th>
                    <th>Account</th>
                    <th>Quantity</th>
                    <th className="text-center">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {displayEntries.map((entry) => (
                    <tr
                      key={`${entry.voucherId}-${entry.entryId}`}
                      className={entry.isProcessed ? 'table-success' : ''}
                    >
                      <td>
                        <Badge bg="secondary" className="mb-1">JV-{entry.voucherId}</Badge>
                        <br />
                        <small>E-Id: {entry.entryId}</small>
                      </td>
                      <td>{new Date(entry.entryDate).toLocaleDateString()}</td>
                      <td>{entry.description || '-'}</td>
                      <td>{entry.accountName || '-'}</td>
                      <td>{entry.quantity}</td>
                      <td className="text-center">
                        <Button
                          variant={entry.isProcessed ? 'success' : 'primary'}
                          size="sm"
                          onClick={() => handleOpenDCModal(entry.voucherId, entry.entryId)}
                          disabled={processing}
                        >
                          <i className={`fas fa-${entry.isProcessed ? 'check-circle' : 'pencil'}`}></i>{' '}
                          {entry.isProcessed ? 'Processed' : 'Process'}
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          </Card.Body>
        </Card>
      )}

      {/* Delivery Challan Modal */}
      <Modal show={showDCModal} onHide={() => setShowDCModal(false)} size="lg">
        <Modal.Header closeButton className="bg-primary text-white">
          <Modal.Title>
            <i className="fas fa-truck me-2"></i>Create Delivery Challan
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {existingChallan && (
            <Alert variant="info">
              <i className="fas fa-info-circle me-2"></i>
              This voucher already has a delivery challan. Adding entries will update the existing challan.
            </Alert>
          )}

          <Form>
            <input type="hidden" value={dcForm.journalVoucherId || ''} />
            <input type="hidden" value={dcForm.journalEntryId || ''} />
            <input type="hidden" value={dcForm.challanId || ''} />

            <div className="row">
              <div className="col-md-6 mb-3">
                <Form.Label>DC Date</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                  <Form.Control
                    type="date"
                    value={dcForm.dcDate}
                    onChange={(e) => setDcForm({ ...dcForm, dcDate: e.target.value })}
                    required
                  />
                </InputGroup>
              </div>
              <div className="col-md-6 mb-3">
                <Form.Label>DC Remarks</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-comment"></i></InputGroup.Text>
                  <Form.Control
                    type="text"
                    value={dcForm.remarks}
                    onChange={(e) => setDcForm({ ...dcForm, remarks: e.target.value })}
                  />
                </InputGroup>
              </div>
            </div>

            <div className="row">
              <div className="col-md-4 mb-3">
                <Form.Label>Location</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-map-marker-alt"></i></InputGroup.Text>
                  <Form.Select
                    value={dcForm.locationId || ''}
                    onChange={(e) => setDcForm({ ...dcForm, locationId: e.target.value ? parseInt(e.target.value) : null })}
                    required
                  >
                    <option value="">Select Location</option>
                    {locations.map(loc => (
                      <option key={loc.id} value={loc.id}>{loc.name}</option>
                    ))}
                  </Form.Select>
                </InputGroup>
              </div>
              <div className="col-md-4 mb-3">
                <Form.Label>Station</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-warehouse"></i></InputGroup.Text>
                  <Form.Control
                    type="text"
                    value={dcForm.station}
                    onChange={(e) => setDcForm({ ...dcForm, station: e.target.value })}
                  />
                </InputGroup>
              </div>
              <div className="col-md-4 mb-3">
                <Form.Label>Other Reference</Form.Label>
                <InputGroup>
                  <InputGroup.Text><i className="fas fa-hashtag"></i></InputGroup.Text>
                  <Form.Control
                    type="text"
                    value={dcForm.otherRef}
                    onChange={(e) => setDcForm({ ...dcForm, otherRef: e.target.value })}
                  />
                </InputGroup>
              </div>
            </div>

            <div className="row mb-3">
              <div className="col-md-12">
                <Form.Check
                  type="checkbox"
                  id="includeForCharges"
                  label={
                    <>
                      <strong>Include FOR Charges</strong>
                      <small className="text-muted ms-2">(Freight on Road)</small>
                      {!forChargesAvailable && (
                        <i className="fas fa-info-circle text-info ms-1" title="FOR charges must be configured in project settings to be enabled"></i>
                      )}
                    </>
                  }
                  checked={dcForm.includeForCharges}
                  onChange={(e) => setDcForm({ ...dcForm, includeForCharges: e.target.checked })}
                  disabled={!forChargesAvailable}
                />
                {dcForm.includeForCharges && (
                  <div className="mt-2">
                    <Form.Label>FOR Charges Amount</Form.Label>
                    <InputGroup>
                      <InputGroup.Text><i className="fas fa-money-bill-alt"></i></InputGroup.Text>
                      <Form.Control
                        type="number"
                        min="0"
                        step="1"
                        value={dcForm.forAmount}
                        onChange={(e) => setDcForm({ ...dcForm, forAmount: parseFloat(e.target.value) || 0 })}
                      />
                    </InputGroup>
                  </div>
                )}
              </div>
            </div>
          </Form>
        </Modal.Body>
        <Modal.Footer>
          <Button
            variant="info"
            onClick={handleOpenCartageModal}
            disabled={!dcForm.locationId || processing}
            title={!dcForm.locationId ? 'Please select a location first' : ''}
          >
            <i className="fas fa-shipping-fast me-1"></i> Add Cartage
          </Button>
          <Button variant="secondary" onClick={() => setShowDCModal(false)} disabled={processing}>
            <i className="fas fa-times me-1"></i> Cancel
          </Button>
          <Button
            variant="primary"
            onClick={handlePostSingle}
            disabled={processing || !dcForm.journalEntryId || !dcForm.locationId}
          >
            {processing ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                Posting...
              </>
            ) : (
              <>
                <i className="fas fa-check me-1"></i> Post Single
              </>
            )}
          </Button>
          <Button
            variant="success"
            onClick={handlePostAll}
            disabled={processing || !dcForm.journalVoucherId || !dcForm.locationId}
          >
            {processing ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                Posting...
              </>
            ) : (
              <>
                <i className="fas fa-check-double me-1"></i> Post All
              </>
            )}
          </Button>
        </Modal.Footer>
      </Modal>

      {/* Cartage Modal */}
      <Modal show={showCartageModal} onHide={() => setShowCartageModal(false)}>
        <Modal.Header closeButton className="bg-info text-white">
          <Modal.Title>
            <i className="fas fa-shipping-fast me-2"></i>Add Cartage
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <Form>
            <input type="hidden" value={cartageForm.journalVoucherId || ''} />
            <input type="hidden" value={cartageForm.entryId || ''} />

            <Form.Group className="mb-3">
              <Form.Label>
                Transporter <span className="text-danger">*</span>
              </Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-truck-moving"></i></InputGroup.Text>
                <Form.Select
                  value={cartageForm.transporterId || ''}
                  onChange={(e) => setCartageForm({ ...cartageForm, transporterId: e.target.value ? parseInt(e.target.value) : null })}
                  required
                >
                  <option value="">Select Transporter</option>
                  {transporters.map(t => (
                    <option key={t.id} value={t.id}>{t.name}</option>
                  ))}
                </Form.Select>
              </InputGroup>
            </Form.Group>

            <Form.Group className="mb-3">
              <Form.Label>
                Amount <span className="text-danger">*</span>
              </Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-money-bill-wave"></i></InputGroup.Text>
                <Form.Control
                  type="number"
                  min="1"
                  step="1"
                  placeholder="Enter amount"
                  value={cartageForm.amount}
                  onChange={(e) => setCartageForm({ ...cartageForm, amount: e.target.value })}
                  required
                />
              </InputGroup>
            </Form.Group>

            <Form.Group className="mb-3">
              <Form.Check
                type="checkbox"
                id="postToCustomer"
                label="Post to Customer Account"
                checked={cartageForm.postToCustomer}
                onChange={(e) => setCartageForm({ ...cartageForm, postToCustomer: e.target.checked })}
              />
              <Form.Text className="text-muted">
                If checked, cartage will be debited from customer account.
                If unchecked, it will be debited from delivery item account.
              </Form.Text>
            </Form.Group>
          </Form>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setShowCartageModal(false)} disabled={processing}>
            <i className="fas fa-times me-1"></i> Cancel
          </Button>
          <Button
            variant="primary"
            onClick={handleAddCartage}
            disabled={processing || !cartageForm.transporterId || !cartageForm.amount || parseFloat(cartageForm.amount) <= 0}
          >
            {processing ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                Processing...
              </>
            ) : (
              <>
                <i className="fas fa-plus me-1"></i> Add Cartage & Process
              </>
            )}
          </Button>
        </Modal.Footer>
      </Modal>
    </div>
  );
};

export default ProcessDC;
