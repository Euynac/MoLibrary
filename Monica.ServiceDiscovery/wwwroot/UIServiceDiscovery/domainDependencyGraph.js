/**
 * Subdomain dependency graph visualization
 * Subdomain dependency chart implemented based on d3.js
 * 
 * @module domainDependencyGraph
 */

import { GraphBase, getModernLinkStyle, getModernNodeStyle, focusOnPosition } from '../../Monica.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../Monica.UI/js/d3js/d3-force-layout.js';
import { createLayoutAlgorithms } from '../../Monica.UI/js/d3js/d3-layout-algorithms.js';
import { NodeInteractionHandler, createStaticDragBehavior } from '../../Monica.UI/js/d3js/d3-node-interaction.js';

const DOMAIN_NODE_RADIUS = 30;

const ROLE_COLOR_MAP = {
    primary: 'var(--mud-palette-primary)',
    secondary: 'var(--mud-palette-secondary)',
    tertiary: 'var(--mud-palette-tertiary)',
    info: 'var(--mud-palette-info)',
    success: 'var(--mud-palette-success)',
    warning: 'var(--mud-palette-warning)',
    default: 'var(--mud-palette-text-secondary)'
};

const STATUS_COLOR_MAP = {
    Running: 'var(--mud-palette-success)',
    Error: 'var(--mud-palette-error)',
    Updating: 'var(--mud-palette-warning)',
    Offline: 'var(--mud-palette-dark)',
    Unknown: 'var(--mud-palette-text-secondary)'
};

function resolveRoleColor(role) {
    return ROLE_COLOR_MAP[role] || ROLE_COLOR_MAP.default;
}

/**
 * Domain Dependency Graph Class
 */
class DomainDependencyGraph extends GraphBase {
    constructor(containerId, options = {}) {
        super(containerId, {
            ...options,
            showArrows: true,
            arrowSize: 10
        });

        this.nodes = [];
        this.links = [];
        this.nodeElements = null;
        this.linkElements = null;
        this.dotNetHelper = options.dotNetHelper || null;
        this.pendingCallbacks = new Set();
        this.tooltip = null;
        this.currentLayout = 'force';
        this.texts = {
            labels: {
                serviceCount: 'Service Count',
                relatedServices: 'Related Services ({0})',
                statusCount: '{0}: {1}',
                andMore: 'and {0} more',
                noRelatedServices: 'No related services',
                clickToViewDetails: 'Click to view details',
                dependencyServiceCount: 'Service Count'
            },
            statuses: {
                running: 'Running',
                updating: 'Updating',
                offline: 'Offline',
                error: 'Error',
                unknown: 'Unknown'
            }
        };

        if (options.texts) {
            this.texts = {
                labels: {
                    ...this.texts.labels,
                    ...(options.texts.labels || {})
                },
                statuses: {
                    ...this.texts.statuses,
                    ...(options.texts.statuses || {})
                }
            };
        }
        
        // Create a force-directed layout manager
        this.forceLayout = new ForceLayoutManager(this.width, this.height, {
            linkDistance: 200,
            chargeStrength: -800,
            collisionRadius: 80  // 增加碰撞半径以为下方文本留出空间
        });

        // Initialize layout algorithm manager
        this.layoutAlgorithms = createLayoutAlgorithms(this.width, this.height);
        
        // Initialize interaction handler
        this.interactionHandler = new NodeInteractionHandler({
            onClick: (event, d) => this.handleNodeClick(d),
            onRightClick: (event, d, position) => this.handleNodeRightClick(d, position.clientX, position.clientY),
            onHover: (event, d) => this.showTooltip(event, this.buildDomainTooltipContent(d)),
            onHoverOut: (event, d) => this.hideTooltip(),
            highlightOptions: {
                fadeOpacity: 0.2,
                normalOpacity: 1
            },
            isDarkMode: this.isDarkMode,
            markerIds: this.markerIds // 传递marker IDs
        });
        
        // Static layout dragging behavior
        this.staticDragBehavior = null;

        // Create layer
        this.createLayers();
        
        // Binding events
        this.bindEvents();
    }

    createLayers() {
        // Create connection line layer
        this.linkLayer = this.mainGroup.append('g')
            .attr('class', 'links-layer');
            
        // Create node layer
        this.nodeLayer = this.mainGroup.append('g')
            .attr('class', 'nodes-layer');
    }

