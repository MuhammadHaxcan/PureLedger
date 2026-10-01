import React, { useEffect, useState } from 'react';
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
    const [firstAccessibleRoute, setFirstAccessibleRoute] = useState('/home/entry');

    useEffect(() => {
        // Find first accessible route from navigation menu
        if (navigationMenu && navigationMenu.length > 0) {
            const firstItem = navigationMenu[0];
            if (firstItem.items && firstItem.items.length > 0) {
                setFirstAccessibleRoute(firstItem.items[0].path);
            }
        } else {
            // Fallback: check permissions directly
            if (canAccessHomeEntry || canAccessHome) {
                setFirstAccessibleRoute('/home/entry');
            } else if (canAccessMetaData) {
                setFirstAccessibleRoute('/metadata/projects');
            } else if (canAccessReporting) {
                setFirstAccessibleRoute('/reporting/single-doc');
            } else if (canAccessProcessDC) {
                setFirstAccessibleRoute('/delivery/process-dc');
            } else if (canAccessStockTransfers) {
                setFirstAccessibleRoute('/stock-transfers');
            } else {
                setFirstAccessibleRoute('/account/access-denied');
            }
        }
    }, [
        navigationMenu,
        canAccessHomeEntry,
        canAccessHome,
        canAccessMetaData,
        canAccessReporting,
        canAccessProcessDC,
        canAccessStockTransfers
    ]);

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

