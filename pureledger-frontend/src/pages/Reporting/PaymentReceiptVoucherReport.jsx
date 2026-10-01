import { Fragment, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Badge, Button, Card, Form, InputGroup, Pagination, Spinner, Table } from 'react-bootstrap';
import { usePageTitle } from '../../hooks/usePageTitle';
import LoadingSpinner from '../../components/LoadingSpinner';
import SearchableSelect from '../../components/SearchableSelect';
import paymentReceiptReportApi from '../../services/paymentReceiptReportApi';
import { exportToCsvAsync, generatePdfAsync } from '../../utils/exportUtils';
import { getProjectImageDataUrl } from '../../services/projectImgApi';
import { getDateRange, dateFilterOptions } from '../../utils/dateFilters';
import {
    normalizePaymentInstrumentType,
    paymentInstrumentLabel,
} from '../../constants/paymentInstruments';
import './PaymentReceiptVoucherReport.css';

const PAGE_SIZE = 50;
const EXPORT_PAGE_SIZE = 500;

const blankFilters = {
    startDate: '',
    endDate: '',
    projectId: '',
    categoryId: '',
    accountId: '',
    search: '',
};

const emptyTotals = { debit: 0, credit: 0, voucherCount: 0 };
const unwrap = response => response?.data?.data ?? response?.data ?? {};
const idOf = item => item?.id ?? item?.projectId ?? item?.categoryId ?? item?.accountId;
const nameOf = item => item?.name ?? item?.projectName ?? item?.categoryName ?? item?.accountName ?? '';
const listFrom = (data, ...keys) => {
    if (Array.isArray(data)) return data;
    for (const key of keys) if (Array.isArray(data?.[key])) return data[key];
    return [];
};

const normalizeEntry = (entry, voucher = {}) => ({
    ...entry,
    instrumentType: normalizePaymentInstrumentType(entry.instrumentType),
    journalVoucherId: entry.journalVoucherId ?? voucher.journalVoucherId ?? voucher.voucherId,
    entryDate: entry.entryDate ?? voucher.entryDate,
    projectName: entry.projectName ?? voucher.projectName,
    journalEntryId: entry.journalEntryId ?? entry.entryId ?? entry.id,
});

const normalizeVouchers = data => {
    const candidates = listFrom(data, 'items', 'vouchers', 'results');
    if (candidates.some(item => Array.isArray(item.entries))) {
        return candidates.map(item => ({
            ...item,
            journalVoucherId: item.journalVoucherId ?? item.voucherId ?? item.id,
            entries: item.entries.map(entry => normalizeEntry(entry, item))
                .sort((left, right) => Number(left.journalEntryId || 0) - Number(right.journalEntryId || 0)),
        }));
    }

    const flatEntries = candidates.length ? candidates : listFrom(data, 'entries', 'rows');
    const groups = new Map();
    flatEntries.forEach(raw => {
        const entry = normalizeEntry(raw);
        if (!groups.has(entry.journalVoucherId)) {
            groups.set(entry.journalVoucherId, {
                journalVoucherId: entry.journalVoucherId,
                entryDate: entry.entryDate,
                projectName: entry.projectName,
                entries: [],
            });
        }
        groups.get(entry.journalVoucherId).entries.push(entry);
    });
    return [...groups.values()].map(voucher => ({
        ...voucher,
        entries: voucher.entries.sort((left, right) =>
            Number(left.journalEntryId || 0) - Number(right.journalEntryId || 0)),
    }));
};

const formatDate = value => {
    if (!value) return '—';
    const text = String(value);
    const isoDate = /^(\d{4})-(\d{2})-(\d{2})/.exec(text);
    if (isoDate) return `${isoDate[2]}/${isoDate[3]}/${isoDate[1]}`;

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return text;
    return new Intl.DateTimeFormat('en-US', {
        month: '2-digit',
        day: '2-digit',
        year: 'numeric',
    }).format(date);
};
const money = value => Number(value || 0).toLocaleString('en-PK', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
});

