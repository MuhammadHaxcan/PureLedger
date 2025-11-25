import { useState, useEffect, useMemo } from 'react';
import { Card, Table, Button, Alert, Form, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import SearchableSelect from '../../components/SearchableSelect';

const Categories = () => {
  const [categories, setCategories] = useState([]);
  const [projects, setProjects] = useState([]);
  const [accountTypes, setAccountTypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingCategory, setEditingCategory] = useState(null);
  const initialFormState = { categoryName: '', projectId: null, accountTypeId: null };
  const [formData, setFormData] = useState(initialFormState);
  const [filterProjectId, setFilterProjectId] = useState(null); // Filter for displaying categories

  useEffect(() => {
    loadProjects();
    loadAccountTypes();
  }, []);

  useEffect(() => {
    loadCategories();
  }, [filterProjectId]);

  const loadProjects = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/projects-for-categories');
      if (response.data.success) {
        setProjects(response.data.data);
      }
    } catch (error) {
      setError('Failed to load projects');
    }
  };

  const loadAccountTypes = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/account-types');
      if (response.data.success) {
        setAccountTypes(response.data.data);
      }
    } catch (error) {
      setError('Failed to load account types');
    }
  };

  const loadCategories = async () => {
    try {
      setLoading(true);
      const params = filterProjectId ? { projectId: filterProjectId } : {};
      const response = await apiClient.get('/MetaDataApi/categories', { params });
      if (response.data.success) {
        setCategories(response.data.data);
      }
    } catch (error) {
      setError('Failed to load categories');
    } finally {
      setLoading(false);
    }
  };

  const loadData = async () => {
    await loadCategories();
  };

  const projectMap = useMemo(() => {
    const map = new Map();
    projects.forEach(project => map.set(project.projectId, project.projectName));
    return map;
  }, [projects]);

  const accountTypeMap = useMemo(() => {
    const map = new Map();
    accountTypes.forEach(type => map.set(type.id, type.name));
    return map;
  }, [accountTypes]);

  const handleCreate = () => {
    setEditingCategory(null);
    setFormData({ ...initialFormState });
    setShowModal(true);
  };

  const handleEdit = async (category) => {
    try {
      setLoading(true);
      const response = await apiClient.get(`/MetaDataApi/categories/${category.categoryId}`);
      if (response.data.success) {
        const cat = response.data.data;
        setEditingCategory({ ...cat, hasAccountAssociations: response.data.hasAccountAssociations });
        setFormData({
          categoryName: cat.categoryName,
          projectId: cat.projectId ?? null,
          accountTypeId: cat.accountTypeId ?? null
        });
        // Ensure projects and account types are loaded
        if (projects.length === 0) {
          await loadProjects();
        }
        if (accountTypes.length === 0) {
          await loadAccountTypes();
        }
        setShowModal(true);
      }
    } catch (error) {
      setError('Failed to load category details');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = (category) => {
    setEditingCategory(category);
    setShowDeleteModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    const trimmedName = formData.categoryName.trim();
    if (!trimmedName) {
      setError('Category name is required');
      return;
    }

    // Convert and validate projectId
    let projId = null;
    if (formData.projectId) {
      if (typeof formData.projectId === 'string') {
        projId = parseInt(formData.projectId, 10);
      } else {
        projId = Number(formData.projectId);
      }
    }

    // Convert and validate accountTypeId
    let accTypeId = null;
    if (formData.accountTypeId) {
      if (typeof formData.accountTypeId === 'string') {
        accTypeId = parseInt(formData.accountTypeId, 10);
      } else {
        accTypeId = Number(formData.accountTypeId);
      }
    }

    // Validate before sending
    if (!trimmedName || trimmedName.length === 0) {
      setError('Category name is required');
      return;
    }
    if (!projId || projId <= 0 || isNaN(projId)) {
      setError('Please select a valid project');
      return;
    }
    if (!accTypeId || accTypeId <= 0 || isNaN(accTypeId)) {
      setError('Please select a valid account type');
      return;
    }

    // Ensure values are properly formatted
    const categoryId = editingCategory?.categoryId ? parseInt(editingCategory.categoryId, 10) : 0;

    // Send data with camelCase - ASP.NET Core model binding handles both camelCase and PascalCase
    const categoryData = {
      categoryId: categoryId,
      categoryName: trimmedName,
      projectId: projId,
      accountTypeId: accTypeId
    };

    try {
      const response = editingCategory
        ? await apiClient.put(`/MetaDataApi/categories/${categoryId}`, categoryData)
        : await apiClient.post('/MetaDataApi/categories', categoryData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        setFormData({ ...initialFormState });
        loadData();
      } else {
        setError(response.data.message || 'Failed to save category');
      }
    } catch (error) {
      console.error('Category save error:', error);
      console.error('Error response:', error.response?.data);
      console.error('Request data sent:', categoryData);
      console.error('Full error details:', JSON.stringify(error.response?.data, null, 2));
      
      // Handle ASP.NET Core validation errors
      const validationErrors = error.response?.data?.errors;
      let errorMessage = '';
      
      if (validationErrors && typeof validationErrors === 'object' && !Array.isArray(validationErrors)) {
        // ASP.NET Core returns errors as an object with property names as keys
        // Each key maps to an array of error messages
        const errorMessages = [];
        for (const key in validationErrors) {
          if (Array.isArray(validationErrors[key])) {
            errorMessages.push(...validationErrors[key]);
          } else if (typeof validationErrors[key] === 'string') {
            errorMessages.push(validationErrors[key]);
          } else {
            // Handle nested objects
            errorMessages.push(`${key}: ${JSON.stringify(validationErrors[key])}`);
          }
        }
        errorMessage = errorMessages.length > 0 
          ? errorMessages.join(', ') 
          : (error.response?.data?.title || 'Validation failed');
      } else if (Array.isArray(error.response?.data?.errors)) {
        errorMessage = error.response.data.errors.join(', ');
      } else if (error.response?.data?.message) {
        errorMessage = error.response.data.message;
      } else if (error.response?.data?.title) {
        errorMessage = error.response.data.title;
      } else {
        errorMessage = error.message || 'Failed to save category';
      }
      
      setError(errorMessage);
    }
  };

  const handleConfirmDelete = async () => {
    try {
      // Check if can delete first
      const canDeleteRes = await apiClient.get(`/MetaDataApi/categories/${editingCategory.categoryId}/can-delete`);
      if (!canDeleteRes.data.canDelete) {
        setError(canDeleteRes.data.message);
        setShowDeleteModal(false);
        return;
      }

      const response = await apiClient.delete(`/MetaDataApi/categories/${editingCategory.categoryId}`);
      if (response.data.success) {
        setSuccess(response.data.message);
        setShowDeleteModal(false);
        loadData();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to delete category');
    }
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-sitemap me-2"></i>Categories
        </h2>
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Category
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
            <i className="fas fa-filter me-2"></i>Filter by Project
          </h6>
        </Card.Header>
        <Card.Body>
          <div className="row align-items-end">
            <div className="col-md-6">
              <Form.Label>Project</Form.Label>
              <InputGroup>
                <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                <SearchableSelect
                  value={filterProjectId}
                  onChange={(value) => setFilterProjectId(value ?? null)}
                  options={projects.map(p => ({ id: p.projectId, name: p.projectName }))}
                  placeholder="Select Project"
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
                  <th>Category Name</th>
                  <th>Project</th>
                  <th>Account Type</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {categories.length === 0 ? (
                  <tr>
                    <td colSpan="5" className="text-center text-muted">
                      No categories found
                    </td>
                  </tr>
                ) : (
                  categories.map((category) => (
                    <tr key={category.categoryId}>
                      <td>{category.categoryId}</td>
                      <td>{category.categoryName}</td>
                      <td>{projectMap.get(category.projectId) || category.projectId}</td>
                      <td>{accountTypeMap.get(category.accountTypeId) || category.accountTypeId}</td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(category)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(category)}
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
        title={editingCategory ? 'Edit Category' : 'New Category'}
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingCategory ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form id="categoryForm" onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>Category Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.categoryName}
              onChange={(e) => setFormData({ ...formData, categoryName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Project</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.projectId}
                onChange={(value) => setFormData({ ...formData, projectId: value ?? null })}
                options={projects.map(p => ({ id: p.projectId, name: p.projectName }))}
                placeholder="Select Project"
                required
                disabled={editingCategory?.hasAccountAssociations || loading}
              />
            </InputGroup>
            {editingCategory?.hasAccountAssociations && (
              <Form.Text className="text-muted">
                Project cannot be changed because this category is associated with accounts.
              </Form.Text>
            )}
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Account Type</Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.accountTypeId}
                onChange={(value) => setFormData({ ...formData, accountTypeId: value ?? null })}
                options={accountTypes.map(at => ({ id: at.id, name: at.name }))}
                placeholder="Select Account Type"
                required
                disabled={editingCategory?.hasAccountAssociations || loading}
              />
            </InputGroup>
            {editingCategory?.hasAccountAssociations && (
              <Form.Text className="text-muted">
                Account Type cannot be changed because this category is associated with accounts.
              </Form.Text>
            )}
          </Form.Group>
        </Form>
      </Modal>

      <ConfirmModal
        show={showDeleteModal}
        onHide={() => setShowDeleteModal(false)}
        onConfirm={handleConfirmDelete}
        title="Delete Category"
        message={`Are you sure you want to delete "${editingCategory?.categoryName}"?`}
      />
    </div>
  );
};

export default Categories;

