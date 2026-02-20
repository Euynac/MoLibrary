// Culture helper for client-side cookie management

/**
 * Set the culture cookie that ASP.NET Core middleware expects
 * @param {string} culture - Culture code (e.g., "zh-CN", "en-US")
 * @param {string} cookieName - Cookie name (default: ".AspNetCore.Culture")
 */
export function setCultureCookie(culture, cookieName = ".AspNetCore.Culture") {
    // Format: c=zh-CN|uic=zh-CN (ASP.NET Core format)
    const cookieValue = `c=${culture}|uic=${culture}`;

    // Set cookie with 1 year expiration
    const expires = new Date();
    expires.setFullYear(expires.getFullYear() + 1);

    document.cookie = `${cookieName}=${cookieValue}; expires=${expires.toUTCString()}; path=/; SameSite=Lax`;
}

/**
 * Get the current culture from cookie
 * @param {string} cookieName - Cookie name
 * @returns {string|null} Culture code or null if not set
 */
export function getCultureFromCookie(cookieName = ".AspNetCore.Culture") {
    const cookies = document.cookie.split(';');
    for (let cookie of cookies) {
        const [name, value] = cookie.trim().split('=');
        if (name === cookieName) {
            // Parse format: c=zh-CN|uic=zh-CN
            const match = value.match(/c=([^|]+)/);
            return match ? match[1] : null;
        }
    }
    return null;
}
