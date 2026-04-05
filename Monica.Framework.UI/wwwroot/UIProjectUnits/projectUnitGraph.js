/**
 * Project unit architecture visualization chart
 * Use modular D3.js components
 * 
 * @module projectUnitGraph
 */

// Import common D3.js modules
import { GraphBase, getModernLinkStyle, getModernNodeStyle } from '../../Monica.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../Monica.UI/js/d3js/d3-force-layout.js';
import { NodeInteractionHandler, createStaticDragBehavior } from '../../Monica.UI/js/d3js/d3-node-interaction.js';
import { createLayoutAlgorithms } from '../../Monica.UI/js/d3js/d3-layout-algorithms.js';

// Import project unit-specific modules
import { createProjectUnitCardRenderer } from './projectUnitCardRenderer.js';

// ==================== Configuration ====================

// Node configuration is now provided by the C# layer and is no longer hardcoded in the JS layer

/**
 * Node size configuration
 */
const NODE_SIZE = {
    circle: { 
        radius: 32,
        textOffset: 45  // 文字在圆形下方的偏移距离
    },
    // Complex node sizes are now dynamically calculated by the card renderer
    complex: {
        minWidth: 200,
        maxWidth: 500
    }
};

/**
 * layout type
 */
const LAYOUT_TYPES = {
    FORCE: 'force',
    HIERARCHY: 'hierarchy',
    CIRCULAR: 'circular',
    MULTI_CIRCULAR: 'multi_circular'
};

const DEFAULT_ARROW_SIZE = 12;

// ==================== Main class ====================

/**
 * Project unit chart class
 */
class ProjectUnitGraph {
    constructor(containerId, isDarkMode, dotNetRef) {
        this.containerId = containerId;
        this.isDarkMode = isDarkMode;
        this.dotNetRef = dotNetRef;
        this.currentLayout = LAYOUT_TYPES.FORCE;
        this.nodes = [];
        this.links = [];
        
        // Initialize base graphics
        this.graphBase = new GraphBase(containerId, {
            isDarkMode,
            showArrows: true,
            arrowSize: DEFAULT_ARROW_SIZE,
            onBackgroundClick: () => this.handleBackgroundClick()
        });
        
        // Initialize force-directed layout manager
        this.forceManager = new ForceLayoutManager(
            this.graphBase.width,
            this.graphBase.height,
            {
                linkDistance: 150,
                chargeStrength: -300,
                keepFixed: false // 默认不保持固定，支持双击释放
            }
        );
        
        // Initialize interaction handler
        this.interactionHandler = new NodeInteractionHandler({
            onClick: (event, d) => this.handleNodeClick(d),
            onRightClick: (event, d, position) => this.handleNodeRightClick(d, position),
            onDoubleClick: (event, d) => this.handleNodeDoubleClick(d),
            highlightOptions: {
                fadeOpacity: 0.2,
                normalOpacity: 1
            },
            isDarkMode: isDarkMode,
            markerIds: this.graphBase.markerIds // 传递marker IDs
        });
        
        // Initializing project unit card renderer - passing size configuration
        this.cardRenderer = createProjectUnitCardRenderer(isDarkMode, NODE_SIZE.complex);
        
        // Get modern node styles
        this.nodeStyle = getModernNodeStyle(isDarkMode, 'simple');
        
        // Initialize layout algorithm manager
        this.layoutAlgorithms = createLayoutAlgorithms(
            this.graphBase.width,
            this.graphBase.height
        );
        
        // Static layout dragging behavior
        this.staticDragBehavior = null;
    }
    
