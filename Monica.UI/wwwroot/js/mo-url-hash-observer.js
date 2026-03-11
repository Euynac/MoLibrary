const observers = new Set();

let historyPatched = false;
let listenersAttached = false;

function notifyObservers() {
    const anchorId = normalizeHash(window.location.hash);

    for (const observer of observers) {
        observer.invokeMethodAsync("OnHashChangedAsync", anchorId)
            .catch(() => {
            });
    }
}

function normalizeHash(hash) {
    if (!hash || hash === "#") {
        return null;
    }

    return hash.startsWith("#")
        ? hash.substring(1)
        : hash;
}

function dispatchLocationChanged() {
    window.dispatchEvent(new Event("mo-location-changed"));
}

function ensureHistoryPatched() {
    if (historyPatched) {
        return;
    }

    const originalPushState = history.pushState.bind(history);
    const originalReplaceState = history.replaceState.bind(history);

    history.pushState = function (...args) {
        originalPushState(...args);
        dispatchLocationChanged();
    };

    history.replaceState = function (...args) {
        originalReplaceState(...args);
        dispatchLocationChanged();
    };

    historyPatched = true;
}

function attachListeners() {
    if (listenersAttached) {
        return;
    }

    window.addEventListener("hashchange", notifyObservers);
    window.addEventListener("popstate", notifyObservers);
    window.addEventListener("mo-location-changed", notifyObservers);
    listenersAttached = true;
}

function detachListeners() {
    if (!listenersAttached) {
        return;
    }

    window.removeEventListener("hashchange", notifyObservers);
    window.removeEventListener("popstate", notifyObservers);
    window.removeEventListener("mo-location-changed", notifyObservers);
    listenersAttached = false;
}

export function observeHash(dotNetReference) {
    if (!dotNetReference) {
        return;
    }

    ensureHistoryPatched();
    observers.add(dotNetReference);
    attachListeners();
    notifyObservers();
}

export function unobserveHash(dotNetReference) {
    if (!dotNetReference) {
        return;
    }

    observers.delete(dotNetReference);

    if (observers.size === 0) {
        detachListeners();
    }
}
