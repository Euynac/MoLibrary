/**
 * 子域依赖关系图可视化
 * 基于 d3.js 实现的子域依赖关系图表
 * 
 * @module domainDependencyGraph
 */

import { GraphBase, getModernLinkStyle, getModernNodeStyle, focusOnPosition } from '../../MoLibrary.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../MoLibrary.UI/js/d3js/d3-force-layout.js';
import { createLayoutAlgorithms } from '../../MoLibrary.UI/js/d3js/d3-layout-algorithms.js';
import { createStaticDragBehavior } from '../../MoLibrary.UI/js/d3js/d3-node-interaction.js';

let graphInstance = null;

/**
 * 初始化域依赖关系图
 * @param {string} containerId - 容器ID
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @param {Object} dotNetHelper - .NET回调对象
 */
export function initializeGraph(containerId, isDarkMode = false, dotNetHelper = null) {
    dispose();
    graphInstance = new DomainDependencyGraph(containerId, { isDarkMode, dotNetHelper });
}

/**
 * 更新图表数据
 * @param {Object} data - 图表数据 {nodes, links}
 */
export function updateGraph(data) {
    if (graphInstance) {
        graphInstance.updateData(data);
    }
}

/**
 * 重置视图
 */
export function resetView() {
    if (graphInstance) {
        graphInstance.resetView();
    }
}

/**
 * 设置布局类型
 * @param {string} layoutType - 布局类型
 */
export function setLayout(layoutType) {
    if (graphInstance) {
        graphInstance.setLayout(layoutType);
    }
}

/**
 * 设置力导向距离
 * @param {number} distance - 距离值
 */
export function setForceDistance(distance) {
    if (graphInstance) {
        graphInstance.setForceDistance(distance);
    }
}

/**
 * 设置力导向强度
 * @param {number} strength - 强度值
 */
export function setForceStrength(strength) {
    if (graphInstance) {
        graphInstance.setForceStrength(strength);
    }
}

/**
 * 聚焦到指定节点
 * @param {string} nodeId - 节点ID
 */
export function focusOnNode(nodeId) {
    if (graphInstance) {
        graphInstance.focusOnNode(nodeId);
    }
}

/**
 * 销毁图表实例
 */
export function dispose() {
    if (graphInstance) {
        graphInstance.dispose();
        graphInstance = null;
    }
}

