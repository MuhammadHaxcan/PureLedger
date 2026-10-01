import { useState, useEffect, useRef } from 'react';
import { Card, Table, Button, Alert, Form } from 'react-bootstrap';
import apiClient from '../../services/api';
import Modal from '../../components/Modal';
import ConfirmModal from '../../components/ConfirmModal';
import { usePageTitle } from '../../hooks/usePageTitle';

const Locations = () => {
  usePageTitle('Locations');
  const isSubmittingRef = useRef(false);
  const [locations, setLocations] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [editingLocation, setEditingLocation] = useState(null);
  const [formData, setFormData] = useState({ locationName: '' });

  useEffect(() => {
    loadLocations();
  }, []);

  const loadLocations = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get('/MetaDataApi/locations');
      if (response.data.success) {
        setLocations(response.data.data);
      }
    } catch {
      setError('Failed to load locations');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = () => {
    setEditingLocation(null);
    setFormData({ locationName: '' });
    setShowModal(true);
  };

  const handleEdit = async (location) => {
    try {
      const response = await apiClient.get(`/MetaDataApi/locations/${location.locationId}`);
      if (response.data.success) {
        setEditingLocation(response.data.data);
        setFormData({ locationName: response.data.data.locationName });
        setShowModal(true);
      }
    } catch {
      setError('Failed to load location details');
    }
  };

  const handleDelete = (location) => {
    setEditingLocation(location);
    setShowDeleteModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    setError('');
    setSuccess('');

    try {
      const locationData = {
        locationId: editingLocation?.locationId || 0,
        locationName: formData.locationName
      };

      const response = editingLocation
        ? await apiClient.put(`/MetaDataApi/locations/${editingLocation.locationId}`, locationData)
        : await apiClient.post('/MetaDataApi/locations', locationData);

      if (response.data.success) {
        setSuccess(response.data.message);
        setShowModal(false);
        loadLocations();
      }
    } catch (error) {
      const errors = error.response?.data?.errors || [];
      setError(errors.length > 0 ? errors.join(', ') : 'Failed to save location');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
        const response = await apiClient.delete(`/MetaDataApi/locations/${editingLocation.locationId}`);
        if (response.data.success) {
          setSuccess(response.data.message || 'Location deleted successfully');
          setError('');
          setShowDeleteModal(false);
          loadLocations();
        }
    } catch (error) {
        setError(error?.message || error.response?.data?.message || 'Failed to delete location');
    } finally {
      isSubmittingRef.current = false;
    }
  };

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-end align-items-center mb-4">
        <Button variant="primary" onClick={handleCreate}>
          <i className="fas fa-plus me-2"></i>New Location
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
                  <th>Location Name</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {locations.length === 0 ? (
                  <tr>
                    <td colSpan="3" className="text-center text-muted">
                      No locations found
                    </td>
                  </tr>
                ) : (
                  locations.map((location) => (
                    <tr key={location.locationId}>
                      <td>{location.locationId}</td>
                      <td>{location.locationName}</td>
                      <td>
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="me-2"
                          onClick={() => handleEdit(location)}
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(location)}
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
        title={editingLocation ? 'Edit Location' : 'New Location'}
        footer={
          <>
            <Button variant="secondary" onClick={() => setShowModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" onClick={handleSubmit}>
              {editingLocation ? 'Update' : 'Create'}
            </Button>
          </>
        }
      >
        <Form onSubmit={handleSubmit}>
          <Form.Group className="mb-3">
            <Form.Label>Location Name</Form.Label>
            <Form.Control
              type="text"
              value={formData.locationName}
              onChange={(e) => setFormData({ ...formData, locationName: e.target.value })}
              required
              autoFocus
            />
          </Form.Group>
        </Form>
      </Modal>

      <ConfirmModal
        show={showDeleteModal}
        onHide={() => setShowDeleteModal(false)}
        onConfirm={handleConfirmDelete}
        title="Delete Location"
        message={`Are you sure you want to delete "${editingLocation?.locationName}"?`}
      />
    </div>
  );
};

export default Locations;

