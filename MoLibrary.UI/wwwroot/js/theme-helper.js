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

export function saveThemePreference(isDark) {
    localStorage.setItem('mo-theme-preference', isDark ? 'dark' : 'light');
}

export function getThemePreference() {
    const saved = localStorage.getItem('mo-theme-preference');
    if (saved) {
        return saved === 'dark';
    }
    return getSystemDarkMode();
}