    bindEvents() {
        // Listen for window size changes
        this.handleResize = this.handleResize.bind(this);
        window.addEventListener('resize', this.handleResize);
        
        // Add background click event handling
        this.svg.on('click', (event) => {
            // Only processed when the click is on the background (svg itself)
            if (event.target === this.svg.node()) {
                this.handleBackgroundClick();
            }
        });
    }
    
    /**
     * Handling background click events
     */
    handleBackgroundClick() {
        // If there is a .NET callback object, notify that the background was clicked
        this.invokeDotNet('OnSvgBackgroundClick');
    }

    updateGraph(data) {
        this.nodes = data.nodes || [];
        this.links = data.links || [];
        
        // Update force-directed layout data
        this.forceLayout.setData(this.nodes, this.links);
        
        // Render chart
        this.render();
        
        // Start layout animation
        this.forceLayout.start(() => {
            this.updatePositions();
        });
    }

    render() {
        this.renderLinks();
        this.renderNodes();
    }

    renderLinks() {
        // Bind data
        this.linkElements = this.linkLayer
            .selectAll('.domain-link')
            .data(this.links, d => `${d.source.id || d.source}-${d.target.id || d.target}`);

        // Remove old elements
        this.linkElements.exit().remove();

        // Create new elements - use path instead of line to support better arrow display
        const linkEnter = this.linkElements.enter()
            .append('path')
            .attr('class', 'domain-link modern-link')
            .style('opacity', 0);

        // Merge selection
        this.linkElements = linkEnter.merge(this.linkElements);

        // Set modern styles - completely refer to ProjectUnit implementation, do not use field colors
        const linkStyle = getModernLinkStyle(this.isDarkMode, false, this.markerIds);
        this.linkElements
            .transition()
            .duration(300)
            .style('opacity', 1)
            .attr('stroke', linkStyle.stroke)
            .attr('stroke-width', linkStyle.strokeWidth)
            .attr('stroke-opacity', linkStyle.strokeOpacity)
            .attr('stroke-linecap', linkStyle.strokeLinecap)
            .attr('stroke-linejoin', linkStyle.strokeLinejoin)
            .attr('fill', 'none')
            .attr('marker-end', linkStyle.markerEnd)
            .style('filter', linkStyle.filter)
            .style('pointer-events', 'stroke'); // 允许path元素响应鼠标事件

        // Add interaction - keep only tooltip displayed
        this.linkElements
            .style('cursor', 'pointer')
            .on('mouseenter', (event, d) => {
                this.showTooltip(event, `${d.source.name || d.source} → ${d.target.name || d.target}<br/>${this.texts.labels.dependencyServiceCount}: ${d.serviceCount || 1}`);
            })
            .on('mouseleave', (event, d) => {
                this.hideTooltip();
            });
    }

    renderNodes() {
        // Bind data
        this.nodeElements = this.nodeLayer
            .selectAll('.domain-node')
            .data(this.nodes, d => d.id);

        // Remove old elements
        this.nodeElements.exit().remove();

        // Create a new element group
        const nodeEnter = this.nodeElements.enter()
            .append('g')
            .attr('class', 'domain-node')
            .style('opacity', 0);

        // Add circle node
        nodeEnter.append('circle')
            .attr('class', 'node-circle')
            .attr('r', 0);

        // Add text label (move below node)
        nodeEnter.append('text')
            .attr('class', 'node-text')
            .attr('dy', '50px')
            .attr('text-anchor', 'middle')
            .style('font-size', '0px');

        // Merge selection
        this.nodeElements = nodeEnter.merge(this.nodeElements);

        // animation display
        this.nodeElements
            .transition()
            .duration(500)
            .style('opacity', 1);

        // Update circle node
        this.nodeElements.select('.node-circle')
            .transition()
            .duration(500)
            .attr('r', DOMAIN_NODE_RADIUS)
            .attr('fill', d => resolveRoleColor(d.colorRole))
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 2)
            .style('filter', 'drop-shadow(0 2px 8px rgba(var(--mud-palette-dark-rgb),0.15))');