/**
 * 域依赖关系图类
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
        this.currentLayout = 'force';
        
        // 创建力导向布局管理器
        this.forceLayout = new ForceLayoutManager(this.width, this.height, {
            linkDistance: 200,
            chargeStrength: -800,
            collisionRadius: 80  // 增加碰撞半径以为下方文本留出空间
        });

        // 初始化布局算法管理器
        this.layoutAlgorithms = createLayoutAlgorithms(this.width, this.height);
        
        // 静态布局拖拽行为
        this.staticDragBehavior = null;

        // 创建图层
        this.createLayers();
        
        // 绑定事件
        this.bindEvents();
    }

    createLayers() {
        // 创建连接线层
        this.linkLayer = this.mainGroup.append('g')
            .attr('class', 'links-layer');
            
        // 创建节点层
        this.nodeLayer = this.mainGroup.append('g')
            .attr('class', 'nodes-layer');
    }

    bindEvents() {
        // 监听窗口大小变化
        window.addEventListener('resize', () => {
            this.handleResize();
        });
        
        // 添加背景点击事件处理
        this.svg.on('click', (event) => {
            // 只有当点击的是背景（svg本身）时才处理
            if (event.target === this.svg.node()) {
                this.handleBackgroundClick();
            }
        });
    }
    
    /**
     * 处理背景点击事件
     */
    handleBackgroundClick() {
        // 如果有.NET回调对象，通知背景被点击
        if (this.dotNetHelper && typeof this.dotNetHelper.invokeMethodAsync === 'function') {
            this.dotNetHelper.invokeMethodAsync('OnSvgBackgroundClick');
        }
    }

    updateData(data) {
        this.nodes = data.nodes || [];
        this.links = data.links || [];
        
        // 更新力导向布局数据
        this.forceLayout.setData(this.nodes, this.links);
        
        // 渲染图表
        this.render();
        
        // 启动布局动画
        this.forceLayout.start(() => {
            this.updatePositions();
        });
    }

    render() {
        this.renderLinks();
        this.renderNodes();
    }

    renderLinks() {
        // 绑定数据
        this.linkElements = this.linkLayer
            .selectAll('.domain-link')
            .data(this.links, d => `${d.source.id || d.source}-${d.target.id || d.target}`);

        // 移除旧元素
        this.linkElements.exit().remove();

        // 创建新元素
        const linkEnter = this.linkElements.enter()
            .append('line')
            .attr('class', 'domain-link')
            .style('opacity', 0);

        // 合并选择
        this.linkElements = linkEnter.merge(this.linkElements);

        // 设置样式
        const linkStyle = getModernLinkStyle(this.isDarkMode, false, this.markerIds);
        this.linkElements
            .transition()
            .duration(300)
            .style('opacity', 1)
            .attr('stroke', d => d.color || linkStyle.stroke)
            .attr('stroke-width', d => Math.max(2, Math.min(6, d.serviceCount || 1)))
            .attr('stroke-opacity', linkStyle.strokeOpacity)
            .attr('marker-end', linkStyle.markerEnd)
            .style('filter', linkStyle.filter);

        // 添加交互
        this.linkElements
            .style('cursor', 'pointer')
            .on('mouseenter', (event, d) => {
                this.highlightLink(d, true);
                this.showTooltip(event, `${d.source.name || d.source} → ${d.target.name || d.target}<br/>服务数: ${d.serviceCount || 1}`);
            })
            .on('mouseleave', (event, d) => {
                this.highlightLink(d, false);
                this.hideTooltip();
            });
    }

    renderNodes() {
        // 绑定数据
        this.nodeElements = this.nodeLayer
            .selectAll('.domain-node')
            .data(this.nodes, d => d.id);

        // 移除旧元素
        this.nodeElements.exit().remove();

        // 创建新元素组
        const nodeEnter = this.nodeElements.enter()
            .append('g')
            .attr('class', 'domain-node')
            .style('opacity', 0);

        // 添加圆形节点
        nodeEnter.append('circle')
            .attr('class', 'node-circle')
            .attr('r', 0);

        // 添加文本标签（移动到节点下方）
        nodeEnter.append('text')
            .attr('class', 'node-text')
            .attr('dy', '50px')
            .attr('text-anchor', 'middle')
            .style('font-size', '0px');

        // 合并选择
        this.nodeElements = nodeEnter.merge(this.nodeElements);

        // 动画显示
        this.nodeElements
            .transition()
            .duration(500)
            .style('opacity', 1);

        // 更新圆形节点
        this.nodeElements.select('.node-circle')
            .transition()
            .duration(500)
            .attr('r', 30)
            .attr('fill', d => d.color)
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 2)
            .style('filter', 'drop-shadow(0 2px 8px rgba(0,0,0,0.15))');

        // 更新文本
        this.nodeElements.select('.node-text')
            .transition()
            .duration(500)
            .style('font-size', '14px')
            .style('font-weight', '500')
            .style('text-shadow', '0 1px 3px rgba(0,0,0,0.3)')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => this.truncateText(d.name, 15));

        // 添加拖拽行为
        this.nodeElements.call(this.forceLayout.getDragBehavior());

        // 添加交互事件
        this.nodeElements
            .style('cursor', 'pointer')
            .on('mouseenter', (event, d) => {
                this.highlightNode(d, true);
                this.showTooltip(event, this.buildDomainTooltipContent(d));
            })
            .on('mouseleave', (event, d) => {
                this.highlightNode(d, false);
                this.hideTooltip();
            })
            .on('click', (event, d) => {
                event.stopPropagation();
                this.handleNodeClick(d);
            })
            .on('contextmenu', (event, d) => {
                event.preventDefault();
                event.stopPropagation();
                this.handleNodeRightClick(d, event);
            });
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

    highlightNode(node, highlight) {
        const nodeElement = this.nodeElements.filter(d => d.id === node.id);
        const circle = nodeElement.select('.node-circle');
        
        if (highlight) {
            circle
                .transition()
                .duration(200)
                .attr('r', 35)
                .style('filter', 'drop-shadow(0 4px 12px rgba(0,0,0,0.25))');
        } else {
            circle
                .transition()
                .duration(200)
                .attr('r', 30)
                .style('filter', 'drop-shadow(0 2px 8px rgba(0,0,0,0.15))');
        }
    }

    highlightLink(link, highlight) {
        const linkElement = this.linkElements.filter(d => 
            (d.source.id || d.source) === (link.source.id || link.source) && 
            (d.target.id || d.target) === (link.target.id || link.target)
        );
        
        if (highlight) {
            linkElement
                .transition()
                .duration(200)
                .attr('stroke-width', d => Math.max(4, Math.min(8, (d.serviceCount || 1) + 2)))
                .attr('stroke-opacity', 1);
        } else {
            linkElement
                .transition()
                .duration(200)
                .attr('stroke-width', d => Math.max(2, Math.min(6, d.serviceCount || 1)))
                .attr('stroke-opacity', getModernLinkStyle(this.isDarkMode).strokeOpacity);
        }
    }

    showTooltip(event, content) {
        // 创建或更新tooltip
        let tooltip = d3.select('body').select('.domain-tooltip');
        if (tooltip.empty()) {
            tooltip = d3.select('body')
                .append('div')
                .attr('class', 'domain-tooltip')
                .style('position', 'absolute')
                .style('background', 'rgba(0,0,0,0.8)')
                .style('color', 'white')
                .style('padding', '8px 12px')
                .style('border-radius', '6px')
                .style('font-size', '12px')
                .style('pointer-events', 'none')
                .style('z-index', '10000')
                .style('opacity', 0);
        }

        tooltip
            .html(content)
            .style('left', (event.pageX + 10) + 'px')
            .style('top', (event.pageY - 10) + 'px')
            .transition()
            .duration(200)
            .style('opacity', 1);
    }

    hideTooltip() {
        d3.select('.domain-tooltip')
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
     * 处理节点点击事件
     * @param {Object} node - 被点击的节点数据
     */
    handleNodeClick(node) {
        // 先聚焦到节点
        this.focusOnNode(node.id);
        
        // 如果有.NET回调对象，调用域详情显示方法
        if (this.dotNetHelper && typeof this.dotNetHelper.invokeMethodAsync === 'function') {
            this.dotNetHelper.invokeMethodAsync('OnDomainClickFromJS', node.id || node.name);
        }
    }
    
    /**
     * 处理节点右键事件
     * @param {Object} node - 被右键点击的节点数据
     * @param {Event} event - 原始事件对象
     */
    handleNodeRightClick(node, event) {
        // 隐藏工具提示
        this.hideTooltip();
        
        // 如果有.NET回调对象，调用右键菜单显示方法
        if (this.dotNetHelper && typeof this.dotNetHelper.invokeMethodAsync === 'function') {
            // 获取相对于视口的坐标
            const x = event.clientX;
            const y = event.clientY;
            
            this.dotNetHelper.invokeMethodAsync('OnDomainRightClick', node.id || node.name, x, y);
        }
    }

    /**
     * 构建领域节点的tooltip内容
     * @param {Object} domain - 领域节点数据
     * @returns {string} HTML格式的tooltip内容
     */
    buildDomainTooltipContent(domain) {
        let content = `<strong>${domain.name}</strong>`;
        
        if (domain.description) {
            content += `<br/><span style="color: #ccc;">${domain.description}</span>`;
        }

        // 显示相关微服务信息
        if (domain.services && domain.services.length > 0) {
            content += `<br/><br/><strong>相关微服务 (${domain.services.length}个):</strong>`;
            
            // 按状态分组显示服务
            const servicesByStatus = this.groupServicesByStatus(domain.services);
            
            Object.entries(servicesByStatus).forEach(([status, services]) => {
                const statusText = this.getServiceStatusText(status);
                const statusColor = this.getServiceStatusColor(status);
                content += `<br/><span style="color: ${statusColor};">• ${statusText}: ${services.length}个</span>`;
                
                // 显示前3个服务名称
                if (services.length > 0) {
                    const serviceNames = services.slice(0, 3).map(s => s.name || s.appName).join(', ');
                    content += `<br/><span style="font-size: 11px; color: #aaa; margin-left: 12px;">${serviceNames}`;
                    if (services.length > 3) {
                        content += ` 等${services.length}个`;
                    }
                    content += `</span>`;
                }
            });
        } else {
            content += `<br/><span style="color: #999;">暂无相关微服务</span>`;
        }

        content += `<br/><br/><span style="font-size: 11px; color: #888;">点击查看详情</span>`;
        return content;
    }

    /**
     * 按状态分组服务
     * @param {Array} services - 服务列表
     * @returns {Object} 按状态分组的服务
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
     * 获取服务状态显示文本
     * @param {string} status - 服务状态
     * @returns {string} 状态显示文本
     */
    getServiceStatusText(status) {
        switch (status) {
            case 'Running': return '运行中';
            case 'Error': return '错误';
            case 'Updating': return '更新中';
            case 'Offline': return '离线';
            default: return '未知';
        }
    }

    /**
     * 获取服务状态颜色
     * @param {string} status - 服务状态
     * @returns {string} 状态颜色
     */
    getServiceStatusColor(status) {
        switch (status) {
            case 'Running': return '#4caf50';
            case 'Error': return '#f44336';
            case 'Updating': return '#ff9800';
            case 'Offline': return '#9e9e9e';
            default: return '#757575';
        }
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
            
            // 更新力导向布局中心
            if (this.forceLayout) {
                this.forceLayout.simulation
                    .force('center', d3.forceCenter(this.width / 2, this.height / 2));
                this.forceLayout.simulation.alpha(0.3).restart();
            }
        }
    }

    /**
     * 设置布局
     */
    setLayout(layoutType) {
        this.currentLayout = layoutType;
        
        // 移除之前的拖拽行为
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
     * 应用力导向布局
     */
    applyForceLayout() {
        // 释放所有固定节点
        this.forceLayout.releaseAllFixed(this.nodes);
        
        // 设置数据
        this.forceLayout.setData(this.nodes, this.links);
        
        // 应用拖拽行为
        if (this.nodeElements) {
            this.nodeElements.call(this.forceLayout.getDragBehavior());
        }
        
        // 启动模拟
        this.forceLayout.start(() => {
            this.updatePositions();
        });
    }
    
    /**
     * 应用层次布局
     */
    applyHierarchyLayout() {
        this.forceLayout.stop();
        
        // 计算层次布局
        this.layoutAlgorithms.hierarchicalLayout(this.nodes, this.links);
        
        // 应用静态拖拽
        this.applyStaticDrag();
        
        // 更新位置
        this.updateStaticPositions();
    }
    
    /**
     * 应用环形布局
     */
    applyCircularLayout() {
        this.forceLayout.stop();
        
        // 计算环形布局
        this.layoutAlgorithms.circularLayout(this.nodes, {
            avgNodeSize: 60,
            complexNodeCount: 0
        });
        
        // 应用静态拖拽
        this.applyStaticDrag();
        
        // 更新位置
        this.updateStaticPositions();
    }
    
    /**
     * 应用径向树布局
     */
    applyRadialTreeLayout() {
        this.forceLayout.stop();
        
        // 计算径向树布局
        this.layoutAlgorithms.radialTreeLayout(this.nodes, this.links, {
            radiusStep: 80
        });
        
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
                // 实时更新连接线
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
     * 更新静态位置
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
     * 设置力导向距离
     */
    setForceDistance(distance) {
        this.forceLayout.updateLinkDistance(distance);
    }
    
    /**
     * 设置力导向强度
     */
    setForceStrength(strength) {
        this.forceLayout.updateChargeStrength(strength);
    }

    dispose() {
        // 清理tooltip
        d3.select('.domain-tooltip').remove();
        
        // 停止力导向布局
        if (this.forceLayout) {
            this.forceLayout.dispose();
        }
        
        // 移除窗口事件监听器
        window.removeEventListener('resize', this.handleResize);
        
        // 调用基类的dispose
        super.dispose();
    }
}