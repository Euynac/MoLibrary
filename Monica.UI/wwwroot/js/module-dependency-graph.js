const SVG_NS = "http://www.w3.org/2000/svg";
const EDGE_SOURCE_PADDING = 2;
const EDGE_TARGET_PADDING = 6;

export function calculateDependencyEdgeEndpoints(source, target, sourceRadius, targetRadius) {
    const deltaX = target.x - source.x;
    const deltaY = target.y - source.y;
    const distance = Math.hypot(deltaX, deltaY);
    if (distance === 0) return null;

    const unitX = deltaX / distance;
    const unitY = deltaY / distance;
    return {
        x1: source.x + unitX * (sourceRadius + EDGE_SOURCE_PADDING),
        y1: source.y + unitY * (sourceRadius + EDGE_SOURCE_PADDING),
        x2: target.x - unitX * (targetRadius + EDGE_TARGET_PADDING),
        y2: target.y - unitY * (targetRadius + EDGE_TARGET_PADDING)
    };
}

export function calculateDependencyGraphLayout(nodes, requestedWidth, requestedHeight) {
    const width = Math.max(320, Number.isFinite(requestedWidth) ? requestedWidth : 0);
    const height = Math.max(320, Number.isFinite(requestedHeight) ? requestedHeight : 0);
    const centerX = width / 2;
    const centerY = height / 2;
    const desiredRadiusX = Math.min(365, Math.max(150, nodes.length * 13));
    const desiredRadiusY = Math.min(220, Math.max(110, nodes.length * 8));
    const radiusX = Math.min(desiredRadiusX, Math.max(72, centerX - 70));
    const radiusY = Math.min(desiredRadiusY, Math.max(88, centerY - 64));
    const nonSelectedCount = Math.max(1, nodes.reduce(
        (count, candidate) => count + (candidate.selected ? 0 : 1),
        0));
    let nonSelectedOrdinal = 0;
    const positions = nodes.map(node => {
        if (node.selected) {
            return { id: node.id, x: centerX, y: centerY };
        }

        const angle = (Math.PI * 2 * nonSelectedOrdinal / nonSelectedCount) - Math.PI / 2;
        nonSelectedOrdinal += 1;
        return {
            id: node.id,
            x: centerX + Math.cos(angle) * radiusX,
            y: centerY + Math.sin(angle) * radiusY
        };
    });

    return { width, height, positions };
}

