/**
 * Module dependency graph visualization.
 *
 * This graph renders Monica module dependencies with D3.js and exposes
 * interaction hooks back to the Blazor component.
 */

import { GraphBase, getModernLinkStyle } from './d3js/d3-graph-base.js';
import { ForceLayoutManager } from './d3js/d3-force-layout.js';
import { NodeInteractionHandler } from './d3js/d3-node-interaction.js';
import { createLayoutAlgorithms } from './d3js/d3-layout-algorithms.js';

const ALL_TYPE_FILTERS = Object.freeze(['built-in', 'ui', 'third-party', 'disabled', 'cycle']);

const DEFAULT_FILTERS = Object.freeze({
    edgeFilter: 'all',
    typeFilters: ALL_TYPE_FILTERS,
    searchText: '',
    relatedNodeId: null
});

class ModuleDependencyGraph {
    constructor(containerId, dotNetRef = null) {
        this.containerId = containerId;
        this.container = document.getElementById(containerId);
        this.dotNetRef = dotNetRef;
        this.texts = this.getDefaultTexts();
        this.currentLayout = 'force';
        this.currentFilters = { ...DEFAULT_FILTERS };

        if (!this.container) {
            throw new Error(`Container with ID '${containerId}' not found`);
        }

        this.graphBase = new GraphBase(containerId, {
            showArrows: true,
            onBackgroundClick: () => this.handleBackgroundClick()
        });

        this.forceManager = new ForceLayoutManager(
            this.graphBase.width,
            this.graphBase.height,
            {
                linkDistance: 140,
                chargeStrength: -420,
                keepFixed: false
            }
        );

        this.layoutAlgorithms = createLayoutAlgorithms(
            this.graphBase.width,
            this.graphBase.height
        );

        this.interactionHandler = new NodeInteractionHandler({
            onClick: (event, node) => this.handleNodeClick(node),
            onRightClick: (event, node, position) => this.handleNodeRightClick(node, position),
            onHover: (event, node) => this.showNodeInfo(node),
            onHoverOut: () => this.hideNodeInfo(),
            highlightOptions: {
                fadeOpacity: 0.22,
                normalOpacity: 1
            },
            markerIds: this.graphBase.markerIds
        });
    }

    getDefaultTexts() {
        return {
            labels: {
                directDependencies: 'Direct Dependencies',
                transitiveDependencies: 'Transitive Dependencies',
                dependedBy: 'Depended By',
                circularDependency: 'Circular Dependency',
                moduleStatus: 'Status',
                moduleCategory: 'Module Category'
            },
            states: {
                yes: 'Yes',
                no: 'No'
            },
            messages: {
                hoverToViewDetails: 'Hover over a node to view details'
            }
        };
    }