    /**
     * Update chart data
     */
    updateGraph(data) {
        this.nodes = data.nodes;
        this.links = data.links;
        
        // Clear existing content
        this.graphBase.mainGroup.selectAll('.links').remove();
        this.graphBase.mainGroup.selectAll('.nodes').remove();
        
        // Create a connection line group
        const linkGroup = this.graphBase.mainGroup.append('g')
            .attr('class', 'links');
        
        // Create node group
        const nodeGroup = this.graphBase.mainGroup.append('g')
            .attr('class', 'nodes');
        
        // Draw modern connecting lines - using the MudBlazor color system and rounded styles
        const linkStyle = getModernLinkStyle(this.isDarkMode, false, this.graphBase.markerIds);
        this.linkSelection = linkGroup.selectAll('path')
            .data(this.links)
            .enter().append('path')
            .attr('class', 'link modern-link')
            .attr('stroke', linkStyle.stroke)
            .attr('stroke-opacity', linkStyle.strokeOpacity)
            .attr('stroke-width', linkStyle.strokeWidth)
            .attr('stroke-linecap', linkStyle.strokeLinecap)
            .attr('stroke-linejoin', linkStyle.strokeLinejoin)
            .attr('fill', 'none')
            .attr('marker-end', linkStyle.markerEnd)
            .style('filter', linkStyle.filter)
            .style('pointer-events', 'none'); // 现代化过渡动画
        
        // Create node
        this.nodeSelection = nodeGroup.selectAll('g')
            .data(this.nodes)
            .enter().append('g')
            .attr('class', 'node');
        
        // Draw node graph
        this.nodeSelection.each((d, i, nodes) => {
            const nodeElement = d3.select(nodes[i]);
            this.drawNode(nodeElement, d);
        });
        
        // Bind interaction events
        this.interactionHandler.bindNodeEvents(this.nodeSelection, {
            nodes: this.nodes,
            links: this.links,
            linkSelection: this.linkSelection
        });
        
        // Add tooltip
        this.nodeSelection.append('title')
            .text(d => d.tooltip || d.title);
        
        // Apply current layout
        this.applyLayout(this.currentLayout);
    }
    
    /**
     * draw node
     */
    drawNode(nodeElement, nodeData) {
        // Add alarm level attribute
        nodeElement.attr('data-alert-level', nodeData.alertLevel || 'none');
        
        // Determine node type using configuration from C# layer
        if (nodeData.isComplex) {
            this.drawComplexNode(nodeElement, nodeData);
        } else {
            this.drawSimpleNode(nodeElement, nodeData);
        }
        
        // Add alert visual effects
        this.addAlertEffects(nodeElement, nodeData);
    }
    
    /**
     * Draw complex nodes (card style)
     */
    drawComplexNode(nodeElement, nodeData) {
        // Draw using card renderer
        this.cardRenderer.drawCard(nodeElement, nodeData);
    }
    
    /**
     * Draw a simple node (circle, text below)
     */
    drawSimpleNode(nodeElement, nodeData) {
        const { radius, textOffset } = NODE_SIZE.circle;
        // Use color configuration from C# layer
        const color = nodeData.color || '#9E9E9E';
        
        // Draw a circle
        nodeElement.append('circle')
            .attr('class', 'node-circle')
            .attr('r', radius)
            .attr('fill', color)
            .attr('stroke', this.nodeStyle.strokeColor)
            .attr('stroke-width', this.nodeStyle.strokeWidth)
            .attr('opacity', 1)
            .style('filter', this.nodeStyle.filter)
            .style('cursor', 'pointer');
        
        // Draw text below circle - no truncation, full display
        nodeElement.append('text')
            .attr('y', textOffset)
            .attr('text-anchor', 'middle')
            .attr('fill', this.nodeStyle.textColor)
            .style('font-size', '13px')
            .style('font-weight', '500')
            .style('pointer-events', 'none')
            .text(nodeData.title);
        
        if (nodeData.summary) {
            nodeElement.append('text')
                .attr('y', textOffset + 16)
                .attr('text-anchor', 'middle')
                .attr('fill', this.nodeStyle.textColor)
                .style('font-size', '11px')
                .style('opacity', 0.7)
                .style('pointer-events', 'none')
                .text(nodeData.summary);
        }
    }
    
