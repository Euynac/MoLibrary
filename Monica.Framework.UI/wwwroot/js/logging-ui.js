const scrollListenerMap = new WeakMap();

export function scrollToBottom(element) {
    if (!element) {
        return;
    }

    try {
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                element.scrollTop = element.scrollHeight;
            });
        });
    } catch (error) {
        console.error("Failed to scroll log viewer", error);
    }
}

export async function copyToClipboard(text) {
    if (!text) {
        return false;
    }

    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }

        const host = document.body ?? document.documentElement;
        if (!host) {
            return false;
        }

        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.position = "fixed";
        textArea.style.left = "-999999px";
        textArea.style.top = "-999999px";
        host.appendChild(textArea);
        textArea.focus();
        textArea.select();

        try {
            return document.execCommand("copy");
        } catch (error) {
            console.error("Failed to copy text using fallback method", error);
            return false;
        } finally {
            if (textArea.isConnected) {
                textArea.remove();
            }
        }
    } catch (error) {
        console.error("Failed to copy text to clipboard", error);
        return false;
    }
}

export function getSelectedText() {
    try {
        return window.getSelection()?.toString().trim() ?? "";
    } catch (error) {
        console.error("Failed to get selected text", error);
        return "";
    }
}

export function scrollToTop(element) {
    if (!element) {
        return;
    }

    try {
        element.scrollTop = 0;
    } catch (error) {
        console.error("Failed to scroll to top", error);
    }
}

export function scrollToLineIndex(container, lineIndex) {
    if (!container) {
        return;
    }

    try {
        const targetElement = container.querySelector(`[data-line-index="${lineIndex}"]`);
        if (!targetElement) {
            return;
        }

        const containerRect = container.getBoundingClientRect();
        const elementRect = targetElement.getBoundingClientRect();
        const containerScrollTop = container.scrollTop;
        const elementTop = elementRect.top - containerRect.top + containerScrollTop;
        const centerOffset = (containerRect.height - elementRect.height) / 2;
        const scrollTo = elementTop - centerOffset;

        container.scrollTo({
            top: scrollTo,
            behavior: "smooth"
        });
    } catch (error) {
        console.error("Failed to scroll to line index", error);
    }
}

export async function addScrollTopListener(container, dotNetRef, methodName, threshold = 50) {
    if (!container || !dotNetRef) {
        return;
    }

    await removeScrollListener(container);

    const session = {
        acceptingCallbacks: true,
        isLoading: false,
        lastScrollTop: container.scrollTop,
        pendingCallbacks: new Set(),
        handleScroll: null
    };

    const handleScroll = () => {
        const scrollTop = container.scrollTop;
        const isScrollingUp = scrollTop < session.lastScrollTop;
        session.lastScrollTop = scrollTop;

        if (!session.acceptingCallbacks || !isScrollingUp || scrollTop > threshold || session.isLoading) {
            return;
        }

        session.isLoading = true;
        const callback = dotNetRef.invokeMethodAsync(methodName)
            .catch(error => {
                if (session.acceptingCallbacks) {
                    console.error("Error invoking load more:", error);
                }
            })
            .finally(() => {
                session.isLoading = false;
                session.pendingCallbacks.delete(callback);
            });

        session.pendingCallbacks.add(callback);
    };

    session.handleScroll = handleScroll;
    container.addEventListener("scroll", handleScroll, { passive: true });
    scrollListenerMap.set(container, session);
}

export async function removeScrollListener(container) {
    if (!container) {
        return;
    }

    const session = scrollListenerMap.get(container);
    if (!session) {
        return;
    }

    session.acceptingCallbacks = false;
    container.removeEventListener("scroll", session.handleScroll);
    if (scrollListenerMap.get(container) === session) {
        scrollListenerMap.delete(container);
    }

    await Promise.allSettled([...session.pendingCallbacks]);
    session.pendingCallbacks.clear();
}

export function preserveScrollPosition(container, absoluteLineNumber) {
    if (!container || absoluteLineNumber === null || absoluteLineNumber === undefined) {
        return false;
    }

    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            try {
                const anchorElement = container.querySelector(
                    `[data-absolute-line-number="${absoluteLineNumber}"]`);

                if (anchorElement) {
                    anchorElement.scrollIntoView({ block: "start" });
                }
            } catch (error) {
                console.error("Failed to preserve scroll position", error);
            }
        });
    });

    return true;
}

export function downloadFile(url) {
    if (!url) {
        return false;
    }

    const host = document.body ?? document.documentElement;
    if (!host) {
        return false;
    }

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "";
    anchor.rel = "noopener";
    anchor.style.display = "none";

    host.appendChild(anchor);

    try {
        anchor.click();
        return true;
    } finally {
        if (anchor.isConnected) {
            anchor.remove();
        }
    }
}
