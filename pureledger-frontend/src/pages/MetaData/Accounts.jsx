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
    accountGroupId: ''
  });
  const [filterProjectId, setFilterProjectId] = useState(null); // Filter for displaying accounts
  const [filterCategoryId, setFilterCategoryId] = useState(null); // Filter for displaying accounts
  const [filterCategories, setFilterCategories] = useState([]); // Categories for filter dropdown

  useEffect(() => {
    loadProjects();
    loadAccountGroups();
    loadAccounts();
  }, [filterProjectId, filterCategoryId]);

  useEffect(() => {
    if (filterProjectId) {
      loadFilterCategories(parseInt(filterProjectId));
    } else {
      setFilterCategories([]);
      setFilterCategoryId(null);
    }
  }, [filterProjectId]);

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
    try {
      setLoading(true);
      const params = {};
      if (filterProjectId) params.projectId = filterProjectId;
      if (filterCategoryId) params.categoryId = filterCategoryId;
      const response = await apiClient.get('/MetaDataApi/accounts', { params });
      if (response.data.success) {
        setAccounts(response.data.data);
      }
    } catch (error) {
      setError('Failed to load accounts');
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
        setCategories(response.data.data);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
    }
  };

  const handleCreate = () => {
    setEditingAccount(null);
    setFormData({ accountName: '', projectId: '', categoryId: '', accountGroupId: '' });
    setCategories([]);
    setShowModal(true);
  };

  const handleEdit = async (account) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/accounts/${account.accountId}`);
      if (response.data.success) {
        const acc = response.data.data;
        setEditingAccount(acc);
        setFormData({
          accountName: acc.accountName,
          projectId: '',
          categoryId: acc.categoryId?.toString() || '',
          accountGroupId: acc.accountGroupId?.toString() || ''
        });
        setShowModal(true);
      }
    } catch (error) {
      setError('Failed to load account details');
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
      const accountData = {
        accountId: editingAccount?.accountId || 0,
        accountName: formData.accountName,
        categoryId: parseInt(formData.categoryId),
        accountGroupId: parseInt(formData.accountGroupId)
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
                  options={[{ id: null, name: 'All Projects' }, ...projects.map(p => ({ id: p.projectId, name: p.projectName }))]}
                  placeholder="All Projects"
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
                {accounts.length === 0 ? (
                  <tr>
                    <td colSpan="6" className="text-center text-muted">
                      No accounts found
                    </td>
                  </tr>
                ) : (
                  accounts.map((account) => (
                    <tr key={account.accountId}>
                      <td>{account.accountId}</td>
                      <td>{account.accountName}</td>
                      <td>{account.categoryId}</td>
                      <td>{account.accountGroupId}</td>
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
              <Form.Select
                value={formData.projectId}
                onChange={(e) => setFormData({ ...formData, projectId: e.target.value, categoryId: '' })}
                required
              >
                <option value="">Select Project</option>
                {projects.map((p) => (
                  <option key={p.projectId} value={p.projectId}>
                    {p.projectName}
                  </option>
                ))}
              </Form.Select>
            </Form.Group>
          )}
          <Form.Group className="mb-3">
            <Form.Label>Category</Form.Label>
            <Form.Select
              value={formData.categoryId}
              onChange={(e) => setFormData({ ...formData, categoryId: e.target.value })}
              required
              disabled={editingAccount?.hasJournalAssociations}
            >
              <option value="">Select Category</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Form.Select>
            {editingAccount?.hasJournalAssociations && (
              <Form.Text className="text-muted">
                Category cannot be changed because this account is associated with journal vouchers.
              </Form.Text>
            )}
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Account Group</Form.Label>
            <Form.Select
              value={formData.accountGroupId}
              onChange={(e) => setFormData({ ...formData, accountGroupId: e.target.value })}
              required
              disabled={editingAccount?.hasJournalAssociations}
            >
              <option value="">Select Account Group</option>
              {accountGroups.map((ag) => (
                <option key={ag.id} value={ag.id}>
                  {ag.name}
                </option>
              ))}
            </Form.Select>
            {editingAccount?.hasJournalAssociations && (
              <Form.Text className="text-muted">
                Account Group cannot be changed because this account is associated with journal vouchers.
              </Form.Text>
            )}
          </Form.Group>
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