        // Update text
        this.nodeElements.select('.node-text')
            .transition()
            .duration(500)
            .style('font-size', '14px')
            .style('font-weight', '500')
            .style('text-shadow', '0 1px 3px rgba(var(--mud-palette-dark-rgb),0.3)')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => this.truncateText(d.name, 15));

        // Add dragging behavior
        this.nodeElements.call(this.forceLayout.getDragBehavior());

        // Binding interaction events - using NodeInteractionHandler
        this.interactionHandler.bindNodeEvents(this.nodeElements, {
            nodes: this.nodes,
            links: this.links,
            linkSelection: this.linkElements
        });

        // Set mouse style
        this.nodeElements.style('cursor', 'pointer');
    }

    buildLinkPath(source, target) {
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
        const arrowOffset = DOMAIN_NODE_RADIUS + this.arrowSize;
        const endX = target.x - normX * arrowOffset;
        const endY = target.y - normY * arrowOffset;

        return `M${source.x},${source.y} L${endX},${endY}`;
    }

    findNodeByLinkRef(linkNode) {
        const nodeId = linkNode?.id || linkNode;
        return this.nodes.find(node => node.id === nodeId);
    }

    updatePositions() {
        if (this.nodeElements) {
            this.nodeElements
                .attr('transform', d => `translate(${d.x},${d.y})`);
        }

        if (this.linkElements) {
            this.linkElements
                .attr('d', d => this.buildLinkPath(d.source, d.target));
        }
    }


    showTooltip(event, content) {
        if (!this.tooltip) {
            this.tooltip = d3.select('body')
                .append('div')
                .attr('class', 'domain-tooltip')
                .style('position', 'absolute')
                .style('background', 'rgba(var(--mud-palette-surface-rgb),0.98)')
                .style('color', 'var(--mud-palette-text-primary)')
                .style('border', '1px solid var(--mud-palette-lines-default)')
                .style('padding', '8px 12px')
                .style('border-radius', '6px')
                .style('font-size', '12px')
                .style('pointer-events', 'none')
                .style('z-index', '10000')
                .style('opacity', 0);
        }

        this.tooltip
            .html(content)
            .style('left', (event.pageX + 10) + 'px')
            .style('top', (event.pageY - 10) + 'px')
            .transition()
            .duration(200)
            .style('opacity', 1);
    }

    hideTooltip() {
        if (!this.tooltip) {
            return;
        }

        this.tooltip
            .interrupt()
            .transition()
            .duration(200)
            .style('opacity', 0);
    }

    focusOnNode(nodeId) {
        const node = this.nodes.find(n => n.id === nodeId);
        if (node && node.x !== undefined && node.y !== undefined) {
            this.focusOnPosition({ x: node.x, y: node.y }, 1.5);
        }
    }

    truncateText(text, maxLength) {
        if (!text) return '';
        return text.length > maxLength ? text.substring(0, maxLength - 3) + '...' : text;
    }

    /**
     * Handle node click events
     * @param {Object} node - the clicked node data
     */
    handleNodeClick(node) {
        // Focus on the node first
        this.focusOnNode(node.id);
        
        // If there is a .NET callback object, call the domain details display method
        this.invokeDotNet('OnDomainClickFromJS', node.id || node.name);
    }
    
    /**
     * Handle node right-click events
     * @param {Object} node - the node data that was right-clicked
     * @param {number} x - Client X coordinate
     * @param {number} y - Client Y coordinate
     */
    handleNodeRightClick(node, x, y) {
        // Hide tooltip
        this.hideTooltip();
        
        // If there is a .NET callback object, call the right-click menu display method
        this.invokeDotNet('OnDomainRightClick', node.id || node.name, x, y);
    }

    /**
     * Construct the tooltip content of the domain node
     * @param {Object} domain - Domain node data
     * @returns {string} tooltip content in HTML format
     */
    buildDomainTooltipContent(domain) {
        let content = `<strong>${domain.name}</strong>`;
        
        if (domain.description) {
            content += `<br/><span style="color: var(--mud-palette-text-secondary);">${domain.description}</span>`;
        }

        // Display related microservice information
        if (domain.services && domain.services.length > 0) {
            content += `<br/><br/><strong>${this.formatText(this.texts.labels.relatedServices, domain.services.length)}:</strong>`;
            
            // Show services grouped by status
            const servicesByStatus = this.groupServicesByStatus(domain.services);
            
            Object.entries(servicesByStatus).forEach(([status, services]) => {
                const statusText = this.getServiceStatusText(status);
                const statusColor = this.getServiceStatusColor(status);
                content += `<br/><span style="color: ${statusColor};">• ${this.formatText(this.texts.labels.statusCount, statusText, services.length)}</span>`;
                
                // Show first 3 service names
                if (services.length > 0) {
                    const serviceNames = services.slice(0, 3).map(s => s.name || s.appName).join(', ');
                    content += `<br/><span style="font-size: 11px; color: var(--mud-palette-text-secondary); margin-left: 12px;">${serviceNames}`;
                    if (services.length > 3) {
                        content += ` ${this.formatText(this.texts.labels.andMore, services.length)}`;
                    }
                    content += `</span>`;
                }
            });
        } else {
            content += `<br/><span style="color: var(--mud-palette-text-secondary);">${this.texts.labels.noRelatedServices}</span>`;
        }

        content += `<br/><br/><span style="font-size: 11px; color: var(--mud-palette-text-secondary);">${this.texts.labels.clickToViewDetails}</span>`;
        return content;
    }

    /**
     * Group services by status
     * @param {Array} services - list of services
     * @returns {Object} Services grouped by status
     */
    groupServicesByStatus(services) {
        const groups = {};
        services.forEach(service => {
            const status = service.status || service.overallStatus || 'Unknown';
            if (!groups[status]) {
                groups[status] = [];
            }
            groups[status].push(service);
        });
        return groups;
    }

    /**
     * Get service status display text
     * @param {string} status - service status
     * @returns {string} Status display text
     */
    getServiceStatusText(status) {
        switch (status) {
            case 'Running': return this.texts.statuses.running;
            case 'Error': return this.texts.statuses.error;
            case 'Updating': return this.texts.statuses.updating;
            case 'Offline': return this.texts.statuses.offline;
            default: return this.texts.statuses.unknown;
        }
    }

    formatText(template, ...values) {
        return values.reduce((current, value, index) => current.replace(`{${index}}`, value), template);
    }

    /**
     * Get service status color
     * @param {string} status - service status
     * @returns {string} status color
     */
    getServiceStatusColor(status) {
        return STATUS_COLOR_MAP[status] || STATUS_COLOR_MAP.Unknown;
    }

    handleResize() {
        const newWidth = this.container.clientWidth;
        const newHeight = this.container.clientHeight;
        
        if (newWidth !== this.width || newHeight !== this.height) {
            this.width = newWidth;
            this.height = newHeight;
            
            this.svg
                .attr('width', this.width)
                .attr('height', this.height)
                .attr('viewBox', [0, 0, this.width, this.height]);
            
            // Update Force Directed Layout Center
            if (this.forceLayout) {
                this.forceLayout.simulation
                    .force('center', d3.forceCenter(this.width / 2, this.height / 2));
                this.forceLayout.simulation.alpha(0.3).restart();
            }
        }
    }

    /**
     * Set layout
     */
    setLayout(layoutType) {
        this.currentLayout = layoutType;
        
        // Remove previous dragging behavior
        if (this.nodeElements) {
            this.nodeElements.on('.drag', null);
        }
        
        switch (layoutType) {
            case 'force':
                this.applyForceLayout();
                break;
            case 'hierarchy':
                this.applyHierarchyLayout();
                break;
            case 'circular':
                this.applyCircularLayout();
                break;
            case 'radial_tree':
                this.applyRadialTreeLayout();
                break;
            default:
                this.applyForceLayout();
        }
    }
    
    /**
     * Apply force-directed layout
     */
    applyForceLayout() {
        // Release all pinned nodes
        this.forceLayout.releaseAllFixed(this.nodes);
        
        // Set data
        this.forceLayout.setData(this.nodes, this.links);
        
        // Apply drag behavior
        if (this.nodeElements) {
            this.nodeElements.call(this.forceLayout.getDragBehavior());
            
            // Rebind interaction events - use NodeInteractionHandler
            this.interactionHandler.bindNodeEvents(this.nodeElements, {
                nodes: this.nodes,
                links: this.links,
                linkSelection: this.linkElements
            });
            
            // Set mouse style
            this.nodeElements.style('cursor', 'pointer');
        }
        
        // Start simulation
        this.forceLayout.start(() => {
            this.updatePositions();
        });
    }
    
    /**
     * Apply hierarchical layout
     */
    applyHierarchyLayout() {
        this.forceLayout.stop();
        
        // Compute hierarchical layout
        this.layoutAlgorithms.hierarchicalLayout(this.nodes, this.links);
        
        // Apply static drag
        this.applyStaticDrag();
        
        // Update location
        this.updateStaticPositions();
    }
    
    /**
     * Apply ring layout
     */
    applyCircularLayout() {
        this.forceLayout.stop();
        
        // Calculate ring layout
        this.layoutAlgorithms.circularLayout(this.nodes, {
            avgNodeSize: 60,
            complexNodeCount: 0
        });
        
        // Apply static drag
        this.applyStaticDrag();
        
        // Update location
        this.updateStaticPositions();
    }
    
    /**
     * Apply radial tree layout
     */
    applyRadialTreeLayout() {
        this.forceLayout.stop();
        
        // Compute radial tree layout
        this.layoutAlgorithms.radialTreeLayout(this.nodes, this.links, {
            radiusStep: 80
        });
        
        // Apply static drag
        this.applyStaticDrag();
        
        // Update location
        this.updateStaticPositions();
    }
    
    /**
     * Apply static drag
     */
    applyStaticDrag() {
        const self = this;
        
        this.staticDragBehavior = createStaticDragBehavior({
            updateLinks: (draggedNode) => {
                // Live update of connecting lines - use path's d attribute instead of x1,y1,x2,y2
                if (self.linkElements) {
                    self.linkElements
                        .attr('d', d => {
                            const sourceId = d.source.id || d.source;
                            const targetId = d.target.id || d.target;
                            const source = sourceId === draggedNode.id
                                ? draggedNode
                                : self.findNodeByLinkRef(sourceId);
                            const target = targetId === draggedNode.id
                                ? draggedNode
                                : self.findNodeByLinkRef(targetId);

                            return self.buildLinkPath(source, target);
                        });
                }
            }
        });
        
        if (this.nodeElements) {
            this.nodeElements.call(this.staticDragBehavior);
            
            // Rebind interaction events - use NodeInteractionHandler
            this.interactionHandler.bindNodeEvents(this.nodeElements, {
                nodes: this.nodes,
                links: this.links,
                linkSelection: this.linkElements
            });
            
            // Set mouse style
            this.nodeElements.style('cursor', 'pointer');
        }
    }
    
    /**
     * Update static location
     */
    updateStaticPositions() {
        if (this.nodeElements) {
            this.nodeElements
                .transition()
                .duration(750)
                .attr('transform', d => `translate(${d.x},${d.y})`);
        }
        
        if (this.linkElements) {
            this.linkElements
                .transition()
                .duration(750)
                .attr('d', d => {
                    const source = this.findNodeByLinkRef(d.source);
                    const target = this.findNodeByLinkRef(d.target);

                    return this.buildLinkPath(source, target);
                });
        }
    }
    
    /**
     * Set force guide distance
     */
    setForceDistance(distance) {
        this.forceLayout.updateLinkDistance(distance);
    }
    
    /**
     * Set force guide strength
     */
    setForceStrength(strength) {
        this.forceLayout.updateChargeStrength(strength);
    }

    invokeDotNet(methodName, ...args) {
        const reference = this.dotNetHelper;
        if (!reference || typeof reference.invokeMethodAsync !== 'function') {
            return Promise.resolve();
        }

        const callback = reference.invokeMethodAsync(methodName, ...args)
            .catch(error => {
                if (this.dotNetHelper) {
                    console.error(`Domain graph callback '${methodName}' failed.`, error);
                }
            })
            .finally(() => this.pendingCallbacks.delete(callback));

        this.pendingCallbacks.add(callback);
        return callback;
    }

    async dispose() {
        this.dotNetHelper = null;

        // Remove only the tooltip owned by this graph mount.
        this.tooltip?.remove();
        this.tooltip = null;
        
        // Stop force oriented layout
        if (this.forceLayout) {
            this.forceLayout.dispose();
        }
        
        // Remove window event listener
        window.removeEventListener('resize', this.handleResize);
        
        // Call dispose of the base class
        super.dispose();

        await Promise.allSettled([...this.pendingCallbacks]);
        this.pendingCallbacks.clear();
    }
}

/**
 * Creates an independently owned graph controller for one component mount.
 * The caller must invoke dispose() on the returned controller.
 */
export function createGraph(containerId, isDarkMode = false, dotNetHelper = null, texts = {}) {
    return new DomainDependencyGraph(containerId, { isDarkMode, dotNetHelper, texts });
}
