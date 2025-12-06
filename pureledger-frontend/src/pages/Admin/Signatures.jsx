import { useState, useEffect, useRef } from 'react';
import { Card, Button, Alert, Modal, Table, Badge } from 'react-bootstrap';
import apiClient from '../../services/api';
import SignatureCanvas from '../../components/SignatureCanvas';
import { usePageTitle } from '../../hooks/usePageTitle';

const Signatures = () => {
  usePageTitle('Signatures');
  const isSubmittingRef = useRef(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [signatures, setSignatures] = useState([]);
  const [showModal, setShowModal] = useState(false);
  const [editingSignature, setEditingSignature] = useState(null);
  const [deletingSignature, setDeletingSignature] = useState(null);

  useEffect(() => {
    loadSignatures();
  }, []);

  const loadSignatures = async () => {
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get('/AdminApi/signatures');
      if (response.data.success) {
        setSignatures(response.data.data || []);
      } else {
        setError('Failed to load signatures');
      }
    } catch (error) {
      console.error('Error loading signatures:', error);
      setError('Failed to load signatures');
    } finally {
      setLoading(false);
    }
  };

  const handleAddNew = () => {
    setEditingSignature(null);
    setShowModal(true);
  };

  const handleEdit = (signature) => {
    setEditingSignature(signature);
    setShowModal(true);
  };

  const handleDelete = async (id) => {
    if (!window.confirm('Are you sure you want to delete this signature?')) {
      return;
    }

    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;

    try {
      setLoading(true);
      setError('');
      const response = await apiClient.delete(`/AdminApi/signatures/${id}`);
      if (response.data.success) {
        setSuccess('Signature deleted successfully');
        await loadSignatures();
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(response.data.message || 'Failed to delete signature');
      }
    } catch (error) {
      console.error('Error deleting signature:', error);
      setError(error.response?.data?.message || 'Failed to delete signature');
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  const handleSetActive = async (id) => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.put(`/AdminApi/signatures/${id}/set-active`);
      if (response.data.success) {
        setSuccess('Active signature updated successfully');
        await loadSignatures();
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(response.data.message || 'Failed to set active signature');
      }
    } catch (error) {
      console.error('Error setting active signature:', error);
      setError(error.response?.data?.message || 'Failed to set active signature');
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  const handleSave = async (name, imageData) => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    
    try {
      setLoading(true);
      setError('');
      
      const payload = {
        name: name,
        signatureImageBase64: imageData,
        isActive: false
      };

      let response;
      if (editingSignature) {
        // Update existing
        payload.id = editingSignature.id;
        response = await apiClient.put(`/AdminApi/signatures/${editingSignature.id}`, payload);
      } else {
        // Create new
        response = await apiClient.post('/AdminApi/signatures', payload);
      }

      if (response.data.success) {
        setSuccess(editingSignature ? 'Signature updated successfully' : 'Signature created successfully');
        setShowModal(false);
        setEditingSignature(null);
        await loadSignatures();
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(response.data.message || 'Failed to save signature');
      }
    } catch (error) {
      console.error('Error saving signature:', error);
      setError(error.response?.data?.message || 'Failed to save signature');
    } finally {
      setLoading(false);
      isSubmittingRef.current = false;
    }
  };

  const handleCancel = () => {
    setShowModal(false);
    setEditingSignature(null);
  };

  const getImageUrl = (base64String) => {
    if (!base64String) return null;
    // If it already has data URL prefix, return as is
    if (base64String.startsWith('data:')) {
      return base64String;
    }
    // Otherwise, add the prefix
    return `data:image/png;base64,${base64String}`;
  };

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>Signature Management</h2>
        <Button variant="primary" onClick={handleAddNew} disabled={loading}>
          <i className="fas fa-plus me-2"></i>
          Add New Signature
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
          {loading && !signatures.length ? (
            <div className="text-center py-5">
              <div className="spinner-border" role="status">
                <span className="visually-hidden">Loading...</span>
              </div>
            </div>
          ) : signatures.length === 0 ? (
            <div className="text-center py-5 text-muted">
              <p>No signatures found. Click "Add New Signature" to create one.</p>
            </div>
          ) : (
            <Table striped bordered hover responsive>
              <thead>
                <tr>
                  <th className="text-center">Name</th>
                  <th className="text-center">Preview</th>
                  <th className="text-center">Status</th>
                  <th className="text-center">Created Date</th>
                  <th className="text-center">Actions</th>
                </tr>
              </thead>
              <tbody>
                {signatures.map((signature) => (
                  <tr key={signature.id}>
                    <td className="align-middle">{signature.name}</td>
                    <td className="align-middle text-center">
                      {signature.signatureImage && (
                        <img
                          src={getImageUrl(signature.signatureImage)}
                          alt={signature.name}
                          style={{
                            maxHeight: '60px',
                            maxWidth: '200px',
                            objectFit: 'contain',
                            backgroundColor: '#f8f9fa',
                            padding: '5px',
                            borderRadius: '4px'
                          }}
                        />
                      )}
                    </td>
                    <td className="align-middle text-center">
                      {signature.isActive ? (
                        <Badge bg="success">
                          <i className="fas fa-check-circle me-1"></i>
                          Active
                        </Badge>
                      ) : (
                        <Badge bg="secondary">
                          <i className="fas fa-times-circle me-1"></i>
                          Inactive
                        </Badge>
                      )}
                    </td>
                    <td className="align-middle text-center">
                      {new Date(signature.createdDate).toLocaleDateString()}
                    </td>
                    <td className="align-middle text-center">
                      <div className="d-flex justify-content-center gap-2">
                        {!signature.isActive && (
                          <Button
                            variant="success"
                            size="sm"
                            onClick={() => handleSetActive(signature.id)}
                            disabled={loading}
                            title="Set as Active"
                          >
                            <i className="fas fa-check-circle"></i>
                          </Button>
                        )}
                        <Button
                          variant="outline-primary"
                          size="sm"
                          onClick={() => handleEdit(signature)}
                          disabled={loading}
                          title="Edit"
                        >
                          <i className="fas fa-edit"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          onClick={() => handleDelete(signature.id)}
                          disabled={loading}
                          title="Delete"
                        >
                          <i className="fas fa-trash"></i>
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </Card.Body>
      </Card>

      <Modal show={showModal} onHide={handleCancel} size="lg" centered>
        <Modal.Header closeButton>
          <Modal.Title>
            {editingSignature ? 'Edit Signature' : 'Add New Signature'}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <SignatureCanvas
            onSave={handleSave}
            onCancel={handleCancel}
            initialImage={editingSignature ? getImageUrl(editingSignature.signatureImage) : null}
            initialName={editingSignature ? editingSignature.name : ''}
            width={600}
            height={250}
          />
        </Modal.Body>
      </Modal>
    </div>
  );
};

export default Signatures;

