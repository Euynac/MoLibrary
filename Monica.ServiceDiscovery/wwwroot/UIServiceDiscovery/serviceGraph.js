/**
 * Microservice architecture diagram visualization
 * Microservice architecture chart implemented based on d3.js, supporting status alarm flashing effect
 * 
 * @module serviceGraph
 */

import { GraphBase, getModernLinkStyle, getModernNodeStyle, focusOnPosition } from '../../Monica.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../Monica.UI/js/d3js/d3-force-layout.js';
import { createLayoutAlgorithms } from '../../Monica.UI/js/d3js/d3-layout-algorithms.js';
import { createStaticDragBehavior } from '../../Monica.UI/js/d3js/d3-node-interaction.js';

const ROLE_COLOR_MAP = {
    primary: 'var(--mud-palette-primary)',
    secondary: 'var(--mud-palette-secondary)',
    tertiary: 'var(--mud-palette-tertiary)',
    info: 'var(--mud-palette-info)',
    success: 'var(--mud-palette-success)',
    warning: 'var(--mud-palette-warning)',
    error: 'var(--mud-palette-error)',
    dark: 'var(--mud-palette-dark)',
    default: 'var(--mud-palette-text-secondary)'
};

const STATUS_COLOR_MAP = {
    Running: 'var(--mud-palette-success)',
    Updating: 'var(--mud-palette-warning)',
    Offline: 'var(--mud-palette-dark)',
    Error: 'var(--mud-palette-error)',
    Unknown: 'var(--mud-palette-text-secondary)'
};

function resolveRoleColor(role) {
    return ROLE_COLOR_MAP[role] || ROLE_COLOR_MAP.default;
}

/**
 * Microservice architecture diagram class
 */
