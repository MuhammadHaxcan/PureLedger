import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Badge, Button, Card, Form, Spinner, Table } from 'react-bootstrap';
import paymentReceiptApi from '../../services/paymentReceiptApi';
import paymentReceiptVoucherApi from '../../services/paymentReceiptVoucherApi';
import approvalApi from '../../services/approvalApi';
import { normalizePaymentInstrumentType, PAYMENT_INSTRUMENTS } from '../../constants/paymentInstruments';
import SearchableSelect from '../../components/SearchableSelect';
import {
    rememberLastSelectedProject,
    resolveLastSelectedProject,
} from '../../utils/lastSelectedProject';
import './PaymentReceipt.css';

const today = () => {
    const now = new Date();
    const offset = now.getTimezoneOffset();
    return new Date(now.getTime() - offset * 60000).toISOString().slice(0, 10);
};

const clientId = () => (
    window.crypto?.randomUUID?.() || `pr_${Date.now()}_${Math.random().toString(16).slice(2)}`
);

const unwrap = (response) => response?.data?.data ?? response?.data ?? {};

const listFrom = (value, ...keys) => {
    if (Array.isArray(value)) return value;
    for (const key of keys) {
        if (Array.isArray(value?.[key])) return value[key];
    }
    return [];
};

const idOf = (item) => item?.id ?? item?.projectId ?? item?.categoryId ?? item?.accountId;
const nameOf = (item) => item?.name ?? item?.projectName ?? item?.categoryName ?? item?.accountName ?? '';

const blankRow = () => ({
    clientId: clientId(),
    originalJournalEntryId: null,
    categoryId: '',
    accountId: '',
    description: '',
    instrumentType: 'Cash',
    referenceNumber: '',
    instrumentDate: '',
    debit: '',
    credit: '',
});

const normalizeRow = row => ({
    clientId: clientId(),
    originalJournalEntryId:
        row.originalJournalEntryId ?? row.journalEntryId ?? row.entryId ?? null,
    categoryId: String(row.categoryId ?? row.category?.id ?? ''),
    accountId: String(row.accountId ?? row.account?.id ?? ''),
    description: row.description ?? row.des ?? '',
    instrumentType: normalizePaymentInstrumentType(row.instrumentType),
    referenceNumber: row.referenceNumber ?? row.referenceChequeNumber ?? '',
    instrumentDate: row.instrumentDate ? String(row.instrumentDate).slice(0, 10) : '',
    debit: Number(row.debit || 0) ? String(row.debit) : '',
    credit: Number(row.credit || 0) ? String(row.credit) : '',
});

const entriesOf = (data) =>
    listFrom(data?.entries ?? data?.approvalVoucherEntries, 'entries', 'approvalVoucherEntries');

const normalizeReference = value => String(value ?? '').trim().toUpperCase();
const duplicateSignature = (instrumentType, referenceNumber) =>
    `${normalizePaymentInstrumentType(instrumentType)}|${normalizeReference(referenceNumber)}`;

const normalizeWarning = (warning, index) => ({
    key: warning.id ?? `${warning.rowIndex ?? 'legacy'}_${warning.matchedReference ?? ''}_${index}`,
    instrumentType: normalizePaymentInstrumentType(warning.instrumentType),
    referenceNumber: warning.referenceNumber ?? '',
    rowIndex: Number.isInteger(warning.rowIndex) ? warning.rowIndex : null,
    matchedReference: warning.matchedReference ?? '',
    message: warning.message ?? warning.warning ?? 'A matching non-cash reference already exists.',
    restricted: Boolean(warning.restricted ?? warning.isRestricted),
});

const warningsOf = (data) =>
    listFrom(data?.duplicateWarnings ?? data?.warnings ?? data?.conflicts, 'duplicateWarnings', 'warnings', 'conflicts')
        .map((warning, index) => normalizeWarning({
            ...warning,
            message: warning.message || (
                warning.source === 'PendingApproval'
                    ? `Reference ${warning.referenceNumber || ''} also appears in a pending request.`
                    : `Reference ${warning.referenceNumber || ''} also appears in another P&R voucher.`
            ),
        }, index));

const errorMessages = (error, fallback) => {
    const errors = error?.errors ?? error?.response?.data?.errors;
    if (Array.isArray(errors) && errors.length) return errors;
    if (errors && typeof errors === 'object') return Object.values(errors).flat().map(String);
    return [error?.message ?? error?.response?.data?.message ?? fallback];
};