    initialize(nodes, edges, texts) {
        this.nodes = nodes.map(node => ({ ...node }));
        this.edges = edges.map(edge => ({ ...edge }));
        this.texts = texts || this.getDefaultTexts();

        this.stopNodeStateAnimations();
        this.graphBase.mainGroup.selectAll('*').remove();

        const linkGroup = this.graphBase.mainGroup.append('g').attr('class', 'links');
        const nodeGroup = this.graphBase.mainGroup.append('g').attr('class', 'nodes');

        const linkStyle = getModernLinkStyle(false, false, this.graphBase.markerIds);
        this.linkSelection = linkGroup.selectAll('path')
            .data(this.edges)
            .enter()
            .append('path')
            .attr('class', 'link')
            .attr('fill', 'none')
            .attr('data-base-stroke', linkStyle.stroke)
            .attr('data-base-stroke-width', link => (link.isPartOfCycle ? 3 : 2).toString())
            .attr('data-base-stroke-dasharray', link => link.dependencyType === 'Transitive' ? '5,5' : '')
            .attr('data-base-marker-end', linkStyle.markerEnd)
            .attr('data-base-opacity', String(linkStyle.strokeOpacity))
            .attr('data-base-filter', '')
            .attr('stroke', function() { return this.getAttribute('data-base-stroke'); })
            .attr('stroke-width', function() { return this.getAttribute('data-base-stroke-width'); })
            .attr('stroke-dasharray', function() {
                const value = this.getAttribute('data-base-stroke-dasharray');
                return value || null;
            })
            .attr('stroke-linecap', linkStyle.strokeLinecap)
            .attr('stroke-linejoin', linkStyle.strokeLinejoin)
            .attr('marker-end', function() { return this.getAttribute('data-base-marker-end'); })
            .style('opacity', function() { return this.getAttribute('data-base-opacity'); })
            .style('filter', null);

        this.nodeSelection = nodeGroup.selectAll('g.node-item')
            .data(this.nodes)
            .enter()
            .append('g')
            .attr('class', 'node-item')
            .style('cursor', 'move');

        this.nodeSelection.append('circle')
            .attr('class', 'node-glow')
            .attr('r', 31)
            .attr('fill', node => this.getNodeGlowColor(node) || 'transparent')
            .attr('data-base-opacity', node => this.hasPulseState(node) ? this.getPulseConfig(node).minOpacity : 0)
            .style('opacity', node => this.hasPulseState(node) ? this.getPulseConfig(node).minOpacity : 0)
            .style('pointer-events', 'none');

        this.nodeSelection.append('circle')
            .attr('class', 'node-core')
            .attr('r', 25)
            .attr('fill', node => this.getNodeFillColor(node))
            .attr('stroke', node => this.getNodeStrokeColor(node))
            .attr('stroke-width', 2);

        this.nodeSelection.append('text')
            .attr('class', 'node-label')
            .text(node => node.label)
            .attr('font-size', 12)
            .attr('text-anchor', 'middle')
            .attr('dy', 40)
            .style('fill', 'var(--mud-palette-text-primary)')
            .style('font-weight', 'bold')
            .style('pointer-events', 'none')
            .style('user-select', 'none');

        this.glowSelection = this.nodeSelection.select('.node-glow');
        this.coreNodeSelection = this.nodeSelection.select('.node-core');

        this.interactionHandler.bindNodeEvents(this.nodeSelection, {
            nodes: this.nodes,
            links: this.edges,
            linkSelection: this.linkSelection
        });

        this.startNodeStateAnimations();
        this.applyLayout(this.currentLayout);
        this.applyFilter(this.currentFilters);
    }

    getNodeFillColor(node) {
        if (node.isDisabled) {
            return 'var(--mud-palette-text-disabled)';
        }

        switch (node.categoryKey) {
            case 'third-party':
                return 'var(--mud-palette-success)';
            case 'ui':
                return 'var(--mud-palette-info)';
            default:
                return 'var(--mud-palette-primary)';
        }
    }

    getNodeStrokeColor(node) {
        if (node.isPartOfCycle) {
            return 'var(--mud-palette-error)';
        }

        if (node.isDisabled) {
            return 'var(--mud-palette-text-disabled)';
        }

        return 'var(--mud-palette-divider)';
    }

    getNodeGlowColor(node) {
        if (node.isPartOfCycle) {
            return 'var(--mud-palette-error)';
        }

        if (node.isDisabled) {
            return 'var(--mud-palette-text-disabled)';
        }

        return null;
    }

    getEdgeColorByType(type) {
        switch (type) {
            case 'Direct':
                return 'var(--mud-palette-success)';
            case 'Transitive':
                return 'var(--mud-palette-secondary)';
            case 'Circular':
                return 'var(--mud-palette-error)';
            default:
                return 'var(--mud-palette-text-disabled)';
        }
    }

    hasPulseState(node) {
        return node.isPartOfCycle || node.isDisabled;
    }

    getPulseConfig(node) {
        return node.isPartOfCycle
            ? { duration: 820, minRadius: 30, maxRadius: 40, minOpacity: 0.18, maxOpacity: 0.48 }
            : { duration: 1200, minRadius: 29, maxRadius: 37, minOpacity: 0.12, maxOpacity: 0.28 };
    }

    applyLayout(layoutType) {
        this.currentLayout = layoutType;
        this.forceManager.stop();

        switch (layoutType) {
            case 'hierarchical':
                this.applyHierarchicalLayout();
                break;
            case 'circular':
                this.applyCircularLayout();
                break;
            case 'tree':
                this.applyTreeLayout();
                break;
            case 'force':
            default:
                this.applyForceLayout();
                break;
        }
    }

    applyForceLayout() {
        this.forceManager.releaseAllFixed(this.nodes);
        this.forceManager.setData(this.nodes, this.edges);
        this.nodeSelection.call(this.forceManager.getDragBehavior());

        this.forceManager.start(() => {
            this.updateNodeTransforms();
            this.updateLinks();
        });
    }

    applyHierarchicalLayout() {
        this.layoutAlgorithms.hierarchicalLayout(this.nodes, this.edges);
        this.pinCurrentNodePositions();
        this.applyStaticLayout();
    }

