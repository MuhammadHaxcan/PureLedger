import { useState, useEffect, useRef } from 'react';
import { Card, Table, Button, Alert, Form } from 'react-bootstrap';
import apiClient, { fileToBase64 } from '../../services/api';
import projectImgApi from '../../services/projectImgApi';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';

const Projects = () => {
  usePageTitle('Projects');
  const isSubmittingRef = useRef(false);
  const logoLoadRef = useRef(0);
  const [projects, setProjects] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingProject, setEditingProject] = useState(null);
  const [formData, setFormData] = useState({ projectName: '' });
  const [logoDataUrl, setLogoDataUrl] = useState(null);
  const [logoAction, setLogoAction] = useState('keep');
  const [logoLoading, setLogoLoading] = useState(false);

  useEffect(() => {
    loadProjects();
  }, []);

  const loadProjects = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get('/MetaDataApi/projects');
      if (response.data.success) {
        setProjects(response.data.data);
      }
    } catch {
      setError('Failed to load projects');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    logoLoadRef.current += 1;
    setError('');
    setEditingProject(null);
    setFormData({ projectName: '' });
    setLogoDataUrl(null);
    setLogoAction('keep');
    setLogoLoading(false);
    setShowModal(true);
  };

  const handleEdit = async (project) => {
    const loadId = ++logoLoadRef.current;
    setError('');
    setEditingProject(project);
    setFormData({ projectName: project.projectName });
    setLogoDataUrl(null);
    setLogoAction('keep');
    setLogoLoading(true);
    setShowModal(true);
    try {
      const response = await projectImgApi.get(project.projectId);
      if (logoLoadRef.current === loadId) {
        setLogoDataUrl(response.data?.data?.imageDataUrl || null);
      }
    } catch (err) {
      if (logoLoadRef.current === loadId) {
        setError(err.response?.data?.message || 'Failed to load project logo');
      }
    } finally {
      if (logoLoadRef.current === loadId) setLogoLoading(false);
    }
  };

  const handleLogoChange = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 5 * 1024 * 1024) {
      setError('Choose a PNG or JPEG logo up to 5 MB.');
      event.target.value = '';
      return;
    }
    try {
      setLogoDataUrl(await fileToBase64(file));
      setLogoAction('replace');
      setError('');
    } catch {
      setError('Failed to read the logo file');
    }
  };

  const handleRemoveLogo = () => {
    setLogoDataUrl(null);
    setLogoAction('remove');
  };

  const handleDelete = (project) => {
    setEditingProject(project);
    setShowDeleteModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    if (!formData.projectName.trim()) {
      setError('Project name is required');
      return;
    }
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    let savedProject = null;
    try {
      const projectData = {
        projectId: editingProject?.projectId || 0,
        projectName: formData.projectName,
      };

      const response = editingProject
        ? await apiClient.put(`/MetaDataApi/projects/${editingProject.projectId}`, projectData)
        : await apiClient.post('/MetaDataApi/projects', projectData);

      if (!response.data.success) throw new Error(response.data.message || 'Failed to save project');
      savedProject = response.data.data || { ...projectData, projectId: editingProject?.projectId };
      const projectId = savedProject.projectId;
      if (logoAction === 'replace') await projectImgApi.save(projectId, logoDataUrl);
      if (logoAction === 'remove' && editingProject) await projectImgApi.remove(projectId);
      setSuccess(response.data.message);
      setShowModal(false);
      loadProjects();
    } catch (error) {
      if (savedProject) {
        setEditingProject(savedProject);
        loadProjects();
        setError(`Project saved, but logo update failed: ${error.response?.data?.message || error.message}`);
      } else {
        setError(error.response?.data?.message || error.message || 'Failed to save project');
      }
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
      const response = await apiClient.delete(`/MetaDataApi/projects/${editingProject.projectId}`);
      if (response.data.success) {
        setSuccess(response.data.message);
        setShowDeleteModal(false);
        loadProjects();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to delete project');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-end align-items-center mb-4">
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Project
        </Button>
      </div>

      {error && !showModal && !showDeleteModal && (
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
                  <th>Project Name</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {projects.length === 0 ? (
                  <tr>
                    <td colSpan="3" className="text-center text-muted">
                      No projects found
                    </td>
                  </tr>
                ) : (
                  projects.map((project) => (
                    <tr key={project.projectId}>
                      <td>{project.projectId}</td>
                      <td>{project.projectName}</td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          aria-label={`Edit project ${project.projectName}`}
                          onClick={() => handleEdit(project)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          aria-label={`Delete project ${project.projectName}`}
                          onClick={() => handleDelete(project)}
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
        onHide={() => { logoLoadRef.current += 1; setShowModal(false); setError(''); }}
        error={error}
        onErrorDismiss={() => setError('')}
        title={editingProject ? 'Edit Project' : 'New Project'}
        footer={
          <>
            <Button variant="secondary" onClick={() => { setShowModal(false); setError(''); }}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit} disabled={logoLoading}>
              {editingProject ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <Form.Group className="mb-3" controlId="project-name">
            <Form.Label>Project Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.projectName}
              onChange={(e) => setFormData({ ...formData, projectName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
          <Form.Group className="mb-3" controlId="project-logo">
            <Form.Label>Project Logo</Form.Label>
            <Form.Control type="file" accept="image/png,image/jpeg" onChange={handleLogoChange} disabled={logoLoading} />
            <Form.Text>PNG or JPEG, up to 5 MB.</Form.Text>
          </Form.Group>
          {logoLoading && <div className="text-muted mb-3">Loading logo...</div>}
          {logoDataUrl && (
            <div className="mb-3">
              <img src={logoDataUrl} alt="Project logo preview" className="img-thumbnail d-block mb-2" style={{ maxHeight: 140, maxWidth: 240 }} />
              <Button type="button" variant="outline-danger" size="sm" onClick={handleRemoveLogo}>Remove logo</Button>
            </div>
          )}
        </Form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        show={showDeleteModal}
        onHide={() => { setShowDeleteModal(false); setError(''); }}
        error={error}
        onErrorDismiss={() => setError('')}
        onConfirm={handleConfirmDelete}
        title="Delete Project"
        message={`Are you sure you want to delete "${editingProject?.projectName}"? This action cannot be undone.`}
      />
    </div>
  );
};

export default Projects;

