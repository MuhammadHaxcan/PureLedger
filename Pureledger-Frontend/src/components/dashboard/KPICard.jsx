import { Card } from 'react-bootstrap';

const KPICard = ({ title, value, icon, color = 'primary', subtitle, loading = false }) => {
  const formatValue = (val) => {
    if (typeof val === 'number') {
      if (val >= 1000000) {
        return `Rs ${(val / 1000000).toFixed(2)}M`;
      } else if (val >= 1000) {
        return `Rs ${(val / 1000).toFixed(1)}K`;
      } else if (val % 1 !== 0) {
        return `Rs ${val.toFixed(2)}`;
      }
      return val.toLocaleString();
    }
    return val;
  };

  const colorClasses = {
    primary: 'border-primary text-primary',
    success: 'border-success text-success',
    danger: 'border-danger text-danger',
    warning: 'border-warning text-warning',
    info: 'border-info text-info',
    secondary: 'border-secondary text-secondary'
  };

  const bgClasses = {
    primary: 'bg-primary',
    success: 'bg-success',
    danger: 'bg-danger',
    warning: 'bg-warning',
    info: 'bg-info',
    secondary: 'bg-secondary'
  };

  return (
    <Card className={`kpi-card shadow-sm h-100 border-start border-4 ${colorClasses[color]}`}>
      <Card.Body className="py-3">
        <div className="d-flex justify-content-between align-items-center">
          <div className="flex-grow-1">
            <div className="text-muted text-uppercase small fw-bold mb-1">{title}</div>
            {loading ? (
              <div className="placeholder-glow">
                <span className="placeholder col-6 bg-secondary"></span>
              </div>
            ) : (
              <div className="h4 mb-0 fw-bold">{formatValue(value)}</div>
            )}
            {subtitle && <div className="text-muted small mt-1">{subtitle}</div>}
          </div>
          <div className={`kpi-icon rounded-circle p-3 ${bgClasses[color]} bg-opacity-10`}>
            <i className={`fas ${icon} fa-lg ${colorClasses[color]}`}></i>
          </div>
        </div>
      </Card.Body>
    </Card>
  );
};

export default KPICard;
