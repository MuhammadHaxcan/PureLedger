import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Row, Col, Form, Button, InputGroup, Alert } from 'react-bootstrap';
import apiClient from '../../services/api';
import { usePageTitle } from '../../hooks/usePageTitle';
import SearchableSelect from '../../components/SearchableSelect';
import KPICard from '../../components/dashboard/KPICard';
import TrendChart from '../../components/dashboard/TrendChart';
import CategoryDonutChart from '../../components/dashboard/CategoryDonutChart';
import TopProductsChart from '../../components/dashboard/TopProductsChart';
import ProjectComparisonChart from '../../components/dashboard/ProjectComparisonChart';
import TopCustomersTable from '../../components/dashboard/TopCustomersTable';
import RecentActivityFeed from '../../components/dashboard/RecentActivityFeed';
import AlertsPanel from '../../components/dashboard/AlertsPanel';
import './Dashboard.css';

const Dashboard = () => {
  usePageTitle('Business Intelligence Dashboard');
  const navigate = useNavigate();

  const [projects, setProjects] = useState([]);
  const [selectedProject, setSelectedProject] = useState(null);
  const [days, setDays] = useState(30);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [dashboardData, setDashboardData] = useState({
    kpis: {
      totalRevenue: 0,
      totalPurchases: 0,
      netProfit: 0,
      profitMargin: 0,
      outstandingReceivables: 0,
      missedEntriesCount: 0,
      transactionsToday: 0,
      transactionsThisMonth: 0
    },
    trends: [],
    topProducts: [],
    topCustomers: [],
    categoryDistribution: [],
    projectPerformance: [],
    recentActivity: [],
    alerts: []
  });

  useEffect(() => {
    loadInitData();
  }, []);

  useEffect(() => {
    loadDashboardData();
  }, [selectedProject, days]);

  const loadInitData = async () => {
    try {
      const response = await apiClient.get('/ReportingApi/dashboard-init');
      if (response.data.success) {
        setProjects(response.data.projects || []);
      }
    } catch (err) {
      console.error('Error loading init data:', err);
    }
  };

  const loadDashboardData = async () => {
    setLoading(true);
    setError('');

    try {
      const params = new URLSearchParams();
      if (selectedProject) {
        params.append('projectId', selectedProject);
      }
      params.append('days', days.toString());

      const response = await apiClient.get(`/ReportingApi/dashboard?${params.toString()}`);

      if (response.data.success) {
        const data = response.data.data;

        // Find property with case-insensitive key lookup
        const findKey = (obj, key) => {
          if (!obj) return null;
          const lowerKey = key.toLowerCase();
          const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
          return foundKey ? obj[foundKey] : null;
        };

        const kpisData = findKey(data, 'kpis') || {};

        // Normalize the response to handle different property casing from API
        setDashboardData({
          kpis: {
            totalRevenue: findKey(kpisData, 'totalRevenue') ?? 0,
            totalPurchases: findKey(kpisData, 'totalPurchases') ?? 0,
            netProfit: findKey(kpisData, 'netProfit') ?? 0,
            profitMargin: findKey(kpisData, 'profitMargin') ?? 0,
            outstandingReceivables: findKey(kpisData, 'outstandingReceivables') ?? 0,
            missedEntriesCount: findKey(kpisData, 'missedEntriesCount') ?? 0,
            transactionsToday: findKey(kpisData, 'transactionsToday') ?? 0,
            transactionsThisMonth: findKey(kpisData, 'transactionsThisMonth') ?? 0
          },
          trends: findKey(data, 'trends') || [],
          topProducts: findKey(data, 'topProducts') || [],
          topCustomers: findKey(data, 'topCustomers') || [],
          categoryDistribution: findKey(data, 'categoryDistribution') || [],
          projectPerformance: findKey(data, 'projectPerformance') || [],
          recentActivity: findKey(data, 'recentActivity') || [],
          alerts: findKey(data, 'alerts') || []
        });
      } else {
        setError(response.data.message || 'Failed to load dashboard data');
      }
    } catch (err) {
      console.error('Error loading dashboard data:', err);
      setError(err.response?.data?.message || 'Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = () => {
    loadDashboardData();
  };

  const {
    kpis = {},
    trends = [],
    topProducts = [],
    topCustomers = [],
    categoryDistribution = [],
    projectPerformance = [],
    recentActivity = [],
    alerts = []
  } = dashboardData || {};

  return (
    <div className="container-fluid dashboard-container">
      {/* Header */}
      <div className="d-flex flex-wrap justify-content-between align-items-center mb-4">
        <div>
          <h2 className="mb-1">
            <i className="fas fa-chart-line me-2 text-primary"></i>
            Business Intelligence Dashboard
          </h2>
          <p className="text-muted mb-0">
            Real-time insights into your business performance
          </p>
        </div>
        <div className="d-flex flex-wrap gap-2 align-items-center mt-2 mt-md-0">
          <InputGroup style={{ width: 200 }}>
            <InputGroup.Text><i className="fas fa-project-diagram"></i></InputGroup.Text>
            <SearchableSelect
              value={selectedProject}
              onChange={(value) => setSelectedProject(value ? parseInt(value) : null)}
              options={projects.map(p => ({ id: p.id, name: p.name }))}
              placeholder="All Projects"
              disabled={loading}
            />
          </InputGroup>
          <Form.Select
            value={days}
            onChange={(e) => setDays(parseInt(e.target.value))}
            style={{ width: 130 }}
            disabled={loading}
          >
            <option value={7}>Last 7 days</option>
            <option value={14}>Last 14 days</option>
            <option value={30}>Last 30 days</option>
            <option value={60}>Last 60 days</option>
            <option value={90}>Last 90 days</option>
          </Form.Select>
          <Button
            variant="primary"
            onClick={handleRefresh}
            disabled={loading}
          >
            {loading ? (
              <span className="spinner-border spinner-border-sm" role="status"></span>
            ) : (
              <i className="fas fa-sync-alt"></i>
            )}
          </Button>
          <Button
            variant="outline-secondary"
            onClick={() => navigate('/reporting/dashboard-settings')}
            title="Dashboard Settings"
          >
            <i className="fas fa-cog"></i>
          </Button>
        </div>
      </div>

      {error && (
        <Alert variant="danger" dismissible onClose={() => setError('')}>
          <i className="fas fa-exclamation-circle me-2"></i>{error}
        </Alert>
      )}

      {/* KPI Cards Row */}
      <Row className="mb-4 g-3">
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Total Revenue"
            value={kpis?.totalRevenue ?? 0}
            icon="fa-dollar-sign"
            color="success"
            loading={loading}
          />
        </Col>
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Total Purchases"
            value={kpis?.totalPurchases ?? 0}
            icon="fa-shopping-cart"
            color="primary"
            loading={loading}
          />
        </Col>
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Net Profit"
            value={kpis?.netProfit ?? 0}
            icon="fa-chart-line"
            color={(kpis?.netProfit ?? 0) >= 0 ? 'success' : 'danger'}
            subtitle={`${kpis?.profitMargin ?? 0}% margin`}
            loading={loading}
          />
        </Col>
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Receivables"
            value={kpis?.outstandingReceivables ?? 0}
            icon="fa-clock"
            color="warning"
            loading={loading}
          />
        </Col>
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Missed Entries"
            value={kpis?.missedEntriesCount ?? 0}
            icon="fa-exclamation-triangle"
            color={(kpis?.missedEntriesCount ?? 0) > 0 ? 'danger' : 'secondary'}
            loading={loading}
          />
        </Col>
        <Col xs={12} sm={6} lg={4} xl={2}>
          <KPICard
            title="Today's Txns"
            value={kpis?.transactionsToday ?? 0}
            icon="fa-calendar-day"
            color="info"
            subtitle={`${kpis?.transactionsThisMonth ?? 0} this month`}
            loading={loading}
          />
        </Col>
      </Row>

      {/* Charts Row 1 */}
      <Row className="mb-4 g-3">
        <Col lg={8}>
          <TrendChart data={trends} loading={loading} />
        </Col>
        <Col lg={4}>
          <CategoryDonutChart data={categoryDistribution} loading={loading} />
        </Col>
      </Row>

      {/* Charts Row 2 */}
      <Row className="mb-4 g-3">
        <Col lg={6}>
          <TopProductsChart data={topProducts} loading={loading} />
        </Col>
        <Col lg={6}>
          <ProjectComparisonChart data={projectPerformance} loading={loading} />
        </Col>
      </Row>

      {/* Tables Row */}
      <Row className="g-3">
        <Col lg={4}>
          <TopCustomersTable data={topCustomers} loading={loading} />
        </Col>
        <Col lg={4}>
          <RecentActivityFeed data={recentActivity} loading={loading} />
        </Col>
        <Col lg={4}>
          <AlertsPanel alerts={alerts} loading={loading} />
        </Col>
      </Row>
    </div>
  );
};

export default Dashboard;
