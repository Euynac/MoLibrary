// Cron 表达式解析辅助函数

// 用于跟踪 cronstrue 库加载状态
let cronstrueLoadPromise = null;

/**
 * 动态加载 cronstrue 库
 * @returns {Promise<boolean>}
 */
async function loadCronstrueLibrary() {
    // Already loaded
    if (typeof cronstrue !== 'undefined') {
        return true;
    }

    // Loading in progress
    if (cronstrueLoadPromise) {
        return cronstrueLoadPromise;
    }

    cronstrueLoadPromise = new Promise((resolve, reject) => {
        // Check if script tag already exists
        const existingScript = document.querySelector('script[src*="cronstrue"]');
        if (existingScript) {
            // Script tag exists, wait for it to load
            const checkLoaded = () => {
                if (typeof cronstrue !== 'undefined') {
                    resolve(true);
                } else {
                    setTimeout(checkLoaded, 50);
                }
            };
            // Give it a max of 5 seconds
            const timeout = setTimeout(() => {
                reject(new Error('Timeout waiting for cronstrue to load'));
            }, 5000);

            const checkWithTimeout = () => {
                if (typeof cronstrue !== 'undefined') {
                    clearTimeout(timeout);
                    resolve(true);
                } else {
                    setTimeout(checkWithTimeout, 50);
                }
            };
            checkWithTimeout();
            return;
        }

        // Create and append script
        const script = document.createElement('script');
        // Get base URL from the document's base tag or use root
        const baseElement = document.querySelector('base');
        const baseUrl = baseElement ? baseElement.href : '/';
        const scriptUrl = new URL('_content/MoLibrary.JobScheduler.UI/lib/cronstrue/cronstrue-i18n.min.js', baseUrl).href;
        script.src = scriptUrl;
        script.async = true;

        console.log('[cron-helper] Loading cronstrue from:', scriptUrl);

        script.onload = () => {
            console.log('[cron-helper] Script loaded, checking cronstrue...');
            // Wait a bit for the script to execute
            setTimeout(() => {
                if (typeof cronstrue !== 'undefined') {
                    console.log('[cron-helper] cronstrue initialized successfully');
                    resolve(true);
                } else {
                    console.error('[cron-helper] cronstrue not defined after script load');
                    reject(new Error('cronstrue library failed to initialize after load'));
                }
            }, 100);
        };

        script.onerror = (e) => {
            console.error('[cron-helper] Failed to load cronstrue script:', scriptUrl, e);
            cronstrueLoadPromise = null;
            reject(new Error('Failed to load cronstrue library'));
        };

        document.head.appendChild(script);
        console.log('[cron-helper] Script tag appended to head');
    });

    return cronstrueLoadPromise;
}

/**
 * 确保 cronstrue 库已加载
 * @returns {Promise<boolean>}
 */
async function ensureCronstrueLoaded() {
    try {
        await loadCronstrueLibrary();
        return true;
    } catch (error) {
        console.error('Failed to load cronstrue:', error);
        return false;
    }
}

/**
 * 使用 cronstrue 库将 Cron 表达式转换为中文描述
 * @param {string} expression - Cron 表达式
 * @param {string} format - 格式类型: "standard" (5段) 或 "quartz" (6段)
 * @returns {Promise<object>} { success: boolean, description?: string, error?: string }
 */
export async function parseCronExpression(expression, format) {
    try {
        const loaded = await ensureCronstrueLoaded();
        if (!loaded) {
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

/**
 * 预加载 cronstrue 库（可在页面初始化时调用）
 * @returns {Promise<boolean>}
 */
export async function preloadCronstrue() {
    return ensureCronstrueLoaded();
}
