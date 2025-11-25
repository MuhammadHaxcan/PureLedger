import { Modal as BootstrapModal, Button } from 'react-bootstrap';
import './Modal.css';

const Modal = ({ show, onHide, title, children, footer, size = 'md', centered = true }) => {
  return (
    <BootstrapModal
      show={show}
      onHide={onHide}
      size={size}
      centered={centered}
      backdrop="static"
      keyboard={false}
      className="custom-modal"
    >
      {title && (
        <BootstrapModal.Header closeButton>
          <BootstrapModal.Title>{title}</BootstrapModal.Title>
        </BootstrapModal.Header>
      )}
      <BootstrapModal.Body className="custom-modal-body">{children}</BootstrapModal.Body>
      {footer && <BootstrapModal.Footer>{footer}</BootstrapModal.Footer>}
    </BootstrapModal>
  );
};

export default Modal;