    /**
     * Add alert visual effects
     */
    addAlertEffects(nodeElement, nodeData) {
        if (!nodeData.alertLevel || nodeData.alertLevel === 'none') {
            return;
        }
        
        // Add an alarm glowing effect to the node
        const alertId = `alert-${nodeData.id || Math.random().toString(36).substr(2, 9)}`;
        
        // Create an alert filter
        const defs = this.graphBase.svg.select('defs').empty() 
            ? this.graphBase.svg.append('defs') 
            : this.graphBase.svg.select('defs');
        
        // Remove old alert filter (if present)
        defs.select(`#${alertId}`).remove();
        
        const filter = defs.append('filter')
            .attr('id', alertId)
            .attr('x', '-100%')
            .attr('y', '-100%')
            .attr('width', '300%')
            .attr('height', '300%');
        
        // Set different lighting effects according to alarm levels
        let glowColor, glowStdDeviation, animationClass;
        
        switch (nodeData.alertLevel) {
            case 'error':
                glowColor = '#ff0000';
                glowStdDeviation = 8;
                animationClass = 'alert-glow-error';
                break;
            case 'warning':
                glowColor = '#ffaa00';
                glowStdDeviation = 6;
                animationClass = 'alert-glow-warning';
                break;
            case 'info':
                glowColor = '#0088ff';
                glowStdDeviation = 4;
                animationClass = 'alert-glow-info';
                break;
            default:
                return;
        }
        
        // Add Gaussian Blur
        const gaussianBlur = filter.append('feGaussianBlur')
            .attr('stdDeviation', glowStdDeviation)
            .attr('result', 'coloredBlur');
        
        // Add glow color
        filter.append('feFlood')
            .attr('flood-color', glowColor)
            .attr('flood-opacity', 0.6)
            .attr('result', 'glowColor');
        
        filter.append('feComposite')
            .attr('in', 'glowColor')
            .attr('in2', 'coloredBlur')
            .attr('operator', 'in')
            .attr('result', 'softGlow');
        
        // Merge original image and glow
        const merge = filter.append('feMerge');
        merge.append('feMergeNode')
            .attr('in', 'softGlow');
        merge.append('feMergeNode')
            .attr('in', 'SourceGraphic');
        
        // Apply a filter to the main element of the node
        const mainElement = nodeData.isComplex 
            ? nodeElement.select('.card-background')
            : nodeElement.select('.node-circle');
            
        if (!mainElement.empty()) {
            mainElement.style('filter', `url(#${alertId})`);
            
            // Add flash animation for warning and error levels
            if (nodeData.alertLevel === 'warning' || nodeData.alertLevel === 'error') {
                this.addPulseAnimation(gaussianBlur, nodeData.alertLevel);
            }
        }
    }
    
    /**
     * Add pulse animation
     */
    addPulseAnimation(element, alertLevel) {
        const duration = alertLevel === 'error' ? 800 : 1200; // error闪烁更快
        const minStd = alertLevel === 'error' ? 6 : 4;
        const maxStd = alertLevel === 'error' ? 12 : 8;
        
        // Create animation
        const animate = () => {
            element
                .transition()
                .duration(duration / 2)
                .attr('stdDeviation', maxStd)
                .transition()
                .duration(duration / 2)
                .attr('stdDeviation', minStd)
                .on('end', animate);
        };
        
        animate();
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
        const arrowOffset = this.getTargetArrowOffset(target, dx, dy);
        const endX = target.x - normX * arrowOffset;
        const endY = target.y - normY * arrowOffset;

        return `M${source.x},${source.y} L${endX},${endY}`;
    }

    getTargetArrowOffset(targetNode, dx, dy) {
        if (!targetNode?.isComplex || !targetNode._cardSize) {
            return NODE_SIZE.circle.radius + this.graphBase.arrowSize;
        }

        const halfWidth = targetNode._cardSize.width / 2;
        const halfHeight = targetNode._cardSize.height / 2;
        const angle = Math.atan2(dy, dx);
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);
        let t = Infinity;

        if (Math.abs(cos) > 0.001) {
            const signX = cos > 0 ? 1 : -1;
            const tVertical = (signX * halfWidth) / cos;
            if (Math.abs(tVertical * sin) <= halfHeight) {
                t = Math.min(t, Math.abs(tVertical));
            }
        }

