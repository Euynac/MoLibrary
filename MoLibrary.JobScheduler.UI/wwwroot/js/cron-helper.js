// Cron 表达式解析辅助函数

/**
 * 确保 cronstrue 库已加载
 */
function ensureCronstrueLoaded() {
    if (typeof cronstrue === 'undefined') {
        console.error('cronstrue library is not loaded');
        return false;
    }

    // 检查中文语言包
    if (!cronstrue.locales || !cronstrue.locales['zh_CN']) {
        console.warn('Chinese locale (zh_CN) is not loaded, will fallback to English');
    }

    return true;
}

/**
 * 使用 cronstrue 库将 Cron 表达式转换为中文描述
 * @param {string} expression - Cron 表达式
 * @param {string} format - 格式类型: "standard" (5段) 或 "quartz" (6段)
 * @returns {object} { success: boolean, description?: string, error?: string }
 */
export function parseCronExpression(expression, format) {
    try {
        if (!ensureCronstrueLoaded()) {
            return {
                success: false,
                error: 'cronstrue 库未加载'
            };
        }

        if (!expression || typeof expression !== 'string') {
            return {
                success: false,
                error: '表达式不能为空'
            };
        }

        // cronstrue 配置选项
        const options = {
            locale: 'zh_CN',              // 默认中文
            use24HourTimeFormat: true,    // 24 小时制
            throwExceptionOnParseError: true,
            verbose: false,
            dayOfWeekStartIndexZero: true // 周日为 0
        };

        // cronstrue 会自动检测 5 段或 6 段格式
        const description = cronstrue.toString(expression, options);

        return {
            success: true,
            description: description
        };
    } catch (error) {
        return {
            success: false,
            error: `解析失败: ${error.message || error}`
        };
    }
}

/**
 * 检查 cronstrue 库是否已加载
 * @returns {boolean}
 */
export function isCronstrueLoaded() {
    return typeof cronstrue !== 'undefined';
}
