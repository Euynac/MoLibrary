export function scrollToMatch(container, searchKey) {
    if (!container || !searchKey) {
        return;
    }

    const target = container.querySelector(`[data-search-key="${searchKey}"]`);
    if (!target) {
        return;
    }

    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            const containerRect = container.getBoundingClientRect();
            const targetRect = target.getBoundingClientRect();
            const currentScrollTop = container.scrollTop;
            const centeredTop = targetRect.top - containerRect.top + currentScrollTop
                - (containerRect.height - targetRect.height) / 2;

            container.scrollTo({
                top: Math.max(centeredTop, 0),
                behavior: "smooth"
            });
        });
    });
}
