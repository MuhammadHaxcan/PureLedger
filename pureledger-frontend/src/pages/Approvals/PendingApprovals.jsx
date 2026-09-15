import { Fragment, useCallback, useEffect, useState } from 'react';
import { Alert, Badge, Button, Form, Modal, Spinner, Table } from 'react-bootstrap';
import { useLocation, useNavigate } from 'react-router-dom';
import approvalApi from '../../services/approvalApi';
import { usePageTitle } from '../../hooks/usePageTitle';
import { usePermissions } from '../../hooks/usePermissions';
import { normalizePaymentInstrumentType, paymentInstrumentLabel } from '../../constants/paymentInstruments';

const readPayload = (response) => response?.data?.data ?? response?.data ?? {};

const readRows = (payload, fallbackKey) => {
    if (Array.isArray(payload)) return payload;
    if (Array.isArray(payload.items)) return payload.items;
    if (Array.isArray(payload[fallbackKey])) return payload[fallbackKey];
    if (Array.isArray(payload.approvals)) return payload.approvals;
    return [];
};

const approvalIdOf = (row) => row?.approvalVoucherId ?? row?.id;
const entriesOf = (row) => row?.entries ?? row?.approvalVoucherEntries ?? [];

const normalizeEntry = (entry, index) => ({
    approvalVoucherEntryId: entry.approvalVoucherEntryId ?? entry.id ?? null,
    lineNumber: entry.lineNumber ?? index + 1,
    categoryId: entry.categoryId ?? entry.category?.categoryId ?? entry.category?.id ?? null,
    categoryName: entry.categoryName ?? entry.category?.categoryName ?? entry.category?.name ?? '',
    accountId: entry.accountId ?? entry.account?.accountId ?? entry.account?.id ?? null,
    accountName: entry.accountName ?? entry.account?.accountName ?? entry.account?.name ?? '',
    description: entry.description ?? entry.des ?? '',
    debit: entry.debit ?? 0,
    credit: entry.credit ?? 0,
    instrumentType: entry.instrumentType == null ? null : normalizePaymentInstrumentType(entry.instrumentType),
    referenceNumber: entry.referenceNumber ?? null,
    instrumentDate: entry.instrumentDate ?? null,
});

const isPaymentReceipt = row => String(
    row?.approvalType ?? row?.voucherType ?? row?.approvalVoucherType ?? ''
).toLowerCase() === 'paymentreceipt' || Number(
    row?.approvalType ?? row?.voucherType ?? row?.approvalVoucherType
) === 1;

const usernameOf = (row) =>
    row?.postedByUsername ??
    row?.submittedByUsername ??
    row?.postedByUser?.username ??
    row?.submittedByUser?.username ??
    (row?.postedByUserId ? `User #${row.postedByUserId}` : 'Unknown user');

const decisionUsernameOf = (row) =>
    row.decisionByUsername ??
    row.decisionByUser?.username ??
    (row.decisionByUserId ? `User #${row.decisionByUserId}` : '—');

const projectNameOf = (row) => row?.projectName ?? row?.project?.name ?? '—';

const journalVoucherIdOf = (row) => {
    const journalVoucherId = row?.journalVoucherId ?? row?.voucherId;
    return journalVoucherId ? `JV-${journalVoucherId}` : '—';
};

const submittedDateOf = (row) => row?.submittedDate ?? row?.createdDate ?? row?.requestDate;

const formatDate = (value) => {
    if (!value) return '—';
    const datePart = String(value).split('T')[0];
    const [year, month, day] = datePart.split('-').map(Number);
    if (!year || !month || !day) return String(value);
    return new Intl.DateTimeFormat('en-PK', {
        year: 'numeric',
        month: 'short',
        day: '2-digit',
    }).format(new Date(year, month - 1, day));
};

const money = (value) =>
    new Intl.NumberFormat('en-PK', {
        style: 'currency',
        currency: 'PKR',
        minimumFractionDigits: 2,
    }).format(Number(value || 0));

const statusVariant = (status) => {
    const normalized = String(status || '').toLowerCase();
    if (normalized === 'approved' || normalized === 'postednormally') return 'success';
    if (normalized === 'rejected') return 'danger';
    return 'warning';
};

