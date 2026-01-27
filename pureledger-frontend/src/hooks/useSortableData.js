import { useState, useMemo } from 'react';

/**
 * Custom hook for client-side table sorting
 * @param {Array} items - Array of data items to sort
 * @param {Object} config - Initial sort configuration { key: string, direction: 'asc' | 'desc' } or null
 * @returns {Object} - { sortedItems, sortConfig, requestSort, getSortDirection }
 */
const useSortableData = (items, config = null) => {
    const [sortConfig, setSortConfig] = useState(config);

    const sortedItems = useMemo(() => {
        if (!sortConfig || !sortConfig.key) {
            return items;
        }

        const sortableItems = [...items];
        sortableItems.sort((a, b) => {
            const aValue = getNestedValue(a, sortConfig.key);
            const bValue = getNestedValue(b, sortConfig.key);

            // Handle null/undefined values - push them to the end
            if (aValue == null && bValue == null) return 0;
            if (aValue == null) return 1;
            if (bValue == null) return -1;

            // Compare based on type
            let comparison = 0;
            if (typeof aValue === 'number' && typeof bValue === 'number') {
                comparison = aValue - bValue;
            } else if (typeof aValue === 'string' && typeof bValue === 'string') {
                comparison = aValue.localeCompare(bValue, undefined, { numeric: true, sensitivity: 'base' });
            } else {
                // Convert to string for comparison
                comparison = String(aValue).localeCompare(String(bValue), undefined, { numeric: true, sensitivity: 'base' });
            }

            return sortConfig.direction === 'desc' ? -comparison : comparison;
        });

        return sortableItems;
    }, [items, sortConfig]);

    const requestSort = (key) => {
        let direction = 'asc';
        if (sortConfig && sortConfig.key === key && sortConfig.direction === 'asc') {
            direction = 'desc';
        }
        setSortConfig({ key, direction });
    };

    const getSortDirection = (key) => {
        if (!sortConfig || sortConfig.key !== key) {
            return null;
        }
        return sortConfig.direction;
    };

    const clearSort = () => {
        setSortConfig(null);
    };

    return { sortedItems, sortConfig, requestSort, getSortDirection, clearSort };
};

// Helper function to get nested object values using dot notation
const getNestedValue = (obj, path) => {
    if (!path) return obj;
    const keys = path.split('.');
    let value = obj;
    for (const key of keys) {
        if (value == null) return null;
        value = value[key];
    }
    return value;
};

export default useSortableData;
