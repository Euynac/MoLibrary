/**
 * 模块依赖关系图可视化
 * 使用模块化的 D3.js 组件
 * 
 * @module module-dependency-graph
 */

import { GraphBase, getModernLinkStyle, getModernNodeStyle } from './d3js/d3-graph-base.js';
import { ForceLayoutManager } from './d3js/d3-force-layout.js';
import { NodeInteractionHandler, createStaticDragBehavior } from './d3js/d3-node-interaction.js';
import { createLayoutAlgorithms } from './d3js/d3-layout-algorithms.js';

/**
 * 模块依赖图类
 */
class ModuleDependencyGraph {
    constructor(containerId) {
        this.containerId = containerId;
        this.container = document.getElementById(containerId);
        
        if (!this.container) {
            throw new Error(`Container with ID '${containerId}' not found`);
        }
        
        // 初始化基础图形
        this.graphBase = new GraphBase(containerId, {
            showArrows: true
        });
        
        // 初始化力导向布局管理器
        this.forceManager = new ForceLayoutManager(
            this.graphBase.width,
            this.graphBase.height,
            {
                linkDistance: 120,
                chargeStrength: -400,
                keepFixed: true // 保持拖拽后的位置
            }
        );
        
        // 初始化布局算法
        this.layoutAlgorithms = createLayoutAlgorithms(
            this.graphBase.width,
            this.graphBase.height
        );
        
        // 初始化交互处理器
        this.interactionHandler = new NodeInteractionHandler({
            onClick: (event, d) => this.handleNodeClick(d),
            onRightClick: (event, d, position) => this.handleNodeRightClick(d, position),
            onHover: (event, d) => this.showNodeInfo(d),
            onLeave: (event, d) => this.hideNodeInfo(),
            highlightOptions: {
                fadeOpacity: 0.3,
                normalOpacity: 0.8
            },
            markerIds: this.graphBase.markerIds
        });
        
        // 节点样式
        this.nodeStyle = getModernNodeStyle(false, 'simple');
        
        // 当前布局类型
        this.currentLayout = 'force';
    }
    
    /**
     * 获取节点颜色 - 使用MudBlazor CSS变量
     */
    getNodeColorByType(d) {
        // 基于节点状态返回CSS变量
        if (d.isPartOfCycle) return 'var(--mud-palette-error)'; // 循环依赖 - 错误色
        if (d.isDisabled) return 'var(--mud-palette-warning)'; // 禁用 - 警告色
        return 'var(--mud-palette-primary)'; // 正常 - 主色
    }
    
    /**
     * 获取边颜色 - 使用MudBlazor CSS变量
     */
    getEdgeColorByType(type) {
        switch (type) {
            case 'Direct': return 'var(--mud-palette-success)'; // 直接依赖 - 成功色
            case 'Transitive': return 'var(--mud-palette-secondary)'; // 传递依赖 - 次要色
            case 'Circular': return 'var(--mud-palette-error)'; // 循环依赖 - 错误色
            default: return 'var(--mud-palette-text-disabled)'; // 默认 - 禁用文本色
        }
    }
    
