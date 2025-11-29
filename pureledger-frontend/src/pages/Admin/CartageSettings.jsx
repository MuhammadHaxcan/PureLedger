import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, Form, Button, Alert } from 'react-bootstrap';
import apiClient, { fileToBase64, validateImageFile, base64ToBlobUrl } from '../../services/api';
import SearchableSelect from '../../components/SearchableSelect';

const CartageSettings = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  
  const [projects, setProjects] = useState([]);
  const [categories, setCategories] = useState([]);
  const [forExpenseAccounts, setForExpenseAccounts] = useState([]);
  
  const [formData, setFormData] = useState({
    id: null,
    projectId: null,
    forExpenseCategoryId: null,
    forExpenseAccountId: null,
    cartageTransporterCategoryId: null,
    projectImage: null
  });
  
  const [projectImageFile, setProjectImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);

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
            forExpenseAccountId: response.data.data.forExpenseAccountId || null,
            cartageTransporterCategoryId: response.data.data.cartageTransporterCategoryId || null,
            projectImage: response.data.data.projectImage || null
          });
          
          // Load image preview if exists (now as base64)
          if (response.data.data.projectImage) {
            setImagePreview(response.data.data.projectImage);
          }
          
          // Load accounts if FOR expense account is set
          if (response.data.data.forExpenseAccountId) {
            await loadCategoryForAccount(response.data.data.forExpenseAccountId);
          }
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
        forExpenseAccountId: null,
        cartageTransporterCategoryId: null
      }));
      setForExpenseAccounts([]);
      setImagePreview(null);
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
          forExpenseAccountId: settings.forExpenseAccountId || null,
          cartageTransporterCategoryId: settings.cartageTransporterCategoryId || null,
          projectImage: settings.projectImage || null // Now stores base64 string
        }));
        
        // Load image preview (now as base64)
        if (settings.projectImage) {
          setImagePreview(settings.projectImage);
        } else {
          setImagePreview(null);
        }
        
        // Load accounts if FOR expense account is set
        if (settings.forExpenseAccountId) {
          await loadCategoryForAccount(settings.forExpenseAccountId);
        }
      } else {
        // No existing settings, reset form
        setFormData(prev => ({
          ...prev,
          id: null,
          projectId: projectId,
          forExpenseAccountId: null,
          cartageTransporterCategoryId: null,
          projectImage: null
        }));
        setImagePreview(null);
        setForExpenseAccounts([]);
      }
    } catch (error) {
      console.error('Error loading project data:', error);
      setError('Failed to load project data');
    } finally {
      setLoading(false);
    }
  };

  const loadCategoryForAccount = async (accountId) => {
    try {
      const response = await apiClient.get(`/AdminApi/category-for-account/${accountId}`);
      if (response.data.success && response.data.categoryId) {
        await loadAccountsByCategory(response.data.categoryId);
        setFormData(prev => ({
          ...prev,
          forExpenseCategoryId: response.data.categoryId
        }));
      }
    } catch (error) {
      console.error('Error loading category for account:', error);
    }
  };

  const loadAccountsByCategory = async (categoryId) => {
    if (!categoryId) {
      setForExpenseAccounts([]);
      return;
    }

    try {
      const response = await apiClient.get(`/AdminApi/accounts-by-category/${categoryId}`);
      if (response.data.success) {
        setForExpenseAccounts(response.data.data || []);
      }
    } catch (error) {
      console.error('Error loading accounts:', error);
      setError('Failed to load accounts');
    }
  };

  const handleProjectChange = (projectId) => {
    loadProjectData(projectId);
  };

  const handleForExpenseCategoryChange = (categoryId) => {
    setFormData(prev => ({
      ...prev,
      forExpenseCategoryId: categoryId,
      forExpenseAccountId: null // Clear account when category changes
    }));
    loadAccountsByCategory(categoryId);
  };

  const handleImageChange = async (e) => {
    const file = e.target.files[0];
    if (file) {
      // Validate image file
      const validation = validateImageFile(file, 5); // 5MB max
      if (!validation.valid) {
        setError(validation.error);
        e.target.value = ''; // Clear file input
        return;
      }

      setProjectImageFile(file);
      // Create preview using base64
      try {
        const base64 = await fileToBase64(file);
        setImagePreview(base64);
      } catch (error) {
        console.error('Error reading image file:', error);
        setError('Failed to read image file');
        e.target.value = '';
      }
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    // Validation: At least one of FOR account or Cartage category must be selected
    if (!formData.forExpenseAccountId && !formData.cartageTransporterCategoryId) {
      setError('At least one of FOR Expense Account or Cartage Transporter Category must be selected.');
      return;
    }

    if (!formData.projectId) {
      setError('Project is required.');
      return;
    }

    setLoading(true);

    try {
      // Convert image to base64 if a new file was selected
      let projectImageBase64 = null;
      let projectImageContentType = null;
      
      if (projectImageFile) {
        projectImageBase64 = await fileToBase64(projectImageFile);
        projectImageContentType = projectImageFile.type;
        // Remove data URL prefix if present (backend will handle it)
        if (projectImageBase64.includes(',')) {
          projectImageBase64 = projectImageBase64.split(',')[1];
        }
      } else if (formData.projectImage && formData.projectImage.startsWith('data:')) {
        // If existing image is base64, extract just the base64 part
        projectImageBase64 = formData.projectImage.split(',')[1];
        // Extract content type from data URL
        const match = formData.projectImage.match(/data:([^;]+);base64/);
        projectImageContentType = match ? match[1] : 'image/png';
      }

      // Send as JSON with base64 image
      const submitData = {
        Id: formData.id || 0,
        ProjectId: formData.projectId,
        ForExpenseAccountId: formData.forExpenseAccountId || null,
        CartageTransporterCategoryId: formData.cartageTransporterCategoryId || null,
        ProjectImageBase64: projectImageBase64,
        ProjectImageContentType: projectImageContentType
      };

      const response = await apiClient.post('/AdminApi/project-cartage-settings', submitData);
      
      if (response.data.success) {
        setSuccess(response.data.message || 'Project cartage settings saved successfully');
        // If file was selected, we might need a separate upload endpoint
        // For now, the API might need to be updated to handle file uploads
        setTimeout(() => {
          setSuccess('');
        }, 3000);
      } else {
        setError(response.data.message || 'Failed to save settings');
      }
    } catch (error) {
      console.error('Error saving settings:', error);
      setError(error.response?.data?.message || 'Failed to save cartage settings');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2><i className="fas fa-cog me-2"></i>Project Cartage Settings</h2>
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

      <Card className="shadow mb-4">
        <Card.Header className="py-3">
          <h6 className="m-0 font-weight-bold text-primary">
            <i className="fas fa-truck me-2"></i>Cartage Configuration
          </h6>
        </Card.Header>
        <Card.Body>
          <Form onSubmit={handleSubmit} id="cartage-settings-form">
            <input type="hidden" name="Id" value={formData.id || ''} />
            <input type="hidden" name="ProjectImage" value={formData.projectImage || ''} />

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
                    setFormData(prev => ({ ...prev, forExpenseCategoryId: null, forExpenseAccountId: null }));
                    setForExpenseAccounts([]);
                  }}
                />
              </div>
            </div>

            <div className="mb-3">
              <Form.Label>FOR Expense Account</Form.Label>
              <div className="input-group">
                <span className="input-group-text"><i className="fas fa-file-invoice"></i></span>
                <SearchableSelect
                  value={formData.forExpenseAccountId}
                  onChange={(value) => setFormData(prev => ({ ...prev, forExpenseAccountId: value }))}
                  options={forExpenseAccounts.map(a => ({ id: a.id || a.accountId, name: a.name || a.accountName }))}
                  placeholder="Select Account"
                  disabled={!formData.forExpenseCategoryId || loading}
                  onClear={() => setFormData(prev => ({ ...prev, forExpenseAccountId: null }))}
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

            <div className="mb-3">
              <Form.Label>Project Image</Form.Label>
              <div className="input-group">
                <span className="input-group-text"><i className="fas fa-image"></i></span>
                <Form.Control
                  type="file"
                  accept="image/*"
                  onChange={handleImageChange}
                  disabled={loading}
                />
              </div>
              {imagePreview && (
                <div className="mt-2">
                  <img
                    src={imagePreview}
                    className="img-thumbnail"
                    style={{ maxHeight: '200px' }}
                    alt="Project preview"
                  />
                </div>
              )}
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

