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

export function saveThemeData(themeName, mode) {
    localStorage.setItem('mo-theme-data', `${themeName}|${mode}`);
    // 兼容旧版本
    localStorage.setItem('mo-theme-preference', mode);
}

export function getThemeData() {
    const saved = localStorage.getItem('mo-theme-data');
    if (saved) {
        return saved;
    }
    // 兼容旧版本
    const oldPreference = localStorage.getItem('mo-theme-preference');
    if (oldPreference) {
        return `default|${oldPreference}`;
    }
    return `default|${getSystemDarkMode() ? 'dark' : 'light'}`;
}