    /**
     * 初始化数据
     */
    initialize(nodes, edges) {
        this.nodes = nodes;
        this.edges = edges;
        
        // 清空现有内容
        this.graphBase.mainGroup.selectAll('.links').remove();
        this.graphBase.mainGroup.selectAll('.nodes').remove();
        this.graphBase.mainGroup.selectAll('.labels').remove();
        
        // 创建组
        const linkGroup = this.graphBase.mainGroup.append('g').attr('class', 'links');
        const nodeGroup = this.graphBase.mainGroup.append('g').attr('class', 'nodes');
        const labelGroup = this.graphBase.mainGroup.append('g').attr('class', 'labels');
        
        // 绘制连接线 - 使用path而不是line以支持箭头
        const linkStyle = getModernLinkStyle(false, false, this.graphBase.markerIds);
        this.linkSelection = linkGroup.selectAll('path')
            .data(edges)
            .enter().append('path')
            .attr('class', 'link')
            .attr('stroke', d => this.getEdgeColorByType(d.dependencyType))
            .attr('stroke-width', d => d.isPartOfCycle ? 3 : 2)
            .attr('stroke-dasharray', d => d.dependencyType === 'Transitive' ? '5,5' : null)
            .attr('fill', 'none')
            .attr('marker-end', linkStyle.markerEnd)
            .style('opacity', 0.8);
        
        // 绘制节点
        this.nodeSelection = nodeGroup.selectAll('circle')
            .data(nodes)
            .enter().append('circle')
            .attr('r', 25)
            .attr('fill', d => this.getNodeColorByType(d))
            .attr('stroke', 'var(--mud-palette-divider)')
            .attr('stroke-width', 2)
            .style('cursor', 'move');
        
        // 绘制标签
        this.labelSelection = labelGroup.selectAll('text')
            .data(nodes)
            .enter().append('text')
            .text(d => d.label)
            .attr('font-size', 12)
            .attr('text-anchor', 'middle')
            .attr('dy', 40)
            .style('fill', 'var(--mud-palette-text-primary)')
            .style('font-weight', 'bold')
            .style('pointer-events', 'none')
            .style('user-select', 'none');
        
        // 绑定交互事件
        this.interactionHandler.bindNodeEvents(this.nodeSelection, {
            nodes: this.nodes,
            links: this.edges,
            linkSelection: this.linkSelection
        });
        
        // 应用初始布局
        this.applyLayout(this.currentLayout);
    }
    
    
    /**
     * 应用布局
     */
    applyLayout(layoutType) {
        this.currentLayout = layoutType;
        
        // 停止之前的布局
        this.forceManager.stop();
        
        switch (layoutType) {
            case 'force':
                this.applyForceLayout();
                break;
            case 'hierarchical':
                this.applyHierarchicalLayout();
                break;
            case 'circular':
                this.applyCircularLayout();
                break;
            case 'tree':
                this.applyTreeLayout();
                break;
            default:
                this.applyForceLayout();
        }
    }
    
    /**
     * 应用力导向布局
     */
    applyForceLayout() {
        // 释放固定节点
        this.forceManager.releaseAllFixed(this.nodes);
        
        // 设置数据
        this.forceManager.setData(this.nodes, this.edges);
        
        // 应用拖拽
        this.nodeSelection.call(this.forceManager.getDragBehavior());
        
        // 启动模拟
        this.forceManager.start(() => {
            // 使用path的d属性绘制连接线
            this.linkSelection
                .attr('d', d => {
                    const dx = d.target.x - d.source.x;
                    const dy = d.target.y - d.source.y;
                    const distance = Math.sqrt(dx * dx + dy * dy);
                    
                    if (distance === 0) return '';
                    
                    const normX = dx / distance;
                    const normY = dy / distance;
                    const arrowOffset = 30; // 节点半径 + 箭头间距
                    
                    const endX = d.target.x - normX * arrowOffset;
                    const endY = d.target.y - normY * arrowOffset;
                    return `M${d.source.x},${d.source.y} L${endX},${endY}`;
                });
            
            this.nodeSelection
                .attr('cx', d => d.x)
                .attr('cy', d => d.y);
            
            this.labelSelection
                .attr('x', d => d.x)
                .attr('y', d => d.y);
        });
    }
    
    /**
     * 应用层次布局
     */
    applyHierarchicalLayout() {
        this.layoutAlgorithms.hierarchicalLayout(this.nodes, this.edges);
        this.applyStaticLayout();
    }
    
    /**
     * 应用环形布局
     */
    applyCircularLayout() {
        this.layoutAlgorithms.circularLayout(this.nodes);
        
        // 固定所有节点位置（环形布局通常是固定的）
        this.nodes.forEach(node => {
            node.fx = node.x;
            node.fy = node.y;
        });
        
        this.applyStaticLayout();
    }
    
    /**
     * 应用树形布局
     */
    applyTreeLayout() {
        this.layoutAlgorithms.treeLayout(this.nodes, this.edges, {
            orientation: 'vertical'
        });
        this.applyStaticLayout();
    }
    
