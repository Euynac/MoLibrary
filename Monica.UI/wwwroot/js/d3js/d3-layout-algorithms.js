/**
 * D3.js layout algorithm collection
 * Provides general implementation of multiple graph layout algorithms
 * 
 * @module d3-layout-algorithms
 */

/**
 * Layout Algorithm Manager
 */
export class LayoutAlgorithms {
    constructor(width, height) {
        this.width = width;
        this.height = height;
    }
    
    /**
     * Update canvas size
     */
    updateDimensions(width, height) {
        this.width = width;
        this.height = height;
    }
    
    /**
     * Hierarchical Layout - Hierarchical layout based on node degree
     * @param {Array} nodes - array of nodes
     * @param {Array} links - linked arrays
     * @returns {Object} Object containing node location
     */
    hierarchicalLayout(nodes, links) {
        // Use a larger virtual canvas to avoid node density
        const width = Math.max(this.width * 2, 2000);
        const height = Math.max(this.height * 2, 1500);
        
        // Calculate node degree
        const nodeDegrees = new Map();
        
        // Initialize degree
        nodes.forEach(node => {
            nodeDegrees.set(node.id, 0);
        });
        
        // Calculate out-degree and in-degree
        links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            if (nodeDegrees.has(sourceId)) {
                nodeDegrees.set(sourceId, nodeDegrees.get(sourceId) + 1);
            }
            if (nodeDegrees.has(targetId)) {
                nodeDegrees.set(targetId, nodeDegrees.get(targetId) + 1);
            }
        });
        
        // Stratified by degree
        const layers = [];
        const maxDegree = Math.max(...nodeDegrees.values());
        
        for (let i = 0; i <= maxDegree; i++) {
            layers[i] = [];
        }
        
        nodes.forEach(node => {
            const degree = nodeDegrees.get(node.id);
            layers[degree].push(node);
        });
        
        // Filter the empty layers and invert them (those with higher degrees are on the upper layer)
        const nonEmptyLayers = layers.filter(layer => layer.length > 0).reverse();
        
        // Calculate layout
        const layerHeight = (height - 200) / Math.max(nonEmptyLayers.length, 1);
        const padding = 100;
        const minNodeSpacing = 120;
        
        nonEmptyLayers.forEach((layer, layerIndex) => {
            const y = padding + layerIndex * layerHeight + layerHeight / 2;
            const layerWidth = width - 2 * padding;
            const nodeSpacing = Math.max(minNodeSpacing, layerWidth / Math.max(layer.length, 1));
            
            const totalWidth = nodeSpacing * layer.length;
            const startX = totalWidth <= layerWidth 
                ? padding 
                : (this.width / 2) - (totalWidth / 2);
            
            layer.forEach((node, nodeIndex) => {
                node.x = startX + nodeSpacing * nodeIndex + nodeSpacing / 2;
                node.y = y;
                
                // Zigzag layout avoids overlap
                if (layer.length > 10) {
                    node.y += (nodeIndex % 2 === 0 ? -30 : 30);
                }
            });
        });
        
        return { nodes };
    }
    
    /**
     * ring layout
     * @param {Array} nodes - array of nodes
     * @param {Object} options - layout options
     * @returns {Object} Object containing node location
     */
    circularLayout(nodes, options = {}) {
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const nodeCount = nodes.length;
        
        if (nodeCount === 0) return { nodes };
        
        // Configuration options
        const minRadius = options.minRadius || 100;
        const maxRadius = options.maxRadius || 2500;
        const avgNodeSize = options.avgNodeSize || 60;
        
        // Calculate average node size
        let actualAvgSize = avgNodeSize;
        if (options.complexNodeCount) {
            actualAvgSize = avgNodeSize + options.complexNodeCount * 10;
        }
        
        // Calculate required perimeter and radius
        const requiredCircumference = nodeCount * actualAvgSize * 1.5;
        const calculatedRadius = requiredCircumference / (2 * Math.PI);
        const radius = Math.max(minRadius, Math.min(calculatedRadius, maxRadius));
        
        // Single ring layout
        const angleStep = (2 * Math.PI) / nodeCount;
        
        nodes.forEach((node, i) => {
            const angle = i * angleStep - Math.PI / 2;
            node.x = centerX + radius * Math.cos(angle);
            node.y = centerY + radius * Math.sin(angle);
        });
        
        return { nodes, radius };
    }
    
    /**
     * Multi-level ring layout
     * @param {Array} nodes - array of nodes
     * @param {Object} options - layout options
     * @returns {Object} Object containing node location
     */
    multiCircularLayout(nodes, options = {}) {
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const nodeCount = nodes.length;
        
        if (nodeCount === 0) return { nodes };
        
        // Configuration parameters
        const minRadius = options.minRadius || 100;
        const maxRadius = options.maxRadius || 2500;
        const nodesPerRing = options.nodesPerRing || 15;
        
        // Calculate the number of rings
        const ringsNeeded = Math.ceil(nodeCount / nodesPerRing);
        const ringSpacing = ringsNeeded > 1 ? (maxRadius - minRadius) / (ringsNeeded - 1) : 0;
        
        // Assign nodes to each ring
        nodes.forEach((node, i) => {
            const ringIndex = Math.floor(i / nodesPerRing);
            const positionInRing = i % nodesPerRing;
            const nodesInThisRing = Math.min(nodesPerRing, nodeCount - ringIndex * nodesPerRing);
            const angleStep = (2 * Math.PI) / nodesInThisRing;
            const angle = positionInRing * angleStep - Math.PI / 2;
            const ringRadius = minRadius + ringIndex * ringSpacing;
            
            node.x = centerX + ringRadius * Math.cos(angle);
            node.y = centerY + ringRadius * Math.sin(angle);
        });
        
        return { nodes, rings: ringsNeeded };
    }
    
    /**
     * grid layout
     * @param {Array} nodes - array of nodes
     * @param {Object} options - layout options
     * @returns {Object} Object containing node location
     */
    gridLayout(nodes, options = {}) {
        const nodeCount = nodes.length;
        if (nodeCount === 0) return { nodes };
        
        const padding = options.padding || 50;
        const nodeSpacing = options.nodeSpacing || 100;
        
        // Calculate grid size
        const cols = Math.ceil(Math.sqrt(nodeCount));
        const rows = Math.ceil(nodeCount / cols);
        
        // Calculate starting position (centered)
        const totalWidth = (cols - 1) * nodeSpacing;
        const totalHeight = (rows - 1) * nodeSpacing;
        const startX = (this.width - totalWidth) / 2;
        const startY = (this.height - totalHeight) / 2;
        
        nodes.forEach((node, i) => {
            const col = i % cols;
            const row = Math.floor(i / cols);
            
            node.x = startX + col * nodeSpacing;
            node.y = startY + row * nodeSpacing;
        });
        
        return { nodes, grid: { rows, cols } };
    }
    
    /**
     * tree layout
     * @param {Array} nodes - array of nodes
     * @param {Array} links - linked arrays
     * @param {Object} options - layout options
     * @returns {Object} Object containing node location
     */
    treeLayout(nodes, links, options = {}) {
        if (nodes.length === 0) return { nodes };
        
        const orientation = options.orientation || 'vertical'; // 'vertical' or 'horizontal'
        const levelSpacing = options.levelSpacing || 100;
        const nodeSpacing = options.nodeSpacing || 50;
        
        // Build a parent-child relationship
        const nodeMap = new Map(nodes.map(n => [n.id, n]));
        const roots = [];
        const children = new Map();
        
        // Initialize child node mapping
        nodes.forEach(node => {
            children.set(node.id, []);
        });
        
        // Build a hierarchy
        links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            if (children.has(sourceId)) {
                children.get(sourceId).push(targetId);
            }
        });
        
        // Find the root node (the node with no incoming edges)
        const hasIncomingEdge = new Set();
        links.forEach(link => {
            const targetId = link.target.id || link.target;
            hasIncomingEdge.add(targetId);
        });
        
        nodes.forEach(node => {
            if (!hasIncomingEdge.has(node.id)) {
                roots.push(node.id);
            }
        });
        
        // If there is no root node, select the node with the highest degree as the root
        if (roots.length === 0 && nodes.length > 0) {
            roots.push(nodes[0].id);
        }
        
        // Breadth-first traversal assigns positions
        const visited = new Set();
        const queue = roots.map(id => ({ id, level: 0, index: 0 }));
        const levelNodes = new Map();
        
        while (queue.length > 0) {
            const { id, level, index } = queue.shift();
            
            if (visited.has(id)) continue;
            visited.add(id);
            
            if (!levelNodes.has(level)) {
                levelNodes.set(level, []);
            }
            levelNodes.get(level).push(id);
            
            const childList = children.get(id) || [];
            childList.forEach((childId, i) => {
                if (!visited.has(childId)) {
                    queue.push({ id: childId, level: level + 1, index: i });
                }
            });
        }
        
        // Assign coordinates
        const maxLevel = Math.max(...levelNodes.keys());
        
        levelNodes.forEach((levelNodeIds, level) => {
            const count = levelNodeIds.length;
            const totalWidth = (count - 1) * nodeSpacing;
            
            levelNodeIds.forEach((nodeId, index) => {
                const node = nodeMap.get(nodeId);
                if (node) {
                    if (orientation === 'vertical') {
                        node.x = (this.width / 2) - (totalWidth / 2) + index * nodeSpacing;
                        node.y = 100 + level * levelSpacing;
                    } else {
                        node.x = 100 + level * levelSpacing;
                        node.y = (this.height / 2) - (totalWidth / 2) + index * nodeSpacing;
                    }
                }
            });
        });
        
        return { nodes, roots, levels: maxLevel + 1 };
    }
    
    /**
     * Radial tree layout
     * @param {Array} nodes - array of nodes
     * @param {Array} links - linked arrays
     * @param {Object} options - layout options
     * @returns {Object} Object containing node location
     */
    radialTreeLayout(nodes, links, options = {}) {
        const result = this.treeLayout(nodes, links, { ...options, orientation: 'vertical' });
        
        if (nodes.length === 0) return result;
        
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const radiusStep = options.radiusStep || 80;
        
        // Convert to radial coordinates
        nodes.forEach(node => {
            const dx = node.x - centerX;
            const dy = node.y - 100; // 相对于第一层的偏移
            const level = Math.floor(dy / 100); // 估算层级
            
            const radius = level * radiusStep;
            const angle = (dx / (this.width / 2)) * Math.PI;
            
            node.x = centerX + radius * Math.cos(angle);
            node.y = centerY + radius * Math.sin(angle);
        });
        
        return result;
    }
}

/**
 * Create a layout algorithm instance
 */
export function createLayoutAlgorithms(width, height) {
    return new LayoutAlgorithms(width, height);
}