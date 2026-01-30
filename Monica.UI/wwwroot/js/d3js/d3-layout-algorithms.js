/**
 * D3.js 布局算法集合
 * 提供多种图形布局算法的通用实现
 * 
 * @module d3-layout-algorithms
 */

/**
 * 布局算法管理器
 */
export class LayoutAlgorithms {
    constructor(width, height) {
        this.width = width;
        this.height = height;
    }
    
    /**
     * 更新画布尺寸
     */
    updateDimensions(width, height) {
        this.width = width;
        this.height = height;
    }
    
    /**
     * 层次布局 - 基于节点度数的分层布局
     * @param {Array} nodes - 节点数组
     * @param {Array} links - 连接数组
     * @returns {Object} 包含节点位置的对象
     */
    hierarchicalLayout(nodes, links) {
        // 使用更大的虚拟画布避免节点密集
        const width = Math.max(this.width * 2, 2000);
        const height = Math.max(this.height * 2, 1500);
        
        // 计算节点度数
        const nodeDegrees = new Map();
        
        // 初始化度数
        nodes.forEach(node => {
            nodeDegrees.set(node.id, 0);
        });
        
        // 计算出度和入度
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
        
        // 根据度数分层
        const layers = [];
        const maxDegree = Math.max(...nodeDegrees.values());
        
        for (let i = 0; i <= maxDegree; i++) {
            layers[i] = [];
        }
        
        nodes.forEach(node => {
            const degree = nodeDegrees.get(node.id);
            layers[degree].push(node);
        });
        
        // 过滤空层并反转（度数高的在上层）
        const nonEmptyLayers = layers.filter(layer => layer.length > 0).reverse();
        
        // 计算布局
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
                
                // 之字形布局避免重叠
                if (layer.length > 10) {
                    node.y += (nodeIndex % 2 === 0 ? -30 : 30);
                }
            });
        });
        
        return { nodes };
    }
    
    /**
     * 环形布局
     * @param {Array} nodes - 节点数组
     * @param {Object} options - 布局选项
     * @returns {Object} 包含节点位置的对象
     */
    circularLayout(nodes, options = {}) {
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const nodeCount = nodes.length;
        
        if (nodeCount === 0) return { nodes };
        
        // 配置选项
        const minRadius = options.minRadius || 100;
        const maxRadius = options.maxRadius || 2500;
        const avgNodeSize = options.avgNodeSize || 60;
        
        // 计算平均节点尺寸
        let actualAvgSize = avgNodeSize;
        if (options.complexNodeCount) {
            actualAvgSize = avgNodeSize + options.complexNodeCount * 10;
        }
        
        // 计算所需周长和半径
        const requiredCircumference = nodeCount * actualAvgSize * 1.5;
        const calculatedRadius = requiredCircumference / (2 * Math.PI);
        const radius = Math.max(minRadius, Math.min(calculatedRadius, maxRadius));
        
        // 单环布局
        const angleStep = (2 * Math.PI) / nodeCount;
        
        nodes.forEach((node, i) => {
            const angle = i * angleStep - Math.PI / 2;
            node.x = centerX + radius * Math.cos(angle);
            node.y = centerY + radius * Math.sin(angle);
        });
        
        return { nodes, radius };
    }
    
    /**
     * 多层环形布局
     * @param {Array} nodes - 节点数组
     * @param {Object} options - 布局选项
     * @returns {Object} 包含节点位置的对象
     */
    multiCircularLayout(nodes, options = {}) {
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const nodeCount = nodes.length;
        
        if (nodeCount === 0) return { nodes };
        
        // 配置参数
        const minRadius = options.minRadius || 100;
        const maxRadius = options.maxRadius || 2500;
        const nodesPerRing = options.nodesPerRing || 15;
        
        // 计算环数
        const ringsNeeded = Math.ceil(nodeCount / nodesPerRing);
        const ringSpacing = ringsNeeded > 1 ? (maxRadius - minRadius) / (ringsNeeded - 1) : 0;
        
        // 分配节点到各环
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
     * 网格布局
     * @param {Array} nodes - 节点数组
     * @param {Object} options - 布局选项
     * @returns {Object} 包含节点位置的对象
     */
    gridLayout(nodes, options = {}) {
        const nodeCount = nodes.length;
        if (nodeCount === 0) return { nodes };
        
        const padding = options.padding || 50;
        const nodeSpacing = options.nodeSpacing || 100;
        
        // 计算网格大小
        const cols = Math.ceil(Math.sqrt(nodeCount));
        const rows = Math.ceil(nodeCount / cols);
        
        // 计算起始位置（居中）
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
     * 树形布局
     * @param {Array} nodes - 节点数组
     * @param {Array} links - 连接数组
     * @param {Object} options - 布局选项
     * @returns {Object} 包含节点位置的对象
     */
    treeLayout(nodes, links, options = {}) {
        if (nodes.length === 0) return { nodes };
        
        const orientation = options.orientation || 'vertical'; // 'vertical' or 'horizontal'
        const levelSpacing = options.levelSpacing || 100;
        const nodeSpacing = options.nodeSpacing || 50;
        
        // 构建父子关系
        const nodeMap = new Map(nodes.map(n => [n.id, n]));
        const roots = [];
        const children = new Map();
        
        // 初始化子节点映射
        nodes.forEach(node => {
            children.set(node.id, []);
        });
        
        // 构建层次结构
        links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            if (children.has(sourceId)) {
                children.get(sourceId).push(targetId);
            }
        });
        
        // 找出根节点（没有入边的节点）
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
        
        // 如果没有根节点，选择度数最高的节点作为根
        if (roots.length === 0 && nodes.length > 0) {
            roots.push(nodes[0].id);
        }
        
        // 广度优先遍历分配位置
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
        
        // 分配坐标
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
     * 径向树布局
     * @param {Array} nodes - 节点数组
     * @param {Array} links - 连接数组
     * @param {Object} options - 布局选项
     * @returns {Object} 包含节点位置的对象
     */
    radialTreeLayout(nodes, links, options = {}) {
        const result = this.treeLayout(nodes, links, { ...options, orientation: 'vertical' });
        
        if (nodes.length === 0) return result;
        
        const centerX = this.width / 2;
        const centerY = this.height / 2;
        const radiusStep = options.radiusStep || 80;
        
        // 转换为径向坐标
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
 * 创建布局算法实例
 */
export function createLayoutAlgorithms(width, height) {
    return new LayoutAlgorithms(width, height);
}