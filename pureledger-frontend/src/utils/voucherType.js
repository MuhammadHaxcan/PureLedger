export const resolvePrintVoucherType = (responseData, requestedType = 'JV') => (
    responseData?.resolvedVoucherType ||
    responseData?.type ||
    responseData?.data?.resolvedVoucherType ||
    responseData?.data?.voucherKind ||
    (responseData?.data?.isPaymentReceiptVoucher ? 'PaymentReceipt' : requestedType)
);

export const isPaymentReceiptVoucherType = value =>
    String(value || '').toLowerCase() === 'paymentreceipt';
