import { Card } from 'react-bootstrap';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';

const ProjectComparisonChart = ({ data = [], loading = false }) => {
  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  // Normalize data for chart
  const chartData = data.map(item => {
    const projectName = getProp(item, 'projectName') || 'Unknown';
    return {
      projectId: getProp(item, 'projectId'),
      projectName,
      displayName: projectName.length > 12 ? projectName.substring(0, 12) + '...' : projectName,
      thisMonthRevenue: getProp(item, 'thisMonthRevenue') ?? 0,
      lastMonthRevenue: getProp(item, 'lastMonthRevenue') ?? 0,
      growth: getProp(item, 'growth') ?? 0
    };
  });

  const formatCurrency = (value) => {
    if (value >= 1000000) {
      return `${(value / 1000000).toFixed(1)}M`;
    } else if (value >= 1000) {
      return `${(value / 1000).toFixed(0)}K`;
    }
    return (value || 0).toFixed(0);
  };

  const CustomTooltip = ({ active, payload, label }) => {
    if (active && payload && payload.length) {
      const item = chartData.find(d => d.displayName === label);
      return (
        <div className="bg-white p-2 border rounded shadow-sm">
          <p className="mb-1 fw-bold">{item?.projectName || label}</p>
          {payload.map((entry, index) => (
            <p key={index} className="mb-0 small" style={{ color: entry.color }}>
              {entry.name}: Rs {(entry.value || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}
            </p>
          ))}
          {item && (
            <p className={`mb-0 small ${item.growth >= 0 ? 'text-success' : 'text-danger'}`}>
              Growth: {item.growth >= 0 ? '+' : ''}{item.growth}%
            </p>
          )}
        </div>
      );
    }
    return null;
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-project-diagram me-2"></i>Project Performance (MoM)
        </h6>
      </Card.Header>
      <Card.Body>
        {loading ? (
          <div className="d-flex justify-content-center align-items-center" style={{ height: 300 }}>
            <div className="spinner-border text-primary" role="status">
              <span className="visually-hidden">Loading...</span>
            </div>
          </div>
        ) : chartData.length === 0 ? (
          <div className="d-flex justify-content-center align-items-center text-muted" style={{ height: 300 }}>
            No data available
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={300}>
            <BarChart
              data={chartData}
              margin={{ top: 5, right: 30, left: 20, bottom: 5 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
              <XAxis
                dataKey="displayName"
                tick={{ fontSize: 11 }}
                interval={0}
                angle={-45}
                textAnchor="end"
                height={60}
              />
              <YAxis
                tickFormatter={formatCurrency}
                tick={{ fontSize: 12 }}
              />
              <Tooltip content={<CustomTooltip />} />
              <Legend />
              <Bar
                dataKey="thisMonthRevenue"
                name="This Month"
                fill="#007bff"
                radius={[4, 4, 0, 0]}
              />
              <Bar
                dataKey="lastMonthRevenue"
                name="Last Month"
                fill="#6c757d"
                radius={[4, 4, 0, 0]}
              />
            </BarChart>
          </ResponsiveContainer>
        )}
      </Card.Body>
    </Card>
  );
};

export default ProjectComparisonChart;
