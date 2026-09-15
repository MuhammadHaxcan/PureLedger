import { useNavigate, useParams } from 'react-router-dom';
import PaymentReceiptForm from '../Home/PaymentReceiptForm';
import { usePageTitle } from '../../hooks/usePageTitle';

const EditPaymentReceiptApproval = () => {
    usePageTitle('Edit Pending Payment & Receipt', 'Edit Pending P&R');
    const { approvalId } = useParams();
    const navigate = useNavigate();

    return (
        <div className="container-fluid">
            <div className="d-flex flex-wrap justify-content-end align-items-center gap-2 mb-3">
                <button type="button" className="btn btn-outline-secondary" onClick={() => navigate('/approvals/pending')}>
                    <i className="fas fa-arrow-left me-1"></i>Back to Approvals
                </button>
            </div>
            <PaymentReceiptForm
                mode="pending"
                approvalId={approvalId}
                onSubmitted={(_, message) => navigate('/approvals/pending', {
                    replace: true,
                    state: { approvalMessage: message },
                })}
            />
        </div>
    );
};

export default EditPaymentReceiptApproval;
