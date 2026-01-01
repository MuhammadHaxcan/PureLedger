import { useState, useEffect, useRef } from 'react';
import { Card, Table, Button, Alert, Form } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';

const Projects = () => {
  usePageTitle('Projects');
  const isSubmittingRef = useRef(false);
  const [projects, setProjects] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingProject, setEditingProject] = useState(null);
  const [formData, setFormData] = useState({ projectName: '' });

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
    } catch (error) {
      setError('Failed to load projects');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingProject(null);
    setFormData({ projectName: '' });
    setShowModal(true);
  };

  const handleEdit = (project) => {
    setEditingProject(project);
    setFormData({ projectName: project.projectName });
    setShowModal(true);
  };

  const handleDelete = (project) => {
    setEditingProject(project);
    setShowDeleteModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    try {
      const projectData = {
        projectId: editingProject?.projectId || 0,
        projectName: formData.projectName,
      };

      const response = editingProject
        ? await apiClient.put(`/MetaDataApi/projects/${editingProject.projectId}`, projectData)
        : await apiClient.post('/MetaDataApi/projects', projectData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadProjects();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to save project');
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
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-project-diagram me-2"></i>Projects
        </h2>
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Project
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
                          onClick={() => handleEdit(project)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
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
        onHide={() => setShowModal(false)}
        title={editingProject ? 'Edit Project' : 'New Project'}
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingProject ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>Project Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.projectName}
              onChange={(e) => setFormData({ ...formData, projectName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
        </Form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <ConfirmModal
        show={showDeleteModal}
        onHide={() => setShowDeleteModal(false)}
        onConfirm={handleConfirmDelete}
        title="Delete Project"
        message={`Are you sure you want to delete "${editingProject?.projectName}"? This action cannot be undone.`}
      />
    </div>
  );
};

export default Projects;

