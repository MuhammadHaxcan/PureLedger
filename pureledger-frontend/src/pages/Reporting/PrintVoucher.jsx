import { useEffect, useRef, useState } from 'react';
import { API_BASE_URL } from '../../services/api';
import apiClient from '../../services/api';
import '../../App.css';
import './PrintVoucher.css';

const PrintVoucher = ({ voucherData, voucherType, voucherId, filter = 'all' }) => {
  const [activeSignature, setActiveSignature] = useState(null);
  const normalizedFilter = (filter || 'all').toLowerCase();
  const wasOpenedByWindow = useRef(!!window.opener); // Capture at mount (opener becomes null after parent closes)
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
        .signature-image { 
          height: 64px !important;
          max-height: 64px !important;
          min-height: 64px !important;
          width: auto !important;
          max-width: none !important;
          min-width: auto !important;
          object-fit: contain !important;
          display: block !important;
          -webkit-print-color-adjust: exact !important;
          print-color-adjust: exact !important;
          image-rendering: -webkit-optimize-contrast !important;
          image-rendering: crisp-edges !important;
          transform: scale(1) !important;
          -webkit-transform: scale(1) !important;
          page-break-inside: avoid !important;
          zoom: 1 !important;
          -webkit-zoom: 1 !important;
        }
        img.signature-image {
          height: 64px !important;
        }
        .signature-image-container {
          height: 64px !important;
          position: absolute !important;
          left: 50% !important;
          top: -30px !important;
          transform: translateX(-50%) !important;
          -webkit-transform: translateX(-50%) !important;
          width: auto !important;
          text-align: center !important;
        }
        .signature-image-container img {
          margin: 0 auto !important;
          display: block !important;
        }
        @page {
          size: auto;
          margin: 0;
        }
      }
    `;
    document.head.appendChild(style);
    return () => document.head.removeChild(style);
  }, []);

  useEffect(() => {
    // Fetch active signature
    const fetchActiveSignature = async () => {
      try {
        const response = await apiClient.get('/AdminApi/signatures/active');
        if (response.data.success && response.data.data) {
          setActiveSignature(response.data.data);
        }
      } catch (error) {
        console.error('Error fetching active signature:', error);
      }
    };

    fetchActiveSignature();
  }, []);

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

  const invoiceEntries = voucherData?.entries?.filter(e => e.quantity > 0) || [];
  const invoiceTotal = invoiceEntries.reduce((sum, entry) => sum + calculateEntryAmount(entry), 0);

  if (!voucherData) return null;

  const shouldHideAccountName = (entry) => {
    if (voucherType !== 'JV') return false;
    const debit = entry.debit || 0;
    const credit = entry.credit || 0;
    if (normalizedFilter === 'credit' && debit > 0) return true;
    if (normalizedFilter === 'debit' && credit > 0) return true;
    return false;
  };

  const getDisplayAccountName = (entry) => {
    if (shouldHideAccountName(entry)) return '';
    return entry.accountName ?? entry.AccountName ?? '';
  };

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
    if (wasOpenedByWindow.current) {
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
                            <td style={{ padding: '2px 0' }}>{formatDate(voucherData.dueDate)}</td>
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
                          <tr>
                            <td style={{ padding: '2px 0' }}><strong>Other Ref:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.otherReference || ''}</td>
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
                            <td style={{ padding: '2px 0' }}>{formatDate(voucherData.dueDate)}</td>
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
                            <td style={{ padding: '2px 0', paddingLeft: '15px' }}><strong>Other Ref:</strong></td>
                            <td style={{ padding: '2px 0' }}>{voucherData.otherReference || ''}</td>
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
                        <td style={{ textAlign: 'right' }}>{formatCurrency(entry.rate || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{Math.round(entry.discount || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(entry.netRate || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(amount)}</td>
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
                    <th style={{ textAlign: 'right' }}>{formatCurrency(invoiceTotal)}</th>
                  </tr>
                  {voucherData.forChargesAmount > 0 && (
                    <>
                      <tr className="table-warning">
                        <td colSpan="7" style={{ textAlign: 'right' }}><strong>Less: FOR Charges</strong></td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(voucherData.forChargesAmount || 0)}</td>
                      </tr>
                      <tr className="table-info">
                        <td colSpan="7" style={{ textAlign: 'right' }}><strong>Final Amount</strong></td>
                        <td style={{ textAlign: 'right' }}><strong>{formatCurrency(invoiceTotal - (voucherData.forChargesAmount || 0))}</strong></td>
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
                    <th style={{ width: '50%', textAlign: 'left' }}>Product</th>
                    <th style={{ width: '25%', textAlign: 'right' }}>PKG</th>
                    <th style={{ width: '25%', textAlign: 'right' }}>Qty</th>
                  </tr>
                </thead>
                <tbody>
                  {voucherData.entries?.map((entry, index) => (
                    <tr key={index}>
                      <td>{entry.accountName || entry.productName || ''}</td>
                      <td style={{ textAlign: 'right' }}>{entry.pkg || entry.PKG || 1}</td>
                      <td style={{ textAlign: 'right' }}>{entry.quantity || ''}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="table-active">
                    <th style={{ textAlign: 'right' }}>Total</th>
                    <th style={{ textAlign: 'right' }}></th>
                    <th style={{ textAlign: 'right' }}>{voucherData.totalQuantity || 0}</th>
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
                      <td>{getDisplayAccountName(entry)}</td>
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
                      <span id="previousBalance">{Math.round(voucherData.previousBalance || 0).toLocaleString()}</span>
                    </div>
                    <div style={{ backgroundColor: '#e9ecef', border: '1px solid #dee2e6', padding: '5px', textAlign: 'right' }}>
                      <strong>Customer Running Balance:</strong>
                      <span id="customerRunningBalance">{Math.round(voucherData.customerBalance || 0).toLocaleString()}</span>
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
                  <div style={{ marginTop: '15px', paddingTop: '50px', position: 'relative' }}>
                    <div style={{ borderTop: '1px solid #333' }}></div>
                    <div style={{ fontWeight: 'bold', fontSize: '12px', marginTop: '5px' }}>
                      Accountant
                    </div>
                  </div>
                </td>
                <td width="33%" style={{ textAlign: 'center', padding: '10px' }}>
                  <div style={{ marginTop: '15px', paddingTop: '50px', position: 'relative' }}>
                    {activeSignature?.signatureImage && (() => {
                      const signatureImage = activeSignature.signatureImage;
                      const imageSrc = signatureImage.startsWith('data:image') 
                        ? signatureImage 
                        : `data:image/png;base64,${signatureImage}`;
                      return (
                        <div className="signature-image-container" style={{ position: 'absolute', top: '-30px', left: '50%', transform: 'translateX(-50%)', zIndex: 1 }}>
                          <img 
                            src={imageSrc}
                            alt={activeSignature.name || 'Signature'}
                            className="signature-image"
                            style={{ height: '64px', width: 'auto', objectFit: 'contain', display: 'block' }}
                          />
                        </div>
                      );
                    })()}
                    <div style={{ borderTop: '1px solid #333' }}></div>
                    <div style={{ fontWeight: 'bold', fontSize: '12px', marginTop: '5px' }}>
                      Authorized By
                    </div>
                  </div>
                </td>
                <td width="33%" style={{ textAlign: 'center', padding: '10px' }}>
                  <div style={{ marginTop: '15px', paddingTop: '50px', position: 'relative' }}>
                    <div style={{ borderTop: '1px solid #333' }}></div>
                    <div style={{ fontWeight: 'bold', fontSize: '12px', marginTop: '5px' }}>
                      Received/Paid By
                    </div>
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

