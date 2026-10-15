import { Modal, Button, Alert } from 'react-bootstrap';

const ConfirmModal = ({ 
  show, 
  onHide, 
  onConfirm, 
  title, 
  message, 
  confirmText = 'Confirm', 
  cancelText = 'Cancel', 
  variant = 'danger',
  loading = false,
  error = '',
  onErrorDismiss
}) => {
  return (
    <Modal show={show} onHide={onHide}>
      <Modal.Header closeButton>
        <Modal.Title>{title}</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error && (
          <Alert variant="danger" dismissible={!!onErrorDismiss} onClose={onErrorDismiss}>
            <i className="fas fa-exclamation-circle me-2"></i>{error}
          </Alert>
        )}
        <p>{message}</p>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide} disabled={loading}>
          {cancelText}
        </Button>
        <Button variant={variant} onClick={onConfirm} disabled={loading}>
          {loading ? (
            <>
              <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
              {confirmText}
            </>
          ) : (
            confirmText
          )}
        </Button>
      </Modal.Footer>
    </Modal>
  );
};

export default ConfirmModal;

