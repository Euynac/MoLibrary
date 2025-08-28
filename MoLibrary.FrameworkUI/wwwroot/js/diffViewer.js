export function getScrollPosition(element) {
    if (!element) return 0;
    return element.scrollTop;
}

export function setScrollPosition(element, position) {
    if (!element) return;
    element.scrollTop = position;
}