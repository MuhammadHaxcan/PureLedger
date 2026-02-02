import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams, Link } from 'react-router-dom';
import { Container, Card, Form, Button, InputGroup } from 'react-bootstrap';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import { usePageTitle } from '../hooks/usePageTitle';

const TenantLogin = () => {
    usePageTitle('Login');
    const [tenant, setTenant] = useState('');
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(false);
    const [loading, setLoading] = useState(false);
    const { login } = useAuth();
    const { showError } = useToast();
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();

    // Load remembered credentials on mount
    useEffect(() => {
        const rememberedTenant = localStorage.getItem('rememberedTenant');
        const rememberedUsername = localStorage.getItem('rememberedUsername');
        if (rememberedTenant && rememberedUsername) {
            setTenant(rememberedTenant);
            setUsername(rememberedUsername);
            setRememberMe(true);
        }
    }, []);

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

        // Format username as tenant|username
        const fullUsername = `${tenant.trim().toLowerCase()}|${username.trim().toLowerCase()}`;

        const result = await login(fullUsername, password);
        setLoading(false);

        if (result.success) {
            // Handle remember me functionality
            if (rememberMe) {
                localStorage.setItem('rememberedTenant', tenant.trim().toLowerCase());
                localStorage.setItem('rememberedUsername', username.trim().toLowerCase());
            } else {
                localStorage.removeItem('rememberedTenant');
                localStorage.removeItem('rememberedUsername');
            }

            // Redirect to the first accessible route or default
            const redirectTo = result.redirectTo || '/';
            navigate(redirectTo);
        } else {
            showError(result.message || 'Invalid credentials');
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
                        <Form.Group className="mb-3">
                            <Form.Label>Tenant</Form.Label>
                            <Form.Control
                                type="text"
                                placeholder="Enter tenant name (e.g., prime)"
                                value={tenant}
                                onChange={(e) => setTenant(e.target.value)}
                                required
                                autoFocus
                            />
                            <Form.Text className="text-muted">
                                Contact your administrator if you don't know your tenant name.
                            </Form.Text>
                        </Form.Group>
                        <Form.Group className="mb-3">
                            <Form.Label>Username</Form.Label>
                            <Form.Control
                                type="text"
                                placeholder="Enter username"
                                value={username}
                                onChange={(e) => setUsername(e.target.value)}
                                required
                            />
                        </Form.Group>
                        <Form.Group className="mb-3">
                            <Form.Label>Password</Form.Label>
                            <InputGroup>
                                <Form.Control
                                    type={showPassword ? "text" : "password"}
                                    placeholder="Enter password"
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
                    <hr />
                    <div className="text-center">
                        <small className="text-muted">
                            System Administrator? <Link to="/system-admin/login">Login here</Link>
                        </small>
                    </div>
                </Card.Body>
            </Card>
        </Container>
    );
};

export default TenantLogin;
