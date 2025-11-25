import { useState, useEffect } from 'react';
import { Card, Form, Button, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import PrintVoucher from './PrintVoucher';

const SingleDoc = () => {
  const [voucherId, setVoucherId] = useState('');
  const [voucherType, setVoucherType] = useState('JV');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [voucherData, setVoucherData] = useState(null);

  const loadVoucherFromUrl = async (id, type) => {
    try {
      const response = await apiClient.get(`/ReportingApi/print-voucher/${id}`, {
        params: { type: type, filter: 'all' }
      });

      if (response.data.success) {
        // Merge balance data into voucher data
        const voucherDataWithBalance = {
          ...response.data.data,
          customerBalance: response.data.customerBalance,
          previousBalance: response.data.previousBalance
        };
        setVoucherData(voucherDataWithBalance);
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to load voucher');
    }
  };

  const handleValidateFromUrl = async (id, type) => {
    try {
      setLoading(true);
      setError('');
      const response = await apiClient.get('/ReportingApi/validate-voucher', {
        params: { journalVoucherId: id, type: type }
      });

      if (response.data.success && response.data.exists) {
        await loadVoucherFromUrl(id, type);
      } else {
        setError('Voucher not found or invalid');
      }
    } catch (error) {
      setError(error.response?.data?.message || 'Failed to validate voucher');
    } finally {
      setLoading(false);
    }
  };

  // Read URL parameters on mount
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const urlVoucherId = params.get('voucherId');
    const urlType = params.get('type');
    
    if (urlVoucherId) {
      setVoucherId(urlVoucherId);
      if (urlType && ['JV', 'INV', 'DC'].includes(urlType.toUpperCase())) {
        setVoucherType(urlType.toUpperCase());
      }
      // Auto-load if parameters are present
      const idNum = parseInt(urlVoucherId, 10);
      if (idNum > 0) {
        handleValidateFromUrl(idNum, urlType?.toUpperCase() || 'JV');
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
        // Open voucher in new tab (print-only page without header)
        const url = `/reporting/print-voucher?voucherId=${voucherId}&type=${voucherType}`;
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

  const loadVoucher = async () => {
    try {
      const response = await apiClient.get(`/ReportingApi/print-voucher/${voucherId}`, {
        params: { type: voucherType, filter: 'all' }
      });

      if (response.data.success) {
        // Merge balance data into voucher data
        const voucherDataWithBalance = {
          ...response.data.data,
          customerBalance: response.data.customerBalance,
          previousBalance: response.data.previousBalance
        };
        setVoucherData(voucherDataWithBalance);
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
                          <i className="fas fa-file-alt me-2"></i>Generate
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

