import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, Form, Button, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';
import { usePageTitle } from '../../hooks/usePageTitle';

const CartageSettings = () => {
  usePageTitle('Cartage Settings', 'Project Cartage Settings');
  const navigate = useNavigate();
  const isSubmittingRef = useRef(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  
  const [formData, setFormData] = useState({
    id: null,
    projectId: null,
    forExpenseCategoryId: null,
    cartageTransporterCategoryId: null
  });

  useEffect(() => {
    loadInitialData();
  }, []);

  const loadInitialData = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get('/AdminApi/project-cartage-settings');
      if (response.data.success) {
        setProjects(response.data.projects || []);
        setCategories(response.data.categories || []);
        
        // If there's existing data, populate form
        if (response.data.data && response.data.data.projectId) {
          setFormData({
            id: response.data.data.id || null,
            projectId: response.data.data.projectId,
            forExpenseCategoryId: response.data.data.forExpenseCategoryId || null,
            cartageTransporterCategoryId: response.data.data.cartageTransporterCategoryId || null
          });
        }
      }
    } catch (error) {
      console.error('Error loading initial data:', error);
      setError('Failed to load cartage settings data');
    } finally {
      setLoading(false);
    }
  };

  const loadProjectData = async (projectId) => {
    if (!projectId) {
      setFormData(prev => ({
        ...prev,
        projectId: null,
        forExpenseCategoryId: null,
        cartageTransporterCategoryId: null
      }));
      return;
    }

    try {
      setLoading(true);
      
      // Load categories for this project
      const categoriesRes = await apiClient.get(`/AdminApi/categories-by-project/${projectId}`);
      if (categoriesRes.data.success) {
        setCategories(categoriesRes.data.data || []);
      }
      
      // Load existing settings for this project
      const settingsRes = await apiClient.get(`/AdminApi/project-cartage-settings-by-project/${projectId}`);
      if (settingsRes.data.success && settingsRes.data.data) {
        const settings = settingsRes.data.data;
        setFormData(prev => ({
          ...prev,
          id: settings.id || null,
          projectId: settings.projectId,
          forExpenseCategoryId: settings.forExpenseCategoryId || null,
          cartageTransporterCategoryId: settings.cartageTransporterCategoryId || null
        }));
      } else {
        // No existing settings, reset form
        setFormData(prev => ({
          ...prev,
          id: null,
          projectId: projectId,
          forExpenseCategoryId: null,
          cartageTransporterCategoryId: null
        }));
      }
    } catch (error) {
      console.error('Error loading project data:', error);
      setError('Failed to load project data');
    } finally {
      setLoading(false);
    }
  };

  const handleProjectChange = (projectId) => {
    loadProjectData(projectId);
  };

  const handleForExpenseCategoryChange = (categoryId) => {
    setFormData(prev => ({
      ...prev,
      forExpenseCategoryId: categoryId
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    if (!formData.projectId) {
      setError('Project is required.');
      isSubmittingRef.current = false;
      return;
    }

    setLoading(true);

    try {
      const toNumberOrNull = (value) => {
        if (value === null || value === undefined || value === '') {
          return null;
        }
        const num = Number(value);
        return isNaN(num) ? null : num;
      };
      
      const forExpenseCategoryId = toNumberOrNull(formData.forExpenseCategoryId);
      const cartageTransporterCategoryId = toNumberOrNull(formData.cartageTransporterCategoryId);
      
      if (forExpenseCategoryId === null && cartageTransporterCategoryId === null) {
        setError('At least one of FOR Expense Category or Cartage Transporter Category must be selected.');
        setLoading(false);
        isSubmittingRef.current = false;
        return;
      }
      
      const submitData = {
        id: formData.id ?? 0,
        projectId: Number(formData.projectId),
        forExpenseCategoryId: forExpenseCategoryId,
        cartageTransporterCategoryId: cartageTransporterCategoryId
      };

      const response = await apiClient.post('/AdminApi/project-cartage-settings', submitData);
      
      if (response.data.success) {
        setSuccess(response.data.message || 'Project cartage settings saved successfully');
        setTimeout(() => {
          setSuccess('');
        }, 3000);
      } else {
        setError(response.data.message || 'Failed to save settings');
      }
    } catch (error) {
      // Extract error message from response
      let errorMessage = 'Failed to save cartage settings';
      if (error.response?.data) {
        if (error.response.data.errors && Array.isArray(error.response.data.errors) && error.response.data.errors.length > 0) {
          // Join all error messages
          errorMessage = error.response.data.errors.join('. ');
        } else if (error.response.data.message) {
          errorMessage = error.response.data.message;
        } else if (typeof error.response.data === 'string') {
          errorMessage = error.response.data;
        }
      } else if (error.message) {
        errorMessage = error.message;
      }
      
      setError(errorMessage);
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  return (
    <div className="container-fluid">
      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          <i className="fas fa-exclamation-circle me-2"></i>
          <div>
            {error.split('. ').map((err, idx) => (
              <div key={idx}>{err}</div>
            ))}
          </div>
        </Alert>
      )}

      {success && (
        <Alert variant="success" dismissible onClose={() => setSuccess('')}>
          <i className="fas fa-check-circle me-2"></i>{success}
        </Alert>
      )}

      <Card className="shadow mb-4">
        <Card.Header className="py-3">
          <h6 className="m-0 font-weight-bold text-primary">
            <i className="fas fa-truck me-2"></i>Cartage Configuration
          </h6>
        </Card.Header>
        <Card.Body>
          <Form onSubmit={handleSubmit} id="cartage-settings-form">
            <input type="hidden" name="Id" value={formData.id || ''} />

            <div className="mb-3">
              <Form.Label>Project <span className="text-danger">*</span></Form.Label>
              <div className="input-group">
                <span className="input-group-text"><i className="fas fa-project-diagram"></i></span>
                <SearchableSelect
                  value={formData.projectId}
                  onChange={handleProjectChange}
                  options={projects.map(p => ({ id: p.id, name: p.name }))}
                  placeholder="Select Project"
                  required
                  disabled={loading}
                />
              </div>
            </div>

            <div className="mb-3">
              <Form.Label>FOR Expense Category</Form.Label>
              <div className="input-group">
                <span className="input-group-text"><i className="fas fa-list"></i></span>
                <SearchableSelect
                  value={formData.forExpenseCategoryId}
                  onChange={handleForExpenseCategoryChange}
                  options={categories.map(c => ({ id: c.id || c.categoryId, name: c.name || c.categoryName }))}
                  placeholder="Select Category"
                  disabled={!formData.projectId || loading}
                  onClear={() => {
                    setFormData(prev => ({ ...prev, forExpenseCategoryId: null }));
                  }}
                />
              </div>
            </div>

            <div className="mb-3">
              <Form.Label>Cartage Transporter Category</Form.Label>
              <div className="input-group">
                <span className="input-group-text"><i className="fas fa-truck-loading"></i></span>
                <SearchableSelect
                  value={formData.cartageTransporterCategoryId}
                  onChange={(value) => setFormData(prev => ({ ...prev, cartageTransporterCategoryId: value }))}
                  options={categories.map(c => ({ id: c.id || c.categoryId, name: c.name || c.categoryName }))}
                  placeholder="Select Category"
                  disabled={!formData.projectId || loading}
                  onClear={() => setFormData(prev => ({ ...prev, cartageTransporterCategoryId: null }))}
                />
              </div>
            </div>

            <div className="d-flex justify-content-between mt-4">
              <Button variant="secondary" onClick={() => navigate(-1)}>
                <i className="fas fa-arrow-left me-1"></i> Back
              </Button>
              <Button variant="primary" type="submit" disabled={loading}>
                {loading ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                    Saving...
                  </>
                ) : (
                  <>
                    <i className="fas fa-save me-1"></i> Save Settings
                  </>
                )}
              </Button>
            </div>
          </Form>
        </Card.Body>
      </Card>
    </div>
  );
};

export default CartageSettings;

