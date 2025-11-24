import React from 'react';
import { Link } from 'react-router-dom';

const AccessDenied = () => {
    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6">
                    <div className="card shadow">
                        <div className="card-header bg-danger text-white">
                            <h3 className="mb-0"><i className="fas fa-exclamation-triangle me-2"></i>Access Denied</h3>
                        </div>
                        <div className="card-body">
                            <div className="text-center mb-4">
                                <i className="fas fa-lock text-danger" style={{ fontSize: '4rem' }}></i>
                            </div>
                            <p className="lead text-center">You do not have permission to access this resource.</p>
                            <p className="text-center">Please contact your system administrator if you believe this is an error.</p>
                        </div>
                        <div className="card-footer">
                            <div className="d-grid gap-2">
                                <Link to="/home/entry" className="btn btn-primary">
                                    <i className="fas fa-home me-2"></i>Return to Home
                                </Link>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AccessDenied;

