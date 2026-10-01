import PaymentReceiptForm from './PaymentReceiptForm';
import { usePageTitle } from '../../hooks/usePageTitle';
import { Alert } from 'react-bootstrap';
import { useSearchParams } from 'react-router-dom';

const positiveId = value => {
    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
};

const PaymentReceipt = () => {
    const [searchParams] = useSearchParams();
    const voucherIdParameter = searchParams.get('voucherId');
    const journalVoucherId = positiveId(voucherIdParameter);
    const isRevision = journalVoucherId != null;

    usePageTitle(
        isRevision ? 'Edit Payment & Receipt' : 'Payment & Receipt',
        isRevision ? `Edit Payment & Receipt - JV-${journalVoucherId}` : 'Payment & Receipt'
    );

    return (
        <div className="container-fluid">
            {voucherIdParameter && !isRevision ? (
                <Alert variant="danger">
                    <i className="fas fa-exclamation-circle me-2"></i>Invalid Payment &amp; Receipt voucher ID.
                </Alert>
            ) : (
                <PaymentReceiptForm
                    key={journalVoucherId ?? 'create'}
                    mode={isRevision ? 'revision' : 'create'}
                    journalVoucherId={journalVoucherId}
                />
            )}
        </div>
    );
};

export default PaymentReceipt;