    applyCircularLayout() {
        this.layoutAlgorithms.circularLayout(this.nodes);
        this.pinCurrentNodePositions();
        this.applyStaticLayout();
    }

    applyTreeLayout() {
        this.layoutAlgorithms.treeLayout(this.nodes, this.edges, { orientation: 'vertical' });
        this.pinCurrentNodePositions();
        this.applyStaticLayout();
    }

    pinCurrentNodePositions() {
        this.nodes.forEach(node => {
            node.fx = node.x;
            node.fy = node.y;
        });
    }

    applyStaticLayout() {
        const dragBehavior = d3.drag()
            .on('start', (event) => {
                d3.select(event.sourceEvent.target.closest('.node-item') || event.sourceEvent.target)
                    .style('cursor', 'grabbing');
            })
            .on('drag', (event, node) => {
                node.x = event.x;
                node.y = event.y;
                node.fx = event.x;
                node.fy = event.y;
                this.updateNodeTransforms();
                this.updateLinks();
            })
            .on('end', (event) => {
                d3.select(event.sourceEvent.target.closest('.node-item') || event.sourceEvent.target)
                    .style('cursor', 'move');
            });

        this.nodeSelection.call(dragBehavior);
        this.updatePositions();
    }

    updatePositions() {
        this.nodeSelection
            .transition()
            .duration(750)
            .attr('transform', node => `translate(${node.x},${node.y})`);

        this.linkSelection
            .transition()
            .duration(750)
            .attr('d', link => this.buildLinkPath(link));
    }

    updateNodeTransforms() {
        this.nodeSelection.attr('transform', node => `translate(${node.x},${node.y})`);
    }

    updateLinks() {
        this.linkSelection.attr('d', link => this.buildLinkPath(link));
    }

    buildLinkPath(link) {
        const source = this.findNodeByEdgeRef(link.source);
        const target = this.findNodeByEdgeRef(link.target);

        if (!source || !target) {
            return '';
        }

        const dx = target.x - source.x;
        const dy = target.y - source.y;
        const distance = Math.sqrt(dx * dx + dy * dy);

        if (distance === 0) {
            return '';
        }

        const normX = dx / distance;
        const normY = dy / distance;
        const arrowOffset = 37;
        const endX = target.x - normX * arrowOffset;
        const endY = target.y - normY * arrowOffset;
        return `M${source.x},${source.y} L${endX},${endY}`;
    }

    findNodeByEdgeRef(edgeNode) {
        const nodeId = edgeNode?.id || edgeNode;
        return this.nodes.find(node => node.id === nodeId);
    }

    applyFilter(filterConfig) {
        const rawFilters = typeof filterConfig === 'string'
            ? { ...this.currentFilters, edgeFilter: filterConfig }
            : { ...DEFAULT_FILTERS, ...this.currentFilters, ...(filterConfig || {}) };
        const filters = {
            ...rawFilters,
            typeFilters: this.normalizeTypeFilters(rawFilters.typeFilters ?? rawFilters.typeFilter)
        };

        this.currentFilters = filters;

        const baseVisibleNodeIds = this.getBaseVisibleNodeIds(filters);
        const visibleEdges = this.getVisibleEdges(filters, baseVisibleNodeIds);
        const finalVisibleNodeIds = filters.edgeFilter === 'all'
            ? baseVisibleNodeIds
            : this.collectNodeIdsFromEdges(visibleEdges);
        const visibleEdgeSet = new Set(visibleEdges);

        this.linkSelection.style('display', edge => visibleEdgeSet.has(edge) ? null : 'none');
        this.nodeSelection.style('display', node => finalVisibleNodeIds.has(node.id) ? null : 'none');

        this.hideNodeInfo();
    }

    getBaseVisibleNodeIds(filters) {
        const normalizedSearch = (filters.searchText || '').trim().toLowerCase();
        let visibleNodes = this.nodes;

        if (filters.relatedNodeId) {
            const connectedNodeIds = this.getConnectedNodeIds(filters.relatedNodeId);
            visibleNodes = visibleNodes.filter(node => connectedNodeIds.has(node.id));
        }

        if (Array.isArray(filters.typeFilters)) {
            if (filters.typeFilters.length === 0) {
                visibleNodes = [];
            } else if (filters.typeFilters.length < ALL_TYPE_FILTERS.length) {
                visibleNodes = visibleNodes.filter(node => this.matchesTypeFilters(node, filters.typeFilters));
            }
        }

        if (normalizedSearch) {
            visibleNodes = visibleNodes.filter(node => this.matchesSearch(node, normalizedSearch));
        }

        return new Set(visibleNodes.map(node => node.id));
    }