class ServiceGraph extends GraphBase {
    constructor(containerId, options = {}) {
        super(containerId, {
            ...options,
            showArrows: false // 微服务图不需要箭头，因为暂时没有依赖关系
        });

        this.nodes = [];
        this.links = [];
        this.nodeElements = null;
        this.linkElements = null;
        this.dotNetRef = options.dotNetRef;
        this.pendingCallbacks = new Set();
        this.tooltip = null;
        this.animations = new Map(); // 存储动画定时器
        this.currentLayout = 'force';
        this.texts = {
            labels: {
                appId: 'AppId',
                domain: 'Domain',
                project: 'Project',
                status: 'Status',
                instances: 'Instances',
                version: 'Version',
                notAvailable: 'N/A',
                unknown: 'Unknown'
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
            linkDistance: 150,
            chargeStrength: -500,
            collisionRadius: 50
        });

        // Initialize layout algorithm manager
        this.layoutAlgorithms = createLayoutAlgorithms(this.width, this.height);
        
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
    
    /**
     * Handle node right-click events
     * @param {Object} node - the node data that was right-clicked
     * @param {Event} event - the original event object
     */
    handleNodeRightClick(node, event) {
        // Hide tooltip
        this.hideTooltip();
        
        // If there is a .NET callback object, call the right-click menu display method
        if (this.dotNetRef && typeof this.dotNetRef.invokeMethodAsync === 'function') {
            // Get coordinates relative to the viewport
            const x = event.clientX;
            const y = event.clientY;
            
            this.invokeDotNet('OnNodeRightClick', node.id, x, y);
        }
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
            .selectAll('.service-link')
            .data(this.links, d => `${d.source.id || d.source}-${d.target.id || d.target}`);

        // Remove old elements
        this.linkElements.exit().remove();

        // Create new element
        const linkEnter = this.linkElements.enter()
            .append('line')
            .attr('class', 'service-link')
            .style('opacity', 0);

        // Merge selection
        this.linkElements = linkEnter.merge(this.linkElements);

        // Set style
        const linkStyle = getModernLinkStyle(this.isDarkMode);
        this.linkElements
            .transition()
            .duration(300)
            .style('opacity', 1)
            .attr('stroke', linkStyle.stroke)
            .attr('stroke-width', linkStyle.strokeWidth)
            .attr('stroke-opacity', linkStyle.strokeOpacity)
            .style('filter', linkStyle.filter);
    }

    renderNodes() {
        // Bind data
        this.nodeElements = this.nodeLayer
            .selectAll('.service-node')
            .data(this.nodes, d => d.id);

        // Remove old elements
        const exitSelection = this.nodeElements.exit();
        exitSelection.each(d => this.stopAnimation(d.id));
        exitSelection.remove();

        // Create a new element group
        const nodeEnter = this.nodeElements.enter()
            .append('g')
            .attr('class', 'service-node')
            .style('opacity', 0);

        // Add outer ring (for sparkle effect)
        nodeEnter.append('circle')
            .attr('class', 'node-ring')
            .attr('r', 0)
            .attr('fill', 'none')
            .attr('stroke-width', 3)
            .style('opacity', 0);

        // Add main node circle
        nodeEnter.append('circle')
            .attr('class', 'node-circle')
            .attr('r', 0);

        // Add status indicator
        nodeEnter.append('circle')
            .attr('class', 'status-indicator')
            .attr('r', 6)
            .attr('cx', 20)
            .attr('cy', -20);

        // Add instance number text
        nodeEnter.append('text')
            .attr('class', 'instance-count')
            .attr('dy', '0.35em')
            .attr('text-anchor', 'middle')
            .style('font-size', '10px')
            .style('font-weight', 'bold');

        // Add service name text
        nodeEnter.append('text')
            .attr('class', 'node-text')
            .attr('dy', '45px')
            .attr('text-anchor', 'middle')
            .style('font-size', '0px');

        // Merge selection
        this.nodeElements = nodeEnter.merge(this.nodeElements);

        // animation display
        this.nodeElements
            .transition()
            .duration(500)
            .style('opacity', 1);

        // Update main circle node
        this.nodeElements.select('.node-circle')
            .transition()
            .duration(500)
            .attr('r', 25)
            .attr('fill', d => resolveRoleColor(d.colorRole))
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 2)
            .style('filter', 'drop-shadow(0 2px 8px rgba(var(--mud-palette-dark-rgb),0.15))');

        // Update outer ring
        this.nodeElements.select('.node-ring')
            .attr('r', 30)
            .attr('stroke', d => this.getStatusColor(d.status));

        // Update status indicator
        this.nodeElements.select('.status-indicator')
            .attr('fill', d => this.getStatusColor(d.status))
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 1);

