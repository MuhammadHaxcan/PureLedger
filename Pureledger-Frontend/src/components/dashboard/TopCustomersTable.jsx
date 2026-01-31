import { Card, Table } from 'react-bootstrap';

const TopCustomersTable = ({ data = [], loading = false }) => {
  const formatCurrency = (value) => {
    return new Intl.NumberFormat('en-PK', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(value || 0);
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: '2-digit'
    });
  };

  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-users me-2"></i>Top 10 Customers
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
            No data available
          </div>
        ) : (
          <div style={{ height: '100%', overflowY: 'auto' }}>
            <Table hover className="mb-0 table-sm">
              <thead className="table-light" style={{ position: 'sticky', top: 0, zIndex: 1 }}>
                <tr>
                  <th className="ps-3">Customer</th>
                  <th className="text-end">Txns</th>
                  <th className="text-end pe-3">Total Value</th>
                </tr>
              </thead>
              <tbody>
                {data.map((customer, index) => (
                  <tr key={getProp(customer, 'accountId') || index}>
                    <td className="ps-3">
                      <div className="d-flex align-items-center">
                        <span className="badge bg-secondary me-2">{index + 1}</span>
                        <div>
                          <div className="fw-medium" style={{ fontSize: '0.85rem' }}>
                            {getProp(customer, 'customerName') || 'Unknown'}
                          </div>
                          <div className="text-muted small">
                            Last: {formatDate(getProp(customer, 'lastTransaction'))}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td className="text-end align-middle">
                      <span className="badge bg-primary">{getProp(customer, 'transactionCount') || 0}</span>
                    </td>
                    <td className="text-end pe-3 align-middle fw-medium">
                      Rs {formatCurrency(getProp(customer, 'totalValue'))}
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        )}
      </Card.Body>
    </Card>
  );
};

export default TopCustomersTable;