    /**
     * 应用静态布局
     */
    applyStaticLayout() {
        // 创建静态拖拽行为
        const staticDrag = d3.drag()
            .on('start', (event, d) => {
                d3.select(event.sourceEvent.target).style('cursor', 'grabbing');
            })
            .on('drag', (event, d) => {
                // 更新节点数据位置
                d.x = event.x;
                d.y = event.y;
                
                // 立即更新被拖拽节点的位置
                this.nodeSelection
                    .filter(node => node.id === d.id)
                    .attr('cx', d.x)
                    .attr('cy', d.y);
                
                // 更新标签位置
                this.labelSelection
                    .filter(node => node.id === d.id)
                    .attr('x', d.x)
                    .attr('y', d.y);
                
                // 更新连接线
                this.linkSelection
                    .attr('d', link => {
                        const sourceId = link.source.id || link.source;
                        const targetId = link.target.id || link.target;
                        
                        const source = sourceId === d.id ? d : 
                                       this.nodes.find(n => n.id === sourceId);
                        const target = targetId === d.id ? d : 
                                       this.nodes.find(n => n.id === targetId);
                        
                        if (!source || !target) return '';
                        
                        const dx = target.x - source.x;
                        const dy = target.y - source.y;
                        const distance = Math.sqrt(dx * dx + dy * dy);
                        
                        if (distance === 0) return '';
                        
                        const normX = dx / distance;
                        const normY = dy / distance;
                        const arrowOffset = 30;
                        
                        const endX = target.x - normX * arrowOffset;
                        const endY = target.y - normY * arrowOffset;
                        return `M${source.x},${source.y} L${endX},${endY}`;
                    });
            })
            .on('end', (event, d) => {
                d3.select(event.sourceEvent.target).style('cursor', 'move');
            });
        
        this.nodeSelection.call(staticDrag);
        
        // 应用位置
        this.updatePositions();
    }
    
    /**
     * 更新位置
     */
    updatePositions() {
        this.nodeSelection
            .transition()
            .duration(750)
            .attr('cx', d => d.x)
            .attr('cy', d => d.y);
        
        this.labelSelection
            .transition()
            .duration(750)
            .attr('x', d => d.x)
            .attr('y', d => d.y);
        
        this.linkSelection
            .transition()
            .duration(750)
            .attr('d', d => {
                const source = this.nodes.find(n => n.id === (d.source.id || d.source));
                const target = this.nodes.find(n => n.id === (d.target.id || d.target));
                
                if (!source || !target) return '';
                
                const dx = target.x - source.x;
                const dy = target.y - source.y;
                const distance = Math.sqrt(dx * dx + dy * dy);
                
                if (distance === 0) return '';
                
                const normX = dx / distance;
                const normY = dy / distance;
                const arrowOffset = 30;
                
                const endX = target.x - normX * arrowOffset;
                const endY = target.y - normY * arrowOffset;
                return `M${source.x},${source.y} L${endX},${endY}`;
            });
    }
    
    /**
     * 应用过滤器
     */
    applyFilter(filter) {
        let visibleEdges = this.edges;
        
        switch (filter) {
            case 'direct':
                visibleEdges = this.edges.filter(e => e.dependencyType === 'Direct');
                break;
            case 'cycle':
                visibleEdges = this.edges.filter(e => e.isPartOfCycle);
                break;
            case 'all':
            default:
                visibleEdges = this.edges;
                break;
        }
        
        // 更新边的可见性
        this.linkSelection.style('display', d => visibleEdges.includes(d) ? 'block' : 'none');
        
        // 更新节点的可见性
        const visibleNodeIds = new Set();
        visibleEdges.forEach(edge => {
            visibleNodeIds.add(edge.source.id || edge.source);
            visibleNodeIds.add(edge.target.id || edge.target);
        });
        
        this.nodeSelection.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
        this.labelSelection.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
    }
    
    /**
     * 处理节点点击
     */
    handleNodeClick(nodeData) {
        console.log('Node clicked:', nodeData);
    }
    
