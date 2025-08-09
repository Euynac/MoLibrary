/**
 * 项目单元架构可视化图表
 * 使用模块化的 D3.js 组件
 * 
 * @module projectUnitGraph
 */


import { GraphBase, getModernLinkStyle, getModernNodeStyle } from '../../../MoLibrary.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../../MoLibrary.UI/js/d3js/d3-force-layout.js';
import { NodeInteractionHandler, createStaticDragBehavior } from '../../../MoLibrary.UI/js/d3js/d3-node-interaction.js';
import { createComplexNodeCardRenderer } from '../../../MoLibrary.UI/js/d3js/d3-complex-node-card.js';

// ==================== 配置 ====================

// 节点配置现在由C#层提供，不再在JS层硬编码

/**
 * 节点尺寸配置
 */
const NODE_SIZE = {
    circle: { 
        radius: 32,
        textOffset: 45  // 文字在圆形下方的偏移距离
    },
    // 复杂节点尺寸现在由卡片渲染器动态计算
    complex: {
        minWidth: 200,
        maxWidth: 350,
        minHeight: 220
    }
};

/**
 * 布局类型
 */
const LAYOUT_TYPES = {
    FORCE: 'force',
    HIERARCHY: 'hierarchy',
    CIRCULAR: 'circular'
};

// ==================== 主类 ====================

/**
 * 项目单元图表类
 */
class ProjectUnitGraph {
    constructor(containerId, isDarkMode, dotNetRef) {
        this.containerId = containerId;
        this.isDarkMode = isDarkMode;
        this.dotNetRef = dotNetRef;
        this.currentLayout = LAYOUT_TYPES.FORCE;
        this.nodes = [];
        this.links = [];
        
        // 初始化基础图形
        this.graphBase = new GraphBase(containerId, {
            isDarkMode,
            showArrows: true,
            onBackgroundClick: () => this.handleBackgroundClick()
        });
        
        // 初始化力导向布局管理器
        this.forceManager = new ForceLayoutManager(
            this.graphBase.width,
            this.graphBase.height,
            {
                linkDistance: 150,
                chargeStrength: -300,
                keepFixed: false // 默认不保持固定，支持双击释放
            }
        );
        
        // 初始化交互处理器
        this.interactionHandler = new NodeInteractionHandler({
            onClick: (event, d) => this.handleNodeClick(d),
            onRightClick: (event, d, position) => this.handleNodeRightClick(d, position),
            onDoubleClick: (event, d) => this.handleNodeDoubleClick(d),
            highlightOptions: {
                fadeOpacity: 0.2,
                normalOpacity: 1
            },
            isDarkMode: isDarkMode
        });
        
        // 初始化复杂节点卡片渲染器
        this.cardRenderer = createComplexNodeCardRenderer(isDarkMode);
        
        // 获取现代化节点样式
        this.nodeStyle = getModernNodeStyle(isDarkMode, 'simple');
        
        // 静态布局拖拽行为
        this.staticDragBehavior = null;
    }
    
    /**
     * 更新图表数据
     */
    updateGraph(data) {
        this.nodes = data.nodes;
        this.links = data.links;
        
        // 清空现有内容
        this.graphBase.mainGroup.selectAll('.links').remove();
        this.graphBase.mainGroup.selectAll('.nodes').remove();
        
        // 创建连接线组
        const linkGroup = this.graphBase.mainGroup.append('g')
            .attr('class', 'links');
        
        // 创建节点组
        const nodeGroup = this.graphBase.mainGroup.append('g')
            .attr('class', 'nodes');
        
        // 绘制现代化连接线 - 使用MudBlazor颜色系统和圆润样式
        const linkStyle = getModernLinkStyle(this.isDarkMode, false);
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
        
        // 创建节点
        this.nodeSelection = nodeGroup.selectAll('g')
            .data(this.nodes)
            .enter().append('g')
            .attr('class', 'node');
        
        // 绘制节点图形
        this.nodeSelection.each((d, i, nodes) => {
            const nodeElement = d3.select(nodes[i]);
            this.drawNode(nodeElement, d);
        });
        
        // 绑定交互事件
        this.interactionHandler.bindNodeEvents(this.nodeSelection, {
            nodes: this.nodes,
            links: this.links,
            linkSelection: this.linkSelection
        });
        
        // 添加工具提示
        this.nodeSelection.append('title')
            .text(d => `${d.title}\n类型: ${d.type}\n依赖数: ${d.dependencyCount}`);
        
        // 应用当前布局
        this.applyLayout(this.currentLayout);
    }
    
