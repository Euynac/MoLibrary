import "../lib/cronstrue/cronstrue-i18n.min.js";

const localeByCulture = new Map([
    ["zh-cn", "zh_CN"],
    ["zh-tw", "zh_TW"],
    ["pt-br", "pt_BR"],
    ["pt-pt", "pt_PT"],
    ["en", "en"],
    ["en-us", "en"]
]);

function resolveLocale(cultureName) {
    if (typeof cultureName !== "string" || cultureName.length === 0) {
        return "en";
    }

    const normalized = cultureName.replace("_", "-").toLowerCase();
    return localeByCulture.get(normalized) ?? normalized.split("-")[0] ?? "en";
}

/**
 * Converts one visible catalog page of Cron expressions into localized descriptions.
 * Each result preserves the caller's key so duplicate expressions remain independently addressable.
 */
export function describeCronExpressions(requests, cultureName) {
    if (!globalThis.cronstrue?.toString) {
        throw new Error("The bundled cronstrue parser is unavailable.");
    }

    const options = {
        locale: resolveLocale(cultureName),
        use24HourTimeFormat: true,
        throwExceptionOnParseError: true,
        verbose: false,
        dayOfWeekStartIndexZero: true,
        logicalAndDayFields: true
    };

    return (requests ?? []).map(request => {
        try {
            return {
                key: request.key,
                description: globalThis.cronstrue.toString(request.expression, options),
                error: null
            };
        } catch (error) {
            return {
                key: request.key,
                description: null,
                error: error?.message ?? String(error)
            };
        }
    });
}
