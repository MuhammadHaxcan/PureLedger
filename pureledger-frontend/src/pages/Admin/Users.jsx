import { useState, useEffect } from 'react';
import { Card, Table, Button, Alert, Form, Badge } from 'react-bootstrap';
import { useAuth } from '../../contexts/AuthContext';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';

const Users = () => {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState([]);
  const [projects, setProjects] = useState([]);
  const [controllerActions, setControllerActions] = useState({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingUser, setEditingUser] = useState(null);
  const [formData, setFormData] = useState({
    username: '',
    fullName: '',
    email: '',
    password: '',
    confirmPassword: '',
    isAdmin: false,
    isActive: true,
    projectIds: [],
    accessPermissions: []
  });

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [usersRes, projectsRes, actionsRes] = await Promise.all([
        apiClient.get('/AdminApi/users'),
        apiClient.get('/AdminApi/users').then(() => apiClient.get('/AdminApi/users/1')), // Get projects from user endpoint
        apiClient.get('/AdminApi/controller-actions')
      ]);

      if (usersRes.data.success) {
        setUsers(usersRes.data.data);
      }
      if (actionsRes.data.success) {
        setControllerActions(actionsRes.data.data);
      }
    } catch (error) {
      setError('Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingUser(null);
    setFormData({
      username: '',
      fullName: '',
      email: '',
      password: '',
      confirmPassword: '',
      isAdmin: false,
      isActive: true,
      projectIds: [],
      accessPermissions: []
    });
    setShowModal(true);
  };

  const handleEdit = async (user) => {
    try {
      const response = await apiClient.get(`/AdminApi/users/${user.userId}`);
      if (response.data.success) {
        const userData = response.data.data;
        setEditingUser(userData);
        setProjects(response.data.projects || []);
        setFormData({
          username: userData.username,
          fullName: userData.fullName,
          email: userData.email || '',
          password: '',
          confirmPassword: '',
          isAdmin: userData.isAdmin,
          isActive: userData.isActive,
          projectIds: userData.ProjectIds || [],
          accessPermissions: userData.AccessPermissions || []
        });
        setShowModal(true);
      }
    } catch (error) {
      setError('Failed to load user details');
    }
  };

  const handleDelete = (user) => {
    if (user.userId === currentUser?.userId) {
      setError('You cannot delete your own account');
      return;
    }
    setEditingUser(user);
    setShowDeleteModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (formData.password && formData.password !== formData.confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    try {
      const userData = {
        userId: editingUser?.userId || 0,
        username: formData.username,
        fullName: formData.fullName,
        email: formData.email,
        password: formData.password || '',
        confirmPassword: formData.confirmPassword || '',
        isAdmin: formData.isAdmin,
        isActive: formData.isActive,
        ProjectIds: formData.projectIds,
        AccessPermissions: formData.accessPermissions
      };

      const response = editingUser
        ? await apiClient.put(`/AdminApi/users/${editingUser.userId}`, userData)
        : await apiClient.post('/AdminApi/users', userData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadData();
      }
    } catch (error) {
      const errors = error.response?.data?.errors || [];
      setError(errors.length > 0 ? errors.join(', ') : 'Failed to save user');
    }
  };

  const handleConfirmDelete = async () => {
    try {
      const response = await apiClient.delete(`/AdminApi/users/${editingUser.userId}`);
      if (response.data.success) {
        setSuccess(response.data.message);
        setShowDeleteModal(false);
        loadData();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to delete user');
    }
  };

  const togglePermission = (permission) => {
    const permissions = [...formData.accessPermissions];
    const index = permissions.indexOf(permission);
    if (index > -1) {
      permissions.splice(index, 1);
    } else {
      permissions.push(permission);
    }
    setFormData({ ...formData, accessPermissions: permissions });
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-users-cog me-2"></i>User Management
        </h2>
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New User
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
                  <th>Username</th>
                  <th>Full Name</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.length === 0 ? (
                  <tr>
                    <td colSpan="7" className="text-center text-muted">
                      No users found
                    </td>
                  </tr>
                ) : (
                  users.map((user) => (
                    <tr key={user.userId}>
                      <td>{user.userId}</td>
                      <td>{user.username}</td>
                      <td>{user.fullName}</td>
                      <td>{user.email || '-'}</td>
                      <td>
                        <Badge bg={user.isAdmin ? 'primary' : 'secondary'}>
                          {user.isAdmin ? 'Admin' : 'User'}
                        </Badge>
                      </td>
                      <td>
                        <Badge bg={user.isActive ? 'success' : 'danger'}>
                          {user.isActive ? 'Active' : 'Inactive'}
                        </Badge>
                      </td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(user)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        {user.userId !== currentUser?.userId && (
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => handleDelete(user)}
                          >
                            <i className="fas fa-trash"></i>
                          </Button>
                        )}
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
        title={editingUser ? 'Edit User' : 'New User'}
        size="lg"
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingUser ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <div className="row">
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>Username</Form.Label>
                <Form.Control
                  type="text"
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                  required
                  disabled={!!editingUser}
                  autoFocus
                />
              </Form.Group>
            </div>
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>Full Name</Form.Label>
                <Form.Control
                  type="text"
                  value={formData.fullName}
                  onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                  required
                />
              </Form.Group>
            </div>
          </div>
          <div className="row">
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>Email</Form.Label>
                <Form.Control
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                />
              </Form.Group>
            </div>
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>Password {editingUser && '(leave blank to keep current)'}</Form.Label>
                <Form.Control
                  type="password"
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  required={!editingUser}
                />
              </Form.Group>
            </div>
          </div>
          {!editingUser && (
            <Form.Group className="mb-3">
              <Form.Label>Confirm Password</Form.Label>
              <Form.Control
                type="password"
                value={formData.confirmPassword}
                onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                required={!editingUser}
              />
            </Form.Group>
          )}
          <div className="row">
            <div className="col-md-6">
              <Form.Check
                type="checkbox"
                label="Admin"
                checked={formData.isAdmin}
                onChange={(e) => setFormData({ ...formData, isAdmin: e.target.checked })}
                disabled={editingUser?.userId === currentUser?.userId}
              />
            </div>
            <div className="col-md-6">
              <Form.Check
                type="checkbox"
                label="Active"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                disabled={editingUser?.userId === currentUser?.userId}
              />
            </div>
          </div>
          {/* Add project selection and access permissions UI here */}
        </Form>
      </Modal>

      <ConfirmModal
        show={showDeleteModal}
        onHide={() => setShowDeleteModal(false)}
        onConfirm={handleConfirmDelete}
        title="Delete User"
        message={`Are you sure you want to delete user "${editingUser?.username}"? This action cannot be undone.`}
      />
    </div>
  );
};

export default Users;

