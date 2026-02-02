import { useState, useEffect } from 'react';
import { Container, Card, Table, Button, Badge, Spinner, Alert } from 'react-bootstrap';
import { useToast } from '../../contexts/ToastContext';
import { usePageTitle } from '../../hooks/usePageTitle';
import {
    getPendingMigrations,
    applyTenantMigrations,
    applyAllMigrations,
} from '../../services/systemAdminApi';

const TenantMigrations = () => {
    usePageTitle('Tenant Migrations');
    const { showSuccess, showError } = useToast();
    const [tenants, setTenants] = useState([]);
    const [loading, setLoading] = useState(true);
    const [applyingAll, setApplyingAll] = useState(false);
    const [applyingTenant, setApplyingTenant] = useState(null);

    useEffect(() => {
        loadMigrationStatus();
    }, []);

    const loadMigrationStatus = async () => {
        try {
            setLoading(true);
            const result = await getPendingMigrations();
            if (result.success) {
                setTenants(result.tenants);
            } else {
                showError(result.message || 'Failed to load migration status');
            }
        } catch (error) {
            showError(error.message || 'Failed to load migration status');
        } finally {
            setLoading(false);
        }
    };

    const handleApplyToTenant = async (tenant) => {
        try {
            setApplyingTenant(tenant.tenantId);
            const result = await applyTenantMigrations(tenant.tenantId);
            if (result.success) {
                showSuccess(`Migrations applied to "${tenant.tenantName}" - ${result.migrationsApplied} migration(s)`);
                loadMigrationStatus();
            } else {
                showError(result.message);
            }
        } catch (error) {
            showError(error.message);
        } finally {
            setApplyingTenant(null);
        }
    };

    const handleApplyAll = async () => {
        try {
            setApplyingAll(true);
            const result = await applyAllMigrations();
            if (result.success) {
                showSuccess(result.message);
            } else {
                showError(result.message);
            }
            loadMigrationStatus();
        } catch (error) {
            showError(error.message);
        } finally {
            setApplyingAll(false);
        }
    };

    const hasPendingMigrations = tenants.some(t => t.pendingMigrationCount > 0);

    return (
        <Container fluid className="py-3">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h2><i className="fas fa-database me-2"></i>Tenant Migrations</h2>
                <div>
                    <Button
                        variant="outline-secondary"
                        className="me-2"
                        onClick={loadMigrationStatus}
                        disabled={loading}
                    >
                        <i className="fas fa-sync-alt me-2"></i>Refresh
                    </Button>
                    <Button
                        variant="primary"
                        onClick={handleApplyAll}
                        disabled={applyingAll || !hasPendingMigrations}
                    >
                        {applyingAll ? (
                            <>
                                <Spinner animation="border" size="sm" className="me-2" />
                                Applying...
                            </>
                        ) : (
                            <>
                                <i className="fas fa-play me-2"></i>Apply All
                            </>
                        )}
                    </Button>
                </div>
            </div>

            <Alert variant="info">
                <i className="fas fa-info-circle me-2"></i>
                Migrations update tenant database schemas when the application is updated.
                Always backup databases before applying migrations to production tenants.
            </Alert>

            <Card>
                <Card.Body>
                    {loading ? (
                        <div className="text-center py-4">
                            <Spinner animation="border" />
                            <p className="mt-2">Checking migration status...</p>
                        </div>
                    ) : (
                        <Table hover responsive>
                            <thead>
                                <tr>
                                    <th>Tenant</th>
                                    <th>Schema</th>
                                    <th>Current Version</th>
                                    <th>Pending</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {tenants.map(tenant => (
                                    <tr key={tenant.tenantId}>
                                        <td>{tenant.tenantName}</td>
                                        <td><code>{tenant.schemaName}</code></td>
                                        <td>
                                            <code>{tenant.currentVersion || 'Initial'}</code>
                                        </td>
                                        <td>
                                            {tenant.pendingMigrationCount === 0 ? (
                                                <Badge bg="success">Up to date</Badge>
                                            ) : tenant.pendingMigrationCount === -1 ? (
                                                <Badge bg="danger">Error</Badge>
                                            ) : (
                                                <Badge bg="warning">
                                                    {tenant.pendingMigrationCount} pending
                                                </Badge>
                                            )}
                                        </td>
                                        <td>
                                            <Button
                                                variant="outline-primary"
                                                size="sm"
                                                onClick={() => handleApplyToTenant(tenant)}
                                                disabled={applyingTenant === tenant.tenantId || tenant.pendingMigrationCount === 0}
                                            >
                                                {applyingTenant === tenant.tenantId ? (
                                                    <Spinner animation="border" size="sm" />
                                                ) : (
                                                    <>
                                                        <i className="fas fa-arrow-up me-1"></i>
                                                        Apply
                                                    </>
                                                )}
                                            </Button>
                                        </td>
                                    </tr>
                                ))}
                                {tenants.length === 0 && (
                                    <tr>
                                        <td colSpan="5" className="text-center text-muted py-4">
                                            No active tenants found.
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </Table>
                    )}
                </Card.Body>
            </Card>
        </Container>
    );
};

export default TenantMigrations;
