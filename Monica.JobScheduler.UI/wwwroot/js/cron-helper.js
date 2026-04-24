// Cron expression parsing helper function

// Used to track cronstrue library loading status
let cronstrueLoadPromise = null;

/**
 * Dynamically load the cronstrue library
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
        const scriptUrl = new URL('_content/Monica.JobScheduler.UI/lib/cronstrue/cronstrue-i18n.min.js', baseUrl).href;
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
 * Make sure the cronstrue library is loaded
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
 * Map Monica/BCP-47 culture names to cronstrue locale identifiers.
 * @param {string} cultureName - UI culture name, for example "zh-CN" or "en-US"
 * @returns {string}
 */
function mapCultureToCronstrueLocale(cultureName) {
    if (!cultureName || typeof cultureName !== 'string') {
        return 'en';
    }

    const normalized = cultureName.replace('_', '-').toLowerCase();
    const localeMap = {
        'zh-cn': 'zh_CN',
        'zh-tw': 'zh_TW',
        'pt-br': 'pt_BR',
        'pt-pt': 'pt_PT',
        'en': 'en',
        'en-us': 'en'
    };

    return localeMap[normalized] || 'en';
}

/**
 * Use the cronstrue library to convert Cron expressions into localized descriptions.
 * @param {string} expression - Cron expression
 * @param {string} format - format type: "standard" (5 paragraphs) or "quartz" (6 paragraphs)
 * @param {string} cultureName - Monica UI culture name
 * @returns {Promise<object>} { success: boolean, description?: string, error?: string }
 */
export async function parseCronExpression(expression, format, cultureName) {
    try {
        const loaded = await ensureCronstrueLoaded();
        if (!loaded) {
            return {
                success: false,
                error: 'cronstrue library is not loaded'
            };
        }

        if (!expression || typeof expression !== 'string') {
            return {
                success: false,
                error: 'Expression cannot be empty'
            };
        }

        // cronstrue configuration options
        const options = {
            locale: mapCultureToCronstrueLocale(cultureName),
            use24HourTimeFormat: true,
            throwExceptionOnParseError: true,
            verbose: false,
            dayOfWeekStartIndexZero: true
        };

        // cronstrue will automatically detect 5-segment or 6-segment format
        const description = cronstrue.toString(expression, options);

        return {
            success: true,
            description: description
        };
    } catch (error) {
        return {
            success: false,
            error: `Parse failed: ${error.message || error}`
        };
    }
}

/**
 * Check if cronstrue library is loaded
 * @returns {boolean}
 */
export function isCronstrueLoaded() {
    return typeof cronstrue !== 'undefined';
}

/**
 * Preload the cronstrue library (can be called when the page is initialized)
 * @returns {Promise<boolean>}
 */
export async function preloadCronstrue() {
    return ensureCronstrueLoaded();
}
