import { Table, Button } from 'react-bootstrap';

const DataTable = ({ 
  data, 
  columns, 
  onEdit, 
  onDelete, 
  onToggleStatus,
  emptyMessage = 'No data available',
  loading = false 
}) => {
  if (loading) {
    return (
      <div className="text-center py-4">
        <div className="spinner-border" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div className="text-center py-4 text-muted">
        {emptyMessage}
      </div>
    );
  }

  return (
    <Table responsive striped bordered hover>
      <thead>
        <tr>
          {columns.map((col) => (
            <th key={col.key}>{col.label}</th>
          ))}
          {(onEdit || onDelete || onToggleStatus) && <th>Actions</th>}
        </tr>
      </thead>
      <tbody>
        {data.map((row, index) => (
          <tr key={row.id || index}>
            {columns.map((col) => (
              <td key={col.key}>
                {col.render ? col.render(row[col.key], row) : row[col.key]}
              </td>
            ))}
            {(onEdit || onDelete || onToggleStatus) && (
              <td>
                <div className="d-flex gap-2">
                  {onEdit && (
                    <Button
                      variant="outline-primary"
                      size="sm"
                      onClick={() => onEdit(row)}
                      aria-label="Edit"
                    >
                      <i className="fas fa-edit"></i>
                    </Button>
                  )}
                  {onToggleStatus && (
                    <Button
                      variant={row.isEnabled ? 'outline-warning' : 'outline-success'}
                      size="sm"
                      onClick={() => onToggleStatus(row)}
                      aria-label={row.isEnabled ? 'Disable' : 'Enable'}
                    >
                      <i className={`fas fa-${row.isEnabled ? 'ban' : 'check'}`}></i>
                    </Button>
                  )}
                  {onDelete && (
                    <Button
                      variant="outline-danger"
                      size="sm"
                      onClick={() => onDelete(row)}
                      aria-label="Delete"
                    >
                      <i className="fas fa-trash"></i>
                    </Button>
                  )}
                </div>
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </Table>
  );
};

export default DataTable;

