export const PAYMENT_INSTRUMENTS = [
    { value: 'Cash', label: 'Cash' },
    { value: 'Cheque', label: 'Cheque' },
    { value: 'BankTransfer', label: 'Bank Transfer' },
    { value: 'WireTransfer', label: 'Wire Transfer' },
];

export const normalizePaymentInstrumentType = value => {
    const numeric = Number(value);
    if (value !== '' && value != null && Number.isInteger(numeric) && PAYMENT_INSTRUMENTS[numeric]) {
        return PAYMENT_INSTRUMENTS[numeric].value;
    }
    return PAYMENT_INSTRUMENTS.find(item => item.value.toLowerCase() === String(value || '').toLowerCase())?.value
        || 'Cash';
};

export const paymentInstrumentLabel = value => {
    const normalized = normalizePaymentInstrumentType(value);
    return PAYMENT_INSTRUMENTS.find(item => item.value === normalized)?.label || normalized;
};
