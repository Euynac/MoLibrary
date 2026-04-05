/**
 * Chat message list auto-scroll module
 */

const scrollThreshold = 100; // pixels from bottom to consider "at bottom"

/**
 * Initialize auto-scroll behavior for a message list element
 * @param {HTMLElement} element - The message list container element
 * @returns {Object} - Control object with dispose method
 */
export function initAutoScroll(element) {
    if (!element) return null;

    let isUserScrolling = false;
    let isProgrammaticScrolling = false;
    let scrollTimeout = null;
    let mutationDebounceTimeout = null;
    let lastScrollHeight = element.scrollHeight;

    const isNearBottom = () => {
        const { scrollTop, scrollHeight, clientHeight } = element;
        return scrollHeight - scrollTop - clientHeight < scrollThreshold;
    };

    const scrollToBottom = () => {
        isProgrammaticScrolling = true;
        element.scrollTo({
            top: element.scrollHeight,
            behavior: 'smooth'
        });
        // Reset after smooth scroll animation completes (~300-500ms)
        setTimeout(() => {
            isProgrammaticScrolling = false;
        }, 400);
    };

    const handleScroll = () => {
        // Ignore scroll events caused by our programmatic scrolling
        if (isProgrammaticScrolling) return;

        clearTimeout(scrollTimeout);
        isUserScrolling = true;
        scrollTimeout = setTimeout(() => {
            isUserScrolling = false;
        }, 150);
    };

    // Debounced scroll check - waits for mutations to settle
    const checkAndScroll = () => {
        clearTimeout(mutationDebounceTimeout);
        mutationDebounceTimeout = setTimeout(() => {
            const newScrollHeight = element.scrollHeight;
            const contentGrew = newScrollHeight > lastScrollHeight;
            lastScrollHeight = newScrollHeight;

            if (contentGrew || (!isUserScrolling && isNearBottom())) {
                requestAnimationFrame(scrollToBottom);
            }
        }, 16); // ~1 frame, allows DOM to settle
    };

    const observer = new MutationObserver(checkAndScroll);

    element.addEventListener('scroll', handleScroll, { passive: true });
    observer.observe(element, {
        childList: true,
        subtree: true,
        characterData: true
    });

    scrollToBottom();

    return {
        scrollToBottom,
        dispose: () => {
            element.removeEventListener('scroll', handleScroll);
            observer.disconnect();
            clearTimeout(scrollTimeout);
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