    /**
     * 处理节点右键
     */
    handleNodeRightClick(nodeData, position) {
        console.log('Node right-clicked:', nodeData, position);
    }
    
    /**
     * 显示节点信息
     */
    showNodeInfo(d) {
        const connected = this.edges.filter(edge => 
            (edge.source.id || edge.source) === d.id || 
            (edge.target.id || edge.target) === d.id
        );
        
        const directDependencies = connected.filter(edge => 
            edge.dependencyType === 'Direct' && (edge.source.id || edge.source) === d.id
        );
        
        const transitiveDependencies = connected.filter(edge => 
            edge.dependencyType === 'Transitive' && (edge.source.id || edge.source) === d.id
        );
        
        const dependedBy = connected.filter(edge => (edge.target.id || edge.target) === d.id);
        
        const info = {
            module: d.label,
            directDependencies: directDependencies.length,
            transitiveDependencies: transitiveDependencies.length,
            dependedBy: dependedBy.length,
            isPartOfCycle: d.isPartOfCycle,
            isDisabled: d.isDisabled
        };
        
        this.updateNodeInfoDisplay(info);
    }
    
    /**
     * 隐藏节点信息
     */
    hideNodeInfo() {
        this.updateNodeInfoDisplay(null);
    }
    
    /**
     * 更新节点信息显示
     */
    updateNodeInfoDisplay(info) {
        const nodeDetailElement = document.querySelector('.node-detail-content');
        if (nodeDetailElement) {
            if (info) {
                nodeDetailElement.innerHTML = `
                    <div class="node-info">
                        <h4 style="margin: 0 0 12px 0; color: var(--mud-palette-primary);">${info.module}</h4>
                        <div style="margin-bottom: 8px;">
                            <strong>直接依赖:</strong> ${info.directDependencies}
                        </div>
                        <div style="margin-bottom: 8px;">
                            <strong>传递依赖:</strong> ${info.transitiveDependencies}
                        </div>
                        <div style="margin-bottom: 8px;">
                            <strong>被依赖:</strong> ${info.dependedBy}
                        </div>
                        <div style="margin-bottom: 8px;">
                            <strong>循环依赖:</strong> ${info.isPartOfCycle ? '是' : '否'}
                        </div>
                        <div>
                            <strong>状态:</strong> ${info.isDisabled ? '禁用' : '启用'}
                        </div>
                    </div>
                `;
            } else {
                nodeDetailElement.innerHTML = `
                    <div class="d-flex align-center justify-center" style="height: 100%; color: var(--mud-palette-text-secondary);">
                        <div class="text-center">
                            <div style="font-size: 3rem; margin-bottom: 12px;">
                                <i class="fas fa-mouse-pointer"></i>
                            </div>
                            <div>鼠标悬停在节点上查看详情</div>
                        </div>
                    </div>
                `;
            }
        }
    }
    
    
    /**
     * 缩放控制
     */
    zoomIn() {
        this.graphBase.zoomIn();
    }
    
    zoomOut() {
        this.graphBase.zoomOut();
    }
    
    resetZoom() {
        this.graphBase.resetView();
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

// 导出函数
let graphInstance = null;

/**
 * 初始化依赖关系图
 */
export async function initializeDependencyGraph(containerId, nodes, edges) {
    try {
        graphInstance = new ModuleDependencyGraph(containerId);
        graphInstance.initialize(nodes, edges);
        console.log('Dependency graph initialized successfully');
    } catch (error) {
        console.error('Failed to initialize dependency graph:', error);
        throw error;
    }
}

/**
 * 改变布局
 */
export async function changeLayout(layout) {
    if (graphInstance) {
        graphInstance.applyLayout(layout);
    }
}

/**
 * 应用过滤器
 */
export async function applyFilter(filter) {
    if (graphInstance) {
        graphInstance.applyFilter(filter);
    }
}

/**
 * 缩放控制
 */
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
        graphInstance.resetZoom();
    }
}


/**
 * 导出图片
 */
export async function exportGraph(filename) {
    // TODO: 实现导出功能
    console.log('Export graph to:', filename);
}

console.log('Module dependency graph module loaded');