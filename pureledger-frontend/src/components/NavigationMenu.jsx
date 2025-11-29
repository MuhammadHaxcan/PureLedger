import { Nav, NavDropdown } from 'react-bootstrap';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const NavigationMenu = () => {
  const { user, navigationMenu, loading } = useAuth();
  const location = useLocation();

  // Show loading state
  if (loading) {
    return (
      <Nav className="me-auto">
        <Nav.Link disabled className="text-muted">
          <i className="fas fa-spinner fa-spin me-2"></i>Loading...
        </Nav.Link>
      </Nav>
    );
  }

  // If user has no access to any controller, show a message
  if (!user?.isAdmin && (!navigationMenu || navigationMenu.length === 0)) {
    return (
      <Nav className="me-auto">
        <Nav.Link disabled className="text-muted">
          <i className="fas fa-info-circle me-2"></i>No access granted. Please contact your administrator.
        </Nav.Link>
      </Nav>
    );
  }

  // Navigation menu is already filtered and built at login time
  return (
    <Nav className="me-auto">
      {navigationMenu.map((item) => (
        <NavDropdown
          key={item.title}
          title={
            <>
              <i className={item.icon}></i> {item.title}
            </>
          }
          id={`${item.title.toLowerCase()}-dropdown`}
        >
          {item.items.map((subItem) => (
            <NavDropdown.Item
              key={subItem.path}
              as={Link}
              to={subItem.path}
              active={location.pathname === subItem.path}
            >
              <i className={subItem.icon}></i> {subItem.title}
            </NavDropdown.Item>
          ))}
        </NavDropdown>
      ))}
    </Nav>
  );
};

export default NavigationMenu;

