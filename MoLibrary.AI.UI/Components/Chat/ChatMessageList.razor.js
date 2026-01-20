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
    let lastScrollHeight = element.scrollHeight; // Track scroll height for content growth detection

    // Check if user is near the bottom
    const isNearBottom = () => {
        const { scrollTop, scrollHeight, clientHeight } = element;
        return scrollHeight - scrollTop - clientHeight < scrollThreshold;
    };

    // Scroll to bottom smoothly
    const scrollToBottom = () => {
        isUserScrolling = false; // Reset flag so MutationObserver will auto-scroll
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

    // MutationObserver for new content - auto-scroll when content grows (streaming) or when near bottom
    const observer = new MutationObserver((mutations) => {
        const newScrollHeight = element.scrollHeight;
        const contentGrew = newScrollHeight > lastScrollHeight;
        lastScrollHeight = newScrollHeight;

        // Always scroll if content grew (streaming), otherwise use near-bottom logic
        if (contentGrew || (!isUserScrolling && isNearBottom())) {
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
