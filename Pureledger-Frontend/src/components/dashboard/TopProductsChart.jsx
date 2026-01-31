import { Card } from 'react-bootstrap';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell
} from 'recharts';

const TopProductsChart = ({ data = [], loading = false }) => {
  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  // Normalize data for chart
  const chartData = data.map(item => {
    const productName = getProp(item, 'productName') || 'Unknown';
    return {
      productName,
      displayName: productName.length > 15 ? productName.substring(0, 15) + '...' : productName,
      revenue: getProp(item, 'revenue') ?? 0,
      cogs: getProp(item, 'cogs') ?? 0,
      profit: getProp(item, 'profit') ?? 0,
      profitMargin: getProp(item, 'profitMargin') ?? 0,
      quantity: getProp(item, 'quantity') ?? 0
    };
  });

  const getBarColor = (profit) => {
    if (profit > 0) return '#28a745';
    if (profit < 0) return '#dc3545';
    return '#6c757d';
  };

  const formatCurrency = (value) => {
    if (value >= 1000000) {
      return `${(value / 1000000).toFixed(1)}M`;
    } else if (value >= 1000) {
      return `${(value / 1000).toFixed(0)}K`;
    }
    return (value || 0).toFixed(0);
  };

  const CustomTooltip = ({ active, payload }) => {
    if (active && payload && payload.length) {
      const item = payload[0].payload;
      return (
        <div className="bg-white p-2 border rounded shadow-sm">
          <p className="mb-1 fw-bold">{item.productName}</p>
          <p className="mb-0 small">Revenue: Rs {(item.revenue || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
          <p className="mb-0 small">COGS: Rs {(item.cogs || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
          <p className="mb-0 small" style={{ color: item.profit >= 0 ? '#28a745' : '#dc3545' }}>
            Profit: Rs {(item.profit || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })} ({item.profitMargin}%)
          </p>
          <p className="mb-0 small">Quantity: {(item.quantity || 0).toLocaleString()}</p>
        </div>
      );
    }
    return null;
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-box me-2"></i>Top 10 Products by Profit
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
              layout="vertical"
              margin={{ top: 5, right: 30, left: 80, bottom: 5 }}
            >
              <CartesianGrid strokeDasharray="3 3" stroke="#eee" horizontal={false} />
              <XAxis
                type="number"
                tickFormatter={formatCurrency}
                tick={{ fontSize: 12 }}
              />
              <YAxis
                type="category"
                dataKey="displayName"
                tick={{ fontSize: 11 }}
                width={80}
              />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="profit" name="Profit" radius={[0, 4, 4, 0]}>
                {chartData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={getBarColor(entry.profit)} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        )}
      </Card.Body>
    </Card>
  );
};

export default TopProductsChart;
