import { useState } from 'react';
import { Card, Form, Button, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import PrintVoucher from './PrintVoucher';

const SingleDoc = () => {
  const [voucherId, setVoucherId] = useState('');
  const [voucherType, setVoucherType] = useState('JV');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [voucherData, setVoucherData] = useState(null);

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
        // Load the voucher data
        await loadVoucher();
      } else {
        setError('Voucher not found or invalid');
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to validate voucher');
    } finally {
      setLoading(false);
    }
  };

  const loadVoucher = async () => {
    try {
      const response = await apiClient.get(`/ReportingApi/print-voucher/${voucherId}`, {
        params: { type: voucherType, filter: 'all' }
      });

      if (response.data.success) {
        setVoucherData(response.data.data);
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to load voucher');
    }
  };

  return (
    <div>
      {!voucherData ? (
        <>
          <h2 className="mb-4">
            <i className="fas fa-file-alt me-2"></i>Single Document
          </h2>

          {error && (
            <Alert variant="danger" dismissible onClose={() => setError('')}>
              {error}
            </Alert>
          )}

          <Card className="mb-4">
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
                  <div className="col-md-3 d-flex align-items-end">
                    <Button
                      variant="primary"
                      onClick={handleValidate}
                      disabled={loading}
                      className="w-100"
                    >
                      {loading ? (
                        <>
                          <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                          Loading...
                        </>
                      ) : (
                        <>
                          <i className="fas fa-search me-2"></i>Load Voucher
                        </>
                      )}
                    </Button>
                  </div>
                </div>
              </Form>
            </Card.Body>
          </Card>
        </>
      ) : (
        <PrintVoucher 
          voucherData={voucherData} 
          voucherType={voucherType} 
          voucherId={voucherId} 
        />
      )}
    </div>
  );
};

export default SingleDoc;