        if (Math.abs(sin) > 0.001) {
            const signY = sin > 0 ? 1 : -1;
            const tHorizontal = (signY * halfHeight) / sin;
            if (Math.abs(tHorizontal * cos) <= halfWidth) {
                t = Math.min(t, Math.abs(tHorizontal));
            }
        }

        const arrowOffset = t + this.graphBase.arrowSize;
        if (!isFinite(arrowOffset) || arrowOffset > 200) {
            return halfWidth + halfHeight + this.graphBase.arrowSize;
        }

        return arrowOffset;
    }

    findNodeByLinkRef(linkNode) {
        const nodeId = linkNode?.id || linkNode;
        return this.nodes.find(node => node.id === nodeId);
    }
    
    /**
     * Apply layout
     */
    applyLayout(layoutType) {
        this.currentLayout = layoutType;
        
        // Remove previous dragging behavior
        this.nodeSelection.on('.drag', null);
        
        switch (layoutType) {
            case LAYOUT_TYPES.FORCE:
                this.applyForceLayout();
                break;
            case LAYOUT_TYPES.HIERARCHY:
                this.applyHierarchyLayout();
                break;
            case LAYOUT_TYPES.CIRCULAR:
                this.applyCircularLayout();
                break;
            case LAYOUT_TYPES.MULTI_CIRCULAR:
                this.applyMultiCircularLayout();
                break;
        }
    }
    
    /**
     * Apply force-directed layout
     */
    applyForceLayout() {
        // Release all pinned nodes
        this.forceManager.releaseAllFixed(this.nodes);
        
        // Set data
        this.forceManager.setData(this.nodes, this.links);
        
        // Apply drag behavior
        this.nodeSelection.call(this.forceManager.getDragBehavior());
        
        // Start simulation
        this.forceManager.start(() => {
            this.linkSelection
                .attr('d', d => this.buildLinkPath(d.source, d.target));
            
            this.nodeSelection
                .attr('transform', d => `translate(${d.x},${d.y})`);
        });
    }
    
    /**
     * Apply hierarchical layout
     */
    applyHierarchyLayout() {
        this.forceManager.stop();
        
        // Compute hierarchical layout
        this.calculateHierarchyLayout();
        
        // Apply static drag
        this.applyStaticDrag();
        
        // Update location
        this.updateStaticPositions();
    }
    
    /**
     * Apply ring layout
     */
    applyCircularLayout() {
        this.forceManager.stop();
        
        // Calculate ring layout
        this.calculateCircularLayout();
        
        // Apply static drag
        this.applyStaticDrag();
        
        // Update location
        this.updateStaticPositions();
    }
    
    /**
     * Compute hierarchical layout - using a common layout algorithm
     */
    calculateHierarchyLayout() {
        this.layoutAlgorithms.hierarchicalLayout(this.nodes, this.links);
    }
    
    /**
     * Calculate ring layout - using universal layout algorithm
     */
    calculateCircularLayout() {
        const complexNodeCount = this.nodes.filter(n => n.isComplex).length;
        this.layoutAlgorithms.circularLayout(this.nodes, {
            avgNodeSize: 60,
            complexNodeCount: complexNodeCount
        });
    }
    
    /**
     * Calculate multi-level ring layout - using universal layout algorithm
     */
    calculateMultiCircularLayout() {
        this.layoutAlgorithms.multiCircularLayout(this.nodes, {
            nodesPerRing: 15
        });
    }
    
    /**
     * Apply a multi-layered ring layout
     */
    applyMultiCircularLayout() {
        this.forceManager.stop();
        
        // Compute multi-level ring layout
        this.calculateMultiCircularLayout();
        
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
                self.linkSelection
                    .attr('d', d => {
                        const sourceId = d.source.id || d.source;
                        const targetId = d.target.id || d.target;
                        
                        const source = sourceId === draggedNode.id ? draggedNode : 
                                       self.findNodeByLinkRef(sourceId);
                        const target = targetId === draggedNode.id ? draggedNode : 
                                       self.findNodeByLinkRef(targetId);

                        return self.buildLinkPath(source, target);
                    });
            }
        });
        
        this.nodeSelection.call(this.staticDragBehavior);
    }
    
    /**
     * Update static location
     */
    updateStaticPositions() {
        this.nodeSelection
            .transition()
            .duration(750)
            .attr('transform', d => `translate(${d.x},${d.y})`);
        
        this.linkSelection
            .transition()
            .duration(750)
            .attr('d', d => {
                const source = this.findNodeByLinkRef(d.source);
                const target = this.findNodeByLinkRef(d.target);

                return this.buildLinkPath(source, target);
            });
    }
    
    /**
     * Set layout
     */
    setLayout(layoutType) {
        this.applyLayout(layoutType);
    }
    
    /**
     * Set force guide distance
     */
    setForceDistance(distance) {
        this.forceManager.updateLinkDistance(distance);
    }
    
    /**
     * Set repulsion strength
     */
    setForceStrength(strength) {
        this.forceManager.updateChargeStrength(strength);
    }
    
    /**
     * reset view
     */
    resetView() {
        this.graphBase.resetView();
    }
    
    /**
     * focus node
     */
    focusOnNode(nodeId) {
        const node = this.nodes.find(n => n.id === nodeId);
        if (node) {
            this.graphBase.focusOnPosition({ x: node.x, y: node.y });
            
            // Highlight node
            const nodeElement = this.nodeSelection.filter(d => d.id === nodeId);
            nodeElement.select('circle, rect')
                .transition()
                .duration(300)
                .attr('stroke-width', 4)
                .transition()
                .delay(300)
                .duration(300)
                .attr('stroke-width', 2);
        }
    }
    
    // Method to get cell color removed - color configuration is now provided by C# layer
    
    /**
     * Handle node clicks
     */
    handleNodeClick(nodeData) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnNodeClick', nodeData.id);
        }
    }
    
    /**
     * Process node right click
     */
    handleNodeRightClick(nodeData, position) {
        if (this.dotNetRef) {
            // Prefer clientX/clientY (relative to the viewport), use pageX/pageY if not present
            const x = position.clientX !== undefined ? position.clientX : position.pageX;
            const y = position.clientY !== undefined ? position.clientY : position.pageY;
            
            this.dotNetRef.invokeMethodAsync('OnNodeRightClick', nodeData.id, x, y);
        }
    }
    
    /**
     * Handle node double click (release pinned)
     */
    handleNodeDoubleClick(nodeData) {
        if (this.currentLayout === LAYOUT_TYPES.FORCE) {
            nodeData.fx = null;
            nodeData.fy = null;
            this.forceManager.simulation.alpha(0.3).restart();
        }
    }
    
    /**
     * Handling background clicks
     */
    handleBackgroundClick() {
        // Close right-click menu
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnSvgBackgroundClick');
        }
    }
    
    /**
     * destroy
     */
    dispose() {
        if (this.forceManager) {
            this.forceManager.dispose();
        }
        if (this.graphBase) {
            this.graphBase.dispose();
        }
    }
}

// ==================== Exported functions ====================

let graphInstance = null;

export function initializeGraph(containerId, isDarkMode, dotNetRef) {
    graphInstance = new ProjectUnitGraph(containerId, isDarkMode, dotNetRef);
}

export function updateGraph(data) {
    if (graphInstance) {
        graphInstance.updateGraph(data);
    }
}

export function setLayout(layoutType) {
    if (graphInstance) {
        graphInstance.setLayout(layoutType);
    }
}

export function setForceDistance(distance) {
    if (graphInstance) {
        graphInstance.setForceDistance(distance);
    }
}

export function setForceStrength(strength) {
    if (graphInstance) {
        graphInstance.setForceStrength(strength);
    }
}

export function resetView() {
    if (graphInstance) {
        graphInstance.resetView();
    }
}

export function focusOnNode(nodeId) {
    if (graphInstance) {
        graphInstance.focusOnNode(nodeId);
    }
}

export function dispose() {
    if (graphInstance) {
        graphInstance.dispose();
        graphInstance = null;
    }
}
