import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { Container, Card, Form, Button, InputGroup } from 'react-bootstrap';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import { usePageTitle } from '../hooks/usePageTitle';

const SystemAdminLogin = () => {
    usePageTitle('System Admin Login');
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [loading, setLoading] = useState(false);
    const { systemAdminLogin } = useAuth();
    const { showError } = useToast();
    const navigate = useNavigate();

    const handleSubmit = async (e) => {
        e.preventDefault();
        setLoading(true);

        const result = await systemAdminLogin(username, password);
        setLoading(false);

        if (result.success) {
            navigate('/system-admin/tenants');
        } else {
            showError(result.message || 'Invalid username or password');
        }
    };

    return (
        <Container className="d-flex align-items-center justify-content-center" style={{ minHeight: '100vh' }}>
            <Card style={{ width: '100%', maxWidth: '400px' }}>
                <Card.Body>
                    <Card.Title className="text-center mb-4">
                        <i className="fas fa-shield-alt me-2"></i>System Admin
                    </Card.Title>
                    <Form onSubmit={handleSubmit}>
                        <Form.Group className="mb-3">
                            <Form.Label>Username</Form.Label>
                            <Form.Control
                                type="text"
                                placeholder="Enter admin username"
                                value={username}
                                onChange={(e) => setUsername(e.target.value)}
                                required
                                autoFocus
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
                        <Button variant="warning" type="submit" className="w-100" disabled={loading}>
                            {loading ? (
                                <>
                                    <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                                    Logging in...
                                </>
                            ) : (
                                <>
                                    <i className="fas fa-sign-in-alt me-2"></i>Admin Login
                                </>
                            )}
                        </Button>
                    </Form>
                    <hr />
                    <div className="text-center">
                        <small className="text-muted">
                            Regular user? <Link to="/login">Login here</Link>
                        </small>
                    </div>
                </Card.Body>
            </Card>
        </Container>
    );
};

export default SystemAdminLogin;
