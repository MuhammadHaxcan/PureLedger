const parseNumericValue = (value) => {
    if (typeof value === 'number') return value;
    if (typeof value !== 'string') return Number.NaN;
    return Number(value.replace(/,/g, ''));
};

export const formatMoney = (value) => {
    if (value === null || value === undefined || value === '') return '';

    const numericValue = parseNumericValue(value);
    if (!Number.isFinite(numericValue)) return '';

    return numericValue.toLocaleString('en-PK', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
};

export const formatMoneyInput = (value) => {
    if (value === null || value === undefined || value === '') return '';

    const cleaned = String(value)
        .replace(/,/g, '')
        .replace(/[^\d.]/g, '');
    if (!cleaned) return '';

    const hasDecimalPoint = cleaned.includes('.');
    const [rawWhole = '', ...fractionParts] = cleaned.split('.');
    const whole = (rawWhole || '0').replace(/^0+(?=\d)/, '');
    const fraction = fractionParts.join('').slice(0, 2);
    const groupedWhole = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',');

    return hasDecimalPoint ? `${groupedWhole}.${fraction}` : groupedWhole;
};

export const parseMoneyInput = (value) => {
    if (value === null || value === undefined || value === '') return null;

    const numericValue = parseNumericValue(String(value));
    return Number.isFinite(numericValue) ? numericValue : null;
};