    /**
     * 绘制节点
     */
    drawNode(nodeElement, nodeData) {
        // 使用来自C#层的配置判断节点类型
        if (nodeData.isComplex) {
            this.drawComplexNode(nodeElement, nodeData);
        } else {
            this.drawSimpleNode(nodeElement, nodeData);
        }
    }
    
    /**
     * 绘制复杂节点（卡片式）
     */
    drawComplexNode(nodeElement, nodeData) {
        // 使用卡片渲染器绘制
        this.cardRenderer.drawCard(nodeElement, nodeData);
    }
    
    /**
     * 绘制简单节点（圆形，文字在下方）
     */
    drawSimpleNode(nodeElement, nodeData) {
        const { radius, textOffset } = NODE_SIZE.circle;
        // 使用来自C#层的颜色配置
        const color = nodeData.color || '#9E9E9E';
        
        // 绘制圆形
        nodeElement.append('circle')
            .attr('r', radius)
            .attr('fill', color)
            .attr('stroke', this.nodeStyle.strokeColor)
            .attr('stroke-width', this.nodeStyle.strokeWidth)
            .attr('opacity', 1)
            .style('filter', this.nodeStyle.filter)
            .style('cursor', 'pointer');
        
        // 在圆形下方绘制文字 - 不截断，完整显示
        nodeElement.append('text')
            .attr('y', textOffset)
            .attr('text-anchor', 'middle')
            .attr('fill', this.nodeStyle.textColor)
            .style('font-size', '13px')
            .style('font-weight', '500')
            .style('pointer-events', 'none')
            .text(nodeData.title);
        
        // 如果有依赖数量，在文字下方显示
        if (nodeData.dependencyCount > 0) {
            nodeElement.append('text')
                .attr('y', textOffset + 16)
                .attr('text-anchor', 'middle')
                .attr('fill', this.nodeStyle.textColor)
                .style('font-size', '11px')
                .style('opacity', 0.7)
                .style('pointer-events', 'none')
                .text(`依赖: ${nodeData.dependencyCount}`);
        }
    }
    