const totalAmountOf = (row) => {
    if (!row) return 0;
    if (row.totalDebit != null) return Number(row.totalDebit);
    if (row.totalAmount != null) return Number(row.totalAmount);
    return entriesOf(row).reduce((sum, entry) => sum + Number(entry.debit || 0), 0);
};

const approvalReviewKey = (row) => {
    const paymentReceipt = isPaymentReceipt(row);
    const entries = entriesOf(row).map(normalizeEntry);
    if (!paymentReceipt) entries.sort((left, right) => left.lineNumber - right.lineNumber);
    return JSON.stringify({
        approvalVoucherId: approvalIdOf(row),
        status: row?.status ?? '',
        requestKind: row?.requestKind ?? '',
        journalVoucherId: row?.journalVoucherId ?? null,
        approvalType: row?.approvalType ?? row?.voucherType ?? '',
        requestedEntryDate: row?.requestedEntryDate ?? null,
        snapshotVersion: row?.snapshotVersion ?? null,
        projectId: row?.projectId ?? null,
        bookerId: row?.bookerId ?? null,
        creditDays: row?.creditDays ?? 0,
        entries: entries.map(entry => ({
            approvalVoucherEntryId: entry.approvalVoucherEntryId,
            ...(!paymentReceipt && { lineNumber: entry.lineNumber }),
            accountId: entry.accountId,
            categoryId: entry.categoryId,
            description: entry.description,
            debit: Number(entry.debit || 0),
            credit: Number(entry.credit || 0),
            instrumentType: entry.instrumentType,
            referenceNumber: entry.referenceNumber,
            instrumentDate: entry.instrumentDate,
        })),
    });
};

const DecisionVoucherSummary = ({ row }) => (
    <div className="border rounded bg-light p-3 mb-3">
        <Badge bg={isPaymentReceipt(row) ? 'info' : 'secondary'} className="mb-2">
            {isPaymentReceipt(row) ? 'P&R' : 'Expense'}
        </Badge>
        <div className="row g-3">
            <div className="col-sm-6">
                <div className="small text-muted">Submitted by</div>
                <div className="fw-semibold">{usernameOf(row)}</div>
            </div>
            <div className="col-sm-6">
                <div className="small text-muted">Submitted date</div>
                <div className="fw-semibold">{formatDate(submittedDateOf(row))}</div>
            </div>
            <div className="col-sm-6">
                <div className="small text-muted">Project</div>
                <div className="fw-semibold">{row?.projectName ?? row?.project?.name ?? '—'}</div>
            </div>
            <div className="col-sm-6">
                <div className="small text-muted">Request type</div>
                <div className="fw-semibold">
                    {String(row?.requestKind || '').toLowerCase() === 'revision'
                        ? 'Voucher revision'
                        : 'New voucher'}
                </div>
            </div>
            {isPaymentReceipt(row) && (
                <div className="col-sm-6">
                    <div className="small text-muted">Requested entry date</div>
                    <div className="fw-semibold">{formatDate(row?.requestedEntryDate ?? row?.entryDate)}</div>
                </div>
            )}
            <div className="col-12">
                <div className="small text-muted">Voucher total</div>
                <div className="fw-bold text-primary fs-5">{money(totalAmountOf(row))}</div>
            </div>
        </div>
    </div>
);

