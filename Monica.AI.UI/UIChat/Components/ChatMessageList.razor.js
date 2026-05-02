/**
 * Chat message list auto-scroll module
 */

const scrollThreshold = 80; // pixels from bottom to consider "at bottom"

/**
 * Initialize auto-scroll behavior for a message list element
 * @param {HTMLElement} element - The message list container element
 * @returns {Object} - Control object with dispose method
 */
export function initAutoScroll(element) {
    if (!element) return null;

    let shouldStickToBottom = true;
    let mutationDebounceTimeout = null;
    let resizeFrame = null;

    const isNearBottom = () => {
        const { scrollTop, scrollHeight, clientHeight } = element;
        return scrollHeight - scrollTop - clientHeight <= scrollThreshold;
    };

    const scrollToBottom = (behavior = 'auto') => {
        shouldStickToBottom = true;
        element.scrollTo({
            top: element.scrollHeight,
            behavior
        });
    };

    const handleScroll = () => {
        shouldStickToBottom = isNearBottom();
    };

    const scheduleStickyScroll = () => {
        if (!shouldStickToBottom) {
            return;
        }

        clearTimeout(mutationDebounceTimeout);
        mutationDebounceTimeout = setTimeout(() => {
            if (!shouldStickToBottom) {
                return;
            }

            requestAnimationFrame(() => scrollToBottom('auto'));
        }, 16);
    };

    const observer = new MutationObserver(scheduleStickyScroll);
    const resizeObserver = new ResizeObserver(() => {
        if (resizeFrame !== null) {
            cancelAnimationFrame(resizeFrame);
        }

        resizeFrame = requestAnimationFrame(() => {
            resizeFrame = null;
            scheduleStickyScroll();
        });
    });

    element.addEventListener('scroll', handleScroll, { passive: true });
    observer.observe(element, {
        childList: true,
        subtree: true,
        attributes: true,
        characterData: true
    });
    resizeObserver.observe(element);

    scrollToBottom();

    return {
        scrollToBottom: () => scrollToBottom('smooth'),
        dispose: () => {
            element.removeEventListener('scroll', handleScroll);
            observer.disconnect();
            resizeObserver.disconnect();
            if (resizeFrame !== null) {
                cancelAnimationFrame(resizeFrame);
            }
            clearTimeout(mutationDebounceTimeout);
        }
    };
}

/**
 * Dispose the auto-scroll instance
 * @param {Object} instance - The instance returned by initAutoScroll
 */
export function disposeAutoScroll(instance) {
    if (instance && typeof instance.dispose === 'function') {
        instance.dispose();
    }
}

/**
 * Manually scroll to bottom
 * @param {Object} instance - The instance returned by initAutoScroll
 */
export function scrollToBottom(instance) {
    if (instance && typeof instance.scrollToBottom === 'function') {
        instance.scrollToBottom();
    }
}
