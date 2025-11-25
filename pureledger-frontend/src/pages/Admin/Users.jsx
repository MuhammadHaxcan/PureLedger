import { useState, useEffect } from 'react';
import { Card, Table, Button, Alert, Form, Badge } from 'react-bootstrap';
import { useAuth } from '../../contexts/AuthContext';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import './Users.css';

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

  // Load projects and controller actions when modal opens
  useEffect(() => {
    if (showModal) {
      loadProjectsAndActions();
    }
  }, [showModal]);

  const loadData = async () => {
    try {
      setLoading(true);
      setError('');
      const [usersRes, actionsRes] = await Promise.all([
        apiClient.get('/AdminApi/users'),
        apiClient.get('/AdminApi/controller-actions')
      ]);

      if (usersRes.data.success) {
        setUsers(usersRes.data.data);
      }
      if (actionsRes.data.success) {
        setControllerActions(actionsRes.data.data);
      }
    } catch (error) {
      console.error('Error loading data:', error);
      setError('Failed to load data. Please refresh the page.');
    } finally {
      setLoading(false);
    }
  };

  const loadProjectsAndActions = async () => {
    try {
      // Get projects for the current admin user
      if (editingUser) {
        // For edit, get projects from the user endpoint (it returns projects for current admin)
        const response = await apiClient.get(`/AdminApi/users/${editingUser.userId}`);
        if (response.data.success) {
          if (response.data.projects) {
            setProjects(response.data.projects);
          }
          // Also load controller actions if available
          if (response.data.controllerActions) {
            setControllerActions(response.data.controllerActions);
          }
        }
      } else {
        // For create, use the dedicated projects endpoint
        const response = await apiClient.get('/AdminApi/projects');
        if (response.data.success && response.data.data) {
          setProjects(response.data.data);
        }
      }
    } catch (error) {
      console.error('Error loading projects:', error);
      setProjects([]);
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
      setLoading(true);
      setError('');
      const response = await apiClient.get(`/AdminApi/users/${user.userId}`);
      console.log('User edit response:', response.data); // Debug log
      if (response.data.success) {
        const userData = response.data.data;
        setEditingUser(userData);
        
        // Load projects from response (available projects for current admin)
        if (response.data.projects) {
          setProjects(response.data.projects);
        } else {
          // Fallback: load projects if not in response
          const projectsRes = await apiClient.get('/AdminApi/projects');
          if (projectsRes.data.success && projectsRes.data.data) {
            setProjects(projectsRes.data.data);
          }
        }
        
        // Load controller actions from response
        if (response.data.controllerActions) {
          setControllerActions(response.data.controllerActions);
        } else {
          // Fallback: load controller actions if not in response
          const actionsRes = await apiClient.get('/AdminApi/controller-actions');
          if (actionsRes.data.success && actionsRes.data.data) {
            setControllerActions(actionsRes.data.data);
          }
        }
        
        // Ensure ProjectIds and AccessPermissions are arrays and properly formatted
        const projectIds = Array.isArray(userData.ProjectIds) 
          ? userData.ProjectIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
          : [];
        const accessPermissions = Array.isArray(userData.AccessPermissions) 
          ? userData.AccessPermissions.filter(p => p && typeof p === 'string')
          : [];
        
        console.log('Setting form data:', { projectIds, accessPermissions }); // Debug log
        
        setFormData({
          username: userData.username || '',
          fullName: userData.fullName || '',
          email: userData.email || '',
          password: '',
          confirmPassword: '',
          isAdmin: userData.isAdmin || false,
          isActive: userData.isActive !== undefined ? userData.isActive : true,
          projectIds: projectIds,
          accessPermissions: accessPermissions
        });
        setShowModal(true);
      } else {
        setError('Failed to load user details.');
      }
    } catch (error) {
      console.error('Error loading user:', error);
      const errorMessage = error.response?.data?.message || 'Failed to load user details. Please try again.';
      setError(errorMessage);
    } finally {
      setLoading(false);
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

    // Validate password
    if (!editingUser && !formData.password) {
      setError('Password is required for new users.');
      return;
    }

    if (formData.password && formData.password !== formData.confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    try {
      setLoading(true);
      // Ensure ProjectIds are numbers
      const projectIds = Array.isArray(formData.projectIds) 
        ? formData.projectIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
        : [];
      // Ensure AccessPermissions is an array and filter out empty/null values
      const accessPermissions = Array.isArray(formData.accessPermissions) 
        ? formData.accessPermissions.filter(p => p && typeof p === 'string' && p.trim().length > 0)
        : [];
      
      console.log('Saving user with permissions:', accessPermissions); // Debug log
      console.log('Saving user with projects:', projectIds); // Debug log
      
      const userData = {
        userId: editingUser?.userId || 0,
        username: formData.username,
        fullName: formData.fullName,
        email: formData.email || '',
        password: formData.password || '',
        confirmPassword: formData.confirmPassword || '',
        isAdmin: formData.isAdmin || false,
        isActive: formData.isActive !== undefined ? formData.isActive : true,
        ProjectIds: projectIds,
        AccessPermissions: accessPermissions
      };

      const response = editingUser
        ? await apiClient.put(`/AdminApi/users/${editingUser.userId}`, userData)
        : await apiClient.post('/AdminApi/users', userData);

      if (response.data.success) {
        setSuccess(response.data.message || (editingUser ? 'User updated successfully.' : 'User created successfully.'));
        setShowModal(false);
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
        await loadData();
      } else {
        setError(response.data.message || 'Failed to save user.');
      }
    } catch (error) {
      console.error('Error saving user:', error);
      const errorResponse = error.response?.data;
      let errorMessage = 'Failed to save user.';

      if (errorResponse) {
        if (Array.isArray(errorResponse.errors)) {
          errorMessage = errorResponse.errors.join(', ');
        } else if (errorResponse.message) {
          errorMessage = errorResponse.message;
        }
      }

      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmDelete = async () => {
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.delete(`/AdminApi/users/${editingUser.userId}`);
      if (response.data.success) {
        setSuccess(response.data.message || 'User deleted successfully.');
        setShowDeleteModal(false);
        setEditingUser(null);
        await loadData();
      } else {
        setError(response.data.message || 'Failed to delete user.');
      }
    } catch (error) {
      console.error('Error deleting user:', error);
      setError(error.response?.data?.message || 'Failed to delete user. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  // Toggle project selection
  const toggleProject = (projectId) => {
    const projectIds = [...formData.projectIds];
    const numProjectId = Number(projectId);
    const index = projectIds.findIndex(id => Number(id) === numProjectId);
    if (index > -1) {
      projectIds.splice(index, 1);
    } else {
      projectIds.push(numProjectId);
    }
    setFormData({ ...formData, projectIds });
  };

  // Toggle permission (controller or action)
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

  // Handle controller checkbox (full access)
  const handleControllerCheck = (controller) => {
    const isChecked = formData.accessPermissions.includes(controller);
    const permissions = [...formData.accessPermissions];
    const actions = controllerActions[controller] || [];

    if (isChecked) {
      // Uncheck controller and all its actions
      const index = permissions.indexOf(controller);
      if (index > -1) permissions.splice(index, 1);
      actions.forEach(action => {
        const actionPerm = `${controller}|${action}`;
        const actionIndex = permissions.indexOf(actionPerm);
        if (actionIndex > -1) permissions.splice(actionIndex, 1);
      });
    } else {
      // Check controller and all its actions
      if (!permissions.includes(controller)) {
        permissions.push(controller);
      }
      actions.forEach(action => {
        const actionPerm = `${controller}|${action}`;
        if (!permissions.includes(actionPerm)) {
          permissions.push(actionPerm);
        }
      });
    }
    setFormData({ ...formData, accessPermissions: permissions });
  };

  // Handle action checkbox
  const handleActionCheck = (controller, action) => {
    const actionPerm = `${controller}|${action}`;
    const permissions = [...formData.accessPermissions];
    const actions = controllerActions[controller] || [];
    const isChecked = permissions.includes(actionPerm);

    if (isChecked) {
      // Uncheck action
      const index = permissions.indexOf(actionPerm);
      if (index > -1) permissions.splice(index, 1);
      // Uncheck controller if it was checked (since we're removing an action)
      if (permissions.includes(controller)) {
        const controllerIndex = permissions.indexOf(controller);
        if (controllerIndex > -1) permissions.splice(controllerIndex, 1);
      }
    } else {
      // Check action
      if (!permissions.includes(actionPerm)) {
        permissions.push(actionPerm);
      }
      // Check controller if all actions are now checked
      const allActionsChecked = actions.every(a => {
        const aPerm = `${controller}|${a}`;
        return permissions.includes(aPerm);
      });
      if (allActionsChecked && !permissions.includes(controller)) {
        permissions.push(controller);
      }
    }
    setFormData({ ...formData, accessPermissions: permissions });
  };

  // Check if controller has full access
  const hasControllerAccess = (controller) => {
    return formData.accessPermissions.includes(controller);
  };

  // Check if action has access
  const hasActionAccess = (controller, action) => {
    return formData.accessPermissions.includes(`${controller}|${action}`) ||
           formData.accessPermissions.includes(controller);
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-users-cog me-2"></i>User Management
        </h2>
        <Button variant="primary" onClick={handleCreate} disabled={loading}>
          <i className="fas fa-plus me-2"></i>New User
        </Button>
      </div>

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

      <Card>
        <Card.Body>
          {loading && !showModal ? (
            <div className="text-center py-4">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : (
            <Table responsive striped bordered hover>
              <thead className="table-light">
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
                    <td colSpan="7" className="text-center text-muted py-4">
                      <i className="fas fa-inbox fa-2x mb-2 d-block"></i>
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
                          disabled={loading}
                          title="Edit User"
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        {user.userId !== currentUser?.userId && (
                          <Button
                            variant="outline-danger"
                            size="sm"
                            onClick={() => handleDelete(user)}
                            disabled={loading}
                            title="Delete User"
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

      {/* Create/Edit User Modal */}
      <Modal
        show={showModal}
        onHide={() => {
          setShowModal(false);
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
          setError('');
        }}
        title={editingUser ? 'Edit User' : 'New User'}
        size="xl"
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)} disabled={loading}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit} disabled={loading} form="userForm">
              {loading ? (
                <>
                  <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                  {editingUser ? 'Updating...' : 'Creating...'}
                </>
              ) : (
                editingUser ? 'Update' : 'Create'
              )}
            </Button>
          </>
        }
      >
        <Form id="userForm" onSubmit={handleSubmit}>
          <div className="row">
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>Username</Form.Label>
                <Form.Control
                  type="text"
                  value={formData.username}
                  onChange={(e) => setFormData({ ...formData, username: e.target.value })}
                  required
                  disabled={!!editingUser || loading}
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
                  disabled={loading}
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
                  disabled={loading}
                />
              </Form.Group>
            </div>
            <div className="col-md-6">
              <Form.Group className="mb-3">
                <Form.Label>
                  Password {editingUser && <small className="text-muted">(leave blank to keep current)</small>}
                </Form.Label>
                <Form.Control
                  type="password"
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  required={!editingUser}
                  disabled={loading}
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
                disabled={loading}
              />
            </Form.Group>
          )}

          <div className="row mb-3">
            <div className="col-md-6">
              <Form.Check
                type="checkbox"
                id="isAdmin"
                label="Is Admin"
                checked={formData.isAdmin}
                onChange={(e) => setFormData({ ...formData, isAdmin: e.target.checked })}
                disabled={loading || (editingUser?.userId === currentUser?.userId)}
              />
            </div>
            <div className="col-md-6">
              <Form.Check
                type="checkbox"
                id="isActive"
                label="Is Active"
                checked={formData.isActive}
                onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                disabled={loading || (editingUser?.userId === currentUser?.userId)}
              />
            </div>
          </div>

          {/* Projects Selection */}
          <Form.Group className="mb-3">
            <Form.Label>Assign Projects</Form.Label>
            <Card>
              <Card.Body style={{ maxHeight: '200px', overflowY: 'auto', overflowX: 'visible' }}>
                {projects.length === 0 ? (
                  <p className="text-muted mb-0">No projects available</p>
                ) : (
                  projects.map((project) => {
                    const projectId = Number(project.id);
                    const isChecked = formData.projectIds.some(id => Number(id) === projectId);
                    return (
                      <Form.Check
                        key={project.id}
                        type="checkbox"
                        id={`project_${project.id}`}
                        label={project.name}
                        checked={isChecked}
                        onChange={() => toggleProject(project.id)}
                        disabled={loading}
                      />
                    );
                  })
                )}
              </Card.Body>
            </Card>
          </Form.Group>

          {/* Access Permissions */}
          <Form.Group className="mb-3">
            <Form.Label>Access Permissions</Form.Label>
            <Card className="access-permissions-card">
              <Card.Body className="access-permissions-body">
                {Object.keys(controllerActions).length === 0 ? (
                  <p className="text-muted mb-0">No permissions available</p>
                ) : (
                  <div className="access-permissions-container">
                    {Object.keys(controllerActions).map((controller) => (
                      <div key={controller} className="mb-4 border-bottom pb-3 access-permission-section">
                        {/* Controller Header with Full Access Option */}
                        <div className="d-flex align-items-center mb-2 flex-wrap">
                          <h6 className="mb-0 me-3">
                            <i className="fas fa-layer-group me-1 text-primary"></i>{controller}
                          </h6>
                          <Form.Check
                            type="checkbox"
                            id={`controller_${controller}`}
                            label={<span className="text-success fw-bold">Grant Full Access</span>}
                            checked={hasControllerAccess(controller)}
                            onChange={() => handleControllerCheck(controller)}
                            disabled={loading}
                            className="flex-shrink-0"
                          />
                        </div>

                        {/* Actions for this controller */}
                        <div className="ms-3">
                          <small className="text-muted mb-2 d-block">Or select specific actions:</small>
                          <div className="row g-2">
                            {controllerActions[controller].map((action) => (
                              <div key={action} className="col-md-6 col-lg-4">
                                <Form.Check
                                  type="checkbox"
                                  id={`action_${controller}_${action}`}
                                  label={
                                    <>
                                      <i className="fas fa-cog me-1 text-secondary"></i>{action}
                                    </>
                                  }
                                  checked={hasActionAccess(controller, action)}
                                  onChange={() => handleActionCheck(controller, action)}
                                  disabled={loading}
                                />
                              </div>
                            ))}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </Card.Body>
            </Card>
          </Form.Group>
        </Form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        show={showDeleteModal}
        onHide={() => {
          setShowDeleteModal(false);
          setEditingUser(null);
        }}
        onConfirm={handleConfirmDelete}
        title="Delete User"
        message={`Are you sure you want to delete user "${editingUser?.username}"? This action cannot be undone.`}
        confirmText="Delete"
        cancelText="Cancel"
        variant="danger"
        loading={loading}
      />
    </div>
  );
};

export default Users;

