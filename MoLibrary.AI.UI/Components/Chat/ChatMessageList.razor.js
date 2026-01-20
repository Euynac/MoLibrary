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
    let scrollTimeout = null;

    // Check if user is near the bottom
    const isNearBottom = () => {
        const { scrollTop, scrollHeight, clientHeight } = element;
        return scrollHeight - scrollTop - clientHeight < scrollThreshold;
    };

    // Scroll to bottom smoothly
    const scrollToBottom = () => {
        element.scrollTo({
            top: element.scrollHeight,
            behavior: 'smooth'
        });
    };

    // Handle user scroll
    const handleScroll = () => {
        clearTimeout(scrollTimeout);
        isUserScrolling = true;

        scrollTimeout = setTimeout(() => {
            isUserScrolling = false;
        }, 150);
    };

    // MutationObserver for new content
    const observer = new MutationObserver((mutations) => {
        // Only auto-scroll if user was near bottom before mutation
        if (!isUserScrolling && isNearBottom()) {
            requestAnimationFrame(scrollToBottom);
        }
    });

    // Start observing
    element.addEventListener('scroll', handleScroll, { passive: true });
    observer.observe(element, {
        childList: true,
        subtree: true,
        characterData: true
    });

    // Initial scroll to bottom
    scrollToBottom();

    // Return control object
    return {
        scrollToBottom,
        dispose: () => {
            element.removeEventListener('scroll', handleScroll);
            observer.disconnect();
            clearTimeout(scrollTimeout);
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