const PaymentReceiptVoucherReport = () => {
    usePageTitle('Payment & Receipt Voucher Report');
    const [initData, setInitData] = useState({ projects: [], categories: [], accounts: [] });
    const [filters, setFilters] = useState(blankFilters);
    const [activeFilters, setActiveFilters] = useState(blankFilters);
    const [resultSearch, setResultSearch] = useState('');
    const [dateFilterType, setDateFilterType] = useState('custom');
    const [vouchers, setVouchers] = useState([]);
    const [totals, setTotals] = useState(emptyTotals);
    const [page, setPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [hasGenerated, setHasGenerated] = useState(false);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [exporting, setExporting] = useState('');
    const [error, setError] = useState('');
    const abortRef = useRef(null);

    useEffect(() => {
        let active = true;
        paymentReceiptReportApi.getInitialization()
            .then(response => {
                if (!active) return;
                const data = unwrap(response);
                setInitData({
                    projects: listFrom(data, 'projects', 'allowedProjects'),
                    categories: listFrom(data, 'categories'),
                    accounts: listFrom(data, 'accounts'),
                });
            })
            .catch(requestError => {
                if (active) setError(requestError.message || 'Failed to load report filters.');
            })
            .finally(() => { if (active) setLoading(false); });
        return () => {
            active = false;
            abortRef.current?.abort();
        };
    }, []);

    const availableCategories = useMemo(() => initData.categories.filter(category => (
        !filters.projectId || !category.projectId || String(category.projectId) === String(filters.projectId)
    )), [filters.projectId, initData.categories]);

    const categoryProjectIds = useMemo(() => new Map(initData.categories.map(category => (
        [String(idOf(category)), category.projectId]
    ))), [initData.categories]);

    const availableAccounts = useMemo(() => initData.accounts.filter(account => {
        const accountProjectId = account.projectId ?? categoryProjectIds.get(String(account.categoryId));
        return (!filters.categoryId || String(account.categoryId) === String(filters.categoryId)) &&
            (!filters.projectId || !accountProjectId || String(accountProjectId) === String(filters.projectId));
    }), [categoryProjectIds, filters.categoryId, filters.projectId, initData.accounts]);

    const applyResponse = (response, requestedPage) => {
        const data = unwrap(response);
        const totalCount = Number(data.totalVouchers ?? data.totalCount ?? data.totalRecords ?? 0);
        const responsePageSize = Number(data.pageSize || PAGE_SIZE);
        setVouchers(normalizeVouchers(data));
        setTotalPages(Math.max(1, Number(data.totalPages || Math.ceil(totalCount / responsePageSize) || 1)));
        setPage(requestedPage);
        setTotals({
            debit: Number(data.totalDebit ?? data.totals?.debit ?? 0),
            credit: Number(data.totalCredit ?? data.totals?.credit ?? 0),
            voucherCount: totalCount,
        });
        setHasGenerated(true);
    };

    const loadPage = async (requestedPage, selectedFilters = activeFilters) => {
        abortRef.current?.abort();
        abortRef.current = new AbortController();
        setGenerating(true);
        setError('');
        try {
            const response = await paymentReceiptReportApi.getReport(
                selectedFilters,
                requestedPage,
                PAGE_SIZE,
                abortRef.current.signal,
            );
            applyResponse(response, requestedPage);
        } catch (requestError) {
            if (requestError.name !== 'CanceledError' && requestError.code !== 'ERR_CANCELED') {
                setError(requestError.message || 'Failed to generate the P&R voucher report.');
            }
        } finally {
            setGenerating(false);
        }
    };

    useEffect(() => {
        if (!hasGenerated) return undefined;

        const search = resultSearch.trim();
        if (search === String(activeFilters.search || '')) return undefined;

        const timeoutId = window.setTimeout(() => {
            const selected = { ...activeFilters, search };
            setActiveFilters(selected);
            loadPage(1, selected);
        }, 300);

        return () => window.clearTimeout(timeoutId);
        // loadPage intentionally uses the active report state captured for this search change.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [resultSearch, hasGenerated, activeFilters.search]);

    const submit = event => {
        event.preventDefault();
        if (!filters.projectId) {
            setError('Please select a project.');
            return;
        }
        if (filters.startDate && filters.endDate && filters.startDate > filters.endDate) {
            setError('Since date cannot be after To date.');
            return;
        }
        const selected = {
            ...filters,
            search: resultSearch.trim(),
        };
        setActiveFilters(selected);
        loadPage(1, selected);
    };

    const resetFilters = () => {
        abortRef.current?.abort();
        setFilters(blankFilters);
        setActiveFilters(blankFilters);
        setResultSearch('');
        setDateFilterType('custom');
        setVouchers([]);
        setTotals(emptyTotals);
        setPage(1);
        setTotalPages(1);
        setHasGenerated(false);
        setError('');
    };

    const updateFilter = (field, value) => setFilters(previous => ({ ...previous, [field]: value }));

    const changeDateFilter = value => {
        setDateFilterType(value);
        const range = getDateRange(value);
        setFilters(previous => ({ ...previous, ...range }));
    };

    const changeProject = value => setFilters(previous => ({
        ...previous,
        projectId: value == null ? '' : String(value),
        categoryId: '',
        accountId: '',
    }));

    const changeCategory = value => setFilters(previous => ({
        ...previous,
        categoryId: value == null ? '' : String(value),
        accountId: '',
    }));

    const fetchAll = async () => {
        const all = [];
        let nextPage = 1;
        let pageCount = 1;
        do {
            const response = await paymentReceiptReportApi.getReport(activeFilters, nextPage, EXPORT_PAGE_SIZE);
            const data = unwrap(response);
            all.push(...normalizeVouchers(data));
            const count = Number(data.totalVouchers ?? data.totalCount ?? data.totalRecords ?? all.length);
            pageCount = Number(data.totalPages || Math.ceil(count / EXPORT_PAGE_SIZE) || 1);
            nextPage += 1;
        } while (nextPage <= pageCount);
        return all;
    };

    const toRows = reportVouchers => reportVouchers.flatMap(voucher => voucher.entries.map(entry => ({
        'JV ID': `JV-${voucher.journalVoucherId}`,
        'Entry Date': formatDate(voucher.entryDate ?? entry.entryDate),
        Project: voucher.projectName ?? entry.projectName ?? '',
        Category: entry.categoryName ?? '',
        Account: entry.accountName ?? '',
        Description: entry.description ?? '',
        Instrument: paymentInstrumentLabel(entry.instrumentType),
        'Reference/Cheque No.': entry.instrumentType === 'Cash' ? '' : entry.referenceNumber ?? '',
        'Instrument Date': entry.instrumentType === 'Cash' ? '' : formatDate(entry.instrumentDate),
        Debit: Number(entry.debit || 0),
        Credit: Number(entry.credit || 0),
        'Filter Match': entry.isFilterMatch ? 'Yes' : 'No',
    })));

    const exportCsv = async () => {
        setExporting('csv');
        setError('');
        try {
            await exportToCsvAsync(toRows(await fetchAll()), `PaymentReceiptVouchers_${new Date().toISOString().slice(0, 10)}.csv`);
        } catch (requestError) {
            setError(requestError.message || 'Failed to export CSV.');
        } finally {
            setExporting('');
        }
    };

    const exportPdf = async () => {
        setExporting('pdf');
        setError('');
        try {
            const rows = toRows(await fetchAll()).map(row => ({
                id: row['JV ID'], date: row['Entry Date'], project: row.Project,
                category: row.Category, account: row.Account, description: row.Description,
                instrument: row.Instrument, reference: row['Reference/Cheque No.'],
                instrumentDate: row['Instrument Date'], debit: money(row.Debit), credit: money(row.Credit),
            }));
            await generatePdfAsync({
                orientation: 'landscape',
                format: 'a4',
                title: 'Payment & Receipt Voucher Report',
                projectName: initData.projects.find(project => String(idOf(project)) === String(activeFilters.projectId))?.name || '',
                logoDataUrl: await getProjectImageDataUrl(activeFilters.projectId),
                dateRange: activeFilters.startDate
                    ? `${activeFilters.startDate} to ${activeFilters.endDate || activeFilters.startDate}`
                    : null,
                generatedInfo: `Generated: ${new Date().toLocaleString()} | Vouchers: ${totals.voucherCount}`,
                columns: [
                    { header: 'JV', dataKey: 'id' }, { header: 'Date', dataKey: 'date' },
                    { header: 'Project', dataKey: 'project' }, { header: 'Category', dataKey: 'category' },
                    { header: 'Account', dataKey: 'account' }, { header: 'Description', dataKey: 'description' },
                    { header: 'Instrument', dataKey: 'instrument' }, { header: 'Reference', dataKey: 'reference' },
                    { header: 'Instrument Date', dataKey: 'instrumentDate' },
                    { header: 'Debit', dataKey: 'debit' }, { header: 'Credit', dataKey: 'credit' },
                ],
                rows,
                fontSize: 7,
                cellPadding: 1.5,
                filename: `PaymentReceiptVouchers_${new Date().toISOString().slice(0, 10)}.pdf`,
            });
        } catch (requestError) {
            setError(requestError.message || 'Failed to export PDF.');
        } finally {
            setExporting('');
        }
    };

    const pageNumbers = useMemo(() => {
        const count = Math.min(5, totalPages);
        const start = Math.max(1, Math.min(page - 2, totalPages - count + 1));
        return Array.from({ length: count }, (_, index) => start + index);
    }, [page, totalPages]);

    const controlsDisabled = generating || Boolean(exporting);

    if (loading) return <LoadingSpinner text="Loading report filters..." />;

    return (
        <div className="container-fluid payment-receipt-report">
            {error && (
                <Alert variant="danger" dismissible onClose={() => setError('')}>
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                </Alert>
            )}

            <Card className="shadow mb-4 report-parameters-card">
                <Card.Header className="py-3 d-flex justify-content-between align-items-center">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-filter me-2"></i>Report Parameters
                    </h6>
                </Card.Header>
                <Card.Body>
                    <Form onSubmit={submit}>
                        <div className="row mb-3">
                            <div className="col-xl-2 col-md-4 mb-3">
                                <Form.Label className="small text-muted">Quick Filter</Form.Label>
                                <Form.Select
                                    size="sm"
                                    aria-label="Quick date filter"
                                    value={dateFilterType}
                                    onChange={event => changeDateFilter(event.target.value)}
                                    disabled={controlsDisabled}
                                >
                                    {dateFilterOptions.map(option => (
                                        <option key={option.value} value={option.value}>{option.label}</option>
                                    ))}
                                </Form.Select>
                            </div>
                            <div className="col-xl-2 col-md-4 mb-3">
                                <Form.Label className="small">Since</Form.Label>
                                <InputGroup size="sm">
                                    <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        aria-label="Since date"
                                        value={filters.startDate}
                                        onChange={event => {
                                            updateFilter('startDate', event.target.value);
                                            setDateFilterType('custom');
                                        }}
                                        disabled={controlsDisabled}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-xl-2 col-md-4 mb-3">
                                <Form.Label className="small">To</Form.Label>
                                <InputGroup size="sm">
                                    <InputGroup.Text><i className="fas fa-calendar-alt"></i></InputGroup.Text>
                                    <Form.Control
                                        type="date"
                                        aria-label="To date"
                                        value={filters.endDate}
                                        onChange={event => {
                                            updateFilter('endDate', event.target.value);
                                            setDateFilterType('custom');
                                        }}
                                        disabled={controlsDisabled}
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-xl-2 col-md-6 mb-3">
                                <Form.Label>Project <span className="text-danger">*</span></Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
                                    <SearchableSelect
                                        ariaLabel="Project filter"
                                        value={filters.projectId || null}
                                        onChange={changeProject}
                                        onClear={() => changeProject(null)}
                                        options={initData.projects.map(item => ({ id: idOf(item), name: nameOf(item) }))}
                                        placeholder="Search project..."
                                        disabled={controlsDisabled}
                                        required
                                        showAllOptionsOnFocus
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-xl-2 col-md-6 mb-3">
                                <Form.Label>Category</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-list"></i></InputGroup.Text>
                                    <SearchableSelect
                                        ariaLabel="Category filter"
                                        value={filters.categoryId || null}
                                        onChange={changeCategory}
                                        onClear={() => changeCategory(null)}
                                        options={[{ id: null, name: 'All Categories' }, ...availableCategories.map(item => ({ id: idOf(item), name: nameOf(item) }))]}
                                        placeholder="Search category..."
                                        disabled={controlsDisabled}
                                        showAllOptionsOnFocus
                                    />
                                </InputGroup>
                            </div>
                            <div className="col-xl-2 col-md-6 mb-3">
                                <Form.Label>Account</Form.Label>
                                <InputGroup>
                                    <InputGroup.Text><i className="fas fa-user"></i></InputGroup.Text>
                                    <SearchableSelect
                                        ariaLabel="Account filter"
                                        value={filters.accountId || null}
                                        onChange={value => updateFilter('accountId', value == null ? '' : String(value))}
                                        onClear={() => updateFilter('accountId', '')}
                                        options={[{ id: null, name: 'All Accounts' }, ...availableAccounts.map(item => ({ id: idOf(item), name: nameOf(item) }))]}
                                        placeholder={filters.categoryId ? 'Search account...' : 'Select category first'}
                                        disabled={!filters.categoryId || controlsDisabled}
                                        showAllOptionsOnFocus
                                    />
                                </InputGroup>
                            </div>
                        </div>

                        <div className="row">
                            <div className="col-xl-4 col-md-6 ms-auto">
                                <div className="d-flex gap-2">
                                    <Button
                                        type="button"
                                        variant="outline-secondary"
                                        className="flex-fill"
                                        onClick={resetFilters}
                                        disabled={controlsDisabled}
                                    >
                                        <i className="fas fa-undo me-2"></i>Reset
                                    </Button>
                                    <Button
                                        type="submit"
                                        variant="primary"
                                        className="flex-fill"
                                        disabled={controlsDisabled || !filters.projectId}
                                        title={!filters.projectId ? 'Please select a project' : ''}
                                    >
                                        {generating ? (
                                            <><Spinner size="sm" className="me-2" />Generating...</>
                                        ) : (
                                            <><i className="fas fa-chart-bar me-2"></i>Generate Report</>
                                        )}
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </Form>
                </Card.Body>
            </Card>

            {hasGenerated && (
                <Card className="shadow mb-4 report-results-card">
                    <Card.Header className="py-3 d-flex flex-wrap justify-content-between align-items-center gap-2">
                        <h6 className="m-0 font-weight-bold text-primary">
                            <i className="fas fa-table me-2"></i>Payment &amp; Receipt Voucher Results
                        </h6>
                        <div className="d-flex flex-wrap align-items-center gap-2 report-actions">
                            <Badge bg="primary">{totals.voucherCount} vouchers</Badge>
                            {vouchers.some(voucher => voucher.entries.some(entry => entry.isFilterMatch)) && (
                                <Badge bg="warning" text="dark">Highlighted rows match line filters</Badge>
                            )}
                            <Button variant="outline-success" size="sm" onClick={exportCsv} disabled={!vouchers.length || controlsDisabled}>
                                {exporting === 'csv' ? <Spinner size="sm" className="me-1" /> : <i className="fas fa-file-csv me-1"></i>}
                                Export CSV
                            </Button>
                            <Button variant="outline-danger" size="sm" onClick={exportPdf} disabled={!vouchers.length || controlsDisabled}>
                                {exporting === 'pdf' ? <Spinner size="sm" className="me-1" /> : <i className="fas fa-file-pdf me-1"></i>}
                                Export PDF
                            </Button>
                        </div>
                    </Card.Header>

                    <Card.Body>
                        <div className="payment-receipt-report-container">
                            <div className="row mb-3">
                                <div className="col-12">
                                    <Form.Label htmlFor="paymentReceiptResultSearch">Search</Form.Label>
                                    <InputGroup>
                                        <InputGroup.Text><i className="fas fa-search"></i></InputGroup.Text>
                                        <Form.Control
                                            id="paymentReceiptResultSearch"
                                            aria-label="Search results"
                                            value={resultSearch}
                                            onChange={event => setResultSearch(event.target.value)}
                                            placeholder="Filter results..."
                                            disabled={controlsDisabled}
                                        />
                                    </InputGroup>
                                </div>
                            </div>

                            <div className="table-responsive pr-report-table-wrap">
                            <Table bordered striped hover className="table-sm align-middle mb-0" id="paymentReceiptVoucherReportTable">
                                <thead className="table-light">
                                    <tr>
                                        <th className="voucher-column">JV</th>
                                        <th className="date-column">Entry Date</th>
                                        <th className="account-column">Account</th>
                                        <th className="description-column">Description</th>
                                        <th className="instrument-column">Instrument</th>
                                        <th className="reference-column">Reference/Cheque No.</th>
                                        <th className="date-column">Instrument Date</th>
                                        <th className="money-column text-end">Debit</th>
                                        <th className="money-column text-end">Credit</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {generating && vouchers.length === 0 && (
                                        <tr><td colSpan={9} className="text-center py-5"><Spinner animation="border" size="sm" className="me-2" />Generating report...</td></tr>
                                    )}
                                    {!generating && vouchers.length === 0 && (
                                        <tr>
                                            <td colSpan={9} className="text-center text-muted py-5">
                                                <i className="fas fa-inbox fa-2x d-block mb-2 opacity-50"></i>
                                                No completed, approved P&amp;R vouchers match these filters.
                                            </td>
                                        </tr>
                                    )}
                                    {vouchers.map(voucher => (
                                        <Fragment key={voucher.journalVoucherId}>
                                            {voucher.entries.map((entry, index) => (
                                                <tr
                                                    key={entry.journalEntryId ?? `${voucher.journalVoucherId}_${index}`}
                                                    className={entry.isFilterMatch ? 'table-warning filter-match-row' : ''}
                                                >
                                                    {index === 0 && (
                                                        <>
                                                            <td rowSpan={voucher.entries.length} className="voucher-cell">
                                                                <a
                                                                    className="voucher-link"
                                                                    href={`/reporting/print-voucher?voucherId=${voucher.journalVoucherId}`}
                                                                    target="_blank"
                                                                    rel="noreferrer"
                                                                >
                                                                    JV-{voucher.journalVoucherId}
                                                                </a>
                                                            </td>
                                                            <td rowSpan={voucher.entries.length}>{formatDate(voucher.entryDate ?? entry.entryDate)}</td>
                                                        </>
                                                    )}
                                                    <td>{entry.accountName || '—'}</td>
                                                    <td className="description-cell">{entry.description || '—'}</td>
                                                    <td>{paymentInstrumentLabel(entry.instrumentType)}</td>
                                                    <td>{entry.instrumentType === 'Cash' ? '' : entry.referenceNumber || ''}</td>
                                                    <td>{entry.instrumentType === 'Cash' ? '' : formatDate(entry.instrumentDate)}</td>
                                                    <td className="text-end text-nowrap">{money(entry.debit)}</td>
                                                    <td className="text-end text-nowrap">{money(entry.credit)}</td>
                                                </tr>
                                            ))}
                                        </Fragment>
                                    ))}
                                </tbody>
                                {vouchers.length > 0 && (
                                    <tfoot className="table-secondary">
                                        <tr className="fw-bold">
                                            <td colSpan={7} className="text-end">TOTAL</td>
                                            <td className="text-end text-nowrap">{money(totals.debit)}</td>
                                            <td className="text-end text-nowrap">{money(totals.credit)}</td>
                                        </tr>
                                    </tfoot>
                                )}
                                </Table>
                            </div>
                        </div>
                    </Card.Body>

                    {totalPages > 1 && (
                        <Card.Footer className="d-flex flex-wrap justify-content-between align-items-center gap-2">
                            <span className="small text-muted">Page {page} of {totalPages}</span>
                            <Pagination className="mb-0">
                                <Pagination.First disabled={page <= 1 || generating} onClick={() => loadPage(1)} />
                                <Pagination.Prev disabled={page <= 1 || generating} onClick={() => loadPage(page - 1)} />
                                {pageNumbers.map(pageNumber => (
                                    <Pagination.Item
                                        key={pageNumber}
                                        active={pageNumber === page}
                                        disabled={generating}
                                        onClick={() => loadPage(pageNumber)}
                                    >
                                        {pageNumber}
                                    </Pagination.Item>
                                ))}
                                <Pagination.Next disabled={page >= totalPages || generating} onClick={() => loadPage(page + 1)} />
                                <Pagination.Last disabled={page >= totalPages || generating} onClick={() => loadPage(totalPages)} />
                            </Pagination>
                        </Card.Footer>
                    )}
                </Card>
            )}
        </div>
    );
};

export default PaymentReceiptVoucherReport;
