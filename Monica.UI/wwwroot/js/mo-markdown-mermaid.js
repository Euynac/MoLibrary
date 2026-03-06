let mermaidLoadPromise;

function getMermaidSource() {
    return '/_content/Monica.UI/lib/mermaid/mermaid.min.js';
}

async function ensureMermaid() {
    if (window.mermaid) {
        return window.mermaid;
    }

    if (!mermaidLoadPromise) {
        mermaidLoadPromise = new Promise((resolve, reject) => {
            const existingScript = document.querySelector('script[data-mo-markdown-mermaid]');
            if (existingScript) {
                existingScript.addEventListener('load', () => resolve(window.mermaid), { once: true });
                existingScript.addEventListener('error', () => reject(new Error('Failed to load Mermaid runtime.')), { once: true });
                return;
            }

            const script = document.createElement('script');
            script.src = getMermaidSource();
            script.async = true;
            script.dataset.moMarkdownMermaid = 'true';
            script.onload = () => {
                if (window.mermaid) {
                    resolve(window.mermaid);
                    return;
                }

                reject(new Error('Mermaid runtime loaded without exposing the global API.'));
            };
            script.onerror = () => reject(new Error('Failed to load Mermaid runtime.'));
            document.head.appendChild(script);
        });
    }

    return mermaidLoadPromise;
}

function normalizeDiagramText(definition) {
    return (definition ?? '').replace(/\r\n/g, '\n').trim();
}

function getFontFamily(theme) {
    if (Array.isArray(theme?.fontFamily) && theme.fontFamily.length > 0) {
        return theme.fontFamily.join(', ');
    }

    return getComputedStyle(document.body).fontFamily;
}

function createConfig(theme) {
    return {
        startOnLoad: false,
        securityLevel: 'strict',
        theme: 'base',
        fontFamily: getFontFamily(theme),
        themeVariables: {
            primaryColor: theme.primary,
            primaryTextColor: theme.textPrimary,
            primaryBorderColor: theme.primary,
            secondaryColor: theme.secondary,
            secondaryTextColor: theme.textPrimary,
            secondaryBorderColor: theme.secondary,
            tertiaryColor: theme.surface,
            tertiaryTextColor: theme.textPrimary,
            tertiaryBorderColor: theme.primary,
            mainBkg: theme.surface,
            secondBkg: theme.background,
            tertiaryBkg: theme.surface,
            lineColor: theme.primary,
            textColor: theme.textPrimary,
            nodeTextColor: theme.textPrimary,
            edgeLabelBackground: theme.surface,
            clusterBkg: theme.background,
            clusterBorder: theme.primary,
            titleColor: theme.textPrimary,
            actorBkg: theme.surface,
            actorBorder: theme.primary,
            actorTextColor: theme.textPrimary,
            signalColor: theme.primary,
            signalTextColor: theme.textPrimary,
            labelBoxBkgColor: theme.surface,
            labelBoxBorderColor: theme.primary,
            labelTextColor: theme.textPrimary,
            activationBkgColor: theme.background,
            activationBorderColor: theme.primary,
            sectionBkgColor: theme.background,
            altSectionBkgColor: theme.surface,
            c0: theme.primary,
            c1: theme.secondary,
            c2: theme.info,
            c3: theme.success,
            c4: theme.warning,
            c5: theme.error,
            pie1: theme.primary,
            pie2: theme.secondary,
            pie3: theme.info,
            pie4: theme.success,
            pie5: theme.warning,
            pie6: theme.error,
            pie7: theme.background,
            pie8: theme.surface,
            pieOuterStrokeColor: theme.primary,
            pieTitleTextSize: '22px'
        }
    };
}

export async function clearMermaid(element) {
    if (element) {
        element.innerHTML = '';
    }
}

export async function renderMermaid(element, definition, theme) {
    if (!element) {
        return;
    }

    const normalizedDefinition = normalizeDiagramText(definition);
    if (!normalizedDefinition) {
        element.innerHTML = '';
        return;
    }

    const mermaid = await ensureMermaid();
    mermaid.initialize(createConfig(theme));

    const diagramId = `mo-markdown-mermaid-${crypto.randomUUID()}`;
    const result = await mermaid.render(diagramId, normalizedDefinition);

    element.innerHTML = result.svg;
    if (typeof result.bindFunctions === 'function') {
        result.bindFunctions(element);
    }
}
