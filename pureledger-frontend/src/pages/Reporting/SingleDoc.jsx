import { useState } from 'react';
import { Card, Form, Button, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import { usePageTitle } from '../../hooks/usePageTitle';

const SingleDoc = () => {
  usePageTitle('Single Document');
  const [voucherId, setVoucherId] = useState('');
  const [voucherType, setVoucherType] = useState('JV');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState('all');

  const handleValidate = async () => {
    if (!voucherId || voucherId <= 0) {
      setError('Please enter a valid voucher ID');
      return;
    }

    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get('/ReportingApi/validate-voucher', {
        params: { journalVoucherId: voucherId, type: voucherType }
      });

      if (response.data.success && response.data.exists) {
        // Always open voucher in new tab (print-only page without header)
        const filterParam = voucherType === 'JV' ? `&filter=${filter}` : '';
        const url = `/reporting/print-voucher?voucherId=${voucherId}&type=${voucherType}${filterParam}`;
        window.open(url, '_blank');
        setError('');
      } else {
        setError('Voucher not found or invalid');
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to validate voucher');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="container-fluid">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2>
          <i className="fas fa-file-alt me-2"></i>Single Document
        </h2>
      </div>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          <i className="fas fa-exclamation-circle me-2"></i>{error}
        </Alert>
      )}

      <Card className="shadow mb-4">
        <Card.Header className="py-3 d-flex justify-content-between align-items-center">
          <h6 className="m-0 font-weight-bold text-primary">
            <i className="fas fa-filter me-2"></i>Document Parameters
          </h6>
        </Card.Header>
        <Card.Body>
          <Form>
            <div className="row">
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Voucher ID</Form.Label>
                  <Form.Control
                    type="number"
                    value={voucherId}
                    onChange={(e) => setVoucherId(e.target.value)}
                    placeholder="Enter voucher ID"
                    required
                  />
                </Form.Group>
              </div>
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>Voucher Type</Form.Label>
                  <Form.Select
                    value={voucherType}
                    onChange={(e) => setVoucherType(e.target.value)}
                  >
                    <option value="JV">Journal Voucher</option>
                    <option value="INV">Invoice</option>
                    <option value="DC">Delivery Challan</option>
                  </Form.Select>
                </Form.Group>
              </div>
              {voucherType === 'JV' && (
                <div className="col-md-3">
                  <Form.Group className="mb-3">
                    <Form.Label>Filter</Form.Label>
                    <Form.Select
                      value={filter}
                      onChange={(e) => setFilter(e.target.value)}
                    >
                      <option value="all">All</option>
                      <option value="debit">Debit only</option>
                      <option value="credit">Credit only</option>
                    </Form.Select>
                  </Form.Group>
                </div>
              )}
              <div className="col-md-3">
                <Form.Group className="mb-3">
                  <Form.Label>&nbsp;</Form.Label>
                  <Button
                    variant="primary"
                    onClick={handleValidate}
                    disabled={loading || !voucherId || parseInt(voucherId) <= 0}
                    className="w-100"
                    title={!voucherId || parseInt(voucherId) <= 0 ? 'Please enter a valid voucher ID' : ''}
                  >
                    {loading ? (
                      <>
                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                        Loading...
                      </>
                    ) : (
                      <>
                        <i className="fas fa-file-alt me-2"></i>Open in New Tab
                      </>
                    )}
                  </Button>
                </Form.Group>
              </div>
            </div>
          </Form>
        </Card.Body>
      </Card>
    </div>
  );
};

export default SingleDoc;

