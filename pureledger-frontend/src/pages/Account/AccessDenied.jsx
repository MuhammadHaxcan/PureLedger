import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { usePermissions } from '../../hooks/usePermissions';
import { usePageTitle } from '../../hooks/usePageTitle';

const AccessDenied = () => {
    usePageTitle('Access Denied');
    const navigate = useNavigate();
    const location = useLocation();
    const { user, navigationMenu } = useAuth();
    const {
        canAccessHomeEntry,
        canAccessHome,
        canAccessMetaData,
        canAccessReporting,
        canAccessProcessDC,
        canAccessStockTransfers
    } = usePermissions();
    const resolveFirstAccessibleRoute = () => {
        // Find first accessible route from navigation menu
        const firstItem = navigationMenu?.[0];
        if (firstItem) {
            return firstItem.items?.[0]?.path ?? '/home/entry';
        }
        // Fallback: check permissions directly
        if (canAccessHomeEntry || canAccessHome) return '/home/entry';
        if (canAccessMetaData) return '/metadata/projects';
        if (canAccessReporting) return '/reporting/single-doc';
        if (canAccessProcessDC) return '/delivery/process-dc';
        if (canAccessStockTransfers) return '/stock-transfers';
        return '/account/access-denied';
    };
    const firstAccessibleRoute = resolveFirstAccessibleRoute();

    const attemptedPath = location.state?.from?.pathname || location.pathname;

    return (
        <div className="container-fluid">
            <div className="row justify-content-center">
                <div className="col-md-8">
                    <div className="card shadow">
                        <div className="card-header bg-danger text-white">
                            <h3 className="mb-0">
                                <i className="fas fa-exclamation-triangle me-2"></i>Access Denied
                            </h3>
                        </div>
                        <div className="card-body">
                            <div className="text-center mb-4">
                                <i className="fas fa-lock text-danger" style={{ fontSize: '4rem' }}></i>
                            </div>
                            <h4 className="text-center mb-3">You do not have permission to access this resource.</h4>
                            
                            {attemptedPath && attemptedPath !== '/account/access-denied' && (
                                <div className="alert alert-warning">
                                    <strong>Attempted to access:</strong> <code>{attemptedPath}</code>
                                </div>
                            )}

                            <p className="text-center">
                                You don't have the necessary permissions to view this page. 
                                Please contact your system administrator if you believe this is an error.
                            </p>

                            {user?.isAdmin && (
                                <div className="alert alert-info">
                                    <i className="fas fa-info-circle me-2"></i>
                                    <strong>Note:</strong> You are logged in as an administrator. 
                                    If you're seeing this message, there may be a configuration issue.
                                </div>
                            )}
                        </div>
                        <div className="card-footer">
                            <div className="d-grid gap-2">
                                <button 
                                    onClick={() => navigate(firstAccessibleRoute)} 
                                    className="btn btn-primary"
                                >
                                    <i className="fas fa-home me-2"></i>
                                    Go to Dashboard
                                </button>
                                <button 
                                    onClick={() => navigate(-1)} 
                                    className="btn btn-secondary"
                                >
                                    <i className="fas fa-arrow-left me-2"></i>
                                    Go Back
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AccessDenied;

