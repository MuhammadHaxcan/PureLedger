import { Navbar, Nav, NavDropdown, Container } from 'react-bootstrap';
import { Link, useNavigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const SystemAdminLayout = () => {
    const { user, logout } = useAuth();
    const navigate = useNavigate();

    const handleLogout = () => {
        logout();
        navigate('/system-admin/login');
    };

    return (
        <>
            <Navbar bg="dark" variant="dark" expand="lg" className="shadow-sm">
                <Container fluid className="px-4">
                    <Navbar.Brand as={Link} to="/system-admin/tenants">
                        <i className="fas fa-shield-alt me-2"></i>PureLedger Admin
                    </Navbar.Brand>
                    <Navbar.Toggle aria-controls="admin-navbar-nav" />
                    <Navbar.Collapse id="admin-navbar-nav">
                        <Nav className="me-auto">
                            <Nav.Link as={Link} to="/system-admin/tenants">
                                <i className="fas fa-building me-1"></i>Tenants
                            </Nav.Link>
                            <Nav.Link as={Link} to="/system-admin/migrations">
                                <i className="fas fa-database me-1"></i>Migrations
                            </Nav.Link>
                        </Nav>
                        <Nav className="ms-auto">
                            {user && (
                                <NavDropdown
                                    title={
                                        <>
                                            <i className="fas fa-user-shield me-1"></i> {user.fullName}
                                        </>
                                    }
                                    id="admin-dropdown"
                                >
                                    <NavDropdown.Item onClick={handleLogout}>
                                        <i className="fas fa-sign-out-alt me-2"></i>Logout
                                    </NavDropdown.Item>
                                </NavDropdown>
                            )}
                        </Nav>
                    </Navbar.Collapse>
                </Container>
            </Navbar>
            <Container fluid className="py-2 px-4">
                <main role="main">
                    <Outlet />
                </main>
            </Container>
        </>
    );
};

export default SystemAdminLayout;
