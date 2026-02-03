import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import PrintVoucher from './PrintVoucher';
import { usePageTitle } from '../../hooks/usePageTitle';
import '../../App.css';
import './PrintVoucher.css';

const PrintOnlyVoucher = () => {
  usePageTitle('Print Voucher');
  const [searchParams] = useSearchParams();
  const [voucherData, setVoucherData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const voucherId = searchParams.get('voucherId');
  const voucherType = searchParams.get('type') || 'JV';
  const filter = searchParams.get('filter') || 'all';

  useEffect(() => {
    const loadVoucher = async () => {
      if (!voucherId || parseInt(voucherId) <= 0) {
        setError('Invalid voucher ID');
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError('');
        const response = await apiClient.get(`/ReportingApi/print-voucher/${voucherId}`, {
          params: { type: voucherType, filter: filter }
        });

        if (response.data.success) {
          // Merge balance data into voucher data
          const voucherDataWithBalance = {
            ...response.data.data,
            customerBalance: response.data.customerBalance,
            previousBalance: response.data.previousBalance
          };
          setVoucherData(voucherDataWithBalance);
        } else {
          setError('Failed to load voucher');
        }
      } catch (error) {
        setError(error.response?.data?.message || 'Failed to load voucher');
      } finally {
        setLoading(false);
      }
    };

    loadVoucher();
  }, [voucherId, voucherType, filter]);

  // Monitor parent window - if parent closes, navigate back
  useEffect(() => {
    // Only monitor if opened by another window
    if (!window.opener) return;

    const checkOpener = () => {
      try {
        // Try to access opener - will fail if closed
        if (!window.opener || window.opener.closed) {
          window.history.back();
        }
      } catch (e) {
        // Cross-origin or closed - navigate back
        window.history.back();
      }
    };

    const interval = setInterval(checkOpener, 1000);

    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <LoadingSpinner />
      </div>
    );
  }

  if (error) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <div className="alert alert-danger">
          <i className="fas fa-exclamation-circle me-2"></i>{error}
        </div>
      </div>
    );
  }

  if (!voucherData) {
    return null;
  }

  return (
    <div style={{ 
      minHeight: '100vh', 
      padding: '20px', 
      backgroundColor: '#fff',
      margin: 0,
      position: 'absolute',
      top: 0,
      left: 0,
      right: 0,
      width: '100%'
    }}>
      <style>{`
        body {
          margin: 0 !important;
          padding: 0 !important;
        }
        nav, header, .navbar {
          display: none !important;
        }
      `}</style>
      <PrintVoucher 
        voucherData={voucherData} 
        voucherType={voucherType} 
        voucherId={voucherId}
        filter={filter}
      />
    </div>
  );
};

export default PrintOnlyVoucher;

