import { Card, Badge } from 'react-bootstrap';

const RecentActivityFeed = ({ data = [], loading = false }) => {
  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  const formatCurrency = (value) => {
    if (value >= 1000000) {
      return `${(value / 1000000).toFixed(2)}M`;
    } else if (value >= 1000) {
      return `${(value / 1000).toFixed(1)}K`;
    }
    return (value || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short'
    });
  };

  const getVoucherBadgeColor = (type) => {
    switch (type) {
      case 'Base':
        return 'warning';
      case 'DC':
        return 'info';
      case 'INV':
        return 'success';
      default:
        return 'primary';
    }
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-history me-2"></i>Recent Activity
        </h6>
      </Card.Header>
      <Card.Body className="p-0" style={{ height: 300, overflow: 'hidden' }}>
        {loading ? (
          <div className="d-flex justify-content-center align-items-center h-100">
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : data.length === 0 ? (
          <div className="d-flex justify-content-center align-items-center text-muted h-100">
            No recent activity
          </div>
        ) : (
          <div className="activity-feed" style={{ height: '100%', overflowY: 'auto' }}>
            {data.map((activity, index) => (
              <div
                key={`${getProp(activity, 'journalVoucherId')}-${index}`}
                className="activity-item d-flex align-items-start p-3 border-bottom"
              >
                <div className="activity-icon me-3">
                  <Badge bg={getVoucherBadgeColor(getProp(activity, 'voucherType'))} className="rounded-pill">
                    {getProp(activity, 'voucherType') || 'JV'}
                  </Badge>
                </div>
                <div className="flex-grow-1">
                  <div className="d-flex justify-content-between align-items-start">
                    <div>
                      <div className="fw-medium" style={{ fontSize: '0.9rem' }}>
                        JV-{getProp(activity, 'journalVoucherId')}
                      </div>
                      <div className="text-muted small text-truncate" style={{ maxWidth: 150 }}>
                        {getProp(activity, 'accountName') || 'Multiple'}
                      </div>
                    </div>
                    <div className="text-end">
                      <div className="fw-bold text-success">
                        Rs {formatCurrency(getProp(activity, 'amount'))}
                      </div>
                      <div className="text-muted small">
                        {formatDate(getProp(activity, 'entryDate'))}
                      </div>
                    </div>
                  </div>
                  {getProp(activity, 'projectName') && (
                    <div className="small text-muted mt-1">
                      <i className="fas fa-folder-open me-1"></i>
                      {getProp(activity, 'projectName')}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card.Body>
    </Card>
  );
};

export default RecentActivityFeed;
