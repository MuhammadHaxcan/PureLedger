import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Form, Modal, Table } from 'react-bootstrap';
import apiClient from '../../services/api';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getProjectImageDataUrl } from '../../services/projectImgApi';
import './AgingFifoModal.css';

const currency = value => new Intl.NumberFormat('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(value ?? 0);
const date = value => value ? value.split('-').reverse().join('/') : '—';
// Export formats shared with the other reports: MM/DD/YY dates and 2-decimal amounts.
const exportDate = value => {
  const parts = typeof value === 'string' ? value.split('-') : [];
  return parts.length === 3 ? `${parts[1]}/${parts[2]}/${parts[0].slice(-2)}` : '';
};
const currency2 = value => new Intl.NumberFormat('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value || 0);
const daysBetween = (fromDate, toDate) => {
  if (!fromDate || !toDate) return null;
  // Date-only values use UTC so daylight-saving changes cannot affect the day count.
  const days = (Date.parse(`${toDate}T00:00:00Z`) - Date.parse(`${fromDate}T00:00:00Z`)) / 86400000;
  return Number.isFinite(days) ? Math.round(days) : null;
};
// Days past the creditable (due) date as of the reference date; 0 when not yet due.
const daysOverdue = (creditableDate, referenceDate) => {
  const days = daysBetween(creditableDate, referenceDate);
  return days === null ? '—' : Math.max(0, days);
};
const pageSize = 100;
const exportPageSize = 250;
const viewLabels = { outstanding: 'Outstanding', credits: 'Payments & Credits', history: 'Transaction History' };
const scopeLabels = { full: 'Full Ledger FIFO - Total Due', due: 'Due Till Date Breakdown' };
// Stop spreadsheet apps from running user-entered text as a formula.
const safeCell = value => (typeof value === 'string' && /^[=+\-@\t\r]/.test(value) ? `'${value}` : value);
const amountColumns = {
  outstanding: [['Original debit', 'debit'], ['Settled', 'settledDebit'], ['Remaining', 'remainingDebit']],
  credits: [['Credit amount', 'credit'], ['Applied', 'appliedCredit'], ['Unapplied', 'unappliedCredit']],
  history: [['Debit', 'debit'], ['Credit', 'credit'], ['Running balance', 'runningBalance']]
};

function PageControls({ page, totalRecords, hasMoreData, loading, onPage, label }) {
  return <div className="aging-fifo-pagination">
    <small className="text-muted">{totalRecords === 0 ? '0 records' : `${page * pageSize + 1}–${Math.min((page + 1) * pageSize, totalRecords)} of ${totalRecords}`}</small>
    <div className="d-flex gap-2">
      <Button size="sm" variant="outline-secondary" aria-label={`Previous ${label}`} disabled={loading || page === 0} onClick={() => onPage(page - 1)}>Previous</Button>
      <Button size="sm" variant="outline-secondary" aria-label={`Next ${label}`} disabled={loading || !hasMoreData} onClick={() => onPage(page + 1)}>Next</Button>
    </div>
  </div>;
}

function AllocationDetails({ target, scope, entry, ledgerVersion, onLedgerChange, onClose }) {
  const [page, setPage] = useState(0);
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    apiClient.get('/ReportingApi/aging-report-fifo', {
      params: { projectId: target.filters.projectId, categoryId: target.filters.categoryId,
        accountId: target.accountId, referenceDate: target.filters.referenceDate,
        scope, view: 'allocations', entryId: entry.journalEntryId, page, pageSize }, signal: controller.signal
    }).then(response => {
      if (controller.signal.aborted) return;
      if (!response.data.success || !response.data.data) throw new Error(response.data.message || 'Failed to load allocations');
      setData(response.data.data);
      if (response.data.data.ledgerVersion !== ledgerVersion) onLedgerChange(true);
    }).catch(err => {
      if (!controller.signal.aborted) setError(err.response?.data?.message || err.message || 'Failed to load allocations');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [target, scope, entry.journalEntryId, page, attempt, ledgerVersion, onLedgerChange]);

  return <section className="aging-fifo-allocations" aria-label="Entry allocations">
    <div className="aging-fifo-allocation-heading">
      <strong>Allocations for entry {entry.journalEntryId} · JV {entry.journalVoucherId}</strong>
      <Button size="sm" variant="outline-secondary" onClick={onClose}>Close allocations</Button>
    </div>
    {loading && <div role="status">Loading allocations…</div>}
    {error && <Alert variant="danger">{error} <Button size="sm" variant="outline-danger" onClick={() => { setLoading(true); setError(''); setData(null); setAttempt(x => x + 1); }}>Retry allocations</Button></Alert>}
    {data && <>
      <div className="aging-fifo-table-scroll aging-fifo-allocation-scroll" role="region" aria-label="Allocation details table" tabIndex={0}>
        <Table size="sm" bordered className="aging-fifo-table aging-fifo-allocation-table">
          <thead><tr><th scope="col">Debit entry / voucher</th><th scope="col">Debit date</th><th scope="col">Debit description</th><th scope="col">Credit entry / voucher</th><th scope="col">Credit date</th><th scope="col">Credit description</th><th scope="col" className="text-end">Applied amount</th></tr></thead>
          <tbody>{data.allocations.map(a => <tr key={`${a.debitEntryId}:${a.creditEntryId}`}>
            <td className="aging-fifo-reference">{a.debitEntryId} / JV {a.debitVoucherId}</td><td className="text-nowrap">{date(a.debitEntryDate)}</td><td className="aging-fifo-description">{a.debitDescription || '—'}</td>
            <td className="aging-fifo-reference">{a.creditEntryId} / JV {a.creditVoucherId}</td><td className="text-nowrap">{date(a.creditEntryDate)}</td><td className="aging-fifo-description">{a.creditDescription || '—'}</td>
            <td className="text-end text-nowrap">{currency(a.amount)}</td>
          </tr>)}</tbody>
        </Table>
      </div>
      {data.allocations.length === 0 && <p className="text-muted mb-0">No allocations for this entry.</p>}
      <PageControls {...data} loading={loading} onPage={next => { setLoading(true); setError(''); setData(null); setPage(next); }} label="allocations" />
    </>}
  </section>;
}

function FifoContent({ target, onRefreshReport }) {
  const [scope, setScope] = useState(target.scope);
  const [view, setView] = useState('outstanding');
  const [page, setPage] = useState(0);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [entry, setEntry] = useState(null);
  const [ledgerChanged, setLedgerChanged] = useState(false);
  const [exporting, setExporting] = useState(null);
  const [exportError, setExportError] = useState('');
  const versions = useRef({});

  useEffect(() => {
    const controller = new AbortController();
    apiClient.get('/ReportingApi/aging-report-fifo', {
      params: { projectId: target.filters.projectId, categoryId: target.filters.categoryId,
        accountId: target.accountId, referenceDate: target.filters.referenceDate, scope, view, page, pageSize },
      signal: controller.signal
    }).then(response => {
      if (controller.signal.aborted) return;
      if (!response.data.success || !response.data.data) throw new Error(response.data.message || 'Failed to load FIFO breakdown');
      const result = response.data.data;
      if (versions.current[scope] && versions.current[scope] !== result.ledgerVersion) setLedgerChanged(true);
      versions.current[scope] = result.ledgerVersion;
      setData(result);
    }).catch(err => {
      if (!controller.signal.aborted) setError(err.response?.data?.message || err.message || 'Failed to load FIFO breakdown');
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [target, scope, view, page, attempt]);

  const expected = scope === 'due' ? target.dueTillDate : target.totalDue;
  const differs = data && Math.abs(Number(data.summary.netBalance) - Number(expected)) > 0.00005;
  const summary = data?.summary;
  const showDaysOverdue = view === 'outstanding' || view === 'history';
  const prepareLoad = () => { setLoading(true); setError(''); setData(null); setEntry(null); };
  const selectView = next => {
    if (next === view && page === 0) return;
    prepareLoad(); setView(next); setPage(0);
  };
  const refresh = () => { prepareLoad(); versions.current = {}; setLedgerChanged(false); setPage(0); setAttempt(x => x + 1); };

  // Exports cover every page of the selected view and scope, not just the visible page.
  const fetchAllRows = async () => {
    const all = [];
    for (let exportPage = 0; ; exportPage++) {
      const response = await apiClient.get('/ReportingApi/aging-report-fifo', {
        params: { projectId: target.filters.projectId, categoryId: target.filters.categoryId,
          accountId: target.accountId, referenceDate: target.filters.referenceDate, scope, view, page: exportPage, pageSize: exportPageSize }
      });
      if (!response.data.success || !response.data.data) throw new Error(response.data.message || 'Failed to load rows for export');
      if (data && response.data.data.ledgerVersion !== data.ledgerVersion) setLedgerChanged(true);
      all.push(...response.data.data.rows);
      if (!response.data.data.hasMoreData) return all;
    }
  };
  // Column headers, ID format and MM/DD/YY dates follow the Master Report exports.
  const buildExport = (rows, kind) => {
    const referenceDate = target.filters.referenceDate;
    const pdf = kind === 'pdf';
    const columns = [
      ['ID', row => `JV-${row.journalVoucherId}${pdf ? '\n' : ', '}Entry: ${row.journalEntryId}`],
      ...(showDaysOverdue ? [['Days Overdue', row => daysOverdue(row.creditableDate, referenceDate)]] : []),
      ['Entry Date', row => exportDate(row.entryDate)],
      ['Credit Date', row => exportDate(row.creditableDate)],
      ['Description', row => safeCell(row.description || '')],
      ...amountColumns[view].map(([label, key]) => [label, row => (pdf ? currency2(row[key]) : Number(row[key] ?? 0))])
    ];
    const records = rows.map(row => Object.fromEntries(columns.map(([header, get]) => [header, get(row)])));
    if (pdf) {
      const sum = key => rows.reduce((total, row) => total + Number(row[key] ?? 0), 0);
      const totals = { ID: 'TOTAL' };
      columns.forEach(([header]) => { totals[header] ??= ''; });
      amountColumns[view].forEach(([label, key]) => {
        // A running balance is not additive, so the history total shows the net movement instead.
        totals[label] = currency2(key === 'runningBalance' ? sum('debit') - sum('credit') : sum(key));
      });
      records.push(totals);
    }
    return { columns: columns.map(([header]) => header), records };
  };
  const exportFileName = extension => {
    const account = String(target.accountName ?? target.accountId).replace(/[^\w-]+/g, '_');
    return `AgingFIFO_${account}_${scope}_${view}_${target.filters.referenceDate}.${extension}`;
  };
  const runExport = async kind => {
    setExporting(kind); setExportError('');
    try {
      const rows = await fetchAllRows();
      if (rows.length === 0) { setExportError('There are no rows to export for this view.'); return; }
      const { columns, records } = buildExport(rows, kind);
      if (kind === 'csv') {
        await exportToCsvAsync(records, exportFileName('csv'));
      } else {
        const logoDataUrl = await getProjectImageDataUrl(target.filters.projectId).catch(() => null);
        await generatePdfAsync({
          orientation: 'landscape', format: 'a4',
          title: 'Aging Report - FIFO Breakdown',
          projectName: target.filters.projectName,
          logoDataUrl,
          subtitle: `Account: ${target.accountName}\nCategory: ${target.filters.categoryName}\nReference Date: ${target.filters.referenceDate}\nView: ${viewLabels[view]} | Scope: ${scopeLabels[scope]}`,
          generatedInfo: `Generated: ${new Date().toLocaleString()} | Total Records: ${rows.length}`,
          columns: columns.map(header => ({ header, dataKey: header })), rows: records,
          rowsPerPage: 14, startY: 60, fontSize: 9, cellPadding: 2, filename: exportFileName('pdf')
        });
      }
    } catch (err) {
      setExportError(err.response?.data?.message || err.message || `Failed to export ${kind.toUpperCase()}`);
    } finally {
      setExporting(null);
    }
  };

  return <>
    <div className="aging-fifo-toolbar">
    <div className="aging-fifo-context"><div>{target.filters.projectName} · {target.filters.categoryName}</div><div className="text-muted">Reference date: <strong>{date(target.filters.referenceDate)}</strong></div></div>
    <Form.Group className="aging-fifo-scope" controlId="aging-fifo-scope">
      <Form.Label>FIFO scope</Form.Label>
      <Form.Select value={scope} onChange={e => { prepareLoad(); setScope(e.target.value); setPage(0); setLedgerChanged(false); }}>
        <option value="full">Full Ledger FIFO — Total Due</option>
        <option value="due">Due Till Date Breakdown</option>
      </Form.Select>
    </Form.Group>
    <div className="aging-fifo-export d-flex gap-2" role="group" aria-label="Export">
      <Button size="sm" variant="outline-success" disabled={loading || !data || !!exporting} onClick={() => runExport('csv')}>
        {exporting === 'csv' ? <><span className="spinner-border spinner-border-sm me-2" role="status" />Generating...</> : <><i className="fas fa-file-csv me-1" /> Export CSV</>}
      </Button>
      <Button size="sm" variant="outline-danger" disabled={loading || !data || !!exporting} onClick={() => runExport('pdf')}>
        {exporting === 'pdf' ? <><span className="spinner-border spinner-border-sm me-2" role="status" />Generating...</> : <><i className="fas fa-file-pdf me-1" /> Export PDF</>}
      </Button>
    </div>
    </div>
    {exportError && <Alert variant="danger" dismissible onClose={() => setExportError('')}>{exportError}</Alert>}
    {(differs || ledgerChanged) && <Alert variant="warning">
      {ledgerChanged ? 'The ledger changed while this popup was open. Refresh to review the latest entries.' : 'This balance differs from the generated report. Account entries may have changed since it was generated.'}
      {differs && <div>Generated report: {currency(expected)} · Latest balance: {currency(summary.netBalance)}</div>}
      <div className="d-flex gap-2 mt-2">
        <Button size="sm" variant="outline-secondary" onClick={refresh}>Refresh breakdown</Button>
        {onRefreshReport && <Button size="sm" variant="outline-primary" onClick={onRefreshReport}>Refresh report</Button>}
      </div>
    </Alert>}
    {summary && <>
      <dl className="aging-fifo-summary">
        {[
          ['Total debits', summary.totalDebits], ['Total credits', summary.totalCredits],
          ['Remaining debits', summary.outstandingDebits], ['Unapplied credits', summary.unappliedCredits],
          [summary.netBalance < 0 ? 'Net credit balance' : 'Net balance', summary.netBalance]
        ].map(([label, amount], index) => <div className={`aging-fifo-stat${index === 4 ? ' aging-fifo-stat-balance' : ''}`} key={label}><dt>{label}</dt><dd>{currency(amount)}</dd></div>)}
      </dl>
      {summary.mixedEntryCount > 0 && <Alert variant="warning" className="py-2">{summary.mixedEntryCount} entries contain both debit and credit. Both amounts are included, with the debit processed first within each entry.</Alert>}
    </>}
    <div className="aging-fifo-views" role="group" aria-label="FIFO views">
      {[['outstanding', 'Outstanding'], ['credits', 'Payments & Credits'], ['history', 'Transaction History']].map(([key, label]) =>
        <Button key={key} size="sm" variant={view === key ? 'primary' : 'outline-primary'} aria-pressed={view === key} onClick={() => selectView(key)}>{label}</Button>)}
    </div>
    {loading && <div role="status" className="text-center py-4"><span className="spinner-border spinner-border-sm me-2" />Loading FIFO breakdown…</div>}
    {error && <Alert variant="danger">{error} <Button size="sm" variant="outline-danger" onClick={() => { prepareLoad(); setAttempt(x => x + 1); }}>Retry breakdown</Button></Alert>}
    {data && <>
      <div className="aging-fifo-table-scroll" role="region" aria-label="FIFO entries table" tabIndex={0}>
        <Table bordered striped hover size="sm" className={`aging-fifo-table aging-fifo-entry-table${showDaysOverdue ? ' aging-fifo-entry-table-with-days' : ''}`}>
          <colgroup>{showDaysOverdue && <col className="aging-fifo-days-column" />}<col className="aging-fifo-date-column" /><col className="aging-fifo-date-column" /><col className="aging-fifo-reference-column" /><col /><col className="aging-fifo-amount-column" /><col className="aging-fifo-amount-column" /><col className="aging-fifo-amount-column" /><col className="aging-fifo-action-column" /></colgroup>
          <thead><tr>{showDaysOverdue && <th scope="col" className="text-end" title="Days from the creditable date to the report reference date. 0 means not yet due.">Days overdue</th>}<th scope="col">Entry date</th><th scope="col">Creditable date</th><th scope="col">Entry / voucher</th><th scope="col">Description</th>
            {view === 'outstanding' && <><th scope="col" className="text-end">Original debit</th><th scope="col" className="text-end">Settled</th><th scope="col" className="text-end">Remaining</th></>}
            {view === 'credits' && <><th scope="col" className="text-end">Credit amount</th><th scope="col" className="text-end">Applied</th><th scope="col" className="text-end">Unapplied</th></>}
            {view === 'history' && <><th scope="col" className="text-end">Debit</th><th scope="col" className="text-end">Credit</th><th scope="col" className="text-end">Running balance</th></>}
            <th scope="col" className="aging-fifo-actions">Allocations</th>
          </tr></thead>
          <tbody>{data.rows.map(row => <tr key={row.journalEntryId}>
            {showDaysOverdue && <td className="text-end text-nowrap">{daysOverdue(row.creditableDate, target.filters.referenceDate)}</td>}
            <td className="text-nowrap">{date(row.entryDate)}</td><td className="text-nowrap">{date(row.creditableDate)}</td><td className="aging-fifo-reference">{row.journalEntryId} / JV {row.journalVoucherId}</td><td className="aging-fifo-description">{row.description || '—'}</td>
            {(view === 'outstanding' ? [row.debit, row.settledDebit, row.remainingDebit] : view === 'credits'
              ? [row.credit, row.appliedCredit, row.unappliedCredit] : [row.debit, row.credit, row.runningBalance]).map((amount, i) => <td className="text-end text-nowrap" key={i}>{currency(amount)}</td>)}
            <td className="aging-fifo-actions">{row.allocationCount > 0 ? <Button size="sm" variant="outline-primary" aria-label={`View allocations for entry ${row.journalEntryId}`} onClick={() => setEntry(row)}>View ({row.allocationCount})</Button> : <span className="text-muted">—</span>}</td>
          </tr>)}</tbody>
        </Table>
      </div>
      {data.rows.length === 0 && <p className="text-muted text-center py-3 mb-0">{view === 'outstanding' ? 'No outstanding debit entries.' : view === 'credits' ? 'No credit entries.' : 'No entries in this scope.'}</p>}
      <PageControls {...data} loading={loading} onPage={next => { prepareLoad(); setPage(next); }} label="entries" />
      {entry && <AllocationDetails key={entry.journalEntryId} target={target} scope={scope} entry={entry} ledgerVersion={data.ledgerVersion} onLedgerChange={setLedgerChanged} onClose={() => setEntry(null)} />}
    </>}
  </>;
}

export default function AgingFifoModal({ target, onHide, onRefreshReport }) {
  const key = target && `${target.openId ?? ''}:${target.accountId}:${target.scope}:${target.filters.projectId}:${target.filters.categoryId}:${target.filters.referenceDate}`;
  return <Modal show={!!target} onHide={onHide} size="xl" centered scrollable className="aging-fifo-modal" dialogClassName="aging-fifo-dialog" aria-labelledby="aging-fifo-title">
    <Modal.Header closeButton><Modal.Title id="aging-fifo-title">{target?.accountName} — FIFO breakdown</Modal.Title></Modal.Header>
    <Modal.Body>{target && <FifoContent key={key} target={target} onRefreshReport={onRefreshReport} />}</Modal.Body>
    <Modal.Footer><Button variant="secondary" onClick={onHide}>Close</Button></Modal.Footer>
  </Modal>;
}
