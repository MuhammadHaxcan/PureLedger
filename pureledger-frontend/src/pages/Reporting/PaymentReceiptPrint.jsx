import { useEffect, useRef, useState } from 'react';
import apiClient, { API_BASE_URL } from '../../services/api';
import { normalizePaymentInstrumentType, paymentInstrumentLabel } from '../../constants/paymentInstruments';

const formatDate = value => value ? String(value).slice(0, 10) : '';
const money = value => Number(value || 0).toLocaleString('en-PK', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
});

const baseUrl = () => {
    const configured = API_BASE_URL || 'https://localhost:7062/api';
    return configured.endsWith('/api') ? configured.slice(0, -4) : configured.replace('/api', '');
};

const PaymentReceiptPrint = ({ voucherData, voucherId, filter = 'all' }) => {
    const openedByWindow = useRef(Boolean(window.opener));
    const [activeSignature, setActiveSignature] = useState(null);
    const normalizedFilter = String(filter || 'all').toLowerCase();
    const entries = (voucherData.entries || []).map(entry => ({
        ...entry,
        instrumentType: normalizePaymentInstrumentType(entry.instrumentType),
    }));
    const totalDebit = Number(voucherData.totalDebit ?? entries.reduce((sum, entry) => sum + Number(entry.debit || 0), 0));
    const totalCredit = Number(voucherData.totalCredit ?? entries.reduce((sum, entry) => sum + Number(entry.credit || 0), 0));

    const getDisplayAccountName = entry => {
        const debit = Number(entry.debit || 0);
        const credit = Number(entry.credit || 0);
        if (normalizedFilter === 'credit' && debit > 0) return '';
        if (normalizedFilter === 'debit' && credit > 0) return '';
        return entry.accountName ?? entry.AccountName ?? '';
    };

    useEffect(() => {
        const style = document.createElement('style');
        style.textContent = '@media print { @page { size: A4 landscape; margin: 10mm; } .print-controls { display: none !important; } }';
        document.head.appendChild(style);
        return () => style.remove();
    }, []);

    useEffect(() => {
        let active = true;
        apiClient.get('/AdminApi/signatures/active')
            .then(response => {
                if (active && response.data?.success && response.data?.data) {
                    setActiveSignature(response.data.data);
                }
            })
            .catch(() => {
                // A signature is optional; printing remains available without one.
            });
        return () => { active = false; };
    }, []);

    const projectImage = voucherData.projectImage || voucherData.ProjectImage;
    const projectImageSource = projectImage
        ? (projectImage.startsWith('data:') ? projectImage : `${baseUrl()}/uploads/projects/${projectImage}`)
        : '/images/logo.png';

    return (
        <div className="container-fluid print-container payment-receipt-print">
            <div className="d-print-none text-end mb-3 print-controls">
                <button className="btn btn-secondary me-2" onClick={() => openedByWindow.current ? window.close() : window.history.back()}>
                    <i className="fas fa-times me-1"></i>Close
                </button>
                <button className="btn btn-success" onClick={() => window.print()}>
                    <i className="fas fa-print me-1"></i>Print
                </button>
            </div>

            <div className="card shadow mb-4">
                <div className="card-body p-4">
                    <div className="header-section mb-4 d-flex justify-content-between align-items-center">
                        <div>
                            <h2 className="mb-0">Journal Voucher #{voucherId}</h2>
                            <div className="text-muted">
                                Payment &amp; Receipt - {formatDate(voucherData.entryDate ?? voucherData.createdDate)}
                            </div>
                        </div>
                        <div className="logo-container">
                            <img
                                src={projectImageSource}
                                className="logo"
                                alt="Project Logo"
                                onError={event => { event.currentTarget.src = '/images/logo.png'; }}
                            />
                        </div>
                    </div>

                    <div className="invoice-header mb-3">
                        <table className="header-table" width="100%">
                            <tbody>
                                <tr>
                                    <td width="50%" style={{ verticalAlign: 'top', paddingRight: 15 }}>
                                        <div className="voucher-info">
                                            <h6 className="info-heading">Project Information</h6>
                                            <table className="info-table" width="100%">
                                                <tbody>
                                                    <tr>
                                                        <td><strong>Project:</strong></td>
                                                        <td>{voucherData.projectName || ''}</td>
                                                    </tr>
                                                </tbody>
                                            </table>
                                        </div>
                                    </td>
                                    <td width="50%" style={{ verticalAlign: 'top' }}>
                                        <div className="voucher-info">
                                            <h6 className="info-heading">Voucher Information</h6>
                                            <table className="info-table" width="100%">
                                                <tbody>
                                                    <tr>
                                                        <td><strong>Type:</strong></td>
                                                        <td>Payment &amp; Receipt</td>
                                                    </tr>
                                                    <tr>
                                                        <td><strong>Entry Date:</strong></td>
                                                        <td>{formatDate(voucherData.entryDate ?? voucherData.createdDate)}</td>
                                                    </tr>
                                                </tbody>
                                            </table>
                                        </div>
                                    </td>
                                </tr>
                            </tbody>
                        </table>
                    </div>

                    <div className="table-responsive mb-3">
                        <table className="table table-bordered table-striped" style={{ fontSize: 12 }}>
                            <thead>
                                <tr>
                                    <th style={{ width: '20%' }}>Account</th>
                                    <th style={{ width: '25%' }}>Description</th>
                                    <th className="text-start" style={{ width: '10%' }}>Instrument</th>
                                    <th className="text-start" style={{ width: '15%' }}>Reference/Cheque No.</th>
                                    <th className="text-start" style={{ width: '10%' }}>Instrument Date</th>
                                    <th className="text-end" style={{ width: '10%' }}>Debit</th>
                                    <th className="text-end" style={{ width: '10%' }}>Credit</th>
                                </tr>
                            </thead>
                            <tbody>
                                {entries.map((entry, index) => (
                                    <tr key={entry.journalEntryId ?? entry.entryId ?? index}>
                                        <td>{getDisplayAccountName(entry)}</td>
                                        <td>{entry.description || ''}</td>
                                        <td className="text-start">{paymentInstrumentLabel(entry.instrumentType)}</td>
                                        <td className="text-start">{entry.instrumentType === 'Cash' ? '' : entry.referenceNumber || ''}</td>
                                        <td className="text-start">{entry.instrumentType === 'Cash' ? '' : formatDate(entry.instrumentDate)}</td>
                                        <td className="text-end">{money(entry.debit)}</td>
                                        <td className="text-end">{money(entry.credit)}</td>
                                    </tr>
                                ))}
                            </tbody>
                            <tfoot>
                                <tr className="table-active">
                                    <th colSpan={5} className="text-end">Totals</th>
                                    <th className="text-end">{money(totalDebit)}</th>
                                    <th className="text-end">{money(totalCredit)}</th>
                                </tr>
                            </tfoot>
                        </table>
                    </div>

                    <table width="100%" style={{ marginTop: 50 }}>
                        <tbody>
                            <tr>
                                <td width="33%" style={{ textAlign: 'center', padding: 10 }}>
                                    <div style={{ marginTop: 15, paddingTop: 50, position: 'relative' }}>
                                        <div style={{ borderTop: '1px solid #333' }}></div>
                                        <div style={{ fontWeight: 'bold', fontSize: 12, marginTop: 5 }}>Accountant</div>
                                    </div>
                                </td>
                                <td width="33%" style={{ textAlign: 'center', padding: 10 }}>
                                    <div style={{ marginTop: 15, paddingTop: 50, position: 'relative' }}>
                                        {activeSignature?.signatureImage && (() => {
                                            const signatureImage = activeSignature.signatureImage;
                                            const imageSource = signatureImage.startsWith('data:image')
                                                ? signatureImage
                                                : `data:image/png;base64,${signatureImage}`;
                                            return (
                                                <div className="signature-image-container">
                                                    <img
                                                        src={imageSource}
                                                        alt={activeSignature.name || 'Signature'}
                                                        className="signature-image"
                                                    />
                                                </div>
                                            );
                                        })()}
                                        <div style={{ borderTop: '1px solid #333' }}></div>
                                        <div style={{ fontWeight: 'bold', fontSize: 12, marginTop: 5 }}>Authorized By</div>
                                    </div>
                                </td>
                                <td width="33%" style={{ textAlign: 'center', padding: 10 }}>
                                    <div style={{ marginTop: 15, paddingTop: 50, position: 'relative' }}>
                                        <div style={{ borderTop: '1px solid #333' }}></div>
                                        <div style={{ fontWeight: 'bold', fontSize: 12, marginTop: 5 }}>Received/Paid By</div>
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

export default PaymentReceiptPrint;