        // Update the number of instances
        this.nodeElements.select('.instance-count')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => `${d.runningInstances}/${d.totalInstances}`);

        // Update service name
        this.nodeElements.select('.node-text')
            .transition()
            .duration(500)
            .style('font-size', '12px')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => this.truncateText(d.name, 15));

        // Add dragging behavior
        this.nodeElements.call(this.forceLayout.getDragBehavior());

        // Add interaction events
        this.nodeElements
            .style('cursor', 'pointer')
            .on('mouseenter', (event, d) => {
                this.highlightNode(d, true);
                this.showTooltip(event, this.buildTooltipContent(d));
            })
            .on('mouseleave', (event, d) => {
                this.highlightNode(d, false);
                this.hideTooltip();
            })
            .on('click', async (event, d) => {
                event.stopPropagation();
                await this.invokeDotNet('OnNodeClick', d.id);
            })
            .on('contextmenu', (event, d) => {
                event.preventDefault();
                event.stopPropagation();
                this.handleNodeRightClick(d, event);
            });

        // Start state animation
        this.nodeElements.each(d => this.startStatusAnimation(d));
    }

    updatePositions() {
        if (this.nodeElements) {
            this.nodeElements
                .attr('transform', d => `translate(${d.x},${d.y})`);
        }

        if (this.linkElements) {
            this.linkElements
                .attr('x1', d => d.source.x)
                .attr('y1', d => d.source.y)
                .attr('x2', d => d.target.x)
                .attr('y2', d => d.target.y);
        }
    }

    getStatusColor(status) {
        return STATUS_COLOR_MAP[status] || STATUS_COLOR_MAP.Unknown;
    }

    startStatusAnimation(nodeData) {
        const nodeElement = this.nodeElements.filter(d => d.id === nodeData.id);
        const ring = nodeElement.select('.node-ring');
        const statusIndicator = nodeElement.select('.status-indicator');

        // Stop previous animation
        this.stopAnimation(nodeData.id);

        switch (nodeData.status) {
            case 'Running':
                // Steady green, no flickering
                statusIndicator.style('opacity', 1);
                ring.style('opacity', 0);
                break;

            case 'Error':
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, STATUS_COLOR_MAP.Error, 500);
                break;

            case 'Offline':
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, STATUS_COLOR_MAP.Offline, 800);
                break;

            case 'Updating':
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, STATUS_COLOR_MAP.Updating, 600);
                break;

            default:
                statusIndicator.style('opacity', 0.5);
                ring.style('opacity', 0);
                break;
        }
    }

    startBlinkAnimation(nodeId, ring, statusIndicator, color, interval) {
        let phase = 0;
        
        const animate = () => {
            const opacity = Math.sin(phase) * 0.5 + 0.5; // 0-1之间的正弦波
            const ringOpacity = Math.max(0, Math.sin(phase) * 0.8); // 外圆环透明度
            
            statusIndicator.style('opacity', 0.3 + opacity * 0.7);
            ring.style('opacity', ringOpacity);
            
            phase += Math.PI / 10; // 控制闪烁速度
            
            // Save timer ID
            const timerId = setTimeout(animate, interval / 20);
            this.animations.set(nodeId, timerId);
        };
        
        animate();
    }

    stopAnimation(nodeId) {
        const timerId = this.animations.get(nodeId);
        if (timerId) {
            clearTimeout(timerId);
            this.animations.delete(nodeId);
        }
    }

    highlightNode(node, highlight) {
        const nodeElement = this.nodeElements.filter(d => d.id === node.id);
        const circle = nodeElement.select('.node-circle');
        
        if (highlight) {
            circle
                .transition()
                .duration(200)
                .attr('r', 30)
                .style('filter', 'drop-shadow(0 4px 12px rgba(var(--mud-palette-dark-rgb),0.25))');
        } else {
            circle
                .transition()
                .duration(200)
                .attr('r', 25)
                .style('filter', 'drop-shadow(0 2px 8px rgba(var(--mud-palette-dark-rgb),0.15))');
        }
    }

    buildTooltipContent(node) {
        const statusText = this.getStatusText(node.status);
        let content = `<strong>${node.name}</strong><br/>`;
        content += `<strong>${this.texts.labels.appId}:</strong> ${node.id}<br/>`;
        content += `<strong>${this.texts.labels.domain}:</strong> ${node.domain || this.texts.labels.unknown}<br/>`;
        if (node.project) content += `<strong>${this.texts.labels.project}:</strong> ${node.project}<br/>`;
        content += `<strong>${this.texts.labels.status}:</strong> ${statusText}<br/>`;
        content += `<strong>${this.texts.labels.instances}:</strong> ${node.runningInstances}/${node.totalInstances}`;
        
        if (node.instanceInfo) {
            content += `<br/><strong>${this.texts.labels.version}:</strong> ${node.instanceInfo.registerInfo?.assemblyVersion || this.texts.labels.notAvailable}`;
        }
        
        return content;
    }

    getStatusText(status) {
        switch (status) {
            case 'Running': return this.texts.statuses.running;
            case 'Updating': return this.texts.statuses.updating;
            case 'Offline': return this.texts.statuses.offline;
            case 'Error': return this.texts.statuses.error;
            default: return this.texts.statuses.unknown;
        }
    }

    showTooltip(event, content) {
        if (!this.tooltip) {
            this.tooltip = d3.select('body')
                .append('div')
                .attr('class', 'service-tooltip')
                .style('position', 'absolute')
                .style('background', 'rgba(var(--mud-palette-surface-rgb),0.98)')
                .style('color', 'var(--mud-palette-text-primary)')
                .style('border', '1px solid var(--mud-palette-lines-default)')
                .style('padding', '10px 15px')
                .style('border-radius', '8px')
                .style('font-size', '12px')
                .style('line-height', '1.4')
                .style('pointer-events', 'none')
                .style('z-index', '10000')
                .style('box-shadow', '0 4px 12px rgba(var(--mud-palette-dark-rgb),0.18)')
                .style('max-width', '250px')
                .style('opacity', 0);
        }

        this.tooltip
            .html(content)
            .style('left', (event.pageX + 15) + 'px')
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
            case 'grid':
                this.applyGridLayout();
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
        
        // Compute hierarchical layout - Microservices usually do not have clear hierarchical relationships and are layered by domain
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
     * Apply grid layout
     */
    applyGridLayout() {
        this.forceLayout.stop();
        
        // Compute Grid Layout
        this.layoutAlgorithms.gridLayout(this.nodes, {
            padding: 50,
            nodeSpacing: 120
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
                // Microservice graphs currently have no connection lines, but this method is retained for compatibility
                if (self.linkElements) {
                    self.linkElements
                        .attr('x1', d => {
                            const sourceId = d.source.id || d.source;
                            return sourceId === draggedNode.id ? draggedNode.x : 
                                   self.nodes.find(n => n.id === sourceId)?.x || 0;
                        })
                        .attr('y1', d => {
                            const sourceId = d.source.id || d.source;
                            return sourceId === draggedNode.id ? draggedNode.y : 
                                   self.nodes.find(n => n.id === sourceId)?.y || 0;
                        })
                        .attr('x2', d => {
                            const targetId = d.target.id || d.target;
                            return targetId === draggedNode.id ? draggedNode.x : 
                                   self.nodes.find(n => n.id === targetId)?.x || 0;
                        })
                        .attr('y2', d => {
                            const targetId = d.target.id || d.target;
                            return targetId === draggedNode.id ? draggedNode.y : 
                                   self.nodes.find(n => n.id === targetId)?.y || 0;
                        });
                }
            }
        });
        
        if (this.nodeElements) {
            this.nodeElements.call(this.staticDragBehavior);
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
        
        // Microservice graphs currently have no connection lines, but this method is retained for compatibility
        if (this.linkElements) {
            this.linkElements
                .transition()
                .duration(750)
                .attr('x1', d => {
                    const source = this.nodes.find(n => n.id === (d.source.id || d.source));
                    return source ? source.x : 0;
                })
                .attr('y1', d => {
                    const source = this.nodes.find(n => n.id === (d.source.id || d.source));
                    return source ? source.y : 0;
                })
                .attr('x2', d => {
                    const target = this.nodes.find(n => n.id === (d.target.id || d.target));
                    return target ? target.x : 0;
                })
                .attr('y2', d => {
                    const target = this.nodes.find(n => n.id === (d.target.id || d.target));
                    return target ? target.y : 0;
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
        const reference = this.dotNetRef;
        if (!reference || typeof reference.invokeMethodAsync !== 'function') {
            return Promise.resolve();
        }

        const callback = reference.invokeMethodAsync(methodName, ...args)
            .catch(error => {
                if (this.dotNetRef) {
                    console.error(`Service graph callback '${methodName}' failed.`, error);
                }
            })
            .finally(() => this.pendingCallbacks.delete(callback));

        this.pendingCallbacks.add(callback);
        return callback;
    }

    async dispose() {
        this.dotNetRef = null;

        // Stop all animations
        this.animations.forEach((timerId, nodeId) => {
            clearTimeout(timerId);
        });
        this.animations.clear();
        
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
export function createGraph(containerId, isDarkMode = false, dotNetRef = null, texts = {}) {
    return new ServiceGraph(containerId, { isDarkMode, dotNetRef, texts });
}
