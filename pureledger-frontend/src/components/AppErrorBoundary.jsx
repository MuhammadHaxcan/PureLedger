import { Component } from 'react';
import { Alert, Button } from 'react-bootstrap';
import { logClientError } from '../services/clientErrorLogger';

class AppErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    logClientError(error, {
      type: 'react.error_boundary',
      componentStack: errorInfo?.componentStack,
    });
  }

  render() {
    if (this.state.hasError) {
      return (
        <Alert variant="danger" className="m-4">
          <Alert.Heading>Something went wrong</Alert.Heading>
          <p className="mb-3">
            The error has been logged for review. Please refresh the page and try again.
          </p>
          <Button variant="outline-danger" onClick={() => window.location.reload()}>
            Refresh Page
          </Button>
        </Alert>
      );
    }

    return this.props.children;
  }
}

export default AppErrorBoundary;
