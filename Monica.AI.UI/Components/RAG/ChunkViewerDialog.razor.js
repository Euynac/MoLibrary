export function scrollToMatchedChunk(root) {
    if (!root) {
        return false;
    }

    const scrollRoot = root.querySelector("[data-chunk-scroll-root='true']");
    const target = root.querySelector("[data-rag-chunk-target='true']");
    if (!scrollRoot || !target) {
        return false;
    }

    focusElement(target);
    scrollElementIntoView(scrollRoot, target);
    return true;
}

function focusElement(element) {
    if (!element) {
        return;
    }

    element.setAttribute("tabindex", "-1");

    try {
        element.focus({ preventScroll: true });
    }
    catch {
        element.focus();
    }
}

function scrollElementIntoView(scrollRoot, element) {
    const scrollRootRect = scrollRoot.getBoundingClientRect();
    const elementRect = element.getBoundingClientRect();
    const targetTop =
        scrollRoot.scrollTop + (elementRect.top - scrollRootRect.top) - Math.max(48, scrollRootRect.height * 0.18);

    scrollRoot.scrollTo({
        top: Math.max(targetTop, 0),
        behavior: "smooth"
    });
}
