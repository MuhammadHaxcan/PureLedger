import { useEffect } from 'react';
import '../../App.css';
import './PrintVoucher.css';

const PrintVoucher = ({ voucherData, voucherType, voucherId }) => {
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
      style: 'currency',
      currency: 'PKR',
      minimumFractionDigits: 2
    }).format(value || 0);
  };

  const handlePrint = () => {
    window.print();
  };

  const handleClose = () => {
    window.history.back();
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
              {voucherData.projectImage ? (
                <img 
                  src={`/uploads/projects/${voucherData.projectImage}`} 
                  className="logo" 
                  alt="Project Logo" 
                />
              ) : (
                <img src="/images/logo.png" className="logo" alt="Company Logo" />
              )}
            </div>
          </div>

          {/* Document Content */}
          {isInv && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tr>
                  <td width="40%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="customer-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Customer Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
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
                      </table>
                    </div>
                  </td>
                  <td width="40%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="invoice-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Invoice Details
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tr>
                          <td width="35%" style={{ padding: '2px 0' }}><strong>Due Date:</strong></td>
                          <td style={{ padding: '2px 0' }}>{formatDate(voucherData.createdDate)}</td>
                        </tr>
                        <tr>
                          <td style={{ padding: '2px 0' }}><strong>Adda:</strong></td>
                          <td style={{ padding: '2px 0' }}>{voucherData.adda || ''}</td>
                        </tr>
                      </table>
                    </div>
                  </td>
                </tr>
              </table>
            </div>
          )}

          {isDC && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tr>
                  <td width="50%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="customer-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Customer Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tr>
                          <td width="35%" style={{ padding: '2px 0' }}><strong>Customer:</strong></td>
                          <td style={{ padding: '2px 0' }}>{voucherData.customerAccountName || ''}</td>
                        </tr>
                      </table>
                    </div>
                  </td>
                  <td width="50%" style={{ verticalAlign: 'top' }}>
                    <div className="challan-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Challan Details
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
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
                      </table>
                    </div>
                  </td>
                </tr>
              </table>
            </div>
          )}

          {isJV && (
            <div className="invoice-header mb-3">
              <table className="header-table" width="100%">
                <tr>
                  <td width="50%" style={{ verticalAlign: 'top', paddingRight: '15px' }}>
                    <div className="voucher-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Project Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tr>
                          <td width="35%" style={{ padding: '2px 0' }}><strong>Project:</strong></td>
                          <td style={{ padding: '2px 0' }}>{voucherData.projectName || ''}</td>
                        </tr>
                      </table>
                    </div>
                  </td>
                  <td width="50%" style={{ verticalAlign: 'top' }}>
                    <div className="voucher-info" style={{ border: '1px solid #ddd', padding: '8px', borderRadius: '4px' }}>
                      <h6 className="info-heading" style={{ marginTop: 0, marginBottom: '8px', borderBottom: '1px solid #eee', paddingBottom: '5px', fontSize: '14px' }}>
                        Booker Information
                      </h6>
                      <table className="info-table" width="100%" style={{ fontSize: '12px' }}>
                        <tr>
                          <td width="35%" style={{ padding: '2px 0' }}><strong>Booker:</strong></td>
                          <td style={{ padding: '2px 0' }}>{voucherData.bookerName || ''}</td>
                        </tr>
                      </table>
                    </div>
                  </td>
                </tr>
              </table>
            </div>
          )}

          {/* Entries Table */}
          <div className="table-responsive mb-3">
            <table className="table table-bordered table-striped" style={{ fontSize: '12px' }}>
              <thead>
                <tr>
                  {isInv || isDC ? (
                    <>
                      <th style={{ width: '30%' }}>Product</th>
                      <th style={{ width: '8%', textAlign: 'right' }}>Qty</th>
                      <th style={{ width: '10%', textAlign: 'right' }}>Rate</th>
                      <th style={{ width: '11%', textAlign: 'right' }}>Amount</th>
                    </>
                  ) : (
                    <>
                      <th style={{ width: '30%' }}>Account</th>
                      <th style={{ width: '30%' }}>Description</th>
                      <th style={{ width: '8%', textAlign: 'right' }}>Qty</th>
                      <th style={{ width: '10%', textAlign: 'right' }}>Rate</th>
                      <th style={{ width: '11%', textAlign: 'right' }}>Debit</th>
                      <th style={{ width: '11%', textAlign: 'right' }}>Credit</th>
                    </>
                  )}
                </tr>
              </thead>
              <tbody>
                {voucherData.entries?.map((entry, index) => (
                  <tr key={index}>
                    {isInv || isDC ? (
                      <>
                        <td>{entry.accountName || entry.productName || ''}</td>
                        <td style={{ textAlign: 'right' }}>{entry.quantity || ''}</td>
                        <td style={{ textAlign: 'right' }}>{entry.rate || ''}</td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(entry.debit || entry.amount || 0)}</td>
                      </>
                    ) : (
                      <>
                        <td>{entry.accountName || ''}</td>
                        <td>{entry.description || ''}</td>
                        <td style={{ textAlign: 'right' }}>{entry.quantity || ''}</td>
                        <td style={{ textAlign: 'right' }}>{entry.rate || ''}</td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(entry.debit || 0)}</td>
                        <td style={{ textAlign: 'right' }}>{formatCurrency(entry.credit || 0)}</td>
                      </>
                    )}
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="table-active">
                  {isInv || isDC ? (
                    <>
                      <th colSpan="3" style={{ textAlign: 'right' }}>Total</th>
                      <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount1 || voucherData.totalDebit || 0)}</th>
                    </>
                  ) : (
                    <>
                      <th colSpan="4" style={{ textAlign: 'right' }}>Totals</th>
                      <th style={{ textAlign: 'right' }}>{voucherData.totalQuantity || ''}</th>
                      <th style={{ textAlign: 'right' }}></th>
                      <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount2 || voucherData.totalDebit || 0)}</th>
                      <th style={{ textAlign: 'right' }}>{formatCurrency(voucherData.totalAmount1 || voucherData.totalCredit || 0)}</th>
                    </>
                  )}
                </tr>
              </tfoot>
            </table>
          </div>

          {/* Signature Section */}
          <table width="100%" style={{ marginTop: '50px' }}>
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
          </table>
        </div>
      </div>
    </div>
  );
};

export default PrintVoucher;

