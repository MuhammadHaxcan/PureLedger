import { Modal as BootstrapModal, Button } from 'react-bootstrap';
import './Modal.css';

const Modal = ({ show, onHide, title, children, footer, size = 'md', centered = true }) => {
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
        {children}
      </BootstrapModal.Body>
      {footer && <BootstrapModal.Footer>{footer}</BootstrapModal.Footer>}
    </BootstrapModal>
  );
};

export default Modal;

