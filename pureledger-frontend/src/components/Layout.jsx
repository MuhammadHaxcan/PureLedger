import { Navbar, Nav, NavDropdown, Container, Badge } from 'react-bootstrap';
import { Link, useNavigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import NavigationMenu from './NavigationMenu';

const Layout = () => {
  const { user, logout, tenant } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <>
      <Navbar bg="primary" variant="dark" expand="lg" className="shadow-sm">
        <Container fluid className="px-4">
          <Navbar.Brand as={Link} to="/home/entry">
            <i className="fas fa-book-open me-2"></i>PureLedger
            {tenant?.tenantName && (
              <Badge bg="light" text="dark" className="ms-2" style={{ fontSize: '0.65rem', verticalAlign: 'middle' }}>
                {tenant.tenantName}
              </Badge>
            )}
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="basic-navbar-nav" />
          <Navbar.Collapse id="basic-navbar-nav">
            <NavigationMenu />
            <Nav className="ms-auto">
              {user ? (
                <NavDropdown
                  title={
                    <>
                      <i className="fas fa-user-circle me-1"></i> {user.fullName}
                    </>
                  }
                  id="user-dropdown"
                >
                  <NavDropdown.Item as={Link} to="/admin/edit-profile">
                    <i className="fas fa-user-edit"></i> Edit Profile
                  </NavDropdown.Item>
                  {user?.isAdmin && (
                    <>
                      <NavDropdown.Divider />
                      <NavDropdown.Item as={Link} to="/admin/users">
                        <i className="fas fa-users-cog"></i> User Management
                      </NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/admin/cartage-settings">
                        <i className="fas fa-truck"></i> Cartage Settings
                      </NavDropdown.Item>
                      <NavDropdown.Item as={Link} to="/admin/signatures">
                        <i className="fas fa-signature"></i> Signatures
                      </NavDropdown.Item>
                    </>
                  )}
                  <NavDropdown.Divider />
                  <NavDropdown.Item onClick={handleLogout}>
                    <i className="fas fa-sign-out-alt"></i> Logout
                  </NavDropdown.Item>
                </NavDropdown>
              ) : (
                <Nav.Link as={Link} to="/login">
                  <i className="fas fa-sign-in-alt me-1"></i> Login
                </Nav.Link>
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

export default Layout;