export function createGraph(element, initialModel) {
    let model = initialModel;
    let disposed = false;
    let animationFrame = 0;
    const instanceToken = globalThis.crypto?.randomUUID?.()
        ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
    const arrowMarkerId = `module-dependency-arrow-${instanceToken}`;

    const svg = document.createElementNS(SVG_NS, "svg");
    svg.setAttribute("preserveAspectRatio", "xMidYMid meet");
    svg.setAttribute("focusable", "false");
    element.replaceChildren(svg);

    const scheduleRender = () => {
        if (disposed) return;
        cancelAnimationFrame(animationFrame);
        animationFrame = requestAnimationFrame(render);
    };

    const render = () => {
        if (disposed) return;
        svg.replaceChildren();
        const nodes = Array.isArray(model?.nodes) ? model.nodes : [];
        const edges = Array.isArray(model?.edges) ? model.edges : [];
        const layout = calculateDependencyGraphLayout(nodes, element.clientWidth, element.clientHeight);
        svg.setAttribute("viewBox", `0 0 ${layout.width} ${layout.height}`);
        const positions = new Map(layout.positions.map(position => [position.id, position]));
        const nodeById = new Map(nodes.map(node => [node.id, node]));

        const definitions = document.createElementNS(SVG_NS, "defs");
        const arrowMarker = document.createElementNS(SVG_NS, "marker");
        arrowMarker.setAttribute("id", arrowMarkerId);
        arrowMarker.setAttribute("viewBox", "0 0 10 10");
        arrowMarker.setAttribute("refX", "9");
        arrowMarker.setAttribute("refY", "5");
        arrowMarker.setAttribute("markerWidth", "8");
        arrowMarker.setAttribute("markerHeight", "8");
        arrowMarker.setAttribute("markerUnits", "userSpaceOnUse");
        arrowMarker.setAttribute("orient", "auto");
        const arrowPath = document.createElementNS(SVG_NS, "path");
        arrowPath.setAttribute("d", "M 0 0 L 10 5 L 0 10 z");
        arrowPath.setAttribute("fill", "var(--mud-palette-text-secondary)");
        arrowMarker.appendChild(arrowPath);
        definitions.appendChild(arrowMarker);
        svg.appendChild(definitions);

        const edgeLayer = document.createElementNS(SVG_NS, "g");
        edgeLayer.setAttribute("aria-hidden", "true");
        edges.forEach(edge => {
            const source = positions.get(edge.source);
            const target = positions.get(edge.target);
            if (!source || !target) return;
            const sourceNode = nodeById.get(edge.source);
            const targetNode = nodeById.get(edge.target);
            const endpoints = calculateDependencyEdgeEndpoints(
                source,
                target,
                sourceNode?.selected ? 31 : 23,
                targetNode?.selected ? 31 : 23);
            if (!endpoints) return;

            const line = document.createElementNS(SVG_NS, "line");
            line.setAttribute("x1", endpoints.x1);
            line.setAttribute("y1", endpoints.y1);
            line.setAttribute("x2", endpoints.x2);
            line.setAttribute("y2", endpoints.y2);
            line.setAttribute("data-source", edge.source);
            line.setAttribute("data-target", edge.target);
            line.setAttribute("stroke", "var(--mud-palette-text-secondary)");
            line.setAttribute("stroke-width", "1.75");
            line.setAttribute("marker-end", `url(#${arrowMarkerId})`);
            edgeLayer.appendChild(line);
        });
        svg.appendChild(edgeLayer);

        const nodeLayer = document.createElementNS(SVG_NS, "g");
        nodeLayer.setAttribute("aria-hidden", "true");
        nodes.forEach(node => {
            const position = positions.get(node.id);
            if (!position) return;
            const group = document.createElementNS(SVG_NS, "g");
            const title = document.createElementNS(SVG_NS, "title");
            title.textContent = node.label;
            group.appendChild(title);
            const circle = document.createElementNS(SVG_NS, "circle");
            circle.setAttribute("cx", position.x);
            circle.setAttribute("cy", position.y);
            circle.setAttribute("r", node.selected ? "31" : "23");
            circle.setAttribute("fill", node.failed
                ? "var(--mud-palette-error)"
                : node.selected
                    ? "var(--mud-palette-primary)"
                    : "var(--mud-palette-surface)");
            circle.setAttribute("stroke", node.selected
                ? "var(--mud-palette-primary-lighten)"
                : "var(--mud-palette-lines-inputs)");
            circle.setAttribute("stroke-width", node.selected ? "4" : "2");
            group.appendChild(circle);

            const label = document.createElementNS(SVG_NS, "text");
            label.setAttribute("x", position.x);
            label.setAttribute("y", position.y + (node.selected ? 48 : 39));
            label.setAttribute("text-anchor", "middle");
            label.setAttribute("fill", "var(--mud-palette-text-primary)");
            label.setAttribute("font-size", node.selected ? "14" : "12");
            const maxLabelLength = layout.width < 600 ? 18 : 27;
            label.textContent = node.label.length > maxLabelLength
                ? `${node.label.slice(0, maxLabelLength - 1)}…`
                : node.label;
            group.appendChild(label);
            nodeLayer.appendChild(group);
        });
        svg.appendChild(nodeLayer);
    };

    const resizeObserver = new ResizeObserver(scheduleRender);
    resizeObserver.observe(element);
    const removalObserver = new MutationObserver(() => {
        if (!element.isConnected) cleanup();
    });
    removalObserver.observe(document.body, { childList: true, subtree: true });

    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        cancelAnimationFrame(animationFrame);
        resizeObserver.disconnect();
        removalObserver.disconnect();
        svg.replaceChildren();
    };

    scheduleRender();

    return {
        update(nextModel) {
            if (disposed) return;
            model = nextModel;
            scheduleRender();
        },
        focus(moduleId) {
            if (disposed || !model?.nodes) return;
            model = {
                ...model,
                selectedId: moduleId,
                nodes: model.nodes.map(node => ({ ...node, selected: node.id === moduleId }))
            };
            scheduleRender();
        },
        dispose: cleanup
    };
}
