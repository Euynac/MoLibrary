function getGapWidth(element) {
    const styles = window.getComputedStyle(element);
    const gapValue = styles.columnGap !== "normal" ? styles.columnGap : styles.gap;
    const parsedGap = Number.parseFloat(gapValue);
    return Number.isFinite(parsedGap) ? parsedGap : 0;
}

function measureCategoryWidths(measurementRail) {
    const categoryWidths = Array.from(
        measurementRail.querySelectorAll("[data-nav-measure='category']"),
        (element) => element.getBoundingClientRect().width);

    const moreButton = measurementRail.querySelector("[data-nav-measure='more']");
    const moreWidth = moreButton?.getBoundingClientRect().width ?? 0;

    return { categoryWidths, moreWidth };
}

function calculateVisibleCategoryCount(desktopNav, measurementRail, maxVisibleCategories, safetyMarginPx) {
    const availableWidth = desktopNav.getBoundingClientRect().width - safetyMarginPx;

    if (availableWidth <= 0) {
        return 0;
    }

    const { categoryWidths, moreWidth } = measureCategoryWidths(measurementRail);
    const totalCategories = categoryWidths.length;
    const maxVisibleCount = Math.min(maxVisibleCategories, totalCategories);
    const gapWidth = getGapWidth(desktopNav);

    for (let visibleCount = maxVisibleCount; visibleCount >= 0; visibleCount -= 1) {
        const needsMoreButton = totalCategories > visibleCount;
        const visibleCategoryWidth = categoryWidths
            .slice(0, visibleCount)
            .reduce((sum, width) => sum + width, 0);

        const renderedItemCount = visibleCount + (needsMoreButton ? 1 : 0);
        const renderedGapWidth = Math.max(0, renderedItemCount - 1) * gapWidth;
        const requiredWidth = visibleCategoryWidth
            + (needsMoreButton ? moreWidth : 0)
            + renderedGapWidth;

        if (requiredWidth <= availableWidth) {
            return visibleCount;
        }
    }

    return 0;
}

export function createNavBarLayoutObserver(
    desktopNav,
    measurementRail,
    maxVisibleCategories,
    safetyMarginPx,
    dotNetRef) {
    let disposed = false;
    let frameId = 0;
    let lastVisibleCount = -1;

    const notifyVisibleCount = () => {
        if (disposed) {
            return;
        }

        const visibleCount = calculateVisibleCategoryCount(
            desktopNav,
            measurementRail,
            maxVisibleCategories,
            safetyMarginPx);

        if (visibleCount === lastVisibleCount) {
            return;
        }

        lastVisibleCount = visibleCount;
        void dotNetRef.invokeMethodAsync("UpdateVisibleCategoryCountAsync", visibleCount);
    };

    const scheduleMeasurement = () => {
        if (disposed) {
            return;
        }

        if (frameId !== 0) {
            window.cancelAnimationFrame(frameId);
        }

        frameId = window.requestAnimationFrame(() => {
            frameId = 0;
            notifyVisibleCount();
        });
    };

    const resizeObserver = new ResizeObserver(() => {
        scheduleMeasurement();
    });

    resizeObserver.observe(desktopNav);
    resizeObserver.observe(measurementRail);
    window.addEventListener("resize", scheduleMeasurement);

    scheduleMeasurement();

    return {
        dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            if (frameId !== 0) {
                window.cancelAnimationFrame(frameId);
                frameId = 0;
            }

            resizeObserver.disconnect();
            window.removeEventListener("resize", scheduleMeasurement);
        }
    };
}
