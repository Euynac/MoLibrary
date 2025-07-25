// 主题辅助函数

export function getSystemDarkMode() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

export function watchSystemTheme(callback) {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    mediaQuery.addEventListener('change', (e) => {
        callback(e.matches);
    });
}

export function saveThemeData(themeName, mode) {
    localStorage.setItem('mo-theme-data', `${themeName}|${mode}`);
}

export function getThemeData() {
    const saved = localStorage.getItem('mo-theme-data');
    if (saved) {
        return saved;
    }
    return `default|${getSystemDarkMode() ? 'dark' : 'light'}`;
}

export function setDocumentTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
}

