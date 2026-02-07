// Theme helper functions (DOM operations only, storage handled by mo-browser-storage.js)

export function getSystemDarkMode() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function watchSystemTheme(callback) {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    mediaQuery.addEventListener('change', (e) => {
        callback(e.matches);
    });
}

export function setDocumentTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
}
