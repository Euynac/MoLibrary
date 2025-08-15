/**
 * 微服务架构图可视化
 * 基于 d3.js 实现的微服务架构图表，支持状态告警闪烁效果
 * 
 * @module serviceGraph
 */

import { GraphBase, getModernLinkStyle, getModernNodeStyle, focusOnPosition } from '../../MoLibrary.UI/js/d3js/d3-graph-base.js';
import { ForceLayoutManager } from '../../MoLibrary.UI/js/d3js/d3-force-layout.js';

let graphInstance = null;

/**
 * 初始化微服务架构图
 * @param {string} containerId - 容器ID
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @param {Object} dotNetRef - .NET对象引用
 */
export function initializeGraph(containerId, isDarkMode = false, dotNetRef = null) {
    dispose();
    graphInstance = new ServiceGraph(containerId, { isDarkMode, dotNetRef });
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
 * 微服务架构图类
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
        this.animations = new Map(); // 存储动画定时器
        
        // 创建力导向布局管理器
        this.forceLayout = new ForceLayoutManager(this.width, this.height, {
            linkDistance: 150,
            chargeStrength: -500,
            collisionRadius: 50
        });

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
        this.handleResize = this.handleResize.bind(this);
        window.addEventListener('resize', this.handleResize);
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
            .selectAll('.service-link')
            .data(this.links, d => `${d.source.id || d.source}-${d.target.id || d.target}`);

        // 移除旧元素
        this.linkElements.exit().remove();

        // 创建新元素
        const linkEnter = this.linkElements.enter()
            .append('line')
            .attr('class', 'service-link')
            .style('opacity', 0);

        // 合并选择
        this.linkElements = linkEnter.merge(this.linkElements);

        // 设置样式
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
        // 绑定数据
        this.nodeElements = this.nodeLayer
            .selectAll('.service-node')
            .data(this.nodes, d => d.id);

        // 移除旧元素
        const exitSelection = this.nodeElements.exit();
        exitSelection.each(d => this.stopAnimation(d.id));
        exitSelection.remove();

        // 创建新元素组
        const nodeEnter = this.nodeElements.enter()
            .append('g')
            .attr('class', 'service-node')
            .style('opacity', 0);

        // 添加外圆环（用于闪烁效果）
        nodeEnter.append('circle')
            .attr('class', 'node-ring')
            .attr('r', 0)
            .attr('fill', 'none')
            .attr('stroke-width', 3)
            .style('opacity', 0);

        // 添加主节点圆形
        nodeEnter.append('circle')
            .attr('class', 'node-circle')
            .attr('r', 0);

        // 添加状态指示器
        nodeEnter.append('circle')
            .attr('class', 'status-indicator')
            .attr('r', 6)
            .attr('cx', 20)
            .attr('cy', -20);

        // 添加实例数量文本
        nodeEnter.append('text')
            .attr('class', 'instance-count')
            .attr('dy', '0.35em')
            .attr('text-anchor', 'middle')
            .style('font-size', '10px')
            .style('font-weight', 'bold');

        // 添加服务名称文本
        nodeEnter.append('text')
            .attr('class', 'node-text')
            .attr('dy', '45px')
            .attr('text-anchor', 'middle')
            .style('font-size', '0px');

        // 合并选择
        this.nodeElements = nodeEnter.merge(this.nodeElements);

        // 动画显示
        this.nodeElements
            .transition()
            .duration(500)
            .style('opacity', 1);

        // 更新主圆形节点
        this.nodeElements.select('.node-circle')
            .transition()
            .duration(500)
            .attr('r', 25)
            .attr('fill', d => d.color)
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 2)
            .style('filter', 'drop-shadow(0 2px 8px rgba(0,0,0,0.15))');

        // 更新外圆环
        this.nodeElements.select('.node-ring')
            .attr('r', 30)
            .attr('stroke', d => this.getStatusColor(d.status));

        // 更新状态指示器
        this.nodeElements.select('.status-indicator')
            .attr('fill', d => this.getStatusColor(d.status))
            .attr('stroke', getModernNodeStyle(this.isDarkMode).strokeColor)
            .attr('stroke-width', 1);

        // 更新实例数量
        this.nodeElements.select('.instance-count')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => `${d.runningInstances}/${d.totalInstances}`);

        // 更新服务名称
        this.nodeElements.select('.node-text')
            .transition()
            .duration(500)
            .style('font-size', '12px')
            .attr('fill', getModernNodeStyle(this.isDarkMode).textColor)
            .text(d => this.truncateText(d.name, 15));

        // 添加拖拽行为
        this.nodeElements.call(this.forceLayout.getDragBehavior());

        // 添加交互事件
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
                if (this.dotNetRef) {
                    await this.dotNetRef.invokeMethodAsync('OnNodeClick', d.id);
                }
            });

        // 启动状态动画
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
        switch (status) {
            case 'Running':
                return '#4CAF50'; // 绿色
            case 'Updating':
                return '#FF9800'; // 橙色/黄色
            case 'Offline':
                return '#424242'; // 深灰色/黑色
            case 'Error':
                return '#F44336'; // 红色
            default:
                return '#9E9E9E'; // 灰色
        }
    }

    startStatusAnimation(nodeData) {
        const nodeElement = this.nodeElements.filter(d => d.id === nodeData.id);
        const ring = nodeElement.select('.node-ring');
        const statusIndicator = nodeElement.select('.status-indicator');

        // 停止之前的动画
        this.stopAnimation(nodeData.id);

        switch (nodeData.status) {
            case 'Running':
                // 绿色常亮，无闪烁
                statusIndicator.style('opacity', 1);
                ring.style('opacity', 0);
                break;

            case 'Error':
                // 红色闪烁
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, '#F44336', 500);
                break;

            case 'Offline':
                // 黑色闪烁
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, '#424242', 800);
                break;

            case 'Updating':
                // 黄色闪烁
                this.startBlinkAnimation(nodeData.id, ring, statusIndicator, '#FF9800', 600);
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
            
            // 保存定时器ID
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
                .style('filter', 'drop-shadow(0 4px 12px rgba(0,0,0,0.25))');
        } else {
            circle
                .transition()
                .duration(200)
                .attr('r', 25)
                .style('filter', 'drop-shadow(0 2px 8px rgba(0,0,0,0.15))');
        }
    }

    buildTooltipContent(node) {
        const statusText = this.getStatusText(node.status);
        let content = `<strong>${node.name}</strong><br/>`;
        content += `<strong>AppId:</strong> ${node.id}<br/>`;
        content += `<strong>域:</strong> ${node.domain}<br/>`;
        if (node.project) content += `<strong>项目:</strong> ${node.project}<br/>`;
        content += `<strong>状态:</strong> ${statusText}<br/>`;
        content += `<strong>实例:</strong> ${node.runningInstances}/${node.totalInstances}`;
        
        if (node.instanceInfo) {
            content += `<br/><strong>版本:</strong> ${node.instanceInfo.registerInfo?.assemblyVersion || 'N/A'}`;
        }
        
        return content;
    }

    getStatusText(status) {
        switch (status) {
            case 'Running': return '运行中';
            case 'Updating': return '更新中';
            case 'Offline': return '离线';
            case 'Error': return '异常';
            default: return '未知';
        }
    }

    showTooltip(event, content) {
        // 创建或更新tooltip
        let tooltip = d3.select('body').select('.service-tooltip');
        if (tooltip.empty()) {
            tooltip = d3.select('body')
                .append('div')
                .attr('class', 'service-tooltip')
                .style('position', 'absolute')
                .style('background', 'rgba(0,0,0,0.9)')
                .style('color', 'white')
                .style('padding', '10px 15px')
                .style('border-radius', '8px')
                .style('font-size', '12px')
                .style('line-height', '1.4')
                .style('pointer-events', 'none')
                .style('z-index', '10000')
                .style('box-shadow', '0 4px 12px rgba(0,0,0,0.3)')
                .style('max-width', '250px')
                .style('opacity', 0);
        }

        tooltip
            .html(content)
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 10) + 'px')
            .transition()
            .duration(200)
            .style('opacity', 1);
    }

    hideTooltip() {
        d3.select('.service-tooltip')
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
            
            // 更新力导向布局中心
            if (this.forceLayout) {
                this.forceLayout.simulation
                    .force('center', d3.forceCenter(this.width / 2, this.height / 2));
                this.forceLayout.simulation.alpha(0.3).restart();
            }
        }
    }

    dispose() {
        // 停止所有动画
        this.animations.forEach((timerId, nodeId) => {
            clearTimeout(timerId);
        });
        this.animations.clear();
        
        // 清理tooltip
        d3.select('.service-tooltip').remove();
        
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