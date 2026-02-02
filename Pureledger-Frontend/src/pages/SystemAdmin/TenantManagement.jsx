import { useState, useEffect } from 'react';
import { Container, Card, Table, Button, Modal, Form, Badge, Spinner, Alert } from 'react-bootstrap';
import { useToast } from '../../contexts/ToastContext';
import { usePageTitle } from '../../hooks/usePageTitle';
import {
    getAllTenants,
    createTenant,
    activateTenant,
    deactivateTenant,
    deleteTenant,
    getTenantById,
} from '../../services/systemAdminApi';

const TenantManagement = () => {
    usePageTitle('Tenant Management');
    const { showSuccess, showError } = useToast();
    const [tenants, setTenants] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showCreateModal, setShowCreateModal] = useState(false);
    const [showDetailsModal, setShowDetailsModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [selectedTenant, setSelectedTenant] = useState(null);
    const [createdCredentials, setCreatedCredentials] = useState(null);

    // Form state
    const [newTenantName, setNewTenantName] = useState('');
    const [newTenantSlug, setNewTenantSlug] = useState('');
    const [formLoading, setFormLoading] = useState(false);

    useEffect(() => {
        loadTenants();
    }, []);

    const loadTenants = async () => {
        try {
            setLoading(true);
            const result = await getAllTenants();
            if (result.success) {
                setTenants(result.tenants);
            } else {
                showError(result.message || 'Failed to load tenants');
            }
        } catch (error) {
            showError(error.message || 'Failed to load tenants');
        } finally {
            setLoading(false);
        }
    };

    const handleCreateTenant = async (e) => {
        e.preventDefault();
        setFormLoading(true);
        setCreatedCredentials(null);

        try {
            const result = await createTenant(newTenantName, newTenantSlug);
            if (result.success) {
                showSuccess('Tenant created successfully!');
                setCreatedCredentials(result.credentials);
                loadTenants();
                // Don't close modal yet - show credentials
            } else {
                showError(result.message || 'Failed to create tenant');
            }
        } catch (error) {
            showError(error.message || 'Failed to create tenant');
        } finally {
            setFormLoading(false);
        }
    };

    const handleActivate = async (tenant) => {
        try {
            const result = await activateTenant(tenant.id);
            if (result.success) {
                showSuccess(`Tenant "${tenant.name}" activated`);
                loadTenants();
            } else {
                showError(result.message);
            }
        } catch (error) {
            showError(error.message);
        }
    };

    const handleDeactivate = async (tenant) => {
        try {
            const result = await deactivateTenant(tenant.id);
            if (result.success) {
                showSuccess(`Tenant "${tenant.name}" deactivated`);
                loadTenants();
            } else {
                showError(result.message);
            }
        } catch (error) {
            showError(error.message);
        }
    };

    const handleDelete = async () => {
        if (!selectedTenant) return;

        try {
            const result = await deleteTenant(selectedTenant.id);
            if (result.success) {
                showSuccess(`Tenant "${selectedTenant.name}" deleted permanently`);
                setShowDeleteModal(false);
                setSelectedTenant(null);
                loadTenants();
            } else {
                showError(result.message);
            }
        } catch (error) {
            showError(error.message);
        }
    };

    const handleViewDetails = async (tenant) => {
        try {
            const result = await getTenantById(tenant.id);
            if (result.success) {
                setSelectedTenant({ ...result.tenant, auditLogs: result.auditLogs });
                setShowDetailsModal(true);
            }
        } catch (error) {
            showError(error.message);
        }
    };

    const resetCreateForm = () => {
        setNewTenantName('');
        setNewTenantSlug('');
        setCreatedCredentials(null);
        setShowCreateModal(false);
    };

    const formatDate = (dateString) => {
        if (!dateString) return '-';
        return new Date(dateString).toLocaleString();
    };

    return (
        <Container fluid className="py-3">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-building me-2"></i>Tenant Management</h2>
                <Button variant="primary" onClick={() => setShowCreateModal(true)}>
                    <i className="fas fa-plus me-2"></i>Create Tenant
                </Button>
            </div>

            <Card>
                <Card.Body>
                    {loading ? (
                        <div className="text-center py-4">
                            <Spinner animation="border" />
                            <p className="mt-2">Loading tenants...</p>
                        </div>
                    ) : (
                        <Table hover responsive>
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Slug</th>
                                    <th>Schema</th>
                                    <th>Status</th>
                                    <th>Created</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {tenants.map(tenant => (
                                    <tr key={tenant.id}>
                                        <td>{tenant.name}</td>
                                        <td><code>{tenant.slug}</code></td>
                                        <td><code>{tenant.schemaName}</code></td>
                                        <td>
                                            {tenant.isActive ? (
                                                <Badge bg="success">Active</Badge>
                                            ) : (
                                                <Badge bg="secondary">Inactive</Badge>
                                            )}
                                        </td>
                                        <td>{formatDate(tenant.createdAt)}</td>
                                        <td>
                                            <Button
                                                variant="outline-info"
                                                size="sm"
                                                className="me-1"
                                                onClick={() => handleViewDetails(tenant)}
                                            >
                                                <i className="fas fa-eye"></i>
                                            </Button>
                                            {tenant.isActive ? (
                                                <Button
                                                    variant="outline-warning"
                                                    size="sm"
                                                    className="me-1"
                                                    onClick={() => handleDeactivate(tenant)}
                                                >
                                                    <i className="fas fa-pause"></i>
                                                </Button>
                                            ) : (
                                                <Button
                                                    variant="outline-success"
                                                    size="sm"
                                                    className="me-1"
                                                    onClick={() => handleActivate(tenant)}
                                                >
                                                    <i className="fas fa-play"></i>
                                                </Button>
                                            )}
                                            <Button
                                                variant="outline-danger"
                                                size="sm"
                                                onClick={() => {
                                                    setSelectedTenant(tenant);
                                                    setShowDeleteModal(true);
                                                }}
                                            >
                                                <i className="fas fa-trash"></i>
                                            </Button>
                                        </td>
                                    </tr>
                                ))}
                                {tenants.length === 0 && (
                                    <tr>
                                        <td colSpan="6" className="text-center text-muted py-4">
                                            No tenants found. Click "Create Tenant" to add one.
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </Table>
                    )}
                </Card.Body>
            </Card>

            {/* Create Tenant Modal */}
            <Modal show={showCreateModal} onHide={resetCreateForm} size="lg">
                <Modal.Header closeButton>
                    <Modal.Title>Create New Tenant</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {createdCredentials ? (
                        <Alert variant="success">
                            <Alert.Heading>Tenant Created Successfully!</Alert.Heading>
                            <p>Save these credentials - the password cannot be recovered:</p>
                            <hr />
                            <p><strong>Login Username:</strong> <code>{createdCredentials.username}</code></p>
                            <p><strong>Password:</strong> <code>{createdCredentials.password}</code></p>
                            <hr />
                            <p className="mb-0">
                                Users can login at the main login page using the format: <code>{newTenantSlug}|admin</code>
                            </p>
                        </Alert>
                    ) : (
                        <Form onSubmit={handleCreateTenant}>
                            <Form.Group className="mb-3">
                                <Form.Label>Tenant Name</Form.Label>
                                <Form.Control
                                    type="text"
                                    placeholder="e.g., Prime Logistics"
                                    value={newTenantName}
                                    onChange={(e) => setNewTenantName(e.target.value)}
                                    required
                                />
                                <Form.Text className="text-muted">
                                    The display name for this tenant organization.
                                </Form.Text>
                            </Form.Group>
                            <Form.Group className="mb-3">
                                <Form.Label>Slug</Form.Label>
                                <Form.Control
                                    type="text"
                                    placeholder="e.g., prime"
                                    value={newTenantSlug}
                                    onChange={(e) => setNewTenantSlug(e.target.value.toLowerCase().replace(/[^a-z0-9_]/g, ''))}
                                    required
                                    pattern="^[a-z0-9_]{1,50}$"
                                />
                                <Form.Text className="text-muted">
                                    Lowercase letters, numbers, and underscores only. Used for login (e.g., <code>prime|admin</code>).
                                </Form.Text>
                            </Form.Group>
                            <div className="d-flex justify-content-end">
                                <Button variant="secondary" className="me-2" onClick={resetCreateForm}>
                                    Cancel
                                </Button>
                                <Button variant="primary" type="submit" disabled={formLoading}>
                                    {formLoading ? (
                                        <>
                                            <Spinner animation="border" size="sm" className="me-2" />
                                            Creating...
                                        </>
                                    ) : (
                                        'Create Tenant'
                                    )}
                                </Button>
                            </div>
                        </Form>
                    )}
                </Modal.Body>
                {createdCredentials && (
                    <Modal.Footer>
                        <Button variant="primary" onClick={resetCreateForm}>
                            Close
                        </Button>
                    </Modal.Footer>
                )}
            </Modal>

            {/* Details Modal */}
            <Modal show={showDetailsModal} onHide={() => setShowDetailsModal(false)} size="lg">
                <Modal.Header closeButton>
                    <Modal.Title>Tenant Details</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {selectedTenant && (
                        <>
                            <h5>{selectedTenant.name}</h5>
                            <Table bordered size="sm">
                                <tbody>
                                    <tr><td><strong>ID</strong></td><td><code>{selectedTenant.id}</code></td></tr>
                                    <tr><td><strong>Slug</strong></td><td><code>{selectedTenant.slug}</code></td></tr>
                                    <tr><td><strong>Schema</strong></td><td><code>{selectedTenant.schemaName}</code></td></tr>
                                    <tr><td><strong>Status</strong></td><td>{selectedTenant.isActive ? 'Active' : 'Inactive'}</td></tr>
                                    <tr><td><strong>Created</strong></td><td>{formatDate(selectedTenant.createdAt)}</td></tr>
                                    <tr><td><strong>Activated</strong></td><td>{formatDate(selectedTenant.activatedAt)}</td></tr>
                                    <tr><td><strong>Deactivated</strong></td><td>{formatDate(selectedTenant.deactivatedAt)}</td></tr>
                                    <tr><td><strong>Notes</strong></td><td>{selectedTenant.notes || '-'}</td></tr>
                                </tbody>
                            </Table>

                            <h6 className="mt-4">Recent Audit Logs</h6>
                            {selectedTenant.auditLogs?.length > 0 ? (
                                <Table size="sm" striped>
                                    <thead>
                                        <tr>
                                            <th>Operation</th>
                                            <th>By</th>
                                            <th>Status</th>
                                            <th>Time</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {selectedTenant.auditLogs.map(log => (
                                            <tr key={log.id}>
                                                <td>{log.operation}</td>
                                                <td>{log.performedBy}</td>
                                                <td>
                                                    {log.isSuccess ? (
                                                        <Badge bg="success">Success</Badge>
                                                    ) : (
                                                        <Badge bg="danger">Failed</Badge>
                                                    )}
                                                </td>
                                                <td>{formatDate(log.timestamp)}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </Table>
                            ) : (
                                <p className="text-muted">No audit logs available.</p>
                            )}
                        </>
                    )}
                </Modal.Body>
            </Modal>

            {/* Delete Confirmation Modal */}
            <Modal show={showDeleteModal} onHide={() => setShowDeleteModal(false)}>
                <Modal.Header closeButton>
                    <Modal.Title className="text-danger">
                        <i className="fas fa-exclamation-triangle me-2"></i>Delete Tenant
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <Alert variant="danger">
                        <strong>Warning:</strong> This action is irreversible!
                    </Alert>
                    <p>
                        Are you sure you want to permanently delete tenant <strong>"{selectedTenant?.name}"</strong>?
                    </p>
                    <p>This will:</p>
                    <ul>
                        <li>Delete all tenant data (users, transactions, etc.)</li>
                        <li>Drop the database schema <code>{selectedTenant?.schemaName}</code></li>
                        <li>Remove the tenant record</li>
                    </ul>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant="secondary" onClick={() => setShowDeleteModal(false)}>
                        Cancel
                    </Button>
                    <Button variant="danger" onClick={handleDelete}>
                        Delete Permanently
                    </Button>
                </Modal.Footer>
            </Modal>
        </Container>
    );
};

export default TenantManagement;
