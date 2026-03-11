export function shouldShowSidebarByDefault() {
    return window.matchMedia("(min-width: 961px)").matches;
}