    getVisibleEdges(filters, visibleNodeIds) {
        const edgesInVisibleNodes = this.edges.filter(edge => {
            const sourceId = edge.source.id || edge.source;
            const targetId = edge.target.id || edge.target;
            return visibleNodeIds.has(sourceId) && visibleNodeIds.has(targetId);
        });

        switch (filters.edgeFilter) {
            case 'direct':
                return edgesInVisibleNodes.filter(edge => edge.dependencyType === 'Direct');
            case 'cycle':
                return edgesInVisibleNodes.filter(edge => edge.isPartOfCycle);
            case 'all':
            default:
                return edgesInVisibleNodes;
        }
    }

    collectNodeIdsFromEdges(edges) {
        const nodeIds = new Set();
        edges.forEach(edge => {
            nodeIds.add(edge.source.id || edge.source);
            nodeIds.add(edge.target.id || edge.target);
        });
        return nodeIds;
    }

    getConnectedNodeIds(nodeId) {
        const connected = new Set([nodeId]);
        const queue = [nodeId];

        while (queue.length > 0) {
            const currentId = queue.shift();

            this.edges.forEach(edge => {
                const sourceId = edge.source.id || edge.source;
                const targetId = edge.target.id || edge.target;

                if (sourceId === currentId && !connected.has(targetId)) {
                    connected.add(targetId);
                    queue.push(targetId);
                }

                if (targetId === currentId && !connected.has(sourceId)) {
                    connected.add(sourceId);
                    queue.push(sourceId);
                }
            });
        }

        return connected;
    }

    normalizeTypeFilters(typeFilters) {
        if (Array.isArray(typeFilters)) {
            return [...new Set(typeFilters.filter(type => ALL_TYPE_FILTERS.includes(type)))];
        }

        if (typeof typeFilters === 'string') {
            if (typeFilters === 'all') {
                return [...ALL_TYPE_FILTERS];
            }

            return ALL_TYPE_FILTERS.includes(typeFilters) ? [typeFilters] : [...ALL_TYPE_FILTERS];
        }

        return [...ALL_TYPE_FILTERS];
    }

    matchesTypeFilters(node, typeFilters) {
        return typeFilters.some(typeFilter => this.matchesTypeFilter(node, typeFilter));
    }

    matchesTypeFilter(node, typeFilter) {
        switch (typeFilter) {
            case 'built-in':
            case 'ui':
            case 'third-party':
                return node.categoryKey === typeFilter;
            case 'disabled':
                return !!node.isDisabled;
            case 'cycle':
                return !!node.isPartOfCycle;
            default:
                return true;
        }
    }

    matchesSearch(node, normalizedSearch) {
        return [
            node.label,
            node.id,
            node.moduleCategory,
            node.statusText
        ]
            .filter(Boolean)
            .some(value => String(value).toLowerCase().includes(normalizedSearch));
    }

    showNodeInfo(node) {
        const info = {
            module: node.label,
            directDependencies: node.directDependencyCount ?? 0,
            transitiveDependencies: node.transitiveDependencyCount ?? 0,
            dependedBy: node.dependentModuleCount ?? 0,
            isPartOfCycle: !!node.isPartOfCycle,
            statusText: node.statusText,
            moduleCategory: node.moduleCategory
        };

        this.updateNodeInfoDisplay(info);
    }

    hideNodeInfo() {
        this.updateNodeInfoDisplay(null);
    }

    updateNodeInfoDisplay(info) {
        const nodeDetailElement = document.querySelector('.node-detail-content');
        if (!nodeDetailElement) {
            return;
        }

        if (!info) {
            nodeDetailElement.innerHTML = `
                <div class="d-flex align-center justify-center" style="height: 100%; color: var(--mud-palette-text-secondary);">
                    <div class="text-center">
                        <div style="font-size: 3rem; margin-bottom: 12px;">
                            <i class="fas fa-mouse-pointer"></i>
                        </div>
                        <div>${this.texts.messages.hoverToViewDetails}</div>
                    </div>
                </div>
            `;
            return;
        }

        nodeDetailElement.innerHTML = `
            <div class="node-info">
                <h4 class="node-info__heading">${info.module}</h4>
                <div class="node-info__row">
                    <strong>${this.texts.labels.directDependencies}:</strong> ${info.directDependencies}
                </div>
                <div class="node-info__row">
                    <strong>${this.texts.labels.transitiveDependencies}:</strong> ${info.transitiveDependencies}
                </div>
                <div class="node-info__row">
                    <strong>${this.texts.labels.dependedBy}:</strong> ${info.dependedBy}
                </div>
                <div class="node-info__row">
                    <strong>${this.texts.labels.circularDependency}:</strong> ${info.isPartOfCycle ? this.texts.states.yes : this.texts.states.no}
                </div>
                <div class="node-info__row">
                    <strong>${this.texts.labels.moduleCategory}:</strong> ${info.moduleCategory}
                </div>
                <div class="node-info__row">
                    <strong>${this.texts.labels.moduleStatus}:</strong> ${info.statusText}
                </div>
            </div>
        `;
    }

