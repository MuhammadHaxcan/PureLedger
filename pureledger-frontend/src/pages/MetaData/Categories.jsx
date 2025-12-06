import { useState, useEffect, useMemo, useRef } from 'react';
import { Card, Table, Button, Alert, Form, InputGroup } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const Categories = () => {
  usePageTitle('Categories');
  const isSubmittingRef = useRef(false);
  const [categories, setCategories] = useState([]);
  const [projects, setProjects] = useState([]);
  const [accountTypes, setAccountTypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingCategory, setEditingCategory] = useState(null);
  const [formData, setFormData] = useState({
    categoryName: '',
    projectId: null,
    accountTypeId: null
  });
  const [filterProjectId, setFilterProjectId] = useState(null);

  // Load initial data
  useEffect(() => {
    loadProjects();
    loadAccountTypes();
  }, []);

  // Reload categories when filter changes
  useEffect(() => {
    loadCategories();
  }, [filterProjectId]);

  // Load projects for dropdown
  const loadProjects = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/projects-for-categories');
      if (response.data.success) {
        setProjects(response.data.data);
      }
    } catch (error) {
      console.error('Error loading projects:', error);
      setError(error.message || error.response?.data?.message || 'Failed to load projects. Please refresh the page.');
    }
  };

  // Load account types for dropdown
  const loadAccountTypes = async () => {
    try {
      const response = await apiClient.get('/MetaDataApi/account-types');
      if (response.data.success) {
        setAccountTypes(response.data.data);
      }
    } catch (error) {
      console.error('Error loading account types:', error);
      setError(error.message || error.response?.data?.message || 'Failed to load account types. Please refresh the page.');
    }
  };

  // Load categories list
  const loadCategories = async () => {
    try {
      setLoading(true);
      setError('');
      const params = filterProjectId ? { projectId: filterProjectId } : {};
      const response = await apiClient.get('/MetaDataApi/categories', { params });
      if (response.data.success) {
        setCategories(response.data.data);
      }
    } catch (error) {
      console.error('Error loading categories:', error);
      setError(error.message || error.response?.data?.message || 'Failed to load categories. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  // Create maps for display
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

  // Handle create new category
  const handleCreate = () => {
    setEditingCategory(null);
    setFormData({
      categoryName: '',
      projectId: null,
      accountTypeId: null
    });
    setError('');
    setSuccess('');
    setShowModal(true);
  };

  // Handle edit category
  const handleEdit = async (category) => {
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get(`/MetaDataApi/categories/${category.categoryId}`);
      
      if (response.data.success) {
        const cat = response.data.data;
        setEditingCategory({
          ...cat,
          hasAccountAssociations: response.data.hasAccountAssociations
        });
        // Ensure projectId and accountTypeId are numbers, not strings
        const projectId = cat.projectId ? Number(cat.projectId) : null;
        const accountTypeId = cat.accountTypeId ? Number(cat.accountTypeId) : null;
        setFormData({
          categoryName: cat.categoryName || '',
          projectId: projectId,
          accountTypeId: accountTypeId
        });
        setShowModal(true);
      } else {
        setError('Failed to load category details.');
      }
    } catch (error) {
      console.error('Error loading category:', error);
      setError('Failed to load category details. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  // Handle delete category
  const handleDelete = (category) => {
    setEditingCategory(category);
    setShowDeleteModal(true);
  };

  // Handle form submission (Create/Update)
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    // Client-side validation
    const trimmedName = formData.categoryName?.trim() || '';
    if (!trimmedName) {
      setError('Category name is required.');
      isSubmittingRef.current = false;
      return;
    }

    const projectId = formData.projectId ? Number(formData.projectId) : null;
    const accountTypeId = formData.accountTypeId ? Number(formData.accountTypeId) : null;

    if (!projectId || projectId <= 0 || isNaN(projectId)) {
      setError('Please select a valid project.');
      isSubmittingRef.current = false;
      return;
    }

    if (!accountTypeId || accountTypeId <= 0 || isNaN(accountTypeId)) {
      setError('Please select a valid account type.');
      isSubmittingRef.current = false;
      return;
    }

    // Prepare category data
    const categoryData = {
      categoryId: editingCategory?.categoryId || 0,
      categoryName: trimmedName,
      projectId: projectId,
      accountTypeId: accountTypeId
    };

    try {
      setLoading(true);
      const isEdit = !!editingCategory;
      const url = isEdit 
        ? `/MetaDataApi/categories/${editingCategory.categoryId}`
        : '/MetaDataApi/categories';
      
      const response = isEdit
        ? await apiClient.put(url, categoryData)
        : await apiClient.post(url, categoryData);

      if (response.data.success) {
        setSuccess(response.data.message || (isEdit ? 'Category updated successfully.' : 'Category created successfully.'));
        setShowModal(false);
        setFormData({ categoryName: '', projectId: null, accountTypeId: null });
        setEditingCategory(null);
        await loadCategories();
      } else {
        setError(response.data.message || 'Failed to save category.');
      }
    } catch (error) {
      console.error('Error saving category:', error);
      
      // Handle validation errors from server
      const errorResponse = error.response?.data;
      let errorMessage = 'Failed to save category.';

      if (errorResponse) {
        // Handle array of errors
        if (Array.isArray(errorResponse.errors)) {
          errorMessage = errorResponse.errors.join(', ');
        }
        // Handle object with error properties
        else if (errorResponse.errors && typeof errorResponse.errors === 'object') {
          const errorMessages = [];
          for (const key in errorResponse.errors) {
            if (Array.isArray(errorResponse.errors[key])) {
              errorMessages.push(...errorResponse.errors[key]);
            } else if (typeof errorResponse.errors[key] === 'string') {
              errorMessages.push(errorResponse.errors[key]);
            }
          }
          errorMessage = errorMessages.length > 0 
            ? errorMessages.join(', ') 
            : (errorResponse.message || errorResponse.title || errorMessage);
        }
        // Handle simple message
        else if (errorResponse.message) {
          errorMessage = errorResponse.message;
        }
        // Handle title (ASP.NET Core validation)
        else if (errorResponse.title) {
          errorMessage = errorResponse.title;
        }
      }

      setError(errorMessage);
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  // Handle confirm delete
  const handleConfirmDelete = async () => {
    if (!editingCategory || isSubmittingRef.current) return;
    isSubmittingRef.current = true;

    try {
      setLoading(true);
      setError('');

      // Check if category can be deleted
      const canDeleteRes = await apiClient.get(`/MetaDataApi/categories/${editingCategory.categoryId}/can-delete`);
      
      if (!canDeleteRes.data.canDelete) {
        setError(canDeleteRes.data.message || 'This category cannot be deleted because it is associated with accounts.');
        setShowDeleteModal(false);
        return;
      }

      // Delete the category
      const response = await apiClient.delete(`/MetaDataApi/categories/${editingCategory.categoryId}`);
      
      if (response.data.success) {
        setSuccess(response.data.message || 'Category deleted successfully.');
        setShowDeleteModal(false);
        setEditingCategory(null);
        await loadCategories();
      } else {
        setError(response.data.message || 'Failed to delete category.');
      }
    } catch (error) {
      console.error('Error deleting category:', error);
      const errorMessage = error.response?.data?.message || 'Failed to delete category. Please try again.';
      setError(errorMessage);
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  // Handle modal close
  const handleModalClose = () => {
    setShowModal(false);
    setFormData({ categoryName: '', projectId: null, accountTypeId: null });
    setEditingCategory(null);
    setError('');
    setSuccess('');
  };

  return (
    <div>
      {/* Header */}
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-sitemap me-2"></i>Categories
        </h2>
        <Button variant="primary" onClick={handleCreate} disabled={loading}>
          <i className="fas fa-plus me-2"></i>New Category
        </Button>
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
                  placeholder="Select Project (optional)"
                />
              </InputGroup>
            </div>
          </div>
        </Card.Body>
      </Card>

      {/* Categories Table Card */}
      <Card>
        <Card.Body>
          {loading && !showModal ? (
            <div className="text-center py-4">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
              <p className="mt-2 text-muted">Loading categories...</p>
            </div>
          ) : (
            <Table responsive striped bordered hover>
              <thead className="table-light">
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
                    <td colSpan="5" className="text-center text-muted py-4">
                      <i className="fas fa-inbox fa-2x mb-2 d-block"></i>
                      No categories found{filterProjectId ? ' for the selected project' : ''}.
                    </td>
                  </tr>
                ) : (
                  categories.map((category) => (
                    <tr key={category.categoryId}>
                      <td>{category.categoryId}</td>
                      <td>{category.categoryName}</td>
                      <td>{projectMap.get(category.projectId) || 'N/A'}</td>
                      <td>{accountTypeMap.get(category.accountTypeId) || 'N/A'}</td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(category)}
                          disabled={loading}
                          title="Edit Category"
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(category)}
                          disabled={loading}
                          title="Delete Category"
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

      {/* Create/Edit Modal */}
      <Modal
        show={showModal}
        onHide={handleModalClose}
        title={editingCategory ? 'Edit Category' : 'New Category'}
        footer={
          <>
            <Button variant="secondary" onClick={handleModalClose} disabled={loading}>
              Cancel
            </Button>
            <Button 
              variant="primary" 
              onClick={handleSubmit}
              disabled={loading}
              form="categoryForm"
            >
              {loading ? (
                <>
                  <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                  {editingCategory ? 'Updating...' : 'Creating...'}
                </>
              ) : (
                editingCategory ? 'Update' : 'Create'
              )}
            </Button>
          </>
        }
      >
        {editingCategory?.hasAccountAssociations && (
          <Alert variant="warning" className="mb-3">
            <i className="fas fa-exclamation-triangle me-2"></i>
            <strong>Restricted Edit:</strong> This category is associated with accounts. 
            Only the category name can be changed.
          </Alert>
        )}

        <Form id="categoryForm" onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>
              Category Name <span className="text-danger">*</span>
            </Form.Label>
            <Form.Control
              type="text"
              value={formData.categoryName}
              onChange={(e) => setFormData({ ...formData, categoryName: e.target.value })}
              required
              autoFocus
              disabled={loading}
              placeholder="Enter category name"
            />
          </Form.Group>

          <Form.Group className="mb-3">
            <Form.Label>
              Project <span className="text-danger">*</span>
              {editingCategory?.hasAccountAssociations && (
                <span className="text-muted small ms-2">(Cannot be changed)</span>
              )}
            </Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.projectId}
                onChange={(value) => {
                  const numValue = value !== null && value !== undefined ? Number(value) : null;
                  setFormData({ ...formData, projectId: numValue });
                }}
                options={projects.map(p => ({ id: p.projectId, name: p.projectName }))}
                placeholder="Select Project"
                required
                disabled={editingCategory?.hasAccountAssociations || loading}
              />
            </InputGroup>
            {editingCategory?.hasAccountAssociations && (
              <Form.Text className="text-muted">
                <i className="fas fa-info-circle me-1"></i>
                Project cannot be changed because this category is associated with accounts.
              </Form.Text>
            )}
          </Form.Group>

          <Form.Group className="mb-3">
            <Form.Label>
              Account Type <span className="text-danger">*</span>
              {editingCategory?.hasAccountAssociations && (
                <span className="text-muted small ms-2">(Cannot be changed)</span>
              )}
            </Form.Label>
            <InputGroup>
              <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
              <SearchableSelect
                value={formData.accountTypeId}
                onChange={(value) => {
                  const numValue = value !== null && value !== undefined ? Number(value) : null;
                  setFormData({ ...formData, accountTypeId: numValue });
                }}
                options={accountTypes.map(at => ({ id: at.id, name: at.name }))}
                placeholder="Select Account Type"
                required
                disabled={editingCategory?.hasAccountAssociations || loading}
              />
            </InputGroup>
            {editingCategory?.hasAccountAssociations && (
              <Form.Text className="text-muted">
                <i className="fas fa-info-circle me-1"></i>
                Account Type cannot be changed because this category is associated with accounts.
              </Form.Text>
            )}
          </Form.Group>
        </Form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        show={showDeleteModal}
        onHide={() => {
          setShowDeleteModal(false);
          setEditingCategory(null);
        }}
        onConfirm={handleConfirmDelete}
        title="Delete Category"
        message={`Are you sure you want to delete the category "${editingCategory?.categoryName}"?`}
        confirmText="Delete"
        cancelText="Cancel"
        variant="danger"
        loading={loading}
      />
    </div>
  );
};

export default Categories;
