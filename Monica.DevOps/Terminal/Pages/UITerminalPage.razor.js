export function createTerminalScroller(element) {
    let animationFrame = null;
    let disposed = false;
    let shutdownPromise = null;

    const scrollToBottom = () => {
        if (disposed || !element) {
            return;
        }

        if (animationFrame !== null) {
            cancelAnimationFrame(animationFrame);
        }

        animationFrame = requestAnimationFrame(() => {
            animationFrame = null;
            if (!disposed && element?.isConnected) {
                element.scrollTop = element.scrollHeight;
            }
        });
    };

    const shutdown = () => {
        if (shutdownPromise) {
            return shutdownPromise;
        }

        disposed = true;
        removalObserver.disconnect();
        if (animationFrame !== null) {
            cancelAnimationFrame(animationFrame);
            animationFrame = null;
        }
        element = null;
        shutdownPromise = Promise.resolve();
        return shutdownPromise;
    };

    const removalObserver = new MutationObserver(() => {
        if (!element?.isConnected) {
            void shutdown();
        }
    });
    const observerRoot = element?.ownerDocument?.body;
    if (observerRoot) {
        removalObserver.observe(observerRoot, { childList: true, subtree: true });
    }

    return {
        scrollToBottom,
        shutdown
    };
}