    zoomIn() {
        this.graphBase.zoomIn();
    }

    zoomOut() {
        this.graphBase.zoomOut();
    }

    resetView() {
        const visibleNodes = this.getVisibleNodes();
        this.fitToNodes(visibleNodes);
    }

    focusOnNode(nodeId) {
        const node = this.nodes.find(item => item.id === nodeId);
        if (!node) {
            return;
        }

        this.graphBase.focusOnPosition({ x: node.x, y: node.y }, 1.35);

        this.nodeSelection
            .filter(item => item.id === nodeId)
            .select('.node-core')
            .transition()
            .duration(220)
            .attr('stroke-width', 5)
            .transition()
            .duration(220)
            .attr('stroke-width', 2);
    }

    fitToNodes(nodes, duration = 500) {
        if (!nodes || nodes.length === 0) {
            this.graphBase.resetView(duration);
            return;
        }

        const padding = 72;
        const minX = d3.min(nodes, node => node.x);
        const maxX = d3.max(nodes, node => node.x);
        const minY = d3.min(nodes, node => node.y);
        const maxY = d3.max(nodes, node => node.y);

        const boundsWidth = Math.max(1, maxX - minX);
        const boundsHeight = Math.max(1, maxY - minY);
        const centerX = minX + boundsWidth / 2;
        const centerY = minY + boundsHeight / 2;

        const availableWidth = Math.max(1, this.graphBase.width - padding * 2);
        const availableHeight = Math.max(1, this.graphBase.height - padding * 2);
        const scale = Math.max(0.2, Math.min(2.2, Math.min(
            availableWidth / boundsWidth,
            availableHeight / boundsHeight
        )));

        const transform = d3.zoomIdentity
            .translate(this.graphBase.width / 2, this.graphBase.height / 2)
            .scale(scale)
            .translate(-centerX, -centerY);

        this.graphBase.svg
            .transition()
            .duration(duration)
            .call(this.graphBase.zoom.transform, transform);
    }

    getVisibleNodes() {
        const visibleNodeIds = new Set();
        this.nodeSelection.each(function(node) {
            if (d3.select(this).style('display') !== 'none') {
                visibleNodeIds.add(node.id);
            }
        });

        return this.nodes.filter(node => visibleNodeIds.has(node.id));
    }

    async exportGraph(filename) {
        const svgElement = this.graphBase.svg.node();
        if (!svgElement) {
            return;
        }

        const clonedSvg = this.createExportSvg(svgElement);
        const svgData = new XMLSerializer().serializeToString(clonedSvg);
        const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
        const svgUrl = URL.createObjectURL(svgBlob);
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d');
        const image = new Image();
        const exportName = filename || 'dependency-graph.png';
        const backgroundColor = getComputedStyle(this.container).backgroundColor || '#ffffff';

        image.onload = () => {
            const width = clonedSvg.width.baseVal.value || this.graphBase.width;
            const height = clonedSvg.height.baseVal.value || this.graphBase.height;
            canvas.width = width * 2;
            canvas.height = height * 2;
            context.scale(2, 2);
            context.fillStyle = backgroundColor;
            context.fillRect(0, 0, width, height);
            context.drawImage(image, 0, 0, width, height);

            const link = document.createElement('a');
            link.download = exportName;
            link.href = canvas.toDataURL('image/png');
            link.click();

            URL.revokeObjectURL(svgUrl);
        };

        image.onerror = () => {
            const link = document.createElement('a');
            link.download = exportName.endsWith('.svg') ? exportName : exportName.replace(/\.png$/i, '') + '.svg';
            link.href = svgUrl;
            link.click();
            URL.revokeObjectURL(svgUrl);
        };

        image.src = svgUrl;
    }

