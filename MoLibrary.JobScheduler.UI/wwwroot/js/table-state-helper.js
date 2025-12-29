// Table state persistence helper

/**
 * Save table state to localStorage
 * @param {string} key - Unique identifier for the table state
 * @param {object} state - State object containing { sortBy, sortDirection, pageSize }
 */
export function saveTableState(key, state) {
    try {
        const stateJson = JSON.stringify({
            sortBy: state.sortBy || null,
            sortDirection: state.sortDirection || 'ascending',
            pageSize: state.pageSize || 10
        });
        localStorage.setItem(`mo-table-state-${key}`, stateJson);
    } catch (error) {
        console.error('Failed to save table state:', error);
    }
}

/**
 * Get table state from localStorage
 * @param {string} key - Unique identifier for the table state
 * @returns {object|null} State object or null if not found
 */
export function getTableState(key) {
    try {
        const stateJson = localStorage.getItem(`mo-table-state-${key}`);
        if (stateJson) {
            return JSON.parse(stateJson);
        }
    } catch (error) {
        console.error('Failed to get table state:', error);
    }
    return null;
}

/**
 * Clear table state from localStorage
 * @param {string} key - Unique identifier for the table state
 */
export function clearTableState(key) {
    try {
        localStorage.removeItem(`mo-table-state-${key}`);
    } catch (error) {
        console.error('Failed to clear table state:', error);
    }
}