    /**
     * 应用布局
     */
    applyLayout(layoutType) {
        this.currentLayout = layoutType;
        
        // 移除之前的拖拽行为
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
        }
    }
    
    /**
     * 应用力导向布局
     */
    applyForceLayout() {
        // 释放所有固定节点
        this.forceManager.releaseAllFixed(this.nodes);
        
        // 设置数据
        this.forceManager.setData(this.nodes, this.links);
        
        // 应用拖拽行为
        this.nodeSelection.call(this.forceManager.getDragBehavior());
        
        // 启动模拟
        this.forceManager.start(() => {
            this.linkSelection
                .attr('d', d => {
                    // 计算从源到目标的路径，根据目标节点类型调整终点
                    const dx = d.target.x - d.source.x;
                    const dy = d.target.y - d.source.y;
                    const distance = Math.sqrt(dx * dx + dy * dy);
                    const normX = dx / distance;
                    const normY = dy / distance;
                    
                    // 根据目标节点类型计算箭头终点
                    const targetNode = this.nodes.find(n => n.id === d.target.id);
                    let arrowOffset = 35; // 默认圆形节点的偏移量
                    
                    if (targetNode && targetNode.isComplex && targetNode._cardSize) {
                        // 复杂节点：计算到矩形边界的距离
                        const halfWidth = targetNode._cardSize.width / 2;
                        const halfHeight = targetNode._cardSize.height / 2;
                        
                        // 使用更稳定的算法计算矩形边界交点
                        const angle = Math.atan2(dy, dx);
                        const cos = Math.cos(angle);
                        const sin = Math.sin(angle);
                        
                        // 计算射线与矩形四条边的交点，选择最近的
                        let t = Infinity;
                        
                        // 检查与垂直边的交点
                        if (Math.abs(cos) > 0.001) {
                            const signX = cos > 0 ? 1 : -1;
                            const tVertical = (signX * halfWidth) / cos;
                            if (Math.abs(tVertical * sin) <= halfHeight) {
                                t = Math.min(t, Math.abs(tVertical));
                            }
                        }
                        
                        // 检查与水平边的交点
                        if (Math.abs(sin) > 0.001) {
                            const signY = sin > 0 ? 1 : -1;
                            const tHorizontal = (signY * halfHeight) / sin;
                            if (Math.abs(tHorizontal * cos) <= halfWidth) {
                                t = Math.min(t, Math.abs(tHorizontal));
                            }
                        }
                        
                        // 添加额外间距
                        arrowOffset = t + 10;
                        
                        // 防止无限大的值
                        if (!isFinite(arrowOffset) || arrowOffset > 200) {
                            arrowOffset = halfWidth + halfHeight + 10; // 使用合理的默认值
                        }
                    }
                    
                    // 缩短路径末端，为箭头留出空间
                    const endX = d.target.x - normX * arrowOffset;
                    const endY = d.target.y - normY * arrowOffset;
                    return `M${d.source.x},${d.source.y} L${endX},${endY}`;
                });
            
            this.nodeSelection
                .attr('transform', d => `translate(${d.x},${d.y})`);
        });
    }
    
    /**
     * 应用层次布局
     */
    applyHierarchyLayout() {
        this.forceManager.stop();
        
        // 计算层次布局
        this.calculateHierarchyLayout();
        
        // 应用静态拖拽
        this.applyStaticDrag();
        
        // 更新位置
        this.updateStaticPositions();
    }
    
    /**
     * 应用环形布局
     */
    applyCircularLayout() {
        this.forceManager.stop();
        
        // 计算环形布局
        this.calculateCircularLayout();
        
        // 应用静态拖拽
        this.applyStaticDrag();
        
        // 更新位置
        this.updateStaticPositions();
    }
    
    /**
     * 计算层次布局
     */
    calculateHierarchyLayout() {
        const width = this.graphBase.width;
        const height = this.graphBase.height;
        
        // 构建层次结构
        const nodeMap = new Map(this.nodes.map(n => [n.id, { ...n }]));
        const root = { id: 'root', children: [] };
        
        const targetIds = new Set(this.links.map(l => l.target));
        const rootNodes = this.nodes.filter(n => !targetIds.has(n.id));
        
        function buildTree(nodeId, visited = new Set()) {
            if (visited.has(nodeId)) return null;
            visited.add(nodeId);
            
            const node = nodeMap.get(nodeId);
            if (!node) return null;
            
            const children = this.links
                .filter(l => l.source === nodeId)
                .map(l => buildTree.call(this, l.target, visited))
                .filter(n => n !== null);
            
            return { ...node, children };
        }
        
        root.children = rootNodes.map(n => buildTree.call(this, n.id));
        
        const treeLayout = d3.tree().size([width - 100, height - 100]);
        const hierarchy = d3.hierarchy(root);
        const treeNodes = treeLayout(hierarchy);
        
        treeNodes.descendants().forEach(d => {
            if (d.data.id !== 'root') {
                const node = this.nodes.find(n => n.id === d.data.id);
                if (node) {
                    node.x = d.x + 50;
                    node.y = d.y + 50;
                }
            }
        });
    }
    
    /**
     * 计算环形布局
     */
    calculateCircularLayout() {
        const centerX = this.graphBase.width / 2;
        const centerY = this.graphBase.height / 2;
        const radius = Math.min(this.graphBase.width, this.graphBase.height) / 3;
        const angleStep = (2 * Math.PI) / this.nodes.length;
        
        this.nodes.forEach((node, i) => {
            const angle = i * angleStep - Math.PI / 2;
            node.x = centerX + radius * Math.cos(angle);
            node.y = centerY + radius * Math.sin(angle);
        });
    }
    
    /**
     * 应用静态拖拽
     */
    applyStaticDrag() {
        const self = this;
        
        this.staticDragBehavior = createStaticDragBehavior({
            updateLinks: (draggedNode) => {
                // 实时更新连接线
                self.linkSelection
                    .attr('x1', d => {
                        const source = d.source.id === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === d.source.id) || d.source;
                        return source.x;
                    })
                    .attr('y1', d => {
                        const source = d.source.id === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === d.source.id) || d.source;
                        return source.y;
                    })
                    .attr('x2', d => {
                        const target = d.target.id === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === d.target.id) || d.target;
                        return target.x;
                    })
                    .attr('y2', d => {
                        const target = d.target.id === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === d.target.id) || d.target;
                        return target.y;
                    });
            }
        });
        
        this.nodeSelection.call(this.staticDragBehavior);
    }
    
    /**
     * 更新静态位置
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
                const source = this.nodes.find(n => n.id === (d.source.id || d.source));
                const target = this.nodes.find(n => n.id === (d.target.id || d.target));
                
                if (!source || !target) return '';
                
                // 计算从源到目标的路径，根据目标节点类型调整终点
                const dx = target.x - source.x;
                const dy = target.y - source.y;
                const distance = Math.sqrt(dx * dx + dy * dy);
                
                if (distance === 0) return '';
                
                const normX = dx / distance;
                const normY = dy / distance;
                
                // 根据目标节点类型计算箭头终点
                let arrowOffset = 35; // 默认圆形节点的偏移量
                
                if (target.isComplex && target._cardSize) {
                    // 复杂节点：计算到矩形边界的距离
                    const halfWidth = target._cardSize.width / 2;
                    const halfHeight = target._cardSize.height / 2;
                    
                    // 使用更稳定的算法计算矩形边界交点
                    const angle = Math.atan2(dy, dx);
                    const cos = Math.cos(angle);
                    const sin = Math.sin(angle);
                    
                    // 计算射线与矩形四条边的交点，选择最近的
                    let t = Infinity;
                    
                    // 检查与垂直边的交点
                    if (Math.abs(cos) > 0.001) {
                        const signX = cos > 0 ? 1 : -1;
                        const tVertical = (signX * halfWidth) / cos;
                        if (Math.abs(tVertical * sin) <= halfHeight) {
                            t = Math.min(t, Math.abs(tVertical));
                        }
                    }
                    
                    // 检查与水平边的交点
                    if (Math.abs(sin) > 0.001) {
                        const signY = sin > 0 ? 1 : -1;
                        const tHorizontal = (signY * halfHeight) / sin;
                        if (Math.abs(tHorizontal * cos) <= halfWidth) {
                            t = Math.min(t, Math.abs(tHorizontal));
                        }
                    }
                    
                    // 添加额外间距
                    arrowOffset = t + 10;
                    
                    // 防止无限大的值
                    if (!isFinite(arrowOffset) || arrowOffset > 200) {
                        arrowOffset = halfWidth + halfHeight + 10; // 使用合理的默认值
                    }
                }
                
                // 缩短路径末端，为箭头留出空间
                const endX = target.x - normX * arrowOffset;
                const endY = target.y - normY * arrowOffset;
                return `M${source.x},${source.y} L${endX},${endY}`;
            });
    }
    
    /**
     * 设置布局
     */
    setLayout(layoutType) {
        this.applyLayout(layoutType);
    }
    
    /**
     * 设置力导向距离
     */
    setForceDistance(distance) {
        this.forceManager.updateLinkDistance(distance);
    }
    
    /**
     * 设置斥力强度
     */
    setForceStrength(strength) {
        this.forceManager.updateChargeStrength(strength);
    }
    
    /**
     * 重置视图
     */
    resetView() {
        this.graphBase.resetView();
    }
    
    /**
     * 聚焦节点
     */
    focusOnNode(nodeId) {
        const node = this.nodes.find(n => n.id === nodeId);
        if (node) {
            this.graphBase.focusOnPosition({ x: node.x, y: node.y });
            
            // 高亮节点
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
    
    // 获取单元颜色的方法已移除 - 现在由C#层提供颜色配置
    
    /**
     * 处理节点点击
     */
    handleNodeClick(nodeData) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnNodeClick', nodeData.id);
        }
    }
    
    /**
     * 处理节点右键
     */
    handleNodeRightClick(nodeData, position) {
        if (this.dotNetRef) {
            // 优先使用 clientX/clientY（相对于视口），如果不存在则使用 pageX/pageY
            const x = position.clientX !== undefined ? position.clientX : position.pageX;
            const y = position.clientY !== undefined ? position.clientY : position.pageY;
            
            this.dotNetRef.invokeMethodAsync('OnNodeRightClick', nodeData.id, x, y);
        }
    }
    
    /**
     * 处理节点双击（释放固定）
     */
    handleNodeDoubleClick(nodeData) {
        if (this.currentLayout === LAYOUT_TYPES.FORCE) {
            nodeData.fx = null;
            nodeData.fy = null;
            this.forceManager.simulation.alpha(0.3).restart();
        }
    }
    
    /**
     * 处理背景点击
     */
    handleBackgroundClick() {
        // 关闭右键菜单
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnSvgBackgroundClick');
        }
    }
    
    /**
     * 销毁
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

// ==================== 导出函数 ====================

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