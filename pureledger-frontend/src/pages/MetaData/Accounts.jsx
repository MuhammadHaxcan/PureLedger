import { useState, useEffect } from 'react';
import { Card, Table, Button, Alert, Form, Badge, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import SearchableSelect from '../../components/SearchableSelect';

const Accounts = () => {
  const [accounts, setAccounts] = useState([]);
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [accountGroups, setAccountGroups] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingAccount, setEditingAccount] = useState(null);
  const [formData, setFormData] = useState({
    accountName: '',
    projectId: '',
    categoryId: '',
    accountGroupId: '',
    accountData: {
      accountDataId: null,
      phoneNumber: '',
      email: '',
      address: '',
      area: '',
      city: ''
    },
    productData: {
      productDataId: null,
      pkgForm: '',
      pkg: null,
      type: '',
      saleRate: null
    }
  });
  const [filterProjectId, setFilterProjectId] = useState(null); // Filter for displaying accounts
  const [filterCategoryId, setFilterCategoryId] = useState(null); // Filter for displaying accounts
  const [filterCategories, setFilterCategories] = useState([]); // Categories for filter dropdown

  useEffect(() => {
    loadProjects();
    loadAccountGroups();
  }, []);

  useEffect(() => {
    if (filterProjectId) {
      loadAccounts();
    } else {
      setAccounts([]);
    }
  }, [filterProjectId, filterCategoryId]);

  useEffect(() => {
    if (filterProjectId) {
      loadFilterCategories(parseInt(filterProjectId));
    } else {
      setFilterCategories([]);
      setFilterCategoryId(null);
    }
  }, [filterProjectId]);

  useEffect(() => {
    if (formData.projectId && showModal) {
      loadCategories(parseInt(formData.projectId));
    }
  }, [formData.projectId, showModal]);

  const loadProjects = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/projects-for-accounts');
      if (response.data.success) {
        setProjects(response.data.data);
      }
    } catch (error) {
      setError('Failed to load projects');
    }
  };

  const loadAccountGroups = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/account-groups');
      if (response.data.success) {
        setAccountGroups(response.data.data);
      }
    } catch (error) {
      setError('Failed to load account groups');
    }
  };

  const loadFilterCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/categories/by-project/${projectId}`);
      if (response.data.success) {
        setFilterCategories(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setFilterCategories([]);
    }
  };

  const loadAccounts = async () => {
    if (!filterProjectId) {
      setLoading(false);
      setAccounts([]);
      return;
    }

    const projectId = filterProjectId;
    const categoryId = filterCategoryId;

    try {
      setLoading(true);
      const params = { projectId };
      if (categoryId) {
        params.categoryId = categoryId;
      }
      const response = await apiClient.get('/MetaDataApi/accounts', { params });
      if (response.data.success) {
        if (projectId !== filterProjectId || categoryId !== filterCategoryId) {
          return;
        }
        setAccounts(response.data.data);
      } else {
        setAccounts([]);
      }
    } catch (error) {
      setAccounts([]);
      setError(error.response?.data?.message || 'Failed to load accounts');
    } finally {
      setLoading(false);
    }
  };

  const loadData = async () => {
    await loadAccounts();
  };

  const loadCategories = async (projectId) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/categories/by-project/${projectId}`);
      if (response.data.success) {
        const cats = (response.data.data || []).map(c => ({ id: c.id, name: c.name }));
        setCategories(cats);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setCategories([]);
    }
  };

  const handleCreate = () => {
    setEditingAccount(null);
    setFormData({
      accountName: '',
      projectId: '',
      categoryId: '',
      accountGroupId: '',
      accountData: {
        accountDataId: null,
        phoneNumber: '',
        email: '',
        address: '',
        area: '',
        city: ''
      },
      productData: {
        productDataId: null,
        pkgForm: '',
        pkg: null,
        type: '',
        saleRate: null
      }
    });
    setCategories([]);
    setShowModal(true);
  };

  const handleEdit = async (account) => {
    try {
      setLoading(true);
      const response = await apiClient.get(`/MetaDataApi/accounts/${account.accountId}`);
      if (response.data.success) {
        const acc = response.data.data;
        setEditingAccount({ ...acc, hasJournalAssociations: response.data.hasJournalAssociations });
        
        // Get the category to find the project
        let projectId = null;
        const categoryId = acc.categoryId || acc.CategoryId;
        if (categoryId) {
          try {
            const categoryRes = await apiClient.get(`/MetaDataApi/categories/${categoryId}`);
            if (categoryRes.data.success && categoryRes.data.data) {
              projectId = categoryRes.data.data.projectId || categoryRes.data.data.projectId;
            }
          } catch (err) {
            console.error('Error loading category:', err);
          }
        }
        
        // Handle both camelCase and PascalCase property names from API
        const accountData = acc.accountData || acc.AccountData;
        const productData = acc.productData || acc.ProductData;
        
        setFormData({
          accountName: acc.accountName || acc.AccountName || '',
          projectId: projectId?.toString() || '',
          categoryId: (acc.categoryId || acc.CategoryId)?.toString() || '',
          accountGroupId: (acc.accountGroupId || acc.AccountGroupId)?.toString() || '',
          accountData: accountData ? {
            accountDataId: accountData.accountDataId || accountData.AccountDataId || null,
            phoneNumber: accountData.phoneNumber || accountData.PhoneNumber || '',
            email: accountData.email || accountData.Email || '',
            address: accountData.address || accountData.Address || '',
            area: accountData.area || accountData.Area || '',
            city: accountData.city || accountData.City || ''
          } : {
            accountDataId: null,
            phoneNumber: '',
            email: '',
            address: '',
            area: '',
            city: ''
          },
          productData: productData ? {
            productDataId: productData.productDataId || productData.ProductDataId || null,
            pkgForm: productData.pkgForm || productData.PkgForm || '',
            pkg: productData.pkg || productData.Pkg || null,
            type: productData.type || productData.Type || '',
            saleRate: productData.saleRate || productData.SaleRate || null
          } : {
            productDataId: null,
            pkgForm: '',
            pkg: null,
            type: '',
            saleRate: null
          }
        });
        
        // Load projects and account groups if not loaded
        if (projects.length === 0) {
          await loadProjects();
        }
        if (accountGroups.length === 0) {
          await loadAccountGroups();
        }
        
        // Load categories for the project
        if (projectId) {
          await loadCategories(projectId);
        }
        
        setShowModal(true);
      }
    } catch (error) {
      setError('Failed to load account details');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = (account) => {
    setEditingAccount(account);
    setShowDeleteModal(true);
  };

  const handleToggleStatus = async (account) => {
    try {
      const response = await apiClient.post(`/MetaDataApi/accounts/${account.accountId}/toggle-status`);
      if (response.data.success) {
        setSuccess(response.data.message);
        loadData();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to toggle account status');
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    try {
      const selectedAccountGroup = accountGroups.find(ag => ag.id.toString() === formData.accountGroupId);
      const accountGroupName = selectedAccountGroup?.name?.toLowerCase().trim() || '';
      const isAccountGroup = accountGroupName === 'account';
      const isProductGroup = accountGroupName === 'product';

      const accountData = {
        accountId: editingAccount?.accountId || 0,
        accountName: formData.accountName,
        categoryId: parseInt(formData.categoryId),
        accountGroupId: parseInt(formData.accountGroupId),
        accountData: isAccountGroup ? {
          accountDataId: formData.accountData.accountDataId,
          phoneNumber: formData.accountData.phoneNumber || null,
          email: formData.accountData.email || null,
          address: formData.accountData.address || null,
          area: formData.accountData.area || null,
          city: formData.accountData.city || null
        } : null,
        productData: isProductGroup ? {
          productDataId: formData.productData.productDataId,
          pkgForm: formData.productData.pkgForm || null,
          pkg: formData.productData.pkg || null,
          type: formData.productData.type || null,
          saleRate: formData.productData.saleRate || null
        } : null
      };

      const response = editingAccount
        ? await apiClient.put(`/MetaDataApi/accounts/${editingAccount.accountId}`, accountData)
        : await apiClient.post('/MetaDataApi/accounts', accountData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadData();
      }
    } catch (error) {
      const errors = error.response?.data?.errors || [];
      setError(errors.length > 0 ? errors.join(', ') : 'Failed to save account');
    }
  };

  const handleConfirmDelete = async () => {
    try {
      const canDeleteRes = await apiClient.get(`/MetaDataApi/accounts/${editingAccount.accountId}/can-delete`);
      if (!canDeleteRes.data.canDelete) {
        setError(canDeleteRes.data.message);
        setShowDeleteModal(false);
        return;
      }

      const response = await apiClient.delete(`/MetaDataApi/accounts/${editingAccount.accountId}`);
      if (response.data.success) {
        setSuccess(response.data.message);
        setShowDeleteModal(false);
        loadData();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to delete account');
    }
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-users me-2"></i>Accounts
        </h2>
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Account
        </Button>
      </div>

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
        <Card.Header>
          <h6 className="m-0 fw-bold text-primary">
            <i className="fas fa-filter me-2"></i>Filter by Project & Category
          </h6>
        </Card.Header>
        <Card.Body>
          <div className="row align-items-end">
            <div className="col-md-4">
              <Form.Label>Project</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                <SearchableSelect
                  value={filterProjectId}
                  onChange={(value) => {
                    setFilterProjectId(value ? parseInt(value) : null);
                    setFilterCategoryId(null);
                  }}
                  options={projects.map(p => ({ id: p.projectId, name: p.projectName }))}
                  placeholder="Select Project"
                />
              </InputGroup>
            </div>
            <div className="col-md-4">
              <Form.Label>Category</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                <SearchableSelect
                  value={filterCategoryId}
                  onChange={(value) => setFilterCategoryId(value ? parseInt(value) : null)}
                  options={[{ id: null, name: 'All Categories' }, ...filterCategories.map(c => ({ id: c.id, name: c.name }))]}
                  placeholder="All Categories"
                  disabled={!filterProjectId}
                />
              </InputGroup>
            </div>
          </div>
        </Card.Body>
      </Card>

      <Card>
        <Card.Body>
          {loading ? (
            <div className="text-center py-4">
              <div className="spinner-border" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : (
            <Table responsive striped bordered hover>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Account Name</th>
                  <th>Category</th>
                  <th>Account Group</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {!filterProjectId ? (
                  <tr>
                    <td colSpan="6" className="text-center text-muted">
                      Select a project to view accounts
                    </td>
                  </tr>
                ) : accounts.length === 0 ? (
                  <tr>
                    <td colSpan="6" className="text-center text-muted">
                      No accounts found for the selected filters
                    </td>
                  </tr>
                ) : (
                  accounts.map((account) => (
                    <tr key={account.accountId}>
                      <td>{account.accountId}</td>
                      <td>{account.accountName}</td>
                      <td>{account.categoryName || account.categoryId}</td>
                      <td>{account.accountGroupName || account.accountGroupId}</td>
                      <td>
                        <Badge bg={account.isEnabled ? 'success' : 'secondary'}>
                          {account.isEnabled ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(account)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant={account.isEnabled ? 'outline-warning' : 'outline-success'}
                          size="sm"
                          className="me-2"
                          onClick={() => handleToggleStatus(account)}
                          title={account.isEnabled ? 'Disable' : 'Enable'}
                        >
                          <i className={`fas fa-${account.isEnabled ? 'ban' : 'check'}`}></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(account)}
                        >
                          <i className="fas fa-trash"></i>
                        </Button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </Table>
          )}
        </Card.Body>
      </Card>

      <Modal
        show={showModal}
        onHide={() => setShowModal(false)}
        title={editingAccount ? 'Edit Account' : 'New Account'}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingAccount ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>Account Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.accountName}
              onChange={(e) => setFormData({ ...formData, accountName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
          {!editingAccount && (
            <Form.Group className="mb-3">
              <Form.Label>Project</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                <SearchableSelect
                  value={formData.projectId ? parseInt(formData.projectId) : null}
                  onChange={(value) => {
                    setFormData({ ...formData, projectId: value ? value.toString() : '', categoryId: '' });
                    if (value) {
                      loadCategories(value);
                    } else {
                      setCategories([]);
                    }
                  }}
                  options={projects.map(p => ({ id: p.projectId, name: p.projectName }))}
                  placeholder="Select Project"
                  required
                  disabled={loading}
                />
              </InputGroup>
            </Form.Group>
          )}
          {editingAccount && formData.projectId && (
            <Form.Group className="mb-3">
              <Form.Label>Project</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                <Form.Control
                  type="text"
                  value={projects.find(p => p.projectId.toString() === formData.projectId)?.projectName || ''}
                  readOnly
                  className="bg-light"
                />
              </InputGroup>
              <Form.Text className="text-muted">
                Project is determined by the category and cannot be changed.
              </Form.Text>
            </Form.Group>
          )}
          <Form.Group className="mb-3">
            <Form.Label>Category</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.categoryId ? parseInt(formData.categoryId) : null}
                onChange={(value) => setFormData({ ...formData, categoryId: value ? value.toString() : '' })}
                options={categories.map(c => ({ id: c.id, name: c.name }))}
                placeholder="Select Category"
                required
                disabled={editingAccount?.hasJournalAssociations || !formData.projectId || loading}
              />
            </InputGroup>
            {editingAccount?.hasJournalAssociations && (
              <Form.Text className="text-muted">
                Category cannot be changed because this account is associated with journal vouchers.
              </Form.Text>
            )}
            {!formData.projectId && !editingAccount && (
              <Form.Text className="text-muted">
                Please select a project first.
              </Form.Text>
            )}
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Account Group</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-users"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.accountGroupId ? parseInt(formData.accountGroupId) : null}
                onChange={(value) => {
                  const accountGroupName = accountGroups.find(ag => ag.id.toString() === value?.toString())?.name?.toLowerCase() || '';
                  setFormData({ ...formData, accountGroupId: value ? value.toString() : '' });
                }}
                options={accountGroups.map(ag => ({ id: ag.id, name: ag.name }))}
                placeholder="Select Account Group"
                required
                disabled={editingAccount?.hasJournalAssociations || loading}
              />
            </InputGroup>
            {editingAccount?.hasJournalAssociations && (
              <Form.Text className="text-muted">
                Account Group cannot be changed because this account is associated with journal vouchers.
              </Form.Text>
            )}
          </Form.Group>

          {/* Account Data Fields (visible when Account Group is "Account") */}
          {accountGroups.find(ag => ag.id.toString() === formData.accountGroupId)?.name?.toLowerCase().trim() === 'account' && (
            <div className="mb-3">
              <h5 className="mt-4 mb-3">Account Details</h5>
              <Form.Group className="mb-3">
                <Form.Label>Phone Number</Form.Label>
                <Form.Control
                  type="tel"
                  value={formData.accountData.phoneNumber}
                  onChange={(e) => setFormData({
                    ...formData,
                    accountData: { ...formData.accountData, phoneNumber: e.target.value }
                  })}
                  placeholder="Enter phone number"
                />
              </Form.Group>
              <Form.Group className="mb-3">
                <Form.Label>Email</Form.Label>
                <Form.Control
                  type="email"
                  value={formData.accountData.email}
                  onChange={(e) => setFormData({
                    ...formData,
                    accountData: { ...formData.accountData, email: e.target.value }
                  })}
                  placeholder="Enter email"
                />
              </Form.Group>
              <Form.Group className="mb-3">
                <Form.Label>Address</Form.Label>
                <Form.Control
                  as="textarea"
                  rows={3}
                  value={formData.accountData.address}
                  onChange={(e) => setFormData({
                    ...formData,
                    accountData: { ...formData.accountData, address: e.target.value }
                  })}
                  placeholder="Enter address"
                />
              </Form.Group>
              <div className="row">
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>Area</Form.Label>
                    <Form.Control
                      type="text"
                      value={formData.accountData.area}
                      onChange={(e) => setFormData({
                        ...formData,
                        accountData: { ...formData.accountData, area: e.target.value }
                      })}
                      placeholder="Enter area"
                    />
                  </Form.Group>
                </div>
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>City</Form.Label>
                    <Form.Control
                      type="text"
                      value={formData.accountData.city}
                      onChange={(e) => setFormData({
                        ...formData,
                        accountData: { ...formData.accountData, city: e.target.value }
                      })}
                      placeholder="Enter city"
                    />
                  </Form.Group>
                </div>
              </div>
            </div>
          )}

          {/* Product Data Fields (visible when Account Group is "Product") */}
          {accountGroups.find(ag => ag.id.toString() === formData.accountGroupId)?.name?.toLowerCase().trim() === 'product' && (
            <div className="mb-3">
              <h5 className="mt-4 mb-3">Product Details</h5>
              <div className="row">
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>Package Form</Form.Label>
                    <Form.Control
                      type="text"
                      value={formData.productData.pkgForm}
                      onChange={(e) => setFormData({
                        ...formData,
                        productData: { ...formData.productData, pkgForm: e.target.value }
                      })}
                      placeholder="Enter package form"
                    />
                  </Form.Group>
                </div>
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>Package</Form.Label>
                    <Form.Control
                      type="number"
                      className="no-arrow"
                      min="0"
                      value={formData.productData.pkg || ''}
                      onChange={(e) => setFormData({
                        ...formData,
                        productData: { ...formData.productData, pkg: e.target.value ? parseInt(e.target.value) : null }
                      })}
                      placeholder="Enter package"
                    />
                  </Form.Group>
                </div>
              </div>
              <div className="row">
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>Type</Form.Label>
                    <Form.Control
                      type="text"
                      value={formData.productData.type}
                      onChange={(e) => setFormData({
                        ...formData,
                        productData: { ...formData.productData, type: e.target.value }
                      })}
                      placeholder="Enter type"
                    />
                  </Form.Group>
                </div>
                <div className="col-md-6">
                  <Form.Group className="mb-3">
                    <Form.Label>Sale Rate</Form.Label>
                    <Form.Control
                      type="number"
                      className="no-arrow"
                      min="0"
                      step="0.0001"
                      value={formData.productData.saleRate || ''}
                      onChange={(e) => setFormData({
                        ...formData,
                        productData: { ...formData.productData, saleRate: e.target.value ? parseFloat(e.target.value) : null }
                      })}
                      placeholder="Enter sale rate"
                    />
                  </Form.Group>
                </div>
              </div>
            </div>
          )}
        </Form>
      </Modal>

      <ConfirmModal
        show={showDeleteModal}
        onHide={() => setShowDeleteModal(false)}
        onConfirm={handleConfirmDelete}
        title="Delete Account"
        message={`Are you sure you want to delete "${editingAccount?.accountName}"?`}
      />
    </div>
  );
};

export default Accounts;

