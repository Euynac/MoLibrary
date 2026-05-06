export function scrollToBottom(element) {
    if (!element) {
        return;
    }

    requestAnimationFrame(() => {
        element.scrollTop = element.scrollHeight;
    });
}
