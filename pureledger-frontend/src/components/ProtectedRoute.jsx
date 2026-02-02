import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { usePermissions } from '../hooks/usePermissions';
import { PAGE_PERMISSIONS } from '../constants/permissions';

const ProtectedRoute = ({ children, requireAdmin = false, requireSystemAdmin = false, requiredPermission = null }) => {
  const { isAuthenticated, user, loading, userAccess, permissions, isSystemAdmin } = useAuth();
  const { canAccessPage } = usePermissions();
  const location = useLocation();

  // Show loading while auth state is being determined
  if (loading) {
    return (
      <div className="d-flex justify-content-center align-items-center" style={{ minHeight: '100vh' }}>
        <div className="spinner-border" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  // Redirect to login if not authenticated
  if (!isAuthenticated) {
    // Redirect system admin routes to system admin login
    if (requireSystemAdmin || location.pathname.startsWith('/system-admin')) {
      return <Navigate to="/system-admin/login" replace state={{ from: location }} />;
    }
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  // For system admin-only routes
  if (requireSystemAdmin) {
    if (!isSystemAdmin) {
      return <Navigate to="/system-admin/login" replace state={{ from: location }} />;
    }
    // System admins pass through - they don't need other checks
    return children ? children : <Outlet />;
  }

  // For admin-only routes (tenant admin)
  if (requireAdmin && !user?.isAdmin) {
    return <Navigate to="/account/access-denied" replace state={{ from: location }} />;
  }

  // Admin users bypass all permission checks
  if (user?.isAdmin) {
    return children ? children : <Outlet />;
  }

  // Note: canAccessPage now handles checking userAccess directly if permissions aren't ready
  // So we don't need to wait here - the hook will check userAccess if needed

  // Check page permission if requiredPermission is provided
  if (requiredPermission) {
    if (!canAccessPage(requiredPermission)) {
      return <Navigate to="/account/access-denied" replace state={{ from: location }} />;
    }
  } else {
    // Auto-detect permission from current path
    const currentPath = location.pathname;
    const requiredPermissions = PAGE_PERMISSIONS[currentPath];
    
    // Only check permissions if this route requires them
    if (requiredPermissions && requiredPermissions.length > 0) {
      if (!canAccessPage(currentPath)) {
        return <Navigate to="/account/access-denied" replace state={{ from: location }} />;
      }
    }
  }

  // If children provided, use them (for inline usage), otherwise use Outlet (for nested routes)
  return children ? children : <Outlet />;
};

export default ProtectedRoute;
