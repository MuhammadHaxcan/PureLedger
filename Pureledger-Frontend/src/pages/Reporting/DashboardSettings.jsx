import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Row, Col, Form, Button, Card, Alert, Spinner } from 'react-bootstrap';
import apiClient from '../../services/api';
import { usePageTitle } from '../../hooks/usePageTitle';
import SearchableSelect from '../../components/SearchableSelect';
import './DashboardSettings.css';

const DashboardSettings = () => {
  usePageTitle('Dashboard Settings');
  const navigate = useNavigate();

  const [projects, setProjects] = useState([]);
  const [selectedProject, setSelectedProject] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Categories available for selection
  const [availableCategories, setAvailableCategories] = useState({
    revenueCategories: [],
    purchaseCategories: [],
    expenseCategories: []
  });

  // Current settings
  const [settings, setSettings] = useState({
    useCustomCalculations: false,
    revenueCategoryIds: [],
    purchaseCategoryIds: [],
    expenseCategoryIds: []
  });

  useEffect(() => {
    loadProjects();
  }, []);

  useEffect(() => {
    if (selectedProject) {
      loadSettingsAndCategories();
    }
  }, [selectedProject]);

  const loadProjects = async () => {
    try {
      const response = await apiClient.get('/ReportingApi/dashboard-init');
      if (response.data.success) {
        setProjects(response.data.projects || []);
        // Auto-select first project if available
        if (response.data.projects?.length > 0) {
          setSelectedProject(response.data.projects[0].id);
        }
      }
    } catch (err) {
      console.error('Error loading projects:', err);
      setError('Failed to load projects');
    }
  };

  const loadSettingsAndCategories = async () => {
    setLoading(true);
    setError('');

    try {
      // Load categories and settings in parallel
      const [categoriesRes, settingsRes] = await Promise.all([
        apiClient.get(`/ReportingApi/dashboard-settings/categories/${selectedProject}`),
        apiClient.get(`/ReportingApi/dashboard-settings/${selectedProject}`)
      ]);

      if (categoriesRes.data.success) {
        setAvailableCategories(categoriesRes.data.data || {
          revenueCategories: [],
          purchaseCategories: [],
          expenseCategories: []
        });
      }

      if (settingsRes.data.success && settingsRes.data.data) {
        setSettings({
          useCustomCalculations: settingsRes.data.data.useCustomCalculations || false,
          revenueCategoryIds: settingsRes.data.data.revenueCategoryIds || [],
          purchaseCategoryIds: settingsRes.data.data.purchaseCategoryIds || [],
          expenseCategoryIds: settingsRes.data.data.expenseCategoryIds || []
        });
      } else {
        // Reset to defaults if no settings exist
        setSettings({
          useCustomCalculations: false,
          revenueCategoryIds: [],
          purchaseCategoryIds: [],
          expenseCategoryIds: []
        });
      }
    } catch (err) {
      console.error('Error loading settings:', err);
      setError('Failed to load settings');
    } finally {
      setLoading(false);
    }
  };

  const handleCategoryToggle = (type, categoryId) => {
    setSettings(prev => {
      const key = `${type}CategoryIds`;
      const currentIds = prev[key] || [];
      const newIds = currentIds.includes(categoryId)
        ? currentIds.filter(id => id !== categoryId)
        : [...currentIds, categoryId];
      return { ...prev, [key]: newIds };
    });
  };

  const handleSave = async () => {
    if (!selectedProject) {
      setError('Please select a project');
      return;
    }

    setSaving(true);
    setError('');
    setSuccess('');

    try {
      const response = await apiClient.post('/ReportingApi/dashboard-settings', {
        projectId: selectedProject,
        useCustomCalculations: settings.useCustomCalculations,
        revenueCategoryIds: settings.revenueCategoryIds,
        purchaseCategoryIds: settings.purchaseCategoryIds,
        expenseCategoryIds: settings.expenseCategoryIds
      });

      if (response.data.success) {
        setSuccess('Settings saved successfully');
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(response.data.message || 'Failed to save settings');
      }
    } catch (err) {
      console.error('Error saving settings:', err);
      setError(err.response?.data?.message || 'Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  const getFormulaPreview = () => {
    const getNames = (ids, categories) => {
      return ids.map(id => {
        const cat = categories.find(c => c.categoryId === id);
        return cat?.categoryName || `Cat ${id}`;
      });
    };

    const revenueNames = getNames(settings.revenueCategoryIds, availableCategories.revenueCategories);
    const purchaseNames = getNames(settings.purchaseCategoryIds, availableCategories.purchaseCategories);
    const expenseNames = getNames(settings.expenseCategoryIds, availableCategories.expenseCategories);

    if (!settings.useCustomCalculations) {
      return 'Net Profit = Sum(Credits on Products) - Sum(Debits on Products)';
    }

    const revenuePart = revenueNames.length > 0 ? revenueNames.join(' + ') : '0';
    const purchasePart = purchaseNames.length > 0 ? purchaseNames.join(' + ') : '0';
    const expensePart = expenseNames.length > 0 ? ' - ' + expenseNames.join(' - ') : '';

    return `Net Profit = (${revenuePart}) - (${purchasePart})${expensePart}`;
  };

  const renderCategoryCheckboxes = (categories, selectedIds, type, title, icon, description) => (
    <Card className="mb-3 category-card">
      <Card.Header className={`category-header ${type}`}>
        <i className={`fas ${icon} me-2`}></i>
        {title}
      </Card.Header>
      <Card.Body>
        <p className="text-muted small mb-3">{description}</p>
        {categories.length === 0 ? (
          <p className="text-muted">No categories available</p>
        ) : (
          <div className="category-grid">
            {categories.map(cat => (
              <Form.Check
                key={cat.categoryId}
                type="checkbox"
                id={`${type}-${cat.categoryId}`}
                label={cat.categoryName}
                checked={selectedIds.includes(cat.categoryId)}
                onChange={() => handleCategoryToggle(type, cat.categoryId)}
                disabled={!settings.useCustomCalculations}
                className="category-checkbox"
              />
            ))}
          </div>
        )}
      </Card.Body>
    </Card>
  );

  return (
    <div className="container-fluid dashboard-settings-container">
      {/* Header */}
      <div className="d-flex flex-wrap justify-content-between align-items-center mb-4">
        <div>
          <h2 className="mb-1">
            <i className="fas fa-cog me-2 text-primary"></i>
            Dashboard Settings
          </h2>
          <p className="text-muted mb-0">
            Customize how dashboard KPIs and trends are calculated
          </p>
        </div>
        <div className="d-flex gap-2">
          <Button variant="outline-secondary" onClick={() => navigate('/reporting/dashboard')}>
            <i className="fas fa-arrow-left me-2"></i>Back to Dashboard
          </Button>
        </div>
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

      {/* Project Selection */}
      <Card className="mb-4">
        <Card.Body>
          <Row className="align-items-center">
            <Col md={3}>
              <Form.Label className="fw-bold mb-0">Select Project</Form.Label>
            </Col>
            <Col md={6}>
              <SearchableSelect
                value={selectedProject}
                onChange={(value) => setSelectedProject(value ? parseInt(value) : null)}
                options={projects.map(p => ({ id: p.id, name: p.name }))}
                placeholder="Select a project..."
                disabled={loading}
              />
            </Col>
          </Row>
        </Card.Body>
      </Card>

      {loading ? (
        <div className="text-center py-5">
          <Spinner animation="border" variant="primary" />
          <p className="mt-2 text-muted">Loading settings...</p>
        </div>
      ) : selectedProject ? (
        <>
          {/* Enable Custom Calculations Toggle */}
          <Card className="mb-4">
            <Card.Body>
              <Form.Check
                type="switch"
                id="useCustomCalculations"
                label={
                  <span className="fw-bold">
                    <i className="fas fa-calculator me-2"></i>
                    Enable Custom Calculations
                  </span>
                }
                checked={settings.useCustomCalculations}
                onChange={(e) => setSettings(prev => ({ ...prev, useCustomCalculations: e.target.checked }))}
                className="custom-switch-lg"
              />
              <p className="text-muted small mt-2 mb-0">
                When enabled, dashboard KPIs will use the category selections below instead of default product-based calculations.
              </p>
            </Card.Body>
          </Card>

          {/* Category Selections */}
          <Row>
            <Col lg={4}>
              {renderCategoryCheckboxes(
                availableCategories.revenueCategories || [],
                settings.revenueCategoryIds,
                'revenue',
                'Revenue Categories',
                'fa-dollar-sign',
                'Credits from these categories = Total Revenue'
              )}
            </Col>
            <Col lg={4}>
              {renderCategoryCheckboxes(
                availableCategories.purchaseCategories || [],
                settings.purchaseCategoryIds,
                'purchase',
                'Purchase/COGS Categories',
                'fa-shopping-cart',
                'Debits from these categories = Total Purchases'
              )}
            </Col>
            <Col lg={4}>
              {renderCategoryCheckboxes(
                availableCategories.expenseCategories || [],
                settings.expenseCategoryIds,
                'expense',
                'Expense Categories',
                'fa-receipt',
                'Debits from these categories = Expenses (subtracted from profit)'
              )}
            </Col>
          </Row>

          {/* Formula Preview */}
          <Card className="mb-4 formula-card">
            <Card.Header>
              <i className="fas fa-function me-2"></i>
              Formula Preview
            </Card.Header>
            <Card.Body>
              <code className="formula-text">{getFormulaPreview()}</code>
            </Card.Body>
          </Card>

          {/* Save Button */}
          <div className="d-flex justify-content-end gap-2">
            <Button variant="outline-secondary" onClick={() => navigate('/reporting/dashboard')}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSave} disabled={saving}>
              {saving ? (
                <>
                  <Spinner animation="border" size="sm" className="me-2" />
                  Saving...
                </>
              ) : (
                <>
                  <i className="fas fa-save me-2"></i>
                  Save Settings
                </>
              )}
            </Button>
          </div>
        </>
      ) : (
        <Card>
          <Card.Body className="text-center py-5">
            <i className="fas fa-project-diagram fa-3x text-muted mb-3"></i>
            <p className="text-muted">Please select a project to configure dashboard settings</p>
          </Card.Body>
        </Card>
      )}
    </div>
  );
};

export default DashboardSettings;
