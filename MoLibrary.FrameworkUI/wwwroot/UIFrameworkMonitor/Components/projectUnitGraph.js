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
        maxWidth: 500
    }
};

/**
 * 布局类型
 */
const LAYOUT_TYPES = {
    FORCE: 'force',
    HIERARCHY: 'hierarchy',
    CIRCULAR: 'circular',
    MULTI_CIRCULAR: 'multi_circular'
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
            isDarkMode: isDarkMode,
            markerIds: this.graphBase.markerIds // 传递marker IDs
        });
        
        // 初始化复杂节点卡片渲染器 - 传递尺寸配置
        this.cardRenderer = createComplexNodeCardRenderer(isDarkMode, NODE_SIZE.complex);
        
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
            .text(d => `${d.title}\n类型: ${d.type}\n依赖数: ${d.dependencyCount}\n被依赖数: ${d.dependedByCount || 0}`);
        
        // 应用当前布局
        this.applyLayout(this.currentLayout);
    }
    
    /**
     * 绘制节点
     */
    drawNode(nodeElement, nodeData) {
        // 添加告警级别属性
        nodeElement.attr('data-alert-level', nodeData.alertLevel || 'none');
        
        // 使用来自C#层的配置判断节点类型
        if (nodeData.isComplex) {
            this.drawComplexNode(nodeElement, nodeData);
        } else {
            this.drawSimpleNode(nodeElement, nodeData);
        }
        
        // 添加告警视觉效果
        this.addAlertEffects(nodeElement, nodeData);
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
            .attr('class', 'node-circle')
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
        
        // 显示依赖和被依赖数量
        const counts = [];
        if (nodeData.dependencyCount > 0) {
            counts.push(`依赖: ${nodeData.dependencyCount}`);
        }
        if (nodeData.dependedByCount > 0) {
            counts.push(`被依赖: ${nodeData.dependedByCount}`);
        }
        
        if (counts.length > 0) {
            nodeElement.append('text')
                .attr('y', textOffset + 16)
                .attr('text-anchor', 'middle')
                .attr('fill', this.nodeStyle.textColor)
                .style('font-size', '11px')
                .style('opacity', 0.7)
                .style('pointer-events', 'none')
                .text(counts.join(' | '));
        }
    }
    
    /**
     * 添加告警视觉效果
     */
    addAlertEffects(nodeElement, nodeData) {
        if (!nodeData.alertLevel || nodeData.alertLevel === 'none') {
            return;
        }
        
        // 为节点添加告警发光效果
        const alertId = `alert-${nodeData.id || Math.random().toString(36).substr(2, 9)}`;
        
        // 创建告警滤镜
        const defs = this.graphBase.svg.select('defs').empty() 
            ? this.graphBase.svg.append('defs') 
            : this.graphBase.svg.select('defs');
        
        // 移除旧的告警滤镜（如果存在）
        defs.select(`#${alertId}`).remove();
        
        const filter = defs.append('filter')
            .attr('id', alertId)
            .attr('x', '-100%')
            .attr('y', '-100%')
            .attr('width', '300%')
            .attr('height', '300%');
        
        // 根据告警级别设置不同的发光效果
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
        
        // 添加高斯模糊
        const gaussianBlur = filter.append('feGaussianBlur')
            .attr('stdDeviation', glowStdDeviation)
            .attr('result', 'coloredBlur');
        
        // 添加发光颜色
        filter.append('feFlood')
            .attr('flood-color', glowColor)
            .attr('flood-opacity', 0.6)
            .attr('result', 'glowColor');
        
        filter.append('feComposite')
            .attr('in', 'glowColor')
            .attr('in2', 'coloredBlur')
            .attr('operator', 'in')
            .attr('result', 'softGlow');
        
        // 合并原图和发光
        const merge = filter.append('feMerge');
        merge.append('feMergeNode')
            .attr('in', 'softGlow');
        merge.append('feMergeNode')
            .attr('in', 'SourceGraphic');
        
        // 应用滤镜到节点的主要元素
        const mainElement = nodeData.isComplex 
            ? nodeElement.select('.card-background')
            : nodeElement.select('.node-circle');
            
        if (!mainElement.empty()) {
            mainElement.style('filter', `url(#${alertId})`);
            
            // 为warning和error级别添加闪烁动画
            if (nodeData.alertLevel === 'warning' || nodeData.alertLevel === 'error') {
                this.addPulseAnimation(gaussianBlur, nodeData.alertLevel);
            }
        }
    }
    
    /**
     * 添加脉冲动画
     */
    addPulseAnimation(element, alertLevel) {
        const duration = alertLevel === 'error' ? 800 : 1200; // error闪烁更快
        const minStd = alertLevel === 'error' ? 6 : 4;
        const maxStd = alertLevel === 'error' ? 12 : 8;
        
        // 创建动画
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
            case LAYOUT_TYPES.MULTI_CIRCULAR:
                this.applyMultiCircularLayout();
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
     * 计算层次布局 - 基于节点的连通度（度数）
     */
    calculateHierarchyLayout() {
        // 使用更大的虚拟画布尺寸，避免节点过于密集
        const width = Math.max(this.graphBase.width * 2, 2000);
        const height = Math.max(this.graphBase.height * 2, 1500);
        
        // 计算每个节点的度数（入度 + 出度）
        const nodeDegrees = new Map();
        
        // 初始化所有节点的度数为0
        this.nodes.forEach(node => {
            nodeDegrees.set(node.id, 0);
        });
        
        // 计算出度和入度
        this.links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            // 增加源节点的出度
            if (nodeDegrees.has(sourceId)) {
                nodeDegrees.set(sourceId, nodeDegrees.get(sourceId) + 1);
            }
            
            // 增加目标节点的入度
            if (nodeDegrees.has(targetId)) {
                nodeDegrees.set(targetId, nodeDegrees.get(targetId) + 1);
            }
        });
        
        // 根据度数对节点进行分层
        const layers = [];
        const maxDegree = Math.max(...nodeDegrees.values());
        
        // 创建层次，度数高的节点在中心层
        for (let i = 0; i <= maxDegree; i++) {
            layers[i] = [];
        }
        
        // 将节点分配到对应的层
        this.nodes.forEach(node => {
            const degree = nodeDegrees.get(node.id);
            layers[degree].push(node);
        });
        
        // 过滤空层并反转（度数高的在上层）
        const nonEmptyLayers = layers.filter(layer => layer.length > 0).reverse();
        
        // 计算布局 - 增加层间距和节点间距
        const layerHeight = (height - 200) / Math.max(nonEmptyLayers.length, 1);
        const padding = 100;
        const minNodeSpacing = 120; // 最小节点间距
        
        nonEmptyLayers.forEach((layer, layerIndex) => {
            const y = padding + layerIndex * layerHeight + layerHeight / 2;
            const layerWidth = width - 2 * padding;
            // 确保节点间距不会太小
            const nodeSpacing = Math.max(minNodeSpacing, layerWidth / Math.max(layer.length, 1));
            
            // 如果节点需要的总宽度超过画布宽度，则水平居中排列
            const totalWidth = nodeSpacing * layer.length;
            const startX = totalWidth <= layerWidth 
                ? padding 
                : (this.graphBase.width / 2) - (totalWidth / 2); // 居中对齐
            
            layer.forEach((node, nodeIndex) => {
                // 均匀分布节点
                node.x = startX + nodeSpacing * nodeIndex + nodeSpacing / 2;
                node.y = y;
                
                // 如果节点太多，使用之字形布局避免重叠
                if (layer.length > 10) {
                    // 偶数索引的节点稍微上移，奇数索引的节点稍微下移
                    node.y += (nodeIndex % 2 === 0 ? -30 : 30);
                }
            });
        });
    }
    
    /**
     * 计算环形布局 - 动态调整半径以减少重叠
     */
    calculateCircularLayout() {
        const centerX = this.graphBase.width / 2;
        const centerY = this.graphBase.height / 2;
        
        // 根据节点数量动态计算半径
        const nodeCount = this.nodes.length;
        const minRadius = 100; // 最小半径
        const maxRadius = 2500; // 最大半径
        
        // 计算节点的平均尺寸（考虑复杂节点的大小）
        let avgNodeSize = 60; // 默认尺寸
        const complexNodeCount = this.nodes.filter(n => n.isComplex).length;
        if (complexNodeCount > 0) {
            // 如果有复杂节点，增加平均尺寸估算
            avgNodeSize = 80 + complexNodeCount * 10;
        }
        
        // 根据节点数量和大小计算所需的周长
        const requiredCircumference = nodeCount * avgNodeSize * 1.5; // 1.5倍间距
        const calculatedRadius = requiredCircumference / (2 * Math.PI);
        
        // 限制在最小和最大半径之间
        const radius = Math.max(minRadius, Math.min(calculatedRadius, maxRadius));
        
        // 单环布局
        const angleStep = (2 * Math.PI) / nodeCount;
        
        this.nodes.forEach((node, i) => {
            const angle = i * angleStep - Math.PI / 2;
            node.x = centerX + radius * Math.cos(angle);
            node.y = centerY + radius * Math.sin(angle);
        });
    }
    
    /**
     * 计算多层环形布局
     */
    calculateMultiCircularLayout() {
        const centerX = this.graphBase.width / 2;
        const centerY = this.graphBase.height / 2;
        const nodeCount = this.nodes.length;
        
        if (nodeCount === 0) return;
        
        // 配置参数
        const minRadius = 100;
        const maxRadius = 2500;
        const nodesPerRing = 15; // 每环最多节点数
        
        // 计算需要的环数
        const ringsNeeded = Math.ceil(nodeCount / nodesPerRing);
        const ringSpacing = ringsNeeded > 1 ? (maxRadius - minRadius) / (ringsNeeded - 1) : 0;
        
        // 分配节点到各环
        this.nodes.forEach((node, i) => {
            const ringIndex = Math.floor(i / nodesPerRing);
            const positionInRing = i % nodesPerRing;
            const nodesInThisRing = Math.min(nodesPerRing, nodeCount - ringIndex * nodesPerRing);
            const angleStep = (2 * Math.PI) / nodesInThisRing;
            const angle = positionInRing * angleStep - Math.PI / 2;
            const ringRadius = minRadius + ringIndex * ringSpacing;
            
            node.x = centerX + ringRadius * Math.cos(angle);
            node.y = centerY + ringRadius * Math.sin(angle);
        });
    }
    
    /**
     * 应用多层环形布局
     */
    applyMultiCircularLayout() {
        this.forceManager.stop();
        
        // 计算多层环形布局
        this.calculateMultiCircularLayout();
        
        // 应用静态拖拽
        this.applyStaticDrag();
        
        // 更新位置
        this.updateStaticPositions();
    }
    
    /**
     * 应用静态拖拽
     */
    applyStaticDrag() {
        const self = this;
        
        this.staticDragBehavior = createStaticDragBehavior({
            updateLinks: (draggedNode) => {
                // 实时更新连接线 - 使用path的d属性而不是x1,y1,x2,y2
                self.linkSelection
                    .attr('d', d => {
                        const sourceId = d.source.id || d.source;
                        const targetId = d.target.id || d.target;
                        
                        const source = sourceId === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === sourceId);
                        const target = targetId === draggedNode.id ? draggedNode : 
                                       self.nodes.find(n => n.id === targetId);
                        
                        if (!source || !target) return '';
                        
                        // 计算从源到目标的路径
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
                            
                            const angle = Math.atan2(dy, dx);
                            const cos = Math.cos(angle);
                            const sin = Math.sin(angle);
                            
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
                            
                            arrowOffset = t + 10;
                            
                            if (!isFinite(arrowOffset) || arrowOffset > 200) {
                                arrowOffset = halfWidth + halfHeight + 10;
                            }
                        }
                        
                        const endX = target.x - normX * arrowOffset;
                        const endY = target.y - normY * arrowOffset;
                        return `M${source.x},${source.y} L${endX},${endY}`;
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