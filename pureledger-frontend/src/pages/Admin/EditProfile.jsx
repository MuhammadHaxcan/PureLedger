import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import apiClient from '../../services/api';
import LoadingSpinner from '../../components/LoadingSpinner';
import { usePageTitle } from '../../hooks/usePageTitle';

const EditProfile = () => {
    usePageTitle('Edit Profile', 'Edit Your Profile');
    const navigate = useNavigate();
    const isSubmittingRef = useRef(false);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [formData, setFormData] = useState({
        userId: 0,
        username: '',
        fullName: '',
        email: '',
        password: '',
        confirmPassword: '',
        isAdmin: false,
        isActive: true,
        projectIds: [],
        accessPermissions: []
    });

    useEffect(() => {
        loadProfile();
    }, []);

    const loadProfile = async () => {
        setLoading(true);
        try {
            const response = await apiClient.get('/AdminApi/profile');
            if (response.data.success && response.data.data) {
                const userData = response.data.data;
                setFormData({
                    userId: userData.userId || 0,
                    username: userData.username || '',
                    fullName: userData.fullName || '',
                    email: userData.email || '',
                    password: '',
                    confirmPassword: '',
                    isAdmin: userData.isAdmin || false,
                    isActive: userData.isActive ?? true,
                    projectIds: userData.projectIds || [],
                    accessPermissions: userData.accessPermissions || []
                });
            }
        } catch {
            setError('Failed to load profile');
        } finally {
            setLoading(false);
        }
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (isSubmittingRef.current) return;
        isSubmittingRef.current = true;
        
        setSaving(true);
        setError('');
        setSuccess('');

        if (formData.password && formData.password !== formData.confirmPassword) {
            setError('Passwords do not match');
            setSaving(false);
            isSubmittingRef.current = false;
            return;
        }

        try {
            // Send all required fields for UserViewModel
            const updateData = {
                userId: formData.userId,
                username: formData.username,
                fullName: formData.fullName,
                email: formData.email || '',
                password: formData.password || '',
                confirmPassword: formData.confirmPassword || '',
                isAdmin: formData.isAdmin,
                isActive: formData.isActive,
                projectIds: formData.projectIds,
                accessPermissions: formData.accessPermissions
            };

            const response = await apiClient.put('/AdminApi/profile', updateData);
            if (response.data.success) {
                setSuccess('Profile updated successfully');
                setTimeout(() => {
                    navigate('/home/entry');
                }, 2000);
            } else {
                setError(response.data.message || 'Failed to update profile');
            }
        } catch (err) {
            const errorMessage = err.response?.data?.message 
                || err.response?.data?.errors?.join(', ') 
                || 'Failed to update profile';
            setError(errorMessage);
        } finally {
            setSaving(false);
            isSubmittingRef.current = false;
        }
    };

    if (loading) {
        return <LoadingSpinner />;
    }

    return (
        <div className="container-fluid">
            {success && (
                <div className="alert alert-success alert-dismissible fade show" role="alert">
                    <i className="fas fa-check-circle me-2"></i>{success}
                    <button type="button" className="btn-close" onClick={() => setSuccess('')}></button>
                </div>
            )}

            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    <i className="fas fa-exclamation-circle me-2"></i>{error}
                    <button type="button" className="btn-close" onClick={() => setError('')}></button>
                </div>
            )}

            <div className="card shadow mb-4">
                <div className="card-header py-3">
                    <h6 className="m-0 font-weight-bold text-primary">
                        <i className="fas fa-id-card me-2"></i>Profile Information
                    </h6>
                </div>
                <div className="card-body">
                    <form onSubmit={handleSubmit}>
                        <div className="row mb-3">
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-user me-1"></i>Username
                                        <i className="fas fa-lock text-muted ms-2" title="Username cannot be changed"></i>
                                    </label>
                                    <input
                                        className="form-control bg-light"
                                        value={formData.username}
                                        readOnly
                                        disabled
                                        style={{ cursor: 'not-allowed' }}
                                    />
                                    <small className="text-muted">Username cannot be changed</small>
                                </div>
                            </div>
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-signature me-1"></i>Full Name <span className="text-danger">*</span>
                                    </label>
                                    <input
                                        className="form-control"
                                        value={formData.fullName}
                                        onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                                        required
                                        minLength={2}
                                        maxLength={50}
                                        placeholder="Enter your full name"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="row mb-3">
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-envelope me-1"></i>Email
                                    </label>
                                    <input
                                        type="email"
                                        className="form-control"
                                        value={formData.email}
                                        onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                                        placeholder="Enter your email"
                                    />
                                </div>
                            </div>
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-shield-alt me-1"></i>Account Status
                                    </label>
                                    <div className="d-flex align-items-center mt-2">
                                        {formData.isAdmin && (
                                            <span className="badge bg-primary me-2">
                                                <i className="fas fa-crown me-1"></i>Administrator
                                            </span>
                                        )}
                                        <span className={`badge ${formData.isActive ? 'bg-success' : 'bg-danger'}`}>
                                            <i className={`fas fa-${formData.isActive ? 'check' : 'times'} me-1`}></i>
                                            {formData.isActive ? 'Active' : 'Inactive'}
                                        </span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <hr className="my-4" />
                        
                        <h6 className="text-muted mb-3">
                            <i className="fas fa-key me-2"></i>Change Password
                            <small className="text-muted ms-2">(leave blank to keep current password)</small>
                        </h6>

                        <div className="row mb-3">
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-lock me-1"></i>New Password
                                    </label>
                                    <input
                                        type="password"
                                        className="form-control"
                                        value={formData.password}
                                        onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                                        placeholder="Enter new password"
                                        autoComplete="new-password"
                                    />
                                </div>
                            </div>
                            <div className="col-md-6">
                                <div className="form-group">
                                    <label className="form-label">
                                        <i className="fas fa-lock me-1"></i>Confirm Password
                                    </label>
                                    <input
                                        type="password"
                                        className="form-control"
                                        value={formData.confirmPassword}
                                        onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                                        placeholder="Confirm new password"
                                        autoComplete="new-password"
                                    />
                                    {formData.password && formData.confirmPassword && formData.password !== formData.confirmPassword && (
                                        <small className="text-danger">
                                            <i className="fas fa-exclamation-triangle me-1"></i>Passwords do not match
                                        </small>
                                    )}
                                </div>
                            </div>
                        </div>

                        <div className="mt-4 d-flex gap-2">
                            <button 
                                type="submit" 
                                className="btn btn-primary" 
                                disabled={saving || (formData.password && formData.password !== formData.confirmPassword)}
                            >
                                {saving ? (
                                    <>
                                        <span className="spinner-border spinner-border-sm me-2" role="status"></span>
                                        Saving...
                                    </>
                                ) : (
                                    <>
                                        <i className="fas fa-save me-1"></i> Update Profile
                                    </>
                                )}
                            </button>
                            <button
                                type="button"
                                className="btn btn-secondary"
                                onClick={() => navigate('/home/entry')}
                                disabled={saving}
                            >
                                <i className="fas fa-arrow-left me-1"></i> Back
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    );
};

export default EditProfile;


