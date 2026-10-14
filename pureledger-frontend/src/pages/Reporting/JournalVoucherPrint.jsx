import { useEffect, useRef, useState } from 'react';
import apiClient, { API_BASE_URL } from '../../services/api';
import { getJournalPrintModel, journalDate, journalInstrument, journalIsCash, journalMoney } from '../../utils/journalVoucherPrint';
import './JournalVoucherPrint.css';

const JournalVoucherPrint = ({ voucherData, voucherId, filter = 'all', paymentReceipt = false }) => {
    const openedByWindow = useRef(Boolean(window.opener));
    const [activeSignature, setActiveSignature] = useState(null);
    const [failedLogo, setFailedLogo] = useState(null);

    useEffect(() => {
        let active = true;
        apiClient.get('/AdminApi/signatures/active').then(response => {
            if (active && response.data?.success) setActiveSignature(response.data.data);
        }).catch(() => { /* Printing remains available without an optional signature. */ });
        return () => { active = false; };
    }, []);

    useEffect(() => {
        const style = document.createElement('style');
        style.textContent = '@media print { body { margin: 0 !important; } @page { size: A4 portrait; margin: 12mm; } }';
        document.head.appendChild(style);
        return () => style.remove();
    }, []);

    if (!voucherData) return null;

    const model = getJournalPrintModel(voucherData, filter);
    const rows = model.vendorCopy ? model.slips : model.displayedEntries;
    const hasInstruments = paymentReceipt || rows.some(entry => entry.instrumentType != null);
    const hasQuantity = (!model.vendorCopy && !paymentReceipt) || rows.some(entry => Number(entry.quantity || 0) !== 0);
    const hasRate = (!model.vendorCopy && !paymentReceipt) || rows.some(entry => Number(entry.rate || 0) !== 0);
    const totalLabelColumns = 2 + (hasInstruments ? 3 : 0);
    const totalQuantity = rows.reduce((sum, entry) => sum + Number(entry.quantity || 0), 0);
    const projectImage = voucherData.projectImage || voucherData.ProjectImage;
    const baseUrl = (API_BASE_URL || 'https://localhost:7062/api').replace(/\/api\/?$/, '');
    const imageSource = projectImage
        ? (projectImage.startsWith('data:') ? projectImage : `${baseUrl}/uploads/projects/${projectImage}`)
        : '/images/logo.png';
    const copyLabel = model.vendorCopy ? 'Vendor copy'
        : model.normalizedFilter === 'debit' ? 'Debit copy'
        : model.normalizedFilter === 'credit' ? 'Credit copy' : 'Office copy';
    const signatureSource = activeSignature?.signatureImage;

    return (
        <div className="journal-print">
            <div className="journal-print-controls print-controls">
                <button className="btn btn-secondary" onClick={() => openedByWindow.current ? window.close() : window.history.back()}>Close</button>
                <button className="btn btn-success" onClick={() => window.print()}>Print</button>
            </div>
            <article className="journal-sheet" aria-label={model.vendorCopy ? 'Vendor voucher' : 'Journal voucher'}>
                <div className="journal-heading">
                    <div className="journal-brand">
                        {projectImage && failedLogo !== imageSource
                            ? <img src={imageSource} alt="Project Logo" onError={() => setFailedLogo(imageSource)} />
                            : <span className="journal-logo-placeholder">Logo</span>}
                        <div>
                            <div className="journal-project-name">{voucherData.projectName || 'Journal Voucher'}</div>
                        </div>
                    </div>
                    <div className="journal-document">
                        <span className="journal-copy-label">{copyLabel}</span>
                        <h1>{model.vendorCopy ? 'Payment Voucher' : 'Journal Voucher'}</h1>
                        <div className="journal-document-meta">
                            <span>#{voucherId ?? voucherData.jvId}</span>
                            <span>{journalDate(voucherData.entryDate ?? voucherData.createdDate)}</span>
                        </div>
                    </div>
                </div>

                <div className={`journal-summary${model.vendorCopy ? '' : ' journal-summary-standard'}`}>
                    {model.vendorCopy && <div className="journal-payee">
                        <div className="journal-eyebrow">Paid to</div>
                        <div className="journal-payee-name">{model.paidTo}</div>
                    </div>}
                    <div className="journal-amount-summary">
                        <div className="journal-eyebrow">{model.vendorCopy ? 'Total amount (PKR)' : 'Total debit (PKR)'}</div>
                        <div className="journal-summary-amount">{journalMoney(model.totalDebit)}</div>
                    </div>
                    {!model.vendorCopy && <div className="journal-amount-summary">
                        <div className="journal-eyebrow">Total credit (PKR)</div>
                        <div className="journal-summary-amount">{journalMoney(model.totalCredit)}</div>
                    </div>}
                </div>

                <div className="journal-breakdown-heading">
                    <h2 className="journal-eyebrow">{model.vendorCopy ? 'Slip-wise breakdown' : 'Journal entries'}</h2>
                    <span className="journal-muted">{rows.length} {model.vendorCopy ? (rows.length === 1 ? 'slip' : 'slips') : (rows.length === 1 ? 'entry' : 'entries')}</span>
                </div>
                <div className="journal-table-scroll">
                    <table className="journal-entries-table">
                        <thead><tr>
                            <th>{model.vendorCopy ? '#' : 'Account'}</th>
                            <th>Description</th>
                            {hasInstruments && <>
                                <th>Instrument date</th><th>Payment mode</th><th>Reference/Cheque No.</th>
                            </>}
                            {hasQuantity && <th className="journal-number">Qty</th>}
                            {hasRate && <th className="journal-number">Rate</th>}
                            {model.vendorCopy ? <th className="journal-number">Amount (PKR)</th> : <>
                                <th className="journal-number">Debit</th><th className="journal-number">Credit</th>
                            </>}
                        </tr></thead>
                        <tbody>{rows.map((entry, index) => <tr key={entry.journalEntryId ?? entry.entryId ?? index}>
                            <td>{model.vendorCopy ? index + 1 : model.displayAccountName(entry)}</td>
                            <td>{entry.description || ''}</td>
                            {hasInstruments && <>
                                <td>{journalIsCash(entry) ? '' : journalDate(entry.instrumentDate)}</td>
                                <td>{journalInstrument(entry)}</td>
                                <td className="journal-reference">{journalIsCash(entry) ? '' : entry.referenceNumber || ''}</td>
                            </>}
                            {hasQuantity && <td className="journal-number">{entry.quantity || ''}</td>}
                            {hasRate && <td className="journal-number">{Number(entry.rate) ? journalMoney(entry.rate) : ''}</td>}
                            {model.vendorCopy ? <td className="journal-number">{journalMoney(entry[model.slipSide])}</td> : <>
                                <td className="journal-number">{journalMoney(entry.debit)}</td>
                                <td className="journal-number">{journalMoney(entry.credit)}</td>
                            </>}
                        </tr>)}</tbody>
                        <tfoot><tr>
                            <th colSpan={totalLabelColumns} className="journal-total-label">{model.vendorCopy ? 'Total' : 'Totals'}</th>
                            {hasQuantity && <th className="journal-number">{totalQuantity || ''}</th>}
                            {hasRate && <th />}
                            <th className="journal-number">{journalMoney(model.totalDebit)}</th>
                            {!model.vendorCopy && <th className="journal-number">{journalMoney(model.totalCredit)}</th>}
                        </tr></tfoot>
                    </table>
                </div>

                <div className="journal-notes">
                    {model.vendorCopy && model.descriptions.length > 0 && <p><strong>Against:</strong> {model.descriptions.join(' | ')}</p>}
                    {voucherData.remarks && <p><strong>Remarks:</strong> {voucherData.remarks}</p>}
                    {voucherData.otherReference && <p><strong>Other reference:</strong> {voucherData.otherReference}</p>}
                    {voucherData.bookerName && <p><strong>Booker:</strong> {voucherData.bookerName}</p>}
                    {model.vendorCopy && <p>Please match these entries with your statement and confirm receipt.</p>}
                </div>
                <div className="journal-signatures">
                    <div><span>Accountant</span></div>
                    <div>{signatureSource && <img className="journal-signature-image"
                        src={signatureSource.startsWith('data:image') ? signatureSource : `data:image/png;base64,${signatureSource}`}
                        alt={activeSignature.name || 'Signature'} />}<span>Authorized By</span></div>
                    <div><span>Received/Paid By</span></div>
                </div>
            </article>
        </div>
    );
};

export default JournalVoucherPrint;
