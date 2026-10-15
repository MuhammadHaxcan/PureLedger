import { useState, useEffect, useRef } from 'react';
import { Card, Table, Button, Alert, Form, Badge } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';

const Bookers = () => {
  usePageTitle('Bookers');
  const isSubmittingRef = useRef(false);
  const [bookers, setBookers] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingBooker, setEditingBooker] = useState(null);
  const [formData, setFormData] = useState({ bookerName: '' });

  useEffect(() => {
    loadBookers();
  }, []);

  const loadBookers = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get('/MetaDataApi/bookers');
      if (response.data.success) {
        setBookers(response.data.data);
      }
    } catch {
      setError('Failed to load bookers');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingBooker(null);
    setFormData({ bookerName: '' });
    setShowModal(true);
  };

  const handleEdit = async (booker) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/bookers/${booker.bookerID}`);
      if (response.data.success) {
        setEditingBooker(response.data.data);
        setFormData({ bookerName: response.data.data.bookerName });
        setShowModal(true);
      }
    } catch {
      setError('Failed to load booker details');
    }
  };

  const handleDelete = (booker) => {
    setEditingBooker(booker);
    setShowDeleteModal(true);
  };

  const handleToggleStatus = async (booker) => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
      const response = await apiClient.post(`/MetaDataApi/bookers/${booker.bookerID}/toggle-status`);
      if (response.data.success) {
        setSuccess(response.data.message);
        loadBookers();
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to toggle booker status');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    try {
      const bookerData = {
        bookerID: editingBooker?.bookerID || 0,
        bookerName: formData.bookerName
      };

      const response = editingBooker
        ? await apiClient.put(`/MetaDataApi/bookers/${editingBooker.bookerID}`, bookerData)
        : await apiClient.post('/MetaDataApi/bookers', bookerData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadBookers();
      }
    } catch (error) {
      const errors = error.response?.data?.errors || [];
      setError(errors.length > 0 ? errors.join(', ') : 'Failed to save booker');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
      const response = await apiClient.delete(`/MetaDataApi/bookers/${editingBooker.bookerID}`);
      if (response.data.success) {
        setSuccess(response.data.message || 'Booker deleted successfully');
        setError('');
        setShowDeleteModal(false);
        loadBookers();
      }
    } catch (error) {
      setError(error?.message || error.response?.data?.message || 'Failed to delete booker');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-end align-items-center mb-4">
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Booker
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
                  <th>Booker Name</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {bookers.length === 0 ? (
                  <tr>
                    <td colSpan="4" className="text-center text-muted">
                      No bookers found
                    </td>
                  </tr>
                ) : (
                  bookers.map((booker) => (
                    <tr key={booker.bookerID}>
                      <td>{booker.bookerID}</td>
                      <td>{booker.bookerName}</td>
                      <td>
                        <Badge bg={booker.isEnabled ? 'success' : 'secondary'}>
                          {booker.isEnabled ? 'Enabled' : 'Disabled'}
                        </Badge>
                      </td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(booker)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant={booker.isEnabled ? 'outline-warning' : 'outline-success'}
                          size="sm"
                          className="me-2"
                          onClick={() => handleToggleStatus(booker)}
                          title={booker.isEnabled ? 'Disable' : 'Enable'}
                        >
                          <i className={`fas fa-${booker.isEnabled ? 'ban' : 'check'}`}></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(booker)}
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
        onHide={() => { setShowModal(false); setError(''); }}
        error={error}
        onErrorDismiss={() => setError('')}
        title={editingBooker ? 'Edit Booker' : 'New Booker'}
        footer={
          <>
            <Button variant="secondary" onClick={() => { setShowModal(false); setError(''); }}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingBooker ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>Booker Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.bookerName}
              onChange={(e) => setFormData({ ...formData, bookerName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
        </Form>
      </Modal>

      <ConfirmModal
        show={showDeleteModal}
        onHide={() => { setShowDeleteModal(false); setError(''); }}
        error={error}
        onErrorDismiss={() => setError('')}
        onConfirm={handleConfirmDelete}
        title="Delete Booker"
        message={`Are you sure you want to delete "${editingBooker?.bookerName}"?`}
      />
    </div>
  );
};

export default Bookers;