const PaymentReceiptForm = ({
    mode = 'create',
    journalVoucherId = null,
    approvalId = null,
    initialData = null,
    onSubmitted,
}) => {
    const isRevision = mode === 'revision';
    const isPending = mode === 'pending';
    const [projects, setProjects] = useState([]);
    const [categories, setCategories] = useState([]);
    const [accountsByCategory, setAccountsByCategory] = useState({});
    const [projectId, setProjectId] = useState('');
    const [entryDate, setEntryDate] = useState(today());
    const [rows, setRows] = useState([blankRow(), blankRow()]);
    const [snapshotVersion, setSnapshotVersion] = useState(null);
    const [rowVersion, setRowVersion] = useState(null);
    const [pendingApprovalId, setPendingApprovalId] = useState(null);
    const [duplicateWarnings, setDuplicateWarnings] = useState([]);
    const [errors, setErrors] = useState([]);
    const [fieldErrors, setFieldErrors] = useState({});
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [checkingDuplicates, setCheckingDuplicates] = useState(false);
    const [readOnly, setReadOnly] = useState(false);
    const [success, setSuccess] = useState('');
    const [focusRowId, setFocusRowId] = useState(null);
    const warningVersion = useRef(0);
    const invalidateWarnings = useCallback(() => {
        warningVersion.current += 1;
        setDuplicateWarnings([]);
        setCheckingDuplicates(false);
    }, []);

    const apiBoundary = useMemo(() => {
        if (isPending) {
            return {
                getInitialization: approvalApi.getPaymentReceiptInitialization,
                getCategories: approvalApi.getPaymentReceiptCategories,
                getAccounts: approvalApi.getPaymentReceiptAccounts,
                getDuplicateWarnings: approvalApi.getPaymentReceiptDuplicateWarnings,
            };
        }
        if (isRevision) return paymentReceiptVoucherApi;
        return paymentReceiptApi;
    }, [isPending, isRevision]);

    const applyData = useCallback((source) => {
        if (!source) return;
        warningVersion.current += 1;
        const data = source.paymentReceipt ?? source.voucher ?? source;
        const sourceRows = entriesOf(data);
        setProjectId(String(data.projectId ?? data.project?.id ?? ''));
        setEntryDate(String(data.requestedEntryDate ?? data.entryDate ?? today()).slice(0, 10));
        setRows(sourceRows.length ? sourceRows.map(normalizeRow) : [blankRow(), blankRow()]);
        setSnapshotVersion(data.snapshotVersion ?? source.snapshotVersion ?? null);
        setRowVersion(data.voucherVersion ?? data.rowVersion ?? data.journalVoucherRowVersion ?? source.voucherVersion ?? source.rowVersion ?? null);
        setPendingApprovalId(data.pendingApprovalId ?? source.pendingApprovalId ?? null);
        setDuplicateWarnings(warningsOf(data));
        setReadOnly(Boolean(
            data.isReadOnly ?? source.isReadOnly ??
            (isRevision && (data.pendingApprovalId ?? source.pendingApprovalId))
        ));
    }, [isRevision]);

    useEffect(() => {
        let active = true;
        const load = async () => {
            setLoading(true);
            setErrors([]);
            try {
                const initResponse = await apiBoundary.getInitialization();
                if (!active) return;
                const init = unwrap(initResponse);
                const availableProjects = listFrom(init, 'projects', 'allowedProjects');
                setProjects(availableProjects);

                let source = initialData;
                if (!source && isPending && approvalId) {
                    source = unwrap(await approvalApi.getById(approvalId));
                } else if (!source && isRevision && journalVoucherId) {
                    source = unwrap(await paymentReceiptVoucherApi.getPostedVoucher(journalVoucherId));
                }
                if (!active) return;
                if (source) {
                    applyData(source);
                } else if (!isPending && !isRevision) {
                    setProjectId(resolveLastSelectedProject(availableProjects));
                }
            } catch (error) {
                if (active) setErrors(errorMessages(error, 'Failed to load Payment & Receipt data.'));
            } finally {
                if (active) setLoading(false);
            }
        };
        load();
        return () => {
            active = false;
        };
    }, [apiBoundary, applyData, approvalId, initialData, isPending, isRevision, journalVoucherId]);

    useEffect(() => {
        if (!projectId) {
            setCategories([]);
            return;
        }
        let active = true;
        apiBoundary.getCategories(projectId)
            .then(response => {
                if (active) setCategories(listFrom(unwrap(response), 'categories', 'items'));
            })
            .catch(error => {
                if (active) setErrors(errorMessages(error, 'Failed to load categories.'));
            });
        return () => { active = false; };
    }, [apiBoundary, projectId]);

    useEffect(() => {
        const missing = [...new Set(rows.map(row => row.categoryId).filter(Boolean))]
            .filter(categoryId => !accountsByCategory[categoryId]);
        missing.forEach(categoryId => {
            apiBoundary.getAccounts(categoryId)
                .then(response => {
                    const accounts = listFrom(unwrap(response), 'accounts', 'items');
                    setAccountsByCategory(previous => ({ ...previous, [categoryId]: accounts }));
                })
                .catch(error => setErrors(errorMessages(error, 'Failed to load accounts.')));
        });
    }, [accountsByCategory, apiBoundary, rows]);

    const totals = useMemo(() => rows.reduce((result, row) => ({
        debit: result.debit + Number(row.debit || 0),
        credit: result.credit + Number(row.credit || 0),
    }), { debit: 0, credit: 0 }), [rows]);

    const balance = totals.debit - totals.credit;

    const buildPayload = useCallback(() => ({
        projectId: Number(projectId),
        entryDate,
        snapshotVersion,
        voucherVersion: rowVersion,
        entries: rows.map(row => ({
            originalJournalEntryId: row.originalJournalEntryId,
            categoryId: Number(row.categoryId),
            accountId: Number(row.accountId),
            description: row.description,
            instrumentType: row.instrumentType,
            referenceNumber: row.instrumentType === 'Cash' ? null : row.referenceNumber.trim(),
            instrumentDate: row.instrumentType === 'Cash' ? null : row.instrumentDate,
            debit: Number(row.debit || 0),
            credit: Number(row.credit || 0),
        })),
    }), [entryDate, projectId, rowVersion, rows, snapshotVersion]);

    const validate = () => {
        const next = {};
        const summary = [];
        if (!projectId) summary.push('Project is required.');
        if (!entryDate) summary.push('Entry date is required.');
        if (rows.length < 2) summary.push('At least two voucher entries are required.');
        rows.forEach((row, index) => {
            const prefix = `rows.${index}`;
            if (!row.categoryId) next[`${prefix}.categoryId`] = 'Category is required.';
            if (!row.accountId) next[`${prefix}.accountId`] = 'Account is required.';
            if (!row.instrumentType) next[`${prefix}.instrumentType`] = 'Instrument type is required.';
            if (row.instrumentType !== 'Cash' && !row.referenceNumber.trim()) {
                next[`${prefix}.referenceNumber`] = `${row.instrumentType === 'Cheque' ? 'Cheque number' : 'Reference number'} is required.`;
            }
            if (row.instrumentType !== 'Cash' && !row.instrumentDate) {
                next[`${prefix}.instrumentDate`] = 'Instrument date is required.';
            }
            const debit = Number(row.debit || 0);
            const credit = Number(row.credit || 0);
            if (debit < 0 || credit < 0) {
                next[`${prefix}.amount`] = 'Debit and credit cannot be negative.';
            } else if ((debit <= 0 && credit <= 0) || (debit > 0 && credit > 0)) {
                next[`${prefix}.amount`] = 'Enter a positive debit or credit, but not both.';
            }
        });
        if (totals.debit <= 0 || Math.abs(balance) > 0.01) {
            summary.push('Total debit and total credit must balance within 0.01.');
        }
        if (Object.keys(next).length) summary.push('Correct the highlighted entry fields.');
        setFieldErrors(next);
        setErrors(summary);
        return summary.length === 0;
    };

    const refreshWarnings = useCallback(async () => {
        if (!projectId || rows.length < 2) {
            invalidateWarnings();
            return;
        }
        const nonCashRows = rows.filter(row => row.instrumentType !== 'Cash' && row.referenceNumber.trim());
        if (!nonCashRows.length) {
            invalidateWarnings();
            return;
        }
        const requestVersion = ++warningVersion.current;
        setCheckingDuplicates(true);
        try {
            const response = await apiBoundary.getDuplicateWarnings({
                ...buildPayload(),
                approvalVoucherId: approvalId ? Number(approvalId) : null,
                journalVoucherId: journalVoucherId ? Number(journalVoucherId) : null,
            });
            if (requestVersion === warningVersion.current) setDuplicateWarnings(warningsOf(unwrap(response)));
        } catch (error) {
            // Duplicate checking is advisory. Submission still performs the authoritative check.
            console.warn('Unable to refresh duplicate-reference warnings.', error);
        } finally {
            if (requestVersion === warningVersion.current) setCheckingDuplicates(false);
        }
    }, [apiBoundary, approvalId, buildPayload, invalidateWarnings, journalVoucherId, projectId, rows]);

    const updateRow = (rowIndex, field, value) => {
        if (['referenceNumber', 'instrumentType', 'debit', 'credit'].includes(field)) invalidateWarnings();
        setRows(previous => previous.map((row, index) => {
            if (index !== rowIndex) return row;
            if (field === 'instrumentType') {
                if (value === 'Cash') {
                    return { ...row, instrumentType: value, referenceNumber: '', instrumentDate: '' };
                }

                return {
                    ...row,
                    instrumentType: value,
                    instrumentDate: value === 'Cheque' ? '' : today(),
                };
            }
            if (field === 'categoryId') return { ...row, categoryId: value, accountId: '' };
            if (field === 'debit' && Number(value || 0) > 0) return { ...row, debit: value, credit: '' };
            if (field === 'credit' && Number(value || 0) > 0) return { ...row, credit: value, debit: '' };
            return { ...row, [field]: value };
        }));
        setFieldErrors(previous => {
            const next = { ...previous };
            delete next[`rows.${rowIndex}.${field}`];
            if (field === 'debit' || field === 'credit') delete next[`rows.${rowIndex}.amount`];
            return next;
        });
    };

    const addRow = useCallback(() => {
        if (readOnly || saving) return;
        invalidateWarnings();
        const row = blankRow();
        setRows(previous => [...previous, row]);
        setFocusRowId(row.clientId);
    }, [invalidateWarnings, readOnly, saving]);

    useEffect(() => {
        const handleKeyDown = event => {
            if (event.altKey && event.key.toLowerCase() === 'n' && !loading) {
                event.preventDefault();
                addRow();
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [addRow, loading]);

    const removeRow = (index) => {
        if (rows.length <= 2) {
            setErrors(['At least two voucher entries are required.']);
            return;
        }
        setRows(previous => previous.filter((_, rowIndex) => rowIndex !== index));
        invalidateWarnings();
    };

    const submit = async (event) => {
        event.preventDefault();
        setSuccess('');
        if (!validate()) return;
        setSaving(true);
        try {
            const payload = buildPayload();
            let response;
            if (isPending) {
                response = await approvalApi.updatePaymentReceipt(approvalId, payload);
            } else if (isRevision) {
                response = await paymentReceiptVoucherApi.submitRevision(journalVoucherId, payload);
            } else {
                response = await paymentReceiptApi.submit(payload);
            }
            const data = unwrap(response);
            warningVersion.current += 1;
            setSnapshotVersion(data.snapshotVersion ?? snapshotVersion);
            setRowVersion(data.voucherVersion ?? data.rowVersion ?? rowVersion);
            setPendingApprovalId(data.approvalVoucherId ?? data.approvalId ?? pendingApprovalId);
            setDuplicateWarnings(warningsOf(data));
            const message = response.data?.message || (
                isRevision ? 'Voucher revision submitted for approval.' :
                isPending ? 'Pending Payment & Receipt request updated.' :
                'Payment & Receipt request submitted for approval.'
            );
            setSuccess(message);
            setErrors([]);
            rememberLastSelectedProject(projectId);
            if (onSubmitted) onSubmitted(data, message);
            if (!isPending) setReadOnly(true);
        } catch (error) {
            setErrors(errorMessages(error, 'Failed to save the Payment & Receipt request.'));
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return <div className="text-center py-5"><Spinner animation="border" /></div>;
    }

    const warningRows = new Set(duplicateWarnings.filter(warning => warning.rowIndex !== null)
        .map(warning => warning.rowIndex));
    const warningSignatures = new Set(duplicateWarnings.filter(warning => warning.rowIndex === null).map(warning =>
        duplicateSignature(warning.instrumentType, warning.referenceNumber)));
    const projectName = nameOf(projects.find(project => String(idOf(project)) === projectId));
    const projectOptions = projects.map(project => ({ id: idOf(project), name: nameOf(project) }));
    if (projectId && projectName && !projectOptions.some(project => String(project.id) === projectId)) {
        projectOptions.push({ id: projectId, name: projectName });
    }
    return (
        <Form className="payment-receipt-form" onSubmit={submit} noValidate>
            {errors.length > 0 && (
                <Alert variant="danger" role="alert" aria-live="assertive">
                    <Alert.Heading className="h6">Please review the following</Alert.Heading>
                    <ul className="mb-0">
                        {errors.map((message, index) => <li key={`${message}_${index}`}>{message}</li>)}
                    </ul>
                </Alert>
            )}
            {success && <Alert variant="success" dismissible onClose={() => setSuccess('')}>{success}</Alert>}
            {readOnly && pendingApprovalId && (
                <Alert variant="info">
                    <i className="fas fa-lock me-2"></i>
                    {isRevision
                        ? `A P&R revision is already pending as approval #${pendingApprovalId}. The posted voucher is read-only here.`
                        : `This P&R request was submitted as approval #${pendingApprovalId}.`}
                </Alert>
            )}
            <Card className="shadow mb-3">
                <Card.Header className="py-2 d-flex flex-wrap justify-content-between align-items-center gap-2">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-info-circle me-2"></i>Entry Details
                    </h6>
                    <span className="small fw-semibold text-muted">
                        {isRevision ? `Edit Payment & Receipt — JV-${journalVoucherId}` :
                            isPending ? `Edit Pending Payment & Receipt — #${approvalId}` :
                            'New Payment & Receipt'}
                    </span>
                </Card.Header>
                <Card.Body className="py-2">
                    <div className="row mb-2">
                        <div className="col-md-6 mb-2 mb-md-0">
                            <Form.Group controlId={`payment-receipt-date-${mode}`}>
                                <Form.Label>Entry Date:</Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text">
                                        <i className="fas fa-calendar-alt"></i>
                                    </span>
                                    <Form.Control
                                        type="date"
                                        value={entryDate}
                                        onChange={event => setEntryDate(event.target.value)}
                                        disabled={readOnly || saving}
                                        required
                                    />
                                </div>
                            </Form.Group>
                        </div>
                        <div className="col-md-6">
                            <Form.Group controlId={`payment-receipt-project-${mode}`}>
                                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                                <div className="input-group">
                                    <span className="input-group-text">
                                        <i className="fas fa-project-diagram"></i>
                                    </span>
                                    <SearchableSelect
                                        value={projectId || null}
                                        onChange={value => {
                                            const nextProjectId = value == null ? '' : String(value);
                                            setProjectId(nextProjectId);
                                            rememberLastSelectedProject(nextProjectId);
                                            setRows(previous => previous.map(row => ({ ...row, categoryId: '', accountId: '' })));
                                            invalidateWarnings();
                                        }}
                                        onClear={() => {
                                            setProjectId('');
                                            setRows(previous => previous.map(row => ({ ...row, categoryId: '', accountId: '' })));
                                            invalidateWarnings();
                                        }}
                                        options={projectOptions}
                                        placeholder="Select Project"
                                        ariaLabel="Project"
                                        disabled={isRevision || isPending || readOnly || saving}
                                        isInvalid={!projectId && errors.length > 0}
                                        required
                                    />
                                </div>
                            </Form.Group>
                        </div>
                    </div>
                </Card.Body>
            </Card>

            <Card className="shadow mb-3">
                <Card.Header className="py-2 d-flex flex-wrap justify-content-between align-items-center gap-2">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-list-alt me-2"></i>Journal Entries
                    </h6>
                    <div className="d-flex align-items-center gap-2">
                        {checkingDuplicates && (
                            <span className="small text-muted">
                                <Spinner size="sm" className="me-1" />Checking references…
                            </span>
                        )}
                        <Button type="button" variant="primary" size="sm" onClick={addRow} disabled={readOnly || saving}>
                            <i className="fas fa-plus"></i> Add Row (Alt+N)
                        </Button>
                    </div>
                </Card.Header>
                <Card.Body className="p-0">
                    <div className="table-responsive payment-receipt-table-wrap">
                        <Table bordered className="align-middle payment-receipt-grid mb-0">
                            <thead className="table-primary">
                                <tr>
                                    <th className="category-column text-center">Category <span className="text-danger">*</span></th>
                                    <th className="account-column text-center">Account <span className="text-danger">*</span></th>
                                    <th className="description-column text-center">Description</th>
                                    <th className="instrument-column text-center">Instrument Type <span className="text-danger">*</span></th>
                                    <th className="reference-column text-center">Reference/Cheque No.</th>
                                    <th className="date-column text-center">Instrument Date</th>
                                    <th className="money-column text-end">Debit</th>
                                    <th className="money-column text-end">Credit</th>
                                    <th className="delete-column"></th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map((row, index) => {
                                    const rowError = (field) => fieldErrors[`rows.${index}.${field}`];
                                    const accounts = accountsByCategory[row.categoryId] || [];
                                    const referenceLabel = row.instrumentType === 'Cheque' ? 'Cheque No.' : 'Reference No.';
                                    const hasDuplicateWarning = warningRows.has(index) || warningSignatures.has(
                                        duplicateSignature(row.instrumentType, row.referenceNumber));
                                    return (
                                        <tr key={row.clientId} className={hasDuplicateWarning ? 'duplicate-warning' : ''}>
                                            <td>
                                                <SearchableSelect
                                                    autoFocus={row.clientId === focusRowId}
                                                    ariaLabel={`Entry ${index + 1} category`}
                                                    value={row.categoryId || null}
                                                    onChange={value => updateRow(index, 'categoryId', value == null ? '' : String(value))}
                                                    onClear={() => updateRow(index, 'categoryId', '')}
                                                    options={categories.map(category => ({ id: idOf(category), name: nameOf(category) }))}
                                                    placeholder="Search category..."
                                                    disabled={!projectId || readOnly || saving}
                                                    isInvalid={Boolean(rowError('categoryId'))}
                                                    required
                                                />
                                                {rowError('categoryId') && <div className="field-error">{rowError('categoryId')}</div>}
                                            </td>
                                            <td>
                                                <SearchableSelect
                                                    ariaLabel={`Entry ${index + 1} account`}
                                                    value={row.accountId || null}
                                                    onChange={value => updateRow(index, 'accountId', value == null ? '' : String(value))}
                                                    onClear={() => updateRow(index, 'accountId', '')}
                                                    options={accounts.map(account => ({ id: idOf(account), name: nameOf(account) }))}
                                                    placeholder="Search account..."
                                                    disabled={!row.categoryId || readOnly || saving}
                                                    isInvalid={Boolean(rowError('accountId'))}
                                                    required
                                                />
                                                {rowError('accountId') && <div className="field-error">{rowError('accountId')}</div>}
                                            </td>
                                            <td>
                                                <Form.Control
                                                    size="sm"
                                                    aria-label={`Entry ${index + 1} description`}
                                                    placeholder="Description"
                                                    value={row.description}
                                                    onChange={event => updateRow(index, 'description', event.target.value)}
                                                    maxLength={500}
                                                    disabled={readOnly || saving}
                                                />
                                            </td>
                                            <td>
                                                <Form.Select
                                                    size="sm"
                                                    aria-label={`Entry ${index + 1} instrument type`}
                                                    value={row.instrumentType}
                                                    onChange={event => updateRow(index, 'instrumentType', event.target.value)}
                                                    disabled={readOnly || saving}
                                                >
                                                    {PAYMENT_INSTRUMENTS.map(type => <option key={type.value} value={type.value}>{type.label}</option>)}
                                                </Form.Select>
                                                {hasDuplicateWarning && <Badge bg="warning" text="dark" className="mt-1">Match</Badge>}
                                            </td>
                                            <td>
                                                <Form.Control
                                                    size="sm"
                                                    aria-label={`Entry ${index + 1} ${referenceLabel}`}
                                                    placeholder={row.instrumentType === 'Cash' ? '' : referenceLabel}
                                                    value={row.referenceNumber}
                                                    onChange={event => updateRow(index, 'referenceNumber', event.target.value)}
                                                    onBlur={refreshWarnings}
                                                    maxLength={100}
                                                    disabled={row.instrumentType === 'Cash' || readOnly || saving}
                                                    isInvalid={Boolean(rowError('referenceNumber'))}
                                                />
                                                {rowError('referenceNumber') && <div className="field-error">{rowError('referenceNumber')}</div>}
                                            </td>
                                            <td>
                                                <Form.Control
                                                    size="sm"
                                                    type="date"
                                                    aria-label={`Entry ${index + 1} instrument date`}
                                                    value={row.instrumentDate}
                                                    onChange={event => updateRow(index, 'instrumentDate', event.target.value)}
                                                    disabled={row.instrumentType === 'Cash' || readOnly || saving}
                                                    isInvalid={Boolean(rowError('instrumentDate'))}
                                                />
                                                {rowError('instrumentDate') && <div className="field-error">{rowError('instrumentDate')}</div>}
                                            </td>
                                            <td>
                                                <Form.Control
                                                    size="sm"
                                                    type="number"
                                                    min="0"
                                                    step="0.01"
                                                    inputMode="decimal"
                                                    aria-label={`Entry ${index + 1} debit`}
                                                    className="text-end no-arrow"
                                                    value={row.debit}
                                                    onChange={event => updateRow(index, 'debit', event.target.value)}
                                                    disabled={readOnly || saving}
                                                    isInvalid={Boolean(rowError('amount'))}
                                                />
                                                {rowError('amount') && <div className="field-error">{rowError('amount')}</div>}
                                            </td>
                                            <td>
                                                <Form.Control
                                                    size="sm"
                                                    type="number"
                                                    min="0"
                                                    step="0.01"
                                                    inputMode="decimal"
                                                    aria-label={`Entry ${index + 1} credit`}
                                                    className="text-end no-arrow"
                                                    value={row.credit}
                                                    onChange={event => updateRow(index, 'credit', event.target.value)}
                                                    disabled={readOnly || saving}
                                                    isInvalid={Boolean(rowError('amount'))}
                                                />
                                            </td>
                                            <td className="delete-column">
                                                <Button
                                                    variant="danger"
                                                    size="sm"
                                                    type="button"
                                                    aria-label={`Delete entry ${index + 1}`}
                                                    title="Delete entry from proposed voucher"
                                                    onClick={() => removeRow(index)}
                                                    disabled={readOnly || saving || rows.length <= 2}
                                                >
                                                    <i className="fas fa-times"></i>
                                                </Button>
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                            <tfoot className="table-secondary">
                                <tr>
                                    <td colSpan="6" className="text-end fw-bold">TOTAL</td>
                                    <td className="fw-bold text-end">
                                        {totals.debit.toLocaleString('en-PK', { minimumFractionDigits: 2 })}
                                    </td>
                                    <td className="fw-bold text-end">
                                        {totals.credit.toLocaleString('en-PK', { minimumFractionDigits: 2 })}
                                    </td>
                                    <td></td>
                                </tr>
                                <tr>
                                    <td colSpan="6" className="text-end fw-bold">BALANCE</td>
                                    <td
                                        colSpan="2"
                                        className={`fw-bold text-center ${Math.abs(balance) <= 0.01 ? 'text-success' : 'text-danger'}`}
                                    >
                                        {balance.toLocaleString('en-PK', { minimumFractionDigits: 2 })}
                                    </td>
                                    <td></td>
                                </tr>
                            </tfoot>
                        </Table>
                    </div>
                </Card.Body>
            </Card>

            {!readOnly && (
                <div className="d-flex justify-content-end mb-3">
                    <Button type="submit" variant="primary" disabled={saving}>
                        {saving ? <><Spinner size="sm" className="me-2" />Saving…</> :
                            <><i className="fas fa-save me-1"></i>{isPending ? 'Save Pending Request' : 'Submit for Approval'}</>}
                    </Button>
                </div>
            )}
            {readOnly && !isRevision && !isPending && (
                <div className="d-flex justify-content-end mb-4">
                    <Button
                        type="button"
                        variant="outline-primary"
                        onClick={() => {
                            setRows([blankRow(), blankRow()]);
                            invalidateWarnings();
                            setPendingApprovalId(null);
                            setSnapshotVersion(null);
                            setReadOnly(false);
                            setSuccess('');
                        }}
                    >
                        <i className="fas fa-plus me-2"></i>Start Another P&amp;R
                    </Button>
                </div>
            )}
        </Form>
    );
};

export default PaymentReceiptForm;
