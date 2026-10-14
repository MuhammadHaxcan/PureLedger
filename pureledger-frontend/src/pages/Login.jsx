import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Container, Card, Form, Button, InputGroup } from 'react-bootstrap';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import { usePageTitle } from '../hooks/usePageTitle';

const Login = () => {
  usePageTitle('Login');
  // Keep the existing storage key for remembered usernames and email addresses.
  const [username, setUsername] = useState(() => localStorage.getItem('rememberedUsername') || '');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(() => Boolean(localStorage.getItem('rememberedUsername')));
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const { showError } = useToast();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // Check for session expired parameter on mount
  useEffect(() => {
    const sessionExpired = searchParams.get('sessionExpired');
    if (sessionExpired) {
      showError('Your session has expired due to inactivity. Please log in again.', {
        autoClose: 8000,
        toastId: 'session-expired-login',
      });
    }
  }, [searchParams, showError]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);

    const identifier = username.trim();
    const result = await login(identifier, password);
    setLoading(false);

    if (result.success) {
      // Handle remember me functionality
      if (rememberMe) {
        localStorage.setItem('rememberedUsername', identifier);
      } else {
        localStorage.removeItem('rememberedUsername');
      }

      // Redirect to the first accessible route or default
      const redirectTo = result.redirectTo || '/';
      navigate(redirectTo);
    } else {
      showError(result.message || 'Invalid username/email or password');
    }
  };

  return (
    <Container className="d-flex align-items-center justify-content-center" style={{ minHeight: '100vh' }}>
      <Card style={{ width: '100%', maxWidth: '400px' }}>
        <Card.Body>
          <Card.Title className="text-center mb-4">
            <i className="fas fa-book-open me-2"></i>PureLedger
          </Card.Title>
          <Form onSubmit={handleSubmit}>
            <Form.Group className="mb-3" controlId="login-identifier">
              <Form.Label>Username or email</Form.Label>
              <Form.Control
                type="text"
                placeholder="Enter username or email"
                name="username"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
                autoFocus
              />
            </Form.Group>
            <Form.Group className="mb-3" controlId="login-password">
              <Form.Label>Password</Form.Label>
              <InputGroup>
                <Form.Control
                  type={showPassword ? "text" : "password"}
                  placeholder="Enter password"
                  name="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
                <Button
                  variant="outline-secondary"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{ borderLeft: 'none' }}
                >
                  <i className={showPassword ? "fas fa-eye-slash" : "fas fa-eye"}></i>
                </Button>
              </InputGroup>
            </Form.Group>
            <Form.Group className="mb-3">
              <Form.Check
                id="login-remember-me"
                type="checkbox"
                label="Remember me"
                checked={rememberMe}
                onChange={(e) => setRememberMe(e.target.checked)}
              />
            </Form.Group>
            <Button variant="primary" type="submit" className="w-100" disabled={loading}>
              {loading ? (
                <>
                  <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                  Logging in...
                </>
              ) : (
                <>
                  <i className="fas fa-sign-in-alt me-2"></i>Login
                </>
              )}
            </Button>
          </Form>
        </Card.Body>
      </Card>
    </Container>
  );
};

export default Login;

