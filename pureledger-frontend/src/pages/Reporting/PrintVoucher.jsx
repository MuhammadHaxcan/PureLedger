import { useEffect, useMemo } from 'react';
import { API_BASE_URL } from '../../services/api';
import '../../App.css';
import './PrintVoucher.css';

const PrintVoucher = ({ voucherData, voucherType, voucherId }) => {
  // Get base URL for static files (remove /api from API_BASE_URL)
  const getBaseUrl = () => {
    const baseUrl = API_BASE_URL || 'https://localhost:7062/api';
    // Remove /api from the end if present
    if (baseUrl.endsWith('/api')) {
      return baseUrl.slice(0, -4);
    }
    // If no /api, assume it's already the base URL or use default
    return baseUrl.replace('/api', '') || 'https://localhost:7062';
  };
  useEffect(() => {
    // Add print styles
    const style = document.createElement('style');
    style.textContent = `
      @media print {
        body { margin: 0; }
        .print-controls { display: none !important; }
        .card { border: none !important; box-shadow: none !important; }
        .card-body { padding: 0 !important; }
      }
    `;
    document.head.appendChild(style);
    return () => document.head.removeChild(style);
  }, []);

  if (!voucherData) return null;

  const isDC = voucherType === 'DC';
  const isInv = voucherType === 'INV';
  const isJV = voucherType === 'JV';
  const documentTitle = isInv ? 'Invoice' : isDC ? 'Delivery Challan' : 'Journal Voucher';

  // Calculate invoice entry amounts
  const calculateEntryAmount = (entry) => {
    // For invoices, calculate: PKG * Quantity * NetRate (when RBP is Y)
    // Otherwise use absolute value of LineTotal
    if (entry.rbp === 'Y' && entry.pkg && entry.quantity) {
      const netRate = entry.netRate !== undefined ? entry.netRate : (entry.rate || 0);
      return (entry.pkg || 1) * entry.quantity * netRate;
    }
    return Math.abs(entry.lineTotal || 0);
  };

  const invoiceEntries = useMemo(() => {
    return voucherData.entries?.filter(e => e.quantity > 0) || [];
  }, [voucherData.entries]);

  const invoiceTotal = useMemo(() => {
    return invoiceEntries.reduce((sum, entry) => sum + calculateEntryAmount(entry), 0);
  }, [invoiceEntries]);

  const formatDate = (dateStr) => {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      const day = String(date.getDate()).padStart(2, '0');
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const year = date.getFullYear();
      return `${day}/${month}/${year}`;
    } catch {
      return dateStr;
    }
  };

  const formatCurrency = (value) => {
    return new Intl.NumberFormat('en-PK', {
      minimumFractionDigits: 2
    }).format(value || 0);
  };

  const handlePrint = () => {
    window.print();
  };

  const handleClose = () => {
    // If opened in a new tab/window, close it; otherwise go back
    if (window.opener) {
      window.close();
    } else {
      window.history.back();
    }
  };

  return (
    <div className="container-fluid print-container">
      {/* Print Controls */}
      <div className="d-print-none text-end mb-3 print-controls">
        <button className="btn btn-secondary me-2" onClick={handleClose}>
          <i className="fas fa-times me-1"></i> Close
        </button>
        <button className="btn btn-success" onClick={handlePrint}>
          <i className="fas fa-print me-1"></i> Print
        </button>
      </div>

      <div className="card shadow mb-4">
        <div className="card-body p-4">
          {/* Document Header */}
          <div className="header-section mb-4 d-flex justify-content-between align-items-center">
            <div>
              <h2 className="mb-0">{documentTitle} #{voucherId}</h2>
              <div className="text-muted">
                {isInv && <span>{formatDate(voucherData.createdDate)}</span>}
                {isDC && <span>Delivery Challan - {formatDate(voucherData.createdDate)}</span>}
                {isJV && <span>Journal Voucher - {formatDate(voucherData.createdDate)}</span>}
              </div>
            </div>
            <div className="logo-container">
              {(() => {
                const projectImage = voucherData.projectImage || voucherData.ProjectImage;
                if (projectImage) {
                  // Check if it's a base64 string (starts with "data:") or a file path
                  const imageSrc = projectImage.startsWith('data:') 
                    ? projectImage 
                    : `${getBaseUrl()}/uploads/projects/${projectImage}`;
                  
                  return (
                    <img 
                      src={imageSrc} 
                      className="logo" 
                      alt="Project Logo"
                      onError={(e) => {
                        e.target.src = '/images/logo.png';
                      }}
                    />
                  );
                }
                return <img src="/images/logo.png" className="logo" alt="Company Logo" />;
              })()}
            </div>
          </div>

          {/* Document Content */}
          {isInv && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tbody>
                  <tr>
                    <td width="40%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="customer-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Customer Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Customer:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.customerAccountName || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Address:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.customerAddress || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>City:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.customerCity || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Phone:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.customerPhone || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                  <td width="40%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="invoice-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Invoice Details
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Due Date:</strong></td>
                            <td style={{ padding: '2px 0' }}>{formatDate(voucherData.createdDate)}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Adda:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.adda || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Builty:</strong></td>
                            <td style={{ padding: '2px 0' }}>
                              {voucherData.builty || ''}
                              {voucherData.cartageAmount > 0 ? ` (${Math.round(voucherData.cartageAmount)})` : ''}
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                </tr>
                </tbody>
              </table>
            </div>
          )}

          {isDC && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tbody>
                  <tr>
                  <td width="50%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="customer-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Customer Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Customer:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.customerAccountName || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                  <td width="50%" style={{ verticalAlign: 'top' }}>
                    <div className="challan-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Challan Details
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Due Date:</strong></td>
                            <td style={{ padding: '2px 0' }}>{formatDate(voucherData.createdDate)}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Adda:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.adda || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Builty:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.builty || ''}</td>
                          </tr>
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Remarks:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.remarks || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                </tr>
                </tbody>
              </table>
            </div>
          )}

          {isJV && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tbody>
                  <tr>
                    <td width="50%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="voucher-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Project Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Project:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.projectName || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                  <td width="50%" style={{ verticalAlign: 'top' }}>
                    <div className="voucher-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Booker Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tbody>
                          <tr>
                            <td width="35%" style={{ padding: '2px 0' }}><strong>Booker:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.bookerName || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                  </td>
                </tr>
                </tbody>
              </table>
            </div>
          )}

          {/* Entries Table */}
          {isInv ? (
            <div className="table-responsive mb-3" style={{ width: '100%', overflowX: 'auto' }}>
              <table className="table table-bordered table-striped invoice-table" style={{ fontSize: '14px', width: '100%', tableLayout: 'auto' }}>
                <thead>
                  <tr>
                    <th style={{ width: '5%' }}>SR#</th>
                    <th style={{ width: '32%' }}>Product Name</th>
                    <th style={{ width: '40px', textAlign: 'right' }}>Qty</th>
                    <th style={{ width: '8%', textAlign: 'right' }}>Packaging</th>
                    <th style={{ width: '10%', textAlign: 'right' }}>Rate</th>
                    <th style={{ width: '8%', textAlign: 'right' }}>DISC%</th>
                    <th style={{ width: '12%', textAlign: 'right' }}>NET Rate</th>
                    <th style={{ width: '15%', textAlign: 'right' }}>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {invoiceEntries.map((entry, index) => {
                    const amount = calculateEntryAmount(entry);
                    return (
                      <tr key={index}>
                        <td>{index + 1}</td>
                        <td style={{ wordWrap: 'break-word', whiteSpace: 'normal' }}>{entry.accountName || ''}</td>
                        <td style={{ textAlign: 'right', minWidth: '35px', maxWidth: '45px' }}>{entry.quantity || ''}</td>
                        <td style={{ textAlign: 'right' }}>{entry.pkg || 1}</td>
                        <td style={{ textAlign: 'right' }}>{Math.round(entry.rate || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{Math.round(entry.discount || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{Math.round(entry.netRate || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{Math.round(amount)}</td>
                      </tr>
                    );
                  })}
                </tbody>
                <tfoot>
                  <tr className="table-active" style={{ height: '32px' }}>
                    <th colSpan="2" style={{ textAlign: 'right' }}>Total</th>
                    <th style={{ textAlign: 'right', minWidth: '35px', maxWidth: '45px' }}>{voucherData.totalQuantity || 0}</th>
                    <th style={{ textAlign: 'right' }}></th>
                    <th style={{ textAlign: 'right' }}></th>
                    <th style={{ textAlign: 'right' }}></th>
                    <th style={{ textAlign: 'right' }}>Net Amount</th>
                    <th style={{ textAlign: 'right' }}>{Math.round(invoiceTotal)}</th>
                  </tr>
                  {voucherData.forChargesAmount > 0 && (
                    <>
                      <tr className="table-warning">
                        <td colSpan="7" style={{ textAlign: 'right' }}><strong>Less: FOR Charges</strong></td>
                        <td style={{ textAlign: 'right' }}>{Math.round(voucherData.forChargesAmount || 0)}</td>
                      </tr>
                      <tr className="table-info">
                        <td colSpan="7" style={{ textAlign: 'right' }}><strong>Final Amount</strong></td>
                        <td style={{ textAlign: 'right' }}><strong>{Math.round(invoiceTotal - (voucherData.forChargesAmount || 0))}</strong></td>
                      </tr>
                    </>
                  )}
                </tfoot>
              </table>
            </div>
          ) : isDC ? (
            <div className="table-responsive mb-3">
              <table className="table table-bordered table-striped" style={{ fontSize: '12px' }}>
                <thead>
                  <tr>
                    <th style={{ width: '30%' }}>Product</th>
                    <th style={{ width: '8%', textAlign: 'right' }}>Qty</th>
                    <th style={{ width: '10%', textAlign: 'right' }}>Rate</th>
                    <th style={{ width: '11%', textAlign: 'right' }}>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {voucherData.entries?.map((entry, index) => (
                    <tr key={index}>
                      <td>{entry.accountName || entry.productName || ''}</td>
                      <td style={{ textAlign: 'right' }}>{entry.quantity || ''}</td>
                      <td style={{ textAlign: 'right' }}>{entry.rate || ''}</td>
                      <td style={{ textAlign: 'right' }}>{formatCurrency(entry.debit || entry.amount || 0)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="table-active">
                    <th colSpan="3" style={{ textAlign: 'right' }}>Total</th>
                    <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount1 || voucherData.totalDebit || 0)}</th>
                  </tr>
                </tfoot>
              </table>
            </div>
          ) : (
            <div className="table-responsive mb-3">
              <table className="table table-bordered table-striped" style={{ fontSize: '12px' }}>
                <thead>
                  <tr>
                    <th style={{ width: '30%' }}>Account</th>
                    <th style={{ width: '30%' }}>Description</th>
                    <th style={{ width: '8%', textAlign: 'right' }}>Qty</th>
                    <th style={{ width: '10%', textAlign: 'right' }}>Rate</th>
                    <th style={{ width: '11%', textAlign: 'right' }}>Debit</th>
                    <th style={{ width: '11%', textAlign: 'right' }}>Credit</th>
                  </tr>
                </thead>
                <tbody>
                  {voucherData.entries?.map((entry, index) => (
                    <tr key={index}>
                      <td>{entry.accountName || ''}</td>
                      <td>{entry.description || ''}</td>
                      <td style={{ textAlign: 'right' }}>{entry.quantity || ''}</td>
                      <td style={{ textAlign: 'right' }}>{entry.rate || ''}</td>
                      <td style={{ textAlign: 'right' }}>{formatCurrency(entry.debit || 0)}</td>
                      <td style={{ textAlign: 'right' }}>{formatCurrency(entry.credit || 0)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="table-active">
                    <th colSpan="2" style={{ textAlign: 'right' }}>Totals</th>
                    <th style={{ textAlign: 'right' }}>{voucherData.totalQuantity || ''}</th>
                    <th style={{ textAlign: 'right' }}></th>
                    <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount2 || voucherData.totalDebit || 0)}</th>
                    <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount1 || voucherData.totalCredit || 0)}</th>
                  </tr>
                </tfoot>
              </table>
            </div>
          )}

          {/* Customer Running Balance Display - Only for Invoice */}
          {isInv && (
            <table width="100%" style={{ fontSize: '12px' }}>
              <tbody>
                <tr>
                  <td width="65%"></td>
                  <td width="35%" style={{ paddingTop: '5px' }}>
                    <div style={{ backgroundColor: '#f8f9fa', border: '1px solid #dee2e6', padding: '5px', textAlign: 'right', marginBottom: '5px' }}>
                      <strong>Previous Balance:</strong>
                      <span id="previousBalance">{Math.round(voucherData.previousBalance || 0)}</span>
                    </div>
                    <div style={{ backgroundColor: '#e9ecef', border: '1px solid #dee2e6', padding: '5px', textAlign: 'right' }}>
                      <strong>Customer Running Balance:</strong>
                      <span id="customerRunningBalance">{Math.round(voucherData.customerBalance || 0)}</span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          )}

          {/* Signature Section */}
          <table width="100%" style={{ marginTop: '50px' }}>
            <tbody>
              <tr>
                <td width="33%" style={{ textAlign: 'center', padding: '10px' }}>
                  <div style={{ borderBottom: '1px solid #333', height: '30px', marginBottom: '5px' }}>
                    &nbsp;
                  </div>
                  <div style={{ fontWeight: 'bold', fontSize: '15px' }}>
                    Accountant
                  </div>
                </td>
                <td width="33%" style={{ textAlign: 'center', padding: '10px' }}>
                  <div style={{ borderBottom: '1px solid #333', height: '30px', marginBottom: '5px' }}>
                    {voucherData.authorizedBy || ''}
                  </div>
                  <div style={{ fontWeight: 'bold', fontSize: '15px' }}>
                    Authorized By
                  </div>
                </td>
                <td width="33%" style={{ textAlign: 'center', padding: '10px' }}>
                  <div style={{ borderBottom: '1px solid #333', height: '30px', marginBottom: '5px' }}>
                    {voucherData.receivedBy || ''}
                  </div>
                  <div style={{ fontWeight: 'bold', fontSize: '15px' }}>
                    Received/Paid By
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default PrintVoucher;

