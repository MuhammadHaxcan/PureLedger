import { Alert, Button } from 'react-bootstrap';
import { getAllErrorMessages, getErrorReference, normalizeApiError } from '../utils/errorHandler';

const isAdminUser = () => {
  try {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return user?.isAdmin === true || user?.role === 'Admin' || user?.roles?.includes?.('Admin');
  } catch {
    return false;
  }
};

/**
 * Reusable error alert component
 * @param {Object} props
 * @param {Error|string|string[]} props.error - Error object, error message string, or array of error messages
 * @param {boolean} props.dismissible - Whether the alert can be dismissed
 * @param {Function} props.onDismiss - Callback when alert is dismissed
 * @param {string} props.variant - Bootstrap variant (danger, warning, info)
 * @param {string} props.className - Additional CSS classes
 */
const ErrorAlert = ({ 
  error, 
  dismissible = false, 
  onDismiss, 
  variant = 'danger',
  className = '' 
}) => {
  if (!error) return null;

  const normalizedError = normalizeApiError(error);
  const reference = getErrorReference(normalizedError);
  const hasReference = Boolean(reference.errorId || reference.correlationId);
  const supportText = [
    reference.errorId ? `Error ID: ${reference.errorId}` : null,
    reference.correlationId ? `Correlation ID: ${reference.correlationId}` : null,
    reference.statusCode ? `Status: ${reference.statusCode}` : null,
    reference.errorCode ? `Code: ${reference.errorCode}` : null,
  ].filter(Boolean).join('\n');

  // Get error messages
  let errorMessages = [];
  
  if (typeof error === 'string') {
    errorMessages = [error];
  } else if (Array.isArray(error)) {
    errorMessages = error;
  } else {
    errorMessages = getAllErrorMessages(error);
  }

  if (errorMessages.length === 0) return null;

  const copyReference = async () => {
    if (!supportText || !navigator.clipboard) return;
    await navigator.clipboard.writeText(supportText);
  };

  return (
    <Alert 
      variant={variant} 
      dismissible={dismissible} 
      onClose={onDismiss}
      className={className}
    >
      {errorMessages.length === 1 ? (
        <div>{errorMessages[0]}</div>
      ) : (
        <ul className="mb-0">
          {errorMessages.map((msg, index) => (
            <li key={index}>{msg}</li>
          ))}
        </ul>
      )}
      {hasReference && (
        <div className="mt-2 small">
          {reference.errorId && (
            <div>
              <strong>Reference:</strong> <code>{reference.errorId}</code>
            </div>
          )}
          {reference.correlationId && (
            <div>
              <strong>Correlation:</strong> <code>{reference.correlationId}</code>
            </div>
          )}
          <div className="mt-2 d-flex flex-wrap gap-2">
            {navigator.clipboard && (
              <Button size="sm" variant="outline-secondary" onClick={copyReference}>
                Copy support details
              </Button>
            )}
            {isAdminUser() && reference.errorId && (
              <Button
                size="sm"
                variant="outline-primary"
                href={`/admin/error-logs?errorId=${encodeURIComponent(reference.errorId)}`}
              >
                View admin log
              </Button>
            )}
          </div>
        </div>
      )}
    </Alert>
  );
};

export default ErrorAlert;


