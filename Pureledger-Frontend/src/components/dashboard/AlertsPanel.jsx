import { Card, Alert } from 'react-bootstrap';
import { Link } from 'react-router-dom';

const AlertsPanel = ({ alerts = [], loading = false }) => {
  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  const getAlertVariant = (type) => {
    switch (type) {
      case 'danger':
        return 'danger';
      case 'warning':
        return 'warning';
      case 'info':
        return 'info';
      case 'success':
        return 'success';
      default:
        return 'secondary';
    }
  };

  const getAlertIcon = (type) => {
    switch (type) {
      case 'danger':
        return 'fa-exclamation-circle';
      case 'warning':
        return 'fa-exclamation-triangle';
      case 'info':
        return 'fa-info-circle';
      case 'success':
        return 'fa-check-circle';
      default:
        return 'fa-bell';
    }
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-bell me-2"></i>Alerts & Warnings
        </h6>
      </Card.Header>
      <Card.Body className="p-2" style={{ height: 300, overflow: 'hidden' }}>
        {loading ? (
          <div className="d-flex justify-content-center align-items-center h-100">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : alerts.length === 0 ? (
          <div className="d-flex flex-column justify-content-center align-items-center text-muted h-100">
            <i className="fas fa-check-circle fa-3x text-success mb-3"></i>
            <span className="fw-medium">All clear!</span>
            <small className="text-center mt-1">No pending alerts or warnings.</small>
          </div>
        ) : (
          <div style={{ height: '100%', overflowY: 'auto' }}>
            {alerts.map((alert, index) => {
              const alertType = getProp(alert, 'type') || 'info';
              const alertTitle = getProp(alert, 'title') || 'Alert';
              const alertMessage = getProp(alert, 'message') || '';
              const alertLink = getProp(alert, 'link');

              return (
                <Alert
                  key={index}
                  variant={getAlertVariant(alertType)}
                  className="mb-2 py-2 px-3"
                >
                  <div className="d-flex align-items-start">
                    <i className={`fas ${getAlertIcon(alertType)} me-2 mt-1`}></i>
                    <div className="flex-grow-1">
                      <div className="fw-bold small">{alertTitle}</div>
                      <div className="small">{alertMessage}</div>
                      {alertLink && (
                        <Link
                          to={alertLink}
                          className={`small text-${getAlertVariant(alertType)} fw-medium`}
                        >
                          View Details <i className="fas fa-arrow-right ms-1"></i>
                        </Link>
                      )}
                    </div>
                  </div>
                </Alert>
              );
            })}
          </div>
        )}
      </Card.Body>
    </Card>
  );
};

export default AlertsPanel;
