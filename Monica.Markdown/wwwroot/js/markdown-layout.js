export function shouldShowSidebarByDefault() {
    return window.matchMedia("(min-width: 961px)").matches;
}

let activeHeadingTracker = null;
let activeSearchHit = null;

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

export function clearSearchHit() {
    if (!activeSearchHit || !activeSearchHit.parentNode) {
        activeSearchHit = null;
        return;
    }

    const parent = activeSearchHit.parentNode;
    while (activeSearchHit.firstChild) {
        parent.insertBefore(activeSearchHit.firstChild, activeSearchHit);
    }

    parent.removeChild(activeSearchHit);
    parent.normalize();
    activeSearchHit = null;
}

export function highlightSearchHit(anchorId, headingLevel, matchedText, prefixContext, suffixContext) {
    clearSearchHit();

    if (!matchedText) {
        return false;
    }

    const scrollRoot = document.querySelector("[data-markdown-scroll-root='true']");
    const contentRoot = document.querySelector("[data-markdown-content-root='true']");
    if (!scrollRoot || !contentRoot) {
        return false;
    }

    const textMap = buildNormalizedTextMap(contentRoot);
    if (!textMap.text) {
        return false;
    }

    const searchWindow = resolveSearchWindow(contentRoot, textMap, anchorId, headingLevel);
    const match = locateSearchMatch(
        textMap.text,
        searchWindow,
        matchedText,
        prefixContext ?? "",
        suffixContext ?? "");

    if (!match) {
        return false;
    }

    const mark = wrapMatchRange(textMap, match.start, match.end);
    if (!mark) {
        return false;
    }

    activeSearchHit = mark;
    scrollElementIntoView(scrollRoot, mark);
    return true;
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

function scrollElementIntoView(scrollRoot, element) {
    const scrollRootRect = scrollRoot.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const targetTop = scrollRoot.scrollTop + (elementRect.top - scrollRootRect.top) - Math.max(48, scrollRootRect.height * 0.2);

    scrollRoot.scrollTo({
        top: Math.max(targetTop, 0),
        behavior: "smooth"
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

function buildNormalizedTextMap(root) {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
        acceptNode(node) {
            if (!node.textContent) {
                return NodeFilter.FILTER_REJECT;
            }

            const parentElement = node.parentElement;
            if (!parentElement) {
                return NodeFilter.FILTER_REJECT;
            }

            const tagName = parentElement.tagName;
            if (tagName === "SCRIPT" || tagName === "STYLE") {
                return NodeFilter.FILTER_REJECT;
            }

            return NodeFilter.FILTER_ACCEPT;
        }
    });

    const indexMap = [];
    let text = "";
    let previousWasWhitespace = true;

    while (walker.nextNode()) {
        const node = walker.currentNode;
        const value = node.textContent ?? "";

        for (let index = 0; index < value.length; index++) {
            const character = value[index];
            if (/\s/.test(character)) {
                if (!previousWasWhitespace) {
                    text += " ";
                    indexMap.push({ node, offset: index });
                    previousWasWhitespace = true;
                }

                continue;
            }

            text += character;
            indexMap.push({ node, offset: index });
            previousWasWhitespace = false;
        }
    }

    let trimStart = 0;
    while (trimStart < text.length && text[trimStart] === " ") {
        trimStart++;
    }

    let trimEnd = text.length;
    while (trimEnd > trimStart && text[trimEnd - 1] === " ") {
        trimEnd--;
    }

    return {
        text: text.slice(trimStart, trimEnd),
        indexMap: indexMap.slice(trimStart, trimEnd)
    };
}

function resolveSearchWindow(contentRoot, textMap, anchorId, headingLevel) {
    const windowRange = {
        start: 0,
        end: textMap.text.length
    };

    if (!anchorId) {
        return windowRange;
    }

    const anchorElement = resolveHeadingElement(contentRoot, anchorId);
    if (!anchorElement) {
        return windowRange;
    }

    const startIndex = findFirstIndexForElement(textMap, anchorElement);
    if (startIndex !== null) {
        windowRange.start = startIndex;
    }

    const boundaryHeading = findBoundaryHeading(contentRoot, anchorElement, headingLevel);
    if (!boundaryHeading) {
        return windowRange;
    }

    const endIndex = findFirstIndexForElement(textMap, boundaryHeading);
    if (endIndex !== null && endIndex > windowRange.start) {
        windowRange.end = endIndex;
    }

    return windowRange;
}

function findBoundaryHeading(contentRoot, anchorElement, headingLevel) {
    const headings = Array.from(contentRoot.querySelectorAll("h1, h2, h3, h4, h5, h6"));
    const anchorIndex = headings.indexOf(anchorElement);
    if (anchorIndex < 0) {
        return null;
    }

    const normalizedHeadingLevel = typeof headingLevel === "number"
        ? headingLevel
        : parseHeadingLevel(anchorElement);

    for (let index = anchorIndex + 1; index < headings.length; index++) {
        const candidate = headings[index];
        if (parseHeadingLevel(candidate) <= normalizedHeadingLevel) {
            return candidate;
        }
    }

    return null;
}

function parseHeadingLevel(element) {
    const match = /^H([1-6])$/i.exec(element.tagName);
    return match ? Number.parseInt(match[1], 10) : 6;
}

function findFirstIndexForElement(textMap, element) {
    for (let index = 0; index < textMap.indexMap.length; index++) {
        if (element.contains(textMap.indexMap[index].node)) {
            return index;
        }
    }

    return null;
}

function locateSearchMatch(text, searchWindow, matchedText, prefixContext, suffixContext) {
    const occurrences = findOccurrences(text, matchedText, searchWindow.start, searchWindow.end);
    if (occurrences.length === 0) {
        return null;
    }

    let best = null;

    for (const start of occurrences) {
        const end = start + matchedText.length;
        let score = 0;

        if (prefixContext) {
            const actualPrefix = text.slice(Math.max(searchWindow.start, start - prefixContext.length), start);
            score += actualPrefix.endsWith(prefixContext)
                ? 1000
                : commonSuffixLength(actualPrefix, prefixContext) * 12;
        }

        if (suffixContext) {
            const actualSuffix = text.slice(end, Math.min(searchWindow.end, end + suffixContext.length));
            score += actualSuffix.startsWith(suffixContext)
                ? 1000
                : commonPrefixLength(actualSuffix, suffixContext) * 12;
        }

        score -= Math.abs(start - searchWindow.start) * 0.05;

        if (!best || score > best.score) {
            best = { start, end, score };
        }
    }

    return best;
}

function findOccurrences(text, value, start, end) {
    if (!text || !value || end <= start) {
        return [];
    }

    const matches = [];
    let fromIndex = start;

    while (fromIndex < end) {
        const index = indexOfIgnoreCase(text, value, fromIndex);
        if (index < 0 || index + value.length > end) {
            break;
        }

        matches.push(index);
        fromIndex = index + Math.max(value.length, 1);
    }

    return matches;
}

function indexOfIgnoreCase(text, value, startIndex) {
    return text.toLocaleLowerCase().indexOf(value.toLocaleLowerCase(), startIndex);
}

function commonPrefixLength(left, right) {
    const maxLength = Math.min(left.length, right.length);

    for (let index = 0; index < maxLength; index++) {
        if (left[index] !== right[index]) {
            return index;
        }
    }

    return maxLength;
}

function commonSuffixLength(left, right) {
    const maxLength = Math.min(left.length, right.length);

    for (let offset = 0; offset < maxLength; offset++) {
        if (left[left.length - offset - 1] !== right[right.length - offset - 1]) {
            return offset;
        }
    }

    return maxLength;
}

function wrapMatchRange(textMap, start, end) {
    const startMap = textMap.indexMap[start];
    const endMap = textMap.indexMap[end - 1];

    if (!startMap || !endMap) {
        return null;
    }

    const range = document.createRange();
    range.setStart(startMap.node, startMap.offset);
    range.setEnd(endMap.node, endMap.offset + 1);

    const mark = document.createElement("mark");
    mark.setAttribute("data-markdown-search-hit", "true");
    mark.className = "mo-markdown-search-hit";

    try {
        range.surroundContents(mark);
    }
    catch {
        const fragment = range.extractContents();
        mark.appendChild(fragment);
        range.insertNode(mark);
    }

    return mark;
}

function decodeAnchorId(anchorId) {
    try {
        return decodeURIComponent(anchorId);
    }
    catch {
        return anchorId;
    }
}
