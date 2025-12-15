import { useState, useEffect, useRef } from 'react';
import { Card, Table, Button, Alert, Form, Badge } from 'react-bootstrap';
import { useAuth } from '../../contexts/AuthContext';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';
import './Users.css';

const Users = () => {
  usePageTitle('Users');
  const { user: currentUser } = useAuth();
  const isSubmittingRef = useRef(false);
  const [users, setUsers] = useState([]);
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
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
    hiddenCategoryIds: [],
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
          // Load categories from accessible projects
          if (response.data.categories) {
            // API returns { id, name, projectId } - ensure all values are valid
            const validCategories = response.data.categories
              .filter(c => c && c.id !== undefined && c.id !== null && c.name)
              .map(c => ({
                id: Number(c.id),
                name: String(c.name || ''),
                projectId: Number(c.projectId)
              }));
            setCategories(validCategories);
          }
          // Also load controller actions if available
          if (response.data.controllerActions) {
            setControllerActions(response.data.controllerActions);
          }
        }
      } else {
        // For create, use the dedicated projects endpoint
        const [projectsRes, categoriesRes, actionsRes] = await Promise.all([
          apiClient.get('/AdminApi/projects'),
          apiClient.get('/AdminApi/categories'),
          apiClient.get('/AdminApi/controller-actions')
        ]);
        
        // Load projects
        if (projectsRes.data.success && projectsRes.data.data) {
          // Ensure projects is an array
          const projectsArray = Array.isArray(projectsRes.data.data) 
            ? projectsRes.data.data 
            : [];
          setProjects(projectsArray);
        } else {
          console.warn('Failed to load projects:', projectsRes.data);
          setProjects([]);
        }
        
        // Load categories from accessible projects
        if (categoriesRes.data.success && categoriesRes.data.data) {
          // The API returns { id, name, projectId } - ensure all values are valid
          const validCategories = categoriesRes.data.data
            .filter(c => c && c.id !== undefined && c.id !== null && c.name)
            .map(c => ({
              id: Number(c.id),
              name: String(c.name || ''),
              projectId: Number(c.projectId)
            }));
          setCategories(validCategories);
        } else {
          console.warn('Failed to load categories:', categoriesRes.data);
          setCategories([]);
        }
        
        // Load controller actions
        if (actionsRes.data.success && actionsRes.data.data) {
          setControllerActions(actionsRes.data.data);
        } else {
          console.warn('Failed to load controller actions:', actionsRes.data);
        }
      }
    } catch (error) {
      console.error('Error loading projects:', error);
      setError('Failed to load projects and categories. Please try again.');
      setProjects([]);
      setCategories([]);
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
      hiddenCategoryIds: [],
      accessPermissions: []
    });
    setShowModal(true);
  };

  const handleEdit = async (user) => {
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get(`/AdminApi/users/${user.userId}`);
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
        
        // Load categories from response
        if (response.data.categories) {
          setCategories(response.data.categories);
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
        
        // Ensure projectIds, hiddenCategoryIds and accessPermissions are arrays and properly formatted
        // Note: C# serializes to camelCase, so use lowercase property names
        const projectIds = Array.isArray(userData.projectIds) 
          ? userData.projectIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
          : [];
        const hiddenCategoryIds = Array.isArray(userData.hiddenCategoryIds) 
          ? userData.hiddenCategoryIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
          : [];
        const accessPermissions = Array.isArray(userData.accessPermissions) 
          ? userData.accessPermissions.filter(p => p && typeof p === 'string')
          : [];
        
        setFormData({
          username: userData.username || '',
          fullName: userData.fullName || '',
          email: userData.email || '',
          password: '',
          confirmPassword: '',
          isAdmin: userData.isAdmin || false,
          isActive: userData.isActive !== undefined ? userData.isActive : true,
          projectIds: projectIds,
          hiddenCategoryIds: hiddenCategoryIds,
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
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    // Validate password
    if (!editingUser && !formData.password) {
      setError('Password is required for new users.');
      isSubmittingRef.current = false;
      return;
    }

    if (formData.password && formData.password !== formData.confirmPassword) {
      setError('Passwords do not match.');
      isSubmittingRef.current = false;
      return;
    }

    try {
      setLoading(true);
      // Ensure ProjectIds are numbers
      const projectIds = Array.isArray(formData.projectIds) 
        ? formData.projectIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
        : [];
      // Ensure HiddenCategoryIds are numbers
      const hiddenCategoryIds = Array.isArray(formData.hiddenCategoryIds) 
        ? formData.hiddenCategoryIds.map(id => Number(id)).filter(id => !isNaN(id) && id > 0)
        : [];
      // Ensure AccessPermissions is an array and filter out empty/null values
      const accessPermissions = Array.isArray(formData.accessPermissions) 
        ? formData.accessPermissions.filter(p => p && typeof p === 'string' && p.trim().length > 0)
        : [];
      
      const userData = {
        userId: editingUser?.userId || 0,
        username: formData.username.trim(),
        fullName: formData.fullName.trim(),
        email: (formData.email || '').trim() || null,
        password: formData.password || '',
        confirmPassword: formData.confirmPassword || '',
        isAdmin: formData.isAdmin || false,
        isActive: formData.isActive !== undefined ? formData.isActive : true,
        projectIds: projectIds,
        hiddenCategoryIds: hiddenCategoryIds,
        accessPermissions: accessPermissions
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
          hiddenCategoryIds: [],
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
      isSubmittingRef.current = false;
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
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
      isSubmittingRef.current = false;
    }
  };

  // Handle project selection from SearchableSelect
  const handleProjectSelect = (projectId) => {
    if (!projectId) return;
    const projectIds = [...formData.projectIds];
    const numProjectId = Number(projectId);
    const index = projectIds.findIndex(id => Number(id) === numProjectId);
    if (index === -1) {
      // Add project if not already selected
      projectIds.push(numProjectId);
      setFormData({ ...formData, projectIds });
    }
  };

  // Remove project from selection
  const removeProject = (projectId) => {
    const projectIds = formData.projectIds.filter(id => Number(id) !== Number(projectId));
    setFormData({ ...formData, projectIds });
  };

  // Handle category selection from SearchableSelect
  const handleCategorySelect = (categoryId) => {
    if (!categoryId && categoryId !== 0) return;
    const hiddenCategoryIds = [...formData.hiddenCategoryIds];
    const numCategoryId = Number(categoryId);
    const index = hiddenCategoryIds.findIndex(id => Number(id) === numCategoryId);
    if (index === -1) {
      // Add category if not already selected
      hiddenCategoryIds.push(numCategoryId);
      setFormData({ ...formData, hiddenCategoryIds });
    }
  };

  // Remove category from selection
  const removeCategory = (categoryId) => {
    const hiddenCategoryIds = formData.hiddenCategoryIds.filter(id => Number(id) !== Number(categoryId));
    setFormData({ ...formData, hiddenCategoryIds });
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
    <div className="container-fluid">
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
            hiddenCategoryIds: [],
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
                  onChange={(e) => {
                    const newPassword = e.target.value;
                    // When editing, if password is cleared, also clear confirmPassword
                    if (editingUser && !newPassword) {
                      setFormData({ ...formData, password: newPassword, confirmPassword: '' });
                    } else {
                      setFormData({ ...formData, password: newPassword });
                    }
                  }}
                  required={!editingUser}
                  disabled={loading}
                />
              </Form.Group>
            </div>
          </div>

          {/* Show Confirm Password field for new users OR when editing and password is being changed */}
          {(!editingUser || formData.password) && (
            <Form.Group className="mb-3">
              <Form.Label>Confirm Password</Form.Label>
              <Form.Control
                type="password"
                value={formData.confirmPassword}
                onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                required={!editingUser || !!formData.password}
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
            
            {/* Selected Projects Display */}
            {formData.projectIds.length > 0 && (
              <div className="selected-projects mb-2">
                {formData.projectIds.map((projectId) => {
                  const project = projects.find(p => Number(p.id) === Number(projectId));
                  if (!project) return null;
                  return (
                    <Badge
                      key={projectId}
                      bg="primary"
                      className="me-2 mb-2 project-badge"
                      style={{ fontSize: '0.875rem', padding: '0.5rem 0.75rem' }}
                    >
                      {project.name}
                      <button
                        type="button"
                        className="btn-close btn-close-white ms-2"
                        style={{ fontSize: '0.6rem' }}
                        onClick={() => removeProject(projectId)}
                        disabled={loading}
                        aria-label="Remove"
                      ></button>
                    </Badge>
                  );
                })}
              </div>
            )}

            {/* Project SearchableSelect - filter out already selected projects */}
            <SearchableSelect
              value={null}
              onChange={handleProjectSelect}
              options={projects
                .filter(p => !formData.projectIds.some(id => Number(id) === Number(p.id)))
                .map(p => ({ id: p.id, name: p.name }))}
              placeholder="Search and select projects..."
              disabled={loading || projects.length === 0}
              size="sm"
            />
            {projects.length === 0 && (
              <Form.Text className="text-muted">No projects available</Form.Text>
            )}
          </Form.Group>

          {/* Hidden Categories Selection */}
          <Form.Group className="mb-3">
            <Form.Label>Hide Categories from User</Form.Label>
            <Form.Text className="text-muted d-block mb-2">
              Select categories to hide from this user. Users will see all categories from their accessible projects except those hidden here.
            </Form.Text>
            
            {/* Selected Hidden Categories Display */}
            {formData.hiddenCategoryIds.length > 0 && (
              <div className="selected-projects mb-2">
                {formData.hiddenCategoryIds.map((categoryId) => {
                  const category = categories.find(c => Number(c.id) === Number(categoryId));
                  if (!category) return null;
                  const project = projects.find(p => Number(p.id) === Number(category.projectId));
                  return (
                    <Badge
                      key={categoryId}
                      bg="warning"
                      className="me-2 mb-2 project-badge"
                      style={{ fontSize: '0.875rem', padding: '0.5rem 0.75rem' }}
                    >
                      {category.name}
                      {project && <span className="text-muted ms-1">({project.name})</span>}
                      <button
                        type="button"
                        className="btn-close btn-close-white ms-2"
                        style={{ fontSize: '0.6rem' }}
                        onClick={() => removeCategory(categoryId)}
                        disabled={loading}
                        aria-label="Remove"
                      ></button>
                    </Badge>
                  );
                })}
              </div>
            )}

            {/* Category SearchableSelect - filter out already selected categories and only show categories from accessible projects */}
            <SearchableSelect
              value={null}
              onChange={handleCategorySelect}
              options={categories
                .filter(c => {
                  // Filter out already selected categories
                  const isAlreadySelected = formData.hiddenCategoryIds.some(id => Number(id) === Number(c.id));
                  // Only show categories from selected projects (or all if no projects selected yet)
                  const isFromSelectedProject = formData.projectIds.length === 0 || formData.projectIds.some(pid => Number(pid) === Number(c.projectId));
                  return !isAlreadySelected && isFromSelectedProject;
                })
                .map(c => {
                  const project = projects.find(p => Number(p.id) === Number(c.projectId));
                  return { 
                    id: Number(c.id), // Ensure id is a number
                    name: `${c.name || 'Unknown'}${project ? ` (${project.name})` : ''}` 
                  };
                })}
              placeholder="Search and select categories to hide..."
              disabled={loading || categories.length === 0 || formData.projectIds.length === 0}
              size="sm"
            />
            {categories.length === 0 && (
              <Form.Text className="text-muted">No categories available. Please assign projects first.</Form.Text>
            )}
            {formData.projectIds.length === 0 && (
              <Form.Text className="text-muted">Please assign projects first to see available categories.</Form.Text>
            )}
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
                          <div className="action-checkboxes-container">
                            {controllerActions[controller].map((action) => (
                              <Form.Check
                                key={action}
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
                                className="action-checkbox-item"
                              />
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

