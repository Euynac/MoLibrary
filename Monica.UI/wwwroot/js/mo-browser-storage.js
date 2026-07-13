// Generic browser storage wrapper for localStorage/sessionStorage

/**
 * @param {"local"|"session"} storageType
 * @returns {Storage}
 */
function getStorage(storageType) {
    return storageType === "session" ? sessionStorage : localStorage;
}

/**
 * @param {unknown} error
 * @returns {boolean}
 */
function isQuotaExceeded(error) {
    return error != null && (
        error.name === "QuotaExceededError" ||
        error.name === "NS_ERROR_DOM_QUOTA_REACHED" ||
        error.code === 22 ||
        error.code === 1014);
}

/**
 * @param {unknown} error
 * @returns {boolean}
 */
function isStorageUnavailable(error) {
    return error != null && (
        error.name === "SecurityError" ||
        error.name === "InvalidStateError" ||
        error.name === "NotSupportedError");
}

/**
 * Get an item from browser storage
 * @param {string} storageType - "local" or "session"
 * @param {string} key - Storage key
 * @returns {string|null}
 */
export function getItem(storageType, key) {
    try {
        return getStorage(storageType).getItem(key);
    } catch (e) {
        console.warn("MoBrowserStorage: getItem failed", e);
        return null;
    }
}

/**
 * Set an item in browser storage
 * @param {string} storageType - "local" or "session"
 * @param {string} key - Storage key
 * @param {string} value - JSON string value
 */
export function setItem(storageType, key, value) {
    trySetItem(storageType, key, value);
}

/**
 * Set an item and return a machine-readable failure code when the browser rejects the write.
 * @param {string} storageType - "local" or "session"
 * @param {string} key - Storage key
 * @param {string} value - JSON string value
 * @returns {string|null} null on success; otherwise a classified failure code
 */
export function trySetItem(storageType, key, value) {
    try {
        getStorage(storageType).setItem(key, value);
        return null;
    } catch (e) {
        console.warn("MoBrowserStorage: setItem failed", e);
        if (isQuotaExceeded(e)) {
            return "quota-exceeded";
        }
        if (isStorageUnavailable(e)) {
            return "storage-unavailable";
        }
        return "unknown";
    }
}

/**
 * Remove an item from browser storage
 * @param {string} storageType - "local" or "session"
 * @param {string} key - Storage key
 */
export function removeItem(storageType, key) {
    try {
        getStorage(storageType).removeItem(key);
    } catch (e) {
        console.warn("MoBrowserStorage: removeItem failed", e);
    }
}

/**
 * Get all keys matching a prefix
 * @param {string} storageType - "local" or "session"
 * @param {string} prefix - Key prefix to match
 * @returns {string[]}
 */
export function getKeys(storageType, prefix) {
    try {
        const storage = getStorage(storageType);
        const keys = [];
        for (let i = 0; i < storage.length; i++) {
            const key = storage.key(i);
            if (key && key.startsWith(prefix)) {
                keys.push(key);
            }
        }
        return keys;
    } catch (e) {
        console.warn("MoBrowserStorage: getKeys failed", e);
        return [];
    }
}

/**
 * Clear all keys matching a prefix
 * @param {string} storageType - "local" or "session"
 * @param {string} prefix - Key prefix to match
 * @returns {number} Number of items removed
 */
export function clearByPrefix(storageType, prefix) {
    try {
        const storage = getStorage(storageType);
        const keysToRemove = [];
        for (let i = 0; i < storage.length; i++) {
            const key = storage.key(i);
            if (key && key.startsWith(prefix)) {
                keysToRemove.push(key);
            }
        }
        keysToRemove.forEach(key => storage.removeItem(key));
        return keysToRemove.length;
    } catch (e) {
        console.warn("MoBrowserStorage: clearByPrefix failed", e);
        return 0;
    }
}
