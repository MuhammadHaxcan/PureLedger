import { Nav, NavDropdown } from 'react-bootstrap';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useEffect, useState } from 'react';
import apiClient from '../services/api';

const NavigationMenu = () => {
  const { user } = useAuth();
  const location = useLocation();
  const [userAccess, setUserAccess] = useState({ controllers: [], actions: {} });

  useEffect(() => {
    if (user) {
      loadUserAccess();
    }
  }, [user]);

  const loadUserAccess = async () => {
    try {
      const response = await apiClient.get('/AccountApi/access');
      if (response.data.success) {
        setUserAccess({
          controllers: response.data.controllers || [],
          actions: response.data.actions || {},
        });
      }
    } catch (error) {
      console.error('Error loading user access:', error);
    }
  };

  const hasAccess = (controller, action = null) => {
    if (user?.isAdmin) return true;
    if (action) {
      return userAccess.actions[controller]?.includes(action) || false;
    }
    return userAccess.controllers.includes(controller);
  };

  const navigationItems = [
    {
      title: 'Home',
      icon: 'fas fa-home',
      controller: 'Home',
      items: [
        { action: 'Entry', title: 'New Entry', icon: 'fas fa-plus-circle', path: '/home/entry' },
        { action: 'VoucherEntry', title: 'Edit Entry', icon: 'fas fa-edit', path: '/home/voucher-entry' },
        { action: 'BaseVouchers', title: 'Base Vouchers', icon: 'fas fa-layer-group', path: '/home/base-vouchers' },
        { action: 'ModifiedEntries', title: 'Modified Entries', icon: 'fas fa-history', path: '/home/modified-entries' },
        { action: 'Recipes', title: 'Recipes', icon: 'fas fa-book-open', path: '/home/recipes' },
      ],
    },
    {
      title: 'Metadata',
      icon: 'fas fa-database',
      controller: 'MetaData',
      items: [
        { action: 'Projects', title: 'Projects', icon: 'fas fa-project-diagram', path: '/metadata/projects' },
        { action: 'Categories', title: 'Categories', icon: 'fas fa-sitemap', path: '/metadata/categories' },
        { action: 'Accounts', title: 'Accounts', icon: 'fas fa-users', path: '/metadata/accounts' },
        { action: 'Locations', title: 'Locations', icon: 'fas fa-map-marker-alt', path: '/metadata/locations' },
        { action: 'Bookers', title: 'Bookers', icon: 'fas fa-user-tie', path: '/metadata/bookers' },
      ],
    },
    {
      title: 'Reports',
      icon: 'fas fa-chart-bar',
      controller: 'Reporting',
      items: [
        { action: 'SingleDoc', title: 'Single Document', icon: 'fas fa-file-alt', path: '/reporting/single-doc' },
        { action: 'AgingReport', title: 'Aging Report', icon: 'fas fa-calendar-alt', path: '/reporting/aging-report' },
        { action: 'AccountReport', title: 'Account Report', icon: 'fas fa-user-circle', path: '/reporting/account-report' },
        { action: 'MasterReport', title: 'Master Report', icon: 'fas fa-file-invoice', path: '/reporting/master-report' },
        { action: 'ProductReport', title: 'Product Report', icon: 'fas fa-box', path: '/reporting/product-report' },
        { action: 'MissedEntriesReport', title: 'Missed Entries', icon: 'fas fa-exclamation-circle', path: '/reporting/missed-entries' },
      ],
    },
    {
      title: 'Delivery',
      icon: 'fas fa-truck',
      controller: 'DeliveryChallan',
      items: [
        { action: 'ProcessDC', title: 'Delivery Challan', icon: 'fas fa-truck', path: '/delivery/process-dc' },
      ],
    },
  ];

  const filteredItems = navigationItems.filter((item) => hasAccess(item.controller));

  return (
    <Nav className="me-auto">
      {filteredItems.map((item) => {
        const accessibleItems = item.items.filter((subItem) =>
          hasAccess(item.controller, subItem.action)
        );

        if (accessibleItems.length === 0) return null;

        return (
          <NavDropdown
            key={item.title}
            title={
              <>
                <i className={item.icon}></i> {item.title}
              </>
            }
            id={`${item.title.toLowerCase()}-dropdown`}
          >
            {accessibleItems.map((subItem) => (
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
        );
      })}
      {user?.isAdmin && (
        <Nav.Link as={Link} to="/admin/users">
          <i className="fas fa-users-cog"></i> Admin
        </Nav.Link>
      )}
    </Nav>
  );
};

export default NavigationMenu;

