import { useState, useEffect } from 'react';
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
  const [formData, setFormData] = useState({ categoryName: '', projectId: '', accountTypeId: '' });
  const [filterProjectId, setFilterProjectId] = useState(null); // Filter for displaying categories

  useEffect(() => {
    loadProjects();
    loadAccountTypes();
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

  const handleCreate = () => {
    setEditingCategory(null);
    setFormData({ categoryName: '', projectId: '', accountTypeId: '' });
    setShowModal(true);
  };

  const handleEdit = async (category) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/categories/${category.categoryId}`);
      if (response.data.success) {
        const cat = response.data.data;
        setEditingCategory(cat);
        setFormData({
          categoryName: cat.categoryName,
          projectId: cat.projectId?.toString() || '',
          accountTypeId: cat.accountTypeId?.toString() || ''
        });
        setShowModal(true);
      }
    } catch (error) {
      setError('Failed to load category details');
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

    try {
      const categoryData = {
        categoryId: editingCategory?.categoryId || 0,
        categoryName: formData.categoryName,
        projectId: parseInt(formData.projectId),
        accountTypeId: parseInt(formData.accountTypeId)
      };

      const response = editingCategory
        ? await apiClient.put(`/MetaDataApi/categories/${editingCategory.categoryId}`, categoryData)
        : await apiClient.post('/MetaDataApi/categories', categoryData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadData();
      }
    } catch (error) {
      const errors = error.response?.data?.errors || [];
      setError(errors.length > 0 ? errors.join(', ') : 'Failed to save category');
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
                  onChange={(value) => setFilterProjectId(value ? parseInt(value) : null)}
                  options={[{ id: null, name: 'All Projects' }, ...projects.map(p => ({ id: p.projectId, name: p.projectName }))]}
                  placeholder="All Projects"
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
                      <td>{category.projectId}</td>
                      <td>{category.accountTypeId}</td>
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
        <Form onSubmit={handleSubmit}>
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
            <Form.Select
              value={formData.projectId}
              onChange={(e) => setFormData({ ...formData, projectId: e.target.value })}
              required
              disabled={editingCategory?.hasAccountAssociations}
            >
              <option value="">Select Project</option>
              {projects.map((p) => (
                <option key={p.projectId} value={p.projectId}>
                  {p.projectName}
                </option>
              ))}
            </Form.Select>
            {editingCategory?.hasAccountAssociations && (
              <Form.Text className="text-muted">
                Project cannot be changed because this category is associated with accounts.
              </Form.Text>
            )}
          </Form.Group>
          <Form.Group className="mb-3">
            <Form.Label>Account Type</Form.Label>
            <Form.Select
              value={formData.accountTypeId}
              onChange={(e) => setFormData({ ...formData, accountTypeId: e.target.value })}
              required
              disabled={editingCategory?.hasAccountAssociations}
            >
              <option value="">Select Account Type</option>
              {accountTypes.map((at) => (
                <option key={at.id} value={at.id}>
                  {at.name}
                </option>
              ))}
            </Form.Select>
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

