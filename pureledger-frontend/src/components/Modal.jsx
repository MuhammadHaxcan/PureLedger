import { Modal as BootstrapModal, Alert } from 'react-bootstrap';
import './Modal.css';

// `error` shows a failure inside the dialog. A message drawn on the page behind an open dialog is invisible,
// which makes a failed save look like the button did nothing.
const Modal = ({ show, onHide, title, children, footer, size = 'md', centered = true, error = '', onErrorDismiss }) => {
  return (
    <BootstrapModal
      show={show}
      onHide={onHide}
      size={size}
      centered={centered}
      backdrop={true} // allow closing via ESC/backdrop
      keyboard={true}
      className="custom-modal"
      dialogClassName="custom-modal-dialog"
      contentClassName="custom-modal-content"
    >
      {title && (
        <BootstrapModal.Header closeButton>
          <BootstrapModal.Title>{title}</BootstrapModal.Title>
        </BootstrapModal.Header>
      )}
      <BootstrapModal.Body className="custom-modal-body" style={{ overflow: 'visible' }}>
        {error && (
          <Alert variant="danger" dismissible={!!onErrorDismiss} onClose={onErrorDismiss}>
            <i className="fas fa-exclamation-circle me-2"></i>{error}
          </Alert>
        )}
        {children}
      </BootstrapModal.Body>
      {footer && <BootstrapModal.Footer>{footer}</BootstrapModal.Footer>}
    </BootstrapModal>
  );
};

export default Modal;

