import { paymentInstrumentLabel, normalizePaymentInstrumentType } from '../constants/paymentInstruments';

export const journalMoney = value => Number(value || 0).toLocaleString('en-PK', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
});

// Dates from DateOnly must not move to another day in a browser's time zone.
export const journalDate = value => {
    const match = String(value || '').match(/^(\d{4})-(\d{2})-(\d{2})/);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : '';
};

export const journalInstrument = entry => entry.instrumentType == null
    ? '' : paymentInstrumentLabel(entry.instrumentType);
export const journalIsCash = entry => entry.instrumentType != null &&
    normalizePaymentInstrumentType(entry.instrumentType) === 'Cash';

const accountName = entry => entry.accountName ?? entry.AccountName ?? '';
const accountKey = entry => {
    const id = entry.accountId ?? entry.AccountId;
    if (id != null && Number(id) > 0) return `id:${id}`;
    const name = accountName(entry).trim();
    return name ? `name:${name.toLowerCase()}` : null;
};

export const getJournalPrintModel = (voucherData, filter = 'all') => {
    const normalizedFilter = String(filter || 'all').toLowerCase();
    const entries = voucherData.entries || [];
    const debitEntries = entries.filter(entry => Number(entry.debit) > 0);
    const creditEntries = entries.filter(entry => Number(entry.credit) > 0);
    const totalDebit = entries.reduce((sum, entry) => sum + Number(entry.debit || 0), 0);
    const totalCredit = entries.reduce((sum, entry) => sum + Number(entry.credit || 0), 0);
    const debitAccounts = new Set(debitEntries.map(accountKey));
    const hidden = entry => normalizedFilter === 'debit' ? Number(entry.credit) > 0
        : normalizedFilter === 'credit' ? Number(entry.debit) > 0 : false;
    const displayedEntries = ['debit', 'credit'].includes(normalizedFilter)
        ? [...entries.filter(entry => !hidden(entry)), ...entries.filter(hidden)] : entries;

    // Condense only a complete, balanced voucher with an identifiable recipient.
    // Mixed debit/credit rows and unbalanced data keep every row in the journal table.
    const vendorCopy = normalizedFilter === 'debit' && debitAccounts.size === 1 &&
        !debitAccounts.has(null) && debitEntries.every(entry => accountName(entry).trim()) &&
        !voucherData.isStockTransfer && entries.every(entry => Number(entry.accountGroupId ?? entry.AccountGroupId) !== 2) &&
        creditEntries.length > 0 && Math.abs(totalDebit - totalCredit) < 0.00005 &&
        entries.every(entry => !(Number(entry.debit) > 0 && Number(entry.credit) > 0) &&
            (Number(entry.debit) > 0 || Number(entry.credit) > 0));

    // Normally the funding/credit rows are the individual slips. If instruments
    // exist exclusively on debit rows, use those rows to retain their slip details.
    const useDebitSlips = debitEntries.some(entry => entry.instrumentType != null) &&
        creditEntries.every(entry => entry.instrumentType == null);
    const slips = useDebitSlips ? debitEntries : creditEntries;
    const slipSide = useDebitSlips ? 'debit' : 'credit';
    const descriptions = [...new Set(debitEntries.map(entry => entry.description?.trim()).filter(Boolean))]
        .filter(description => !slips.some(entry => entry.description?.trim() === description));

    return {
        normalizedFilter, vendorCopy, displayedEntries, slips, slipSide,
        paidTo: vendorCopy ? accountName(debitEntries[0]) : '',
        totalDebit, totalCredit, descriptions,
        displayAccountName: entry => hidden(entry) ? '' : accountName(entry),
    };
};