    createExportSvg(svgElement) {
        const clone = svgElement.cloneNode(true);
        const originalElements = [svgElement, ...svgElement.querySelectorAll('*')];
        const clonedElements = [clone, ...clone.querySelectorAll('*')];
        const width = svgElement.viewBox.baseVal?.width || svgElement.clientWidth || this.graphBase.width;
        const height = svgElement.viewBox.baseVal?.height || svgElement.clientHeight || this.graphBase.height;

        clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        clone.setAttribute('width', `${width}`);
        clone.setAttribute('height', `${height}`);

        for (let index = 0; index < originalElements.length; index += 1) {
            this.inlineComputedStyles(originalElements[index], clonedElements[index]);
        }

        return clone;
    }

    inlineComputedStyles(originalElement, clonedElement) {
        const computed = window.getComputedStyle(originalElement);
        const styleProperties = [
            'fill',
            'stroke',
            'stroke-width',
            'stroke-dasharray',
            'opacity',
            'filter',
            'font-size',
            'font-weight',
            'font-family',
            'text-anchor',
            'dominant-baseline',
            'letter-spacing'
        ];

        styleProperties.forEach(property => {
            const value = computed.getPropertyValue(property);
            if (value) {
                clonedElement.style.setProperty(property, value);
            }
        });
    }

    startNodeStateAnimations() {
        this.stopNodeStateAnimations();

        this.glowSelection.each((nodeData, index, elements) => {
            const glowElement = d3.select(elements[index]);

            if (!this.hasPulseState(nodeData)) {
                glowElement.interrupt().style('opacity', 0);
                return;
            }

            const pulse = this.getPulseConfig(nodeData);
            glowElement.attr('data-base-opacity', pulse.minOpacity);

            const animate = () => {
                glowElement
                    .interrupt()
                    .attr('r', pulse.minRadius)
                    .style('opacity', pulse.minOpacity)
                    .transition()
                    .duration(pulse.duration / 2)
                    .attr('r', pulse.maxRadius)
                    .style('opacity', pulse.maxOpacity)
                    .transition()
                    .duration(pulse.duration / 2)
                    .attr('r', pulse.minRadius)
                    .style('opacity', pulse.minOpacity)
                    .on('end', animate);
            };

            animate();
        });
    }

    stopNodeStateAnimations() {
        if (this.glowSelection) {
            this.glowSelection.interrupt();
        }
    }

    handleNodeClick(nodeData) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnNodeClick', nodeData.id);
        }
    }

    handleNodeRightClick(nodeData, position) {
        if (!this.dotNetRef) {
            return;
        }

        const x = position.clientX !== undefined ? position.clientX : position.pageX;
        const y = position.clientY !== undefined ? position.clientY : position.pageY;
        this.dotNetRef.invokeMethodAsync('OnNodeRightClick', nodeData.id, x, y);
    }

    handleBackgroundClick() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnSvgBackgroundClick');
        }
    }

    dispose() {
        this.stopNodeStateAnimations();

        if (this.forceManager) {
            this.forceManager.dispose();
        }

        if (this.graphBase) {
            this.graphBase.dispose();
        }
    }
}

let graphInstance = null;

export async function initializeDependencyGraph(containerId, nodes, edges, texts = null, dotNetRef = null) {
    if (graphInstance) {
        graphInstance.dispose();
    }

    graphInstance = new ModuleDependencyGraph(containerId, dotNetRef);
    graphInstance.initialize(nodes, edges, texts);
    console.log('Dependency graph initialized successfully');
}

export async function changeLayout(layout) {
    if (graphInstance) {
        graphInstance.applyLayout(layout);
    }
}

export async function applyFilter(filterConfig) {
    if (graphInstance) {
        graphInstance.applyFilter(filterConfig);
    }
}

export async function zoomIn() {
    if (graphInstance) {
        graphInstance.zoomIn();
    }
}

export async function zoomOut() {
    if (graphInstance) {
        graphInstance.zoomOut();
    }
}

export async function resetZoom() {
    if (graphInstance) {
        graphInstance.resetView();
    }
}

export async function focusOnNode(nodeId) {
    if (graphInstance) {
        graphInstance.focusOnNode(nodeId);
    }
}

export async function exportGraph(filename) {
    if (graphInstance) {
        await graphInstance.exportGraph(filename);
    }
}

export async function disposeGraph() {
    if (graphInstance) {
        graphInstance.dispose();
        graphInstance = null;
    }
}

console.log('Module dependency graph module loaded');
