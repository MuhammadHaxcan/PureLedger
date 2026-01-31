import { Card } from 'react-bootstrap';
import {
  PieChart,
  Pie,
  Cell,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884D8', '#82CA9D', '#FFC658', '#FF6B6B', '#4ECDC4', '#45B7D1'];

const CategoryDonutChart = ({ data = [], loading = false }) => {
  // Helper to get property case-insensitively
  const getProp = (obj, key) => {
    if (!obj) return null;
    const lowerKey = key.toLowerCase();
    const foundKey = Object.keys(obj).find(k => k.toLowerCase() === lowerKey);
    return foundKey ? obj[foundKey] : null;
  };

  // Normalize data for chart
  const chartData = data.map(item => ({
    categoryId: getProp(item, 'categoryId'),
    categoryName: getProp(item, 'categoryName') || 'Unknown',
    totalValue: getProp(item, 'totalValue') ?? 0,
    transactionCount: getProp(item, 'transactionCount') ?? 0,
    percentage: getProp(item, 'percentage') ?? 0
  }));

  const CustomTooltip = ({ active, payload }) => {
    if (active && payload && payload.length) {
      const item = payload[0].payload;
      return (
        <div className="bg-white p-2 border rounded shadow-sm">
          <p className="mb-1 fw-bold">{item.categoryName}</p>
          <p className="mb-0 small">Value: Rs {(item.totalValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
          <p className="mb-0 small">Share: {(item.percentage || 0).toFixed(1)}%</p>
          <p className="mb-0 small">Transactions: {item.transactionCount || 0}</p>
        </div>
      );
    }
    return null;
  };

  const renderCustomizedLabel = ({ cx, cy, midAngle, innerRadius, outerRadius, percent }) => {
    if (percent < 0.05) return null;
    const RADIAN = Math.PI / 180;
    const radius = innerRadius + (outerRadius - innerRadius) * 0.5;
    const x = cx + radius * Math.cos(-midAngle * RADIAN);
    const y = cy + radius * Math.sin(-midAngle * RADIAN);

    return (
      <text
        x={x}
        y={y}
        fill="white"
        textAnchor={x > cx ? 'start' : 'end'}
        dominantBaseline="central"
        fontSize={12}
        fontWeight="bold"
      >
        {`${(percent * 100).toFixed(0)}%`}
      </text>
    );
  };

  return (
    <Card className="shadow-sm h-100">
      <Card.Header className="bg-white py-3">
        <h6 className="m-0 fw-bold text-primary">
          <i className="fas fa-chart-pie me-2"></i>Category Distribution
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
            <PieChart>
              <Pie
                data={chartData}
                cx="50%"
                cy="50%"
                labelLine={false}
                label={renderCustomizedLabel}
                outerRadius={100}
                innerRadius={50}
                fill="#8884d8"
                dataKey="totalValue"
                nameKey="categoryName"
              >
                {chartData.map((entry, index) => (
                  <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                ))}
              </Pie>
              <Tooltip content={<CustomTooltip />} />
              <Legend
                formatter={(value) => <span className="small">{value}</span>}
                wrapperStyle={{ fontSize: 12 }}
              />
            </PieChart>
          </ResponsiveContainer>
        )}
      </Card.Body>
    </Card>
  );
};

export default CategoryDonutChart;
