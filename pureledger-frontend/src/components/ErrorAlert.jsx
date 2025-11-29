import { Alert } from 'react-bootstrap';
import { getErrorMessage, getAllErrorMessages } from '../utils/errorHandler';

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
    </Alert>
  );
};

export default ErrorAlert;


