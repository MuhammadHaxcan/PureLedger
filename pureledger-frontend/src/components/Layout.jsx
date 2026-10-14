import { useState } from 'react';
import { Navbar, Nav, NavDropdown, Container } from 'react-bootstrap';
import { Link, useNavigate, Outlet } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useTheme } from '../contexts/theme';
import NavbarTitleContext from '../contexts/NavbarTitleContext';
import NavigationMenu from './NavigationMenu';

const Layout = () => {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();
  const [navbarPageTitle, setNavbarPageTitle] = useState(null);
  const nextThemeLabel = theme === 'dark' ? 'Light' : 'Dark';

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <>
      <Navbar bg="primary" variant="dark" expand="lg" className="shadow-sm">
        <Container fluid className="px-4">
          <Navbar.Brand as={Link} to="/">
            <i className="fas fa-book-open me-2"></i>PureLedger
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="basic-navbar-nav" />
          <Navbar.Collapse id="basic-navbar-nav">
            <NavigationMenu />
            {navbarPageTitle && (
              <div className="navbar-page-title" aria-label={`Current page: ${navbarPageTitle}`}>
                <i className="fas fa-book-open" aria-hidden="true"></i>
                <span>{navbarPageTitle}</span>
              </div>
            )}
            <Nav className={`${navbarPageTitle ? '' : 'ms-auto'} navbar-actions`}>
              <button
                type="button"
                className="theme-toggle"
                onClick={toggleTheme}
                aria-label={`Switch to ${nextThemeLabel.toLowerCase()} mode`}
                aria-pressed={theme === 'dark'}
                title={`Switch to ${nextThemeLabel.toLowerCase()} mode`}
              >
                <i
                  className={`fas ${theme === 'dark' ? 'fa-sun' : 'fa-moon'}`}
                  aria-hidden="true"
                ></i>
                <span>{nextThemeLabel}</span>
              </button>
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
                      <NavDropdown.Item as={Link} to="/admin/error-logs">
                        <i className="fas fa-bug"></i> Error Logs
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
          <NavbarTitleContext.Provider value={setNavbarPageTitle}>
            <Outlet />
          </NavbarTitleContext.Provider>
        </main>
      </Container>
    </>
  );
};

export default Layout;

