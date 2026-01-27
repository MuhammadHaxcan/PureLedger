import React from 'react';

/**
 * Sortable table header component
 * @param {Object} props
 * @param {string} props.label - Column header text
 * @param {string} props.sortKey - Key used for sorting
 * @param {string|null} props.sortDirection - Current sort direction ('asc', 'desc', or null)
 * @param {Function} props.onSort - Click handler for sorting
 * @param {string} props.className - Additional CSS classes
 * @param {Object} props.style - Additional inline styles
 */
const SortableHeader = ({
    label,
    sortKey,
    sortDirection,
    onSort,
    className = '',
    style = {}
}) => {
    const handleClick = () => {
        onSort(sortKey);
    };

    const getSortIcon = () => {
        if (sortDirection === 'asc') {
            return <i className="fas fa-sort-up ms-1"></i>;
        }
        if (sortDirection === 'desc') {
            return <i className="fas fa-sort-down ms-1"></i>;
        }
        return <i className="fas fa-sort ms-1 text-muted" style={{ opacity: 0.3 }}></i>;
    };

    return (
        <th
            onClick={handleClick}
            className={`sortable-header ${className}`}
            style={{
                cursor: 'pointer',
                userSelect: 'none',
                whiteSpace: 'nowrap',
                ...style
            }}
            title={`Click to sort by ${label}`}
        >
            {label}
            {getSortIcon()}
        </th>
    );
};

export default SortableHeader;
