export function shouldShowSidebarByDefault() {
    return window.matchMedia("(min-width: 961px)").matches;
}

let activeHeadingTracker = null;

export function observeActiveHeading(headingIds, currentAnchorId, dotNetReference) {
    disposeActiveHeadingTracker();

    const tracker = createActiveHeadingTracker(headingIds, currentAnchorId, dotNetReference);
    if (!tracker) {
        return;
    }

    activeHeadingTracker = tracker;
    tracker.sync(true);
    tracker.scrollRoot.addEventListener("scroll", tracker.handleScroll, { passive: true });
    window.addEventListener("resize", tracker.handleResize);
}

export function disposeActiveHeadingTracker() {
    if (!activeHeadingTracker) {
        return;
    }

    activeHeadingTracker.scrollRoot.removeEventListener("scroll", activeHeadingTracker.handleScroll);
    window.removeEventListener("resize", activeHeadingTracker.handleResize);
    activeHeadingTracker = null;
}

function createActiveHeadingTracker(headingIds, currentAnchorId, dotNetReference) {
    if (!Array.isArray(headingIds) || headingIds.length === 0) {
        return null;
    }

    const scrollRoot = document.querySelector("[data-markdown-scroll-root='true']");
    const markdownBody = document.querySelector("[data-markdown-body='true']");
    if (!scrollRoot || !markdownBody) {
        return null;
    }

    const headings = headingIds
        .map(anchorId => {
            const element = resolveHeadingElement(markdownBody, anchorId);
            if (!element) {
                return null;
            }

            return {
                anchorId,
                element
            };
        })
        .filter(Boolean);

    if (headings.length === 0) {
        return null;
    }

    const tracker = {
        currentAnchorId: null,
        dotNetReference,
        headings,
        scrollRoot,
        syncScheduled: false,
        handleResize: () => scheduleSync(tracker, false),
        handleScroll: () => scheduleSync(tracker, false),
        sync: alignCurrentAnchor => syncActiveHeading(tracker, alignCurrentAnchor)
    };

    tracker.currentAnchorId = normalizeAnchorId(currentAnchorId);
    return tracker;
}

function scheduleSync(tracker, alignCurrentAnchor) {
    if (tracker.syncScheduled) {
        return;
    }

    tracker.syncScheduled = true;
    window.requestAnimationFrame(() => {
        tracker.syncScheduled = false;
        tracker.sync(alignCurrentAnchor);
    });
}

function syncActiveHeading(tracker, alignCurrentAnchor) {
    const activeHeading = resolveActiveHeading(tracker, alignCurrentAnchor);
    const nextAnchorId = activeHeading?.anchorId ?? null;

    if (!nextAnchorId || nextAnchorId === tracker.currentAnchorId) {
        return;
    }

    tracker.currentAnchorId = nextAnchorId;
    replaceLocationHash(nextAnchorId);
    notifyActiveHeadingChanged(tracker, nextAnchorId);
}

function resolveActiveHeading(tracker, alignCurrentAnchor) {
    const scrollRootRect = tracker.scrollRoot.getBoundingClientRect();
    const activationLine = scrollRootRect.top + Math.min(96, scrollRootRect.height * 0.2);

    if (alignCurrentAnchor) {
        const currentHeading = tracker.headings.find(heading => heading.anchorId === tracker.currentAnchorId);
        if (currentHeading) {
            scrollHeadingIntoView(tracker.scrollRoot, currentHeading.element);
        }
    }

    let activeHeading = tracker.headings[0];

    for (const heading of tracker.headings) {
        const rect = heading.element.getBoundingClientRect();
        if (rect.bottom <= scrollRootRect.top) {
            activeHeading = heading;
            continue;
        }

        if (rect.top <= activationLine) {
            activeHeading = heading;
            continue;
        }

        break;
    }

    return activeHeading;
}

function scrollHeadingIntoView(scrollRoot, headingElement) {
    const scrollRootRect = scrollRoot.getBoundingClientRect();
    const headingRect = headingElement.getBoundingClientRect();
    const targetTop = scrollRoot.scrollTop + (headingRect.top - scrollRootRect.top) - 24;

    scrollRoot.scrollTo({
        top: Math.max(targetTop, 0),
        behavior: "auto"
    });
}

function replaceLocationHash(anchorId) {
    const encodedAnchorId = normalizeAnchorId(anchorId);
    const url = new URL(window.location.href);
    const currentHash = normalizeAnchorId(window.location.hash);
    if (currentHash === encodedAnchorId) {
        return;
    }

    url.hash = encodedAnchorId ? `#${encodedAnchorId}` : "";
    history.replaceState(history.state, "", url);
}

function notifyActiveHeadingChanged(tracker, anchorId) {
    if (!tracker.dotNetReference) {
        return;
    }

    tracker.dotNetReference.invokeMethodAsync("OnHashChangedAsync", anchorId)
        .catch(() => {
        });
}

function normalizeAnchorId(anchorId) {
    if (!anchorId) {
        return null;
    }

    const normalized = anchorId.startsWith("#")
        ? anchorId.slice(1)
        : anchorId;

    return normalized || null;
}

function resolveHeadingElement(markdownBody, anchorId) {
    const candidates = [
        anchorId,
        decodeAnchorId(anchorId)
    ].filter(Boolean);

    for (const candidate of candidates) {
        const element = markdownBody.querySelector(`#${CSS.escape(candidate)}`);
        if (element) {
            return element;
        }
    }

    return null;
}

function decodeAnchorId(anchorId) {
    try {
        return decodeURIComponent(anchorId);
    }
    catch {
        return anchorId;
    }
}