const PendingApprovals = () => {
    usePageTitle('Pending Approvals');

    const navigate = useNavigate();
    const location = useLocation();
    const { canAccessReporting } = usePermissions();
    const [activeTab, setActiveTab] = useState('pending');
    const [pendingRows, setPendingRows] = useState([]);
    const [historyRows, setHistoryRows] = useState([]);
    const [expandedIds, setExpandedIds] = useState(new Set());
    const [details, setDetails] = useState({});
    const [loading, setLoading] = useState(true);
    const [workingId, setWorkingId] = useState(null);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState(location.state?.approvalMessage || '');

    const [approving, setApproving] = useState(null);
    const [rejecting, setRejecting] = useState(null);
    const [rejectionReason, setRejectionReason] = useState('');
    const [rejectionTouched, setRejectionTouched] = useState(false);

    const loadRows = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            const [pendingResponse, historyResponse] = await Promise.all([
                approvalApi.getPending(),
                approvalApi.getHistory(),
            ]);
            const pendingPayload = readPayload(pendingResponse);
            const pendingUsers = pendingPayload.users || {};
            setPendingRows(readRows(pendingPayload, 'pending').map(row => ({
                ...row,
                postedByUsername:
                    pendingUsers[row.postedByUserId] ??
                    pendingUsers[String(row.postedByUserId)] ??
                    row.postedByUsername,
            })));
            setHistoryRows(readRows(readPayload(historyResponse), 'history'));
            setDetails({});
            setExpandedIds(new Set());
        } catch (requestError) {
            setError(requestError.message || 'Failed to load approval requests.');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        loadRows();
    }, [loadRows]);

    useEffect(() => {
        if (location.state?.approvalMessage) {
            navigate(location.pathname, { replace: true, state: null });
        }
    }, [location.pathname, location.state?.approvalMessage, navigate]);

    const rows = activeTab === 'pending' ? pendingRows : historyRows;
    const tableColumnCount = activeTab === 'history' ? 8 : 7;

    const loadDetail = async (row, force = false) => {
        const approvalId = approvalIdOf(row);
        if (!approvalId || (!force && details[approvalId])) return details[approvalId] || row;
        const response = await approvalApi.getById(approvalId);
        const detail = readPayload(response);
        setDetails(previous => ({ ...previous, [approvalId]: detail }));
        return detail;
    };

    const toggleExpanded = async (row) => {
        const approvalId = approvalIdOf(row);
        if (!approvalId) return;

        const next = new Set(expandedIds);
        if (next.has(approvalId)) {
            next.delete(approvalId);
            setExpandedIds(next);
            return;
        }

        try {
            setWorkingId(approvalId);
            await loadDetail(row);
            next.add(approvalId);
            setExpandedIds(next);
        } catch (requestError) {
            setError(requestError.message || 'Failed to load approval details.');
        } finally {
            setWorkingId(null);
        }
    };

    const openApprovalModal = async (row) => {
        const approvalId = approvalIdOf(row);
        if (!approvalId) return;
        setError('');
        try {
            setWorkingId(approvalId);
            const detail = await loadDetail(row, true);
            setApproving({ ...row, ...detail });
        } catch (requestError) {
            setError(requestError.message || 'Failed to load approval details.');
        } finally {
            setWorkingId(null);
        }
    };

    const confirmApproval = async () => {
        const approvalId = approvalIdOf(approving);
        if (!approvalId) return;
        setWorkingId(approvalId);
        setError('');
        try {
            const latest = await loadDetail(approving, true);
            if (String(latest?.status || '').toLowerCase() !== 'pending') {
                setApproving(null);
                setError('This approval request has already been decided.');
                await loadRows();
                return;
            }
            if (approvalReviewKey(latest) !== approvalReviewKey(approving)) {
                setApproving(null);
                setExpandedIds(previous => new Set(previous).add(approvalId));
                setError('This voucher changed after you opened it. Review the refreshed expanded details, then approve again.');
                return;
            }
            const response = await approvalApi.approve(
                approvalId,
                isPaymentReceipt(latest) ? latest.snapshotVersion : null,
            );
            const payload = readPayload(response);
            const voucherNumber = payload.voucherNumber ?? payload.journalVoucherId;
            setSuccess(
                response.data?.message ||
                `Voucher approved${voucherNumber ? ` as JV-${voucherNumber}` : ''}.`
            );
            setApproving(null);
            await loadRows();
        } catch (requestError) {
            setError(requestError.message || 'Approval failed.');
        } finally {
            setWorkingId(null);
        }
    };

    const openRejectionModal = async (row) => {
        const approvalId = approvalIdOf(row);
        if (!approvalId) return;
        setError('');
        setRejectionReason('');
        setRejectionTouched(false);
        try {
            setWorkingId(approvalId);
            const detail = await loadDetail(row, true);
            setRejecting({ ...row, ...detail });
        } catch (requestError) {
            setError(requestError.message || 'Failed to load approval details.');
        } finally {
            setWorkingId(null);
        }
    };

    const closeRejectionModal = () => {
        if (workingId === approvalIdOf(rejecting)) return;
        setRejecting(null);
        setRejectionReason('');
        setRejectionTouched(false);
    };

    const submitRejection = async () => {
        const approvalId = approvalIdOf(rejecting);
        const reason = rejectionReason.trim();
        setRejectionTouched(true);
        if (!approvalId || !reason) return;

        setWorkingId(approvalId);
        setError('');
        try {
            const latest = await loadDetail(rejecting, true);
            if (String(latest?.status || '').toLowerCase() !== 'pending') {
                setRejecting(null);
                setError('This approval request has already been decided.');
                await loadRows();
                return;
            }
            if (approvalReviewKey(latest) !== approvalReviewKey(rejecting)) {
                setRejecting(null);
                setExpandedIds(previous => new Set(previous).add(approvalId));
                setError('This voucher changed after you opened it. Review the refreshed expanded details, then reject again.');
                return;
            }
            await approvalApi.reject(
                approvalId,
                reason,
                isPaymentReceipt(latest) ? latest.snapshotVersion : null,
            );
            setSuccess('Voucher rejected.');
            setRejecting(null);
            setRejectionReason('');
            setRejectionTouched(false);
            await loadRows();
        } catch (requestError) {
            setError(requestError.message || 'Rejection failed.');
        } finally {
            setWorkingId(null);
        }
    };

    const editApproval = (row) => {
        const approvalId = approvalIdOf(row);
        if (!approvalId) return;
        navigate(isPaymentReceipt(row)
            ? `/approvals/pending/payment-receipt/edit/${approvalId}`
            : `/approvals/pending/edit/${approvalId}`);
    };

    const approvingId = approvalIdOf(approving);
    const rejectingId = approvalIdOf(rejecting);
    const approvalWorking = Boolean(approvingId && workingId === approvingId);
    const rejectionWorking = Boolean(rejectingId && workingId === rejectingId);
    const rejectionInvalid = rejectionTouched && !rejectionReason.trim();

    return (
        <div className="container-fluid">
            <div className="d-flex flex-wrap justify-content-end align-items-center gap-2 mb-3">
                <Button variant="outline-primary" onClick={loadRows} disabled={loading}>
                    <i className="fas fa-sync-alt me-1"></i> Refresh
                </Button>
            </div>

            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    {error}
                </Alert>
            )}
            {success && (
                <Alert variant="success" dismissible onClose={() => setSuccess('')}>
                    {success}
                </Alert>
            )}

            <div className="btn-group mb-3" role="tablist" aria-label="Approval views">
                <Button
                    variant={activeTab === 'pending' ? 'primary' : 'outline-primary'}
                    onClick={() => setActiveTab('pending')}
                >
                    Pending <Badge bg="light" text="dark">{pendingRows.length}</Badge>
                </Button>
                <Button
                    variant={activeTab === 'history' ? 'primary' : 'outline-primary'}
                    onClick={() => setActiveTab('history')}
                >
                    History
                </Button>
            </div>

            {loading ? (
                <div className="text-center py-5">
                    <Spinner animation="border" />
                </div>
            ) : (
                <div className="table-responsive">
                    <Table hover bordered className="align-middle">
                        <thead className="table-primary">
                            <tr>
                                <th style={{ width: 48 }} aria-label="Expand"></th>
                                <th>Type</th>
                                <th>Submitted by</th>
                                <th>Project</th>
                                {activeTab === 'history' && <th>JV ID</th>}
                                <th>Submitted date</th>
                                <th className="text-end">Total debit</th>
                                {activeTab === 'history' && <th>Status / decision</th>}
                                {activeTab === 'pending' && <th className="text-end">Actions</th>}
                            </tr>
                        </thead>
                        <tbody>
                            {rows.length === 0 ? (
                                <tr>
                                    <td colSpan={tableColumnCount} className="text-center text-muted py-4">
                                        No {activeTab} approval requests.
                                    </td>
                                </tr>
                            ) : rows.map(row => {
                                const approvalId = approvalIdOf(row);
                                const expanded = expandedIds.has(approvalId);
                                const detail = details[approvalId] || row;
                                const detailEntries = entriesOf(detail).map(normalizeEntry);
                                return (
                                    <Fragment key={approvalId}>
                                        <tr>
                                            <td className="text-center">
                                                <Button
                                                    variant="link"
                                                    className="p-1"
                                                    onClick={() => toggleExpanded(row)}
                                                    aria-label={expanded ? 'Collapse voucher' : 'Expand voucher'}
                                                    aria-expanded={expanded}
                                                >
                                                    <i className={`fas fa-chevron-${expanded ? 'up' : 'down'}`}></i>
                                                </Button>
                                            </td>
                                            <td>
                                                <Badge bg={isPaymentReceipt(row) ? 'info' : 'secondary'}>
                                                    {isPaymentReceipt(row) ? 'P&R' : 'Expense'}
                                                </Badge>
                                            </td>
                                            <td>{usernameOf(row)}</td>
                                            <td>{projectNameOf(row)}</td>
                                            {activeTab === 'history' && <td>{journalVoucherIdOf(row)}</td>}
                                            <td>
                                                {formatDate(submittedDateOf(row))}
                                                {isPaymentReceipt(row) && (
                                                    <div className="small text-muted">Entry: {formatDate(row.requestedEntryDate)}</div>
                                                )}
                                            </td>
                                            <td className="text-end fw-semibold">{money(totalAmountOf(detail))}</td>
                                            {activeTab === 'history' && (
                                                <td>
                                                    <Badge bg={statusVariant(row.status)}>{row.status || 'Unknown'}</Badge>
                                                    <div className="small text-muted mt-1">
                                                        {decisionUsernameOf(row)} · {formatDate(row.decisionDate)}
                                                    </div>
                                                </td>
                                            )}
                                            {activeTab === 'pending' && (
                                                <td className="text-end text-nowrap">
                                                    <Button
                                                        size="sm"
                                                        variant="outline-secondary"
                                                        className="me-1"
                                                        onClick={() => editApproval(row)}
                                                        disabled={workingId === approvalId}
                                                    >
                                                        <i className="fas fa-edit me-1"></i>Edit
                                                    </Button>
                                                    <Button
                                                        size="sm"
                                                        variant="success"
                                                        className="me-1"
                                                        onClick={() => openApprovalModal(row)}
                                                        disabled={workingId === approvalId}
                                                    >
                                                        <i className="fas fa-check me-1"></i>Approve
                                                    </Button>
                                                    <Button
                                                        size="sm"
                                                        variant="outline-danger"
                                                        onClick={() => openRejectionModal(row)}
                                                        disabled={workingId === approvalId}
                                                    >
                                                        <i className="fas fa-times me-1"></i>Reject
                                                    </Button>
                                                </td>
                                            )}
                                        </tr>
                                        {expanded && (
                                            <tr>
                                                <td colSpan={tableColumnCount} className="bg-light">
                                                    <div className="d-flex flex-wrap gap-4 mb-2 small">
                                                        <span><strong>Project:</strong> {projectNameOf(detail)}</span>
                                                        {isPaymentReceipt(detail) ? (
                                                            <span><strong>Requested entry date:</strong> {formatDate(detail.requestedEntryDate ?? detail.entryDate)}</span>
                                                        ) : (
                                                            <span><strong>Booker:</strong> {detail.bookerName ?? detail.booker?.name ?? '—'}</span>
                                                        )}
                                                        {detail.journalVoucherId && (
                                                            <span><strong>Voucher:</strong> JV-{detail.journalVoucherId}</span>
                                                        )}
                                                        {isPaymentReceipt(detail) && activeTab === 'history' &&
                                                            String(detail.status || row.status || '').toLowerCase() === 'approved' &&
                                                            canAccessReporting && detail.canPrint && detail.journalVoucherId && (
                                                            <a href={`/reporting/print-voucher?voucherId=${detail.journalVoucherId}`} target="_blank" rel="noreferrer">
                                                                <i className="fas fa-print me-1"></i>Print P&amp;R
                                                            </a>
                                                        )}
                                                    </div>
                                                    {isPaymentReceipt(detail) && Boolean(
                                                        detail.hasDuplicateWarnings || detail.hadDuplicateWarningsAtSubmission || detail.duplicateWarnings?.length
                                                    ) && (
                                                        <Alert variant="warning" className="py-2">
                                                            <i className="fas fa-exclamation-triangle me-2"></i>
                                                            Duplicate non-cash references were found. This warning is advisory.
                                                            {detail.duplicateWarnings?.length > 0 && (
                                                                <ul className="mb-0 mt-1">
                                                                    {detail.duplicateWarnings.map((warning, index) => (
                                                                        <li key={`${warning.instrumentType ?? ''}_${warning.referenceNumber ?? index}`}>
                                                                            {warning.referenceNumber ? `${warning.referenceNumber}: ` : ''}{warning.message}
                                                                        </li>
                                                                    ))}
                                                                </ul>
                                                            )}
                                                        </Alert>
                                                    )}
                                                    <Table size="sm" bordered className="bg-white mb-1">
                                                        <thead>
                                                            <tr>
                                                                <th>Account</th>
                                                                <th>Category</th>
                                                                <th>Description</th>
                                                                {isPaymentReceipt(detail) && <th>Instrument</th>}
                                                                {isPaymentReceipt(detail) && <th>Reference/Cheque No.</th>}
                                                                {isPaymentReceipt(detail) && <th>Instrument Date</th>}
                                                                <th className="text-end">Debit</th>
                                                                <th className="text-end">Credit</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {detailEntries.map((entry, index) => (
                                                                <tr key={entry.approvalVoucherEntryId ?? `${entry.accountId}_${index}`}>
                                                                    <td>{entry.accountName || `Account #${entry.accountId}`}</td>
                                                                    <td>{entry.categoryName || `Category #${entry.categoryId}`}</td>
                                                                    <td>{entry.description || '—'}</td>
                                                                    {isPaymentReceipt(detail) && <td>{paymentInstrumentLabel(entry.instrumentType)}</td>}
                                                                    {isPaymentReceipt(detail) && <td>{entry.instrumentType === 'Cash' ? '' : entry.referenceNumber || ''}</td>}
                                                                    {isPaymentReceipt(detail) && <td>{entry.instrumentType === 'Cash' ? '' : formatDate(entry.instrumentDate)}</td>}
                                                                    <td className="text-end">{money(entry.debit)}</td>
                                                                    <td className="text-end">{money(entry.credit)}</td>
                                                                </tr>
                                                            ))}
                                                        </tbody>
                                                        <tfoot>
                                                            <tr className="fw-semibold">
                                                                <td colSpan={isPaymentReceipt(detail) ? 6 : 3}>Totals</td>
                                                                <td className="text-end">
                                                                    {money(detailEntries.reduce((sum, entry) => sum + Number(entry.debit || 0), 0))}
                                                                </td>
                                                                <td className="text-end">
                                                                    {money(detailEntries.reduce((sum, entry) => sum + Number(entry.credit || 0), 0))}
                                                                </td>
                                                            </tr>
                                                        </tfoot>
                                                    </Table>
                                                    {isPaymentReceipt(detail) && (
                                                        <div className="text-end fw-semibold">
                                                            Balance: {money(
                                                                detailEntries.reduce((sum, entry) => sum + Number(entry.debit || 0), 0) -
                                                                detailEntries.reduce((sum, entry) => sum + Number(entry.credit || 0), 0)
                                                            )}
                                                        </div>
                                                    )}
                                                    {row.rejectionReason && (
                                                        <Alert variant="danger" className="mt-2 mb-0 py-2">
                                                            <strong>Reason:</strong> {row.rejectionReason}
                                                        </Alert>
                                                    )}
                                                </td>
                                            </tr>
                                        )}
                                    </Fragment>
                                );
                            })}
                        </tbody>
                    </Table>
                </div>
            )}

            <Modal
                show={Boolean(approving)}
                onHide={() => {
                    if (!approvalWorking) setApproving(null);
                }}
                backdrop={approvalWorking ? 'static' : true}
                keyboard={!approvalWorking}
                centered
            >
                <Modal.Header closeButton={!approvalWorking}>
                    <Modal.Title>
                        <i className="fas fa-check-circle text-success me-2"></i>
                        Approve and post voucher
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <DecisionVoucherSummary row={approving} />
                    {isPaymentReceipt(approving) && Boolean(
                        approving?.duplicateWarnings?.length ?? approving?.hasDuplicateWarnings
                    ) && (
                        <Alert variant="warning">
                            <i className="fas fa-exclamation-triangle me-2"></i>
                            Duplicate-reference warnings are present. Approval remains available and will recheck them.
                        </Alert>
                    )}
                    <Alert variant="success" className="mb-0">
                        <i className="fas fa-info-circle me-2"></i>
                        {isPaymentReceipt(approving)
                            ? `This will post the P&R ${String(approving?.requestKind || '').toLowerCase() === 'revision' ? 'revision' : 'voucher'} using the requested entry date ${formatDate(approving?.requestedEntryDate ?? approving?.entryDate)}.`
                            : String(approving?.requestKind || '').toLowerCase() === 'revision'
                            ? 'This will post the approved revision using today as the voucher date.'
                            : 'This will assign the voucher its JV number and post it using today as the voucher date.'}
                    </Alert>
                </Modal.Body>
                <Modal.Footer>
                    <Button
                        variant="secondary"
                        onClick={() => setApproving(null)}
                        disabled={approvalWorking}
                    >
                        Cancel
                    </Button>
                    <Button variant="success" onClick={confirmApproval} disabled={approvalWorking}>
                        {approvalWorking ? (
                            <>
                                <Spinner animation="border" size="sm" className="me-2" />
                                Approving…
                            </>
                        ) : (
                            <>
                                <i className="fas fa-check me-1"></i>
                                Approve &amp; Post
                            </>
                        )}
                    </Button>
                </Modal.Footer>
            </Modal>

            <Modal
                show={Boolean(rejecting)}
                onHide={closeRejectionModal}
                backdrop={rejectionWorking ? 'static' : true}
                keyboard={!rejectionWorking}
                centered
            >
                <Modal.Header closeButton={!rejectionWorking}>
                    <Modal.Title>
                        <i className="fas fa-times-circle text-danger me-2"></i>
                        Reject voucher
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <DecisionVoucherSummary row={rejecting} />
                    <p className="text-muted">
                        Rejection cancels this pending request without posting its proposed voucher changes.
                    </p>
                    <Form.Group controlId="approval-rejection-reason">
                        <Form.Label>
                            Rejection reason <span className="text-danger">*</span>
                        </Form.Label>
                        <Form.Control
                            as="textarea"
                            rows={4}
                            value={rejectionReason}
                            onChange={event => {
                                setRejectionReason(event.target.value);
                                if (rejectionTouched) setRejectionTouched(true);
                            }}
                            onBlur={() => setRejectionTouched(true)}
                            maxLength={500}
                            isInvalid={rejectionInvalid}
                            placeholder="Explain why this voucher cannot be approved."
                            autoFocus
                            required
                            disabled={rejectionWorking}
                        />
                        <div className="d-flex justify-content-between align-items-start mt-1">
                            {rejectionInvalid && (
                                <div className="invalid-feedback d-block">
                                A rejection reason is required.
                                </div>
                            )}
                            <Form.Text className="ms-auto">
                                {rejectionReason.length}/500
                            </Form.Text>
                        </div>
                    </Form.Group>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant="secondary" onClick={closeRejectionModal} disabled={rejectionWorking}>
                        Cancel
                    </Button>
                    <Button variant="danger" onClick={submitRejection} disabled={rejectionWorking}>
                        {rejectionWorking ? (
                            <>
                                <Spinner animation="border" size="sm" className="me-2" />
                                Rejecting…
                            </>
                        ) : (
                            <>
                                <i className="fas fa-times me-1"></i>
                                Reject Voucher
                            </>
                        )}
                    </Button>
                </Modal.Footer>
            </Modal>
        </div>
    );
};

export default PendingApprovals;
