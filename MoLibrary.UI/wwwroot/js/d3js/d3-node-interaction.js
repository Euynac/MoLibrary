/**
 * D3.js 节点交互模块
 * 提供节点悬停、点击、高亮等交互功能
 * 
 * @module d3-node-interaction
 */

import { getModernLinkStyle } from './d3-graph-base.js';

/**
 * 节点高亮管理器
 */
export class NodeHighlightManager {
    constructor(options = {}) {
        this.highlightedNodes = new Set();
        this.highlightedLinks = new Set();
        this.fadeOpacity = options.fadeOpacity || 0.2;
        this.normalOpacity = options.normalOpacity || 1;
        this.highlightStrokeWidth = options.highlightStrokeWidth || 3;
        this.normalStrokeWidth = options.normalStrokeWidth || 2;
        this.isDarkMode = options.isDarkMode || false;
        
        // 保存marker IDs
        this.markerIds = options.markerIds || null;
        
        // 获取现代化样式配置，传入marker IDs
        this.normalLinkStyle = getModernLinkStyle(this.isDarkMode, false, this.markerIds);
        this.highlightLinkStyle = getModernLinkStyle(this.isDarkMode, true, this.markerIds);
    }
    
    /**
     * 高亮节点及其相关连接
     * @param {string} nodeId - 节点ID
     * @param {Array} nodes - 所有节点
     * @param {Array} links - 所有连接
     * @param {Object} nodeSelection - D3 节点选择
     * @param {Object} linkSelection - D3 连接选择
     */
    highlightNode(nodeId, nodes, links, nodeSelection, linkSelection) {
        // 清除之前的高亮
        this.clearHighlight(nodeSelection, linkSelection);
        
        // 找到相关的节点和连接，并记录方向
        const relatedNodes = new Set([nodeId]);  // 包含当前节点
        const outgoingLinks = new Set(); // 出边（当前节点 -> 其他节点）
        const incomingLinks = new Set(); // 入边（其他节点 -> 当前节点）
        
        links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            if (sourceId === nodeId) {
                relatedNodes.add(targetId);
                outgoingLinks.add(link); // 出边
            } else if (targetId === nodeId) {
                relatedNodes.add(sourceId);
                incomingLinks.add(link); // 入边
            }
        });
        
        // 应用高亮效果，传递方向信息
        this.applyHighlight(relatedNodes, outgoingLinks, incomingLinks, nodeSelection, linkSelection);
    }
    
    /**
     * 应用高亮效果
     */
    applyHighlight(relatedNodes, outgoingLinks, incomingLinks, nodeSelection, linkSelection) {
        const self = this;
        
        // 高亮节点 - 所有相关节点使用相同的不透明度
        nodeSelection.each(function(d) {
            const node = d3.select(this);
            const isRelated = relatedNodes.has(d.id);
            
            // 控制节点主体元素透明度和边框
            node.select('circle, rect.card-background')
                .transition()
                .duration(200)
                .attr('opacity', isRelated ? 1 : self.fadeOpacity)
                .attr('stroke-width', isRelated ? self.highlightStrokeWidth : self.normalStrokeWidth);
            
            // 对于复杂节点，增强阴影效果
            if (isRelated && d.isComplex) {
                const shadowFilter = node.select('filter feDropShadow');
                if (!shadowFilter.empty()) {
                    shadowFilter
                        .transition()
                        .duration(200)
                        .attr('stdDeviation', 5)
                        .attr('flood-opacity', 0.25);
                }
            }
            
            // 控制所有文本元素透明度（包括标题、依赖数量、chip文字等）
            node.selectAll('text')
                .transition()
                .duration(200)
                .attr('opacity', isRelated ? 1 : self.fadeOpacity);
                
            // 控制复杂节点内所有矩形元素透明度（包括卡片背景、标题栏、状态栏、chip背景等）
            node.selectAll('rect')
                .transition()
                .duration(200)
                .attr('opacity', isRelated ? 1 : self.fadeOpacity);
        });
        
        // 高亮连接 - 根据方向使用不同颜色
        linkSelection.each(function(d) {
            const link = d3.select(this);
            const isOutgoing = outgoingLinks.has(d);
            const isIncoming = incomingLinks.has(d);
            const isRelated = isOutgoing || isIncoming;
            
            if (isRelated) {
                const style = self.highlightLinkStyle;
                // 使用 MudBlazor 颜色系统变量
                // 出边使用 Info 色系，入边使用 Success 色系
                const strokeColor = isOutgoing ? 
                    (self.isDarkMode ? 'var(--mud-palette-info-lighten, #29B6F6)' : 'var(--mud-palette-info, #1976D2)') : // 出边：Info色
                    (self.isDarkMode ? 'var(--mud-palette-success-lighten, #66BB6A)' : 'var(--mud-palette-success, #43A047)');  // 入边：Success色
                
                link.transition()
                    .duration(200)
                    .attr('opacity', style.strokeOpacity)
                    .attr('stroke-width', style.strokeWidth)
                    .attr('stroke', strokeColor)
                    .attr('marker-end', isOutgoing ? 
                        `url(#${self.markerIds?.outgoingMarkerId || 'arrow-highlight-outgoing'})` : 
                        `url(#${self.markerIds?.incomingMarkerId || 'arrow-highlight-incoming'})`)
                    .style('filter', style.filter);
            } else {
                link.transition()
                    .duration(200)
                    .attr('opacity', self.fadeOpacity)
                    .attr('stroke-width', self.normalLinkStyle.strokeWidth)
                    .attr('stroke', self.normalLinkStyle.stroke)
                    .attr('marker-end', self.normalLinkStyle.markerEnd)
                    .style('filter', self.normalLinkStyle.filter);
            }
        });
        
        // 合并出边和入边
        const allRelatedLinks = new Set([...outgoingLinks, ...incomingLinks]);
        this.highlightedNodes = relatedNodes;
        this.highlightedLinks = allRelatedLinks;
    }
    
    /**
     * 清除高亮
     */
    clearHighlight(nodeSelection, linkSelection) {
        const self = this;
        
        // 恢复节点 - 确保所有节点恢复为完全不透明
        nodeSelection.each(function(d) {
            const node = d3.select(this);
            
            node.select('circle, rect.card-background')
                .transition()
                .duration(200)
                .attr('opacity', 1)  // 恢复为完全不透明
                .attr('stroke-width', self.normalStrokeWidth);
            
            // 对于复杂节点，恢复正常阴影
            if (d && d.isComplex) {
                const shadowFilter = node.select('filter feDropShadow');
                if (!shadowFilter.empty()) {
                    shadowFilter
                        .transition()
                        .duration(200)
                        .attr('stdDeviation', 3)
                        .attr('flood-opacity', 0.15);
                }
            }
            
            // 恢复所有文本元素透明度
            node.selectAll('text')
                .transition()
                .duration(200)
                .attr('opacity', 1);
                
            // 恢复复杂节点内所有矩形元素透明度
            node.selectAll('rect')
                .transition()
                .duration(200)
                .attr('opacity', 1);
        });
        
        // 恢复连接 - 使用现代化样式恢复
        linkSelection
            .transition()
            .duration(200)
            .attr('opacity', self.normalLinkStyle.strokeOpacity)
            .attr('stroke-width', self.normalLinkStyle.strokeWidth)
            .attr('stroke', self.normalLinkStyle.stroke)
            .attr('marker-end', self.normalLinkStyle.markerEnd)
            .style('filter', self.normalLinkStyle.filter);
        
        this.highlightedNodes.clear();
        this.highlightedLinks.clear();
    }
}

/**
 * 节点交互处理器
 */
export class NodeInteractionHandler {
    constructor(options = {}) {
        this.onClick = options.onClick;
        this.onRightClick = options.onRightClick;
        this.onHover = options.onHover;
        this.onHoverOut = options.onHoverOut;
        this.onDoubleClick = options.onDoubleClick;
        // 传递 isDarkMode 和 markerIds 给 highlightManager
        const highlightOptions = options.highlightOptions || {};
        highlightOptions.isDarkMode = options.isDarkMode;
        highlightOptions.markerIds = options.markerIds; // 传递marker IDs
        this.highlightManager = new NodeHighlightManager(highlightOptions);
    }
    
    /**
     * 绑定节点交互事件
     * @param {Object} nodeSelection - D3 节点选择
     * @param {Object} context - 上下文对象，包含 nodes, links, linkSelection
     */
    bindNodeEvents(nodeSelection, context) {
        const self = this;
        
        nodeSelection
            .on('click', function(event, d) {
                event.stopPropagation();
                
                // 双击处理
                if (event.detail === 2) {
                    if (self.onDoubleClick) {
                        self.onDoubleClick.call(this, event, d);
                    }
                } else if (self.onClick) {
                    self.onClick.call(this, event, d);
                }
            })
            .on('contextmenu', function(event, d) {
                event.preventDefault();
                event.stopPropagation();
                
                if (self.onRightClick) {
                    // 获取节点在页面中的实际位置
                    const transform = d3.select(this).attr('transform');
                    const matrix = this.getCTM();
                    const pt = this.ownerSVGElement.createSVGPoint();
                    pt.x = d.x || 0;
                    pt.y = d.y || 0;
                    const screenPt = pt.matrixTransform(matrix);
                    
                    // 使用原生事件对象或D3事件对象
                    const nativeEvent = event.sourceEvent || event;
                    
                    self.onRightClick.call(this, event, d, {
                        x: screenPt.x,
                        y: screenPt.y,
                        pageX: nativeEvent.pageX,
                        pageY: nativeEvent.pageY,
                        clientX: nativeEvent.clientX,
                        clientY: nativeEvent.clientY
                    });
                }
            })
            .on('mouseenter', function(event, d) {
                // 高亮相关节点和连接
                if (context && context.nodes && context.links && context.linkSelection) {
                    self.highlightManager.highlightNode(
                        d.id,
                        context.nodes,
                        context.links,
                        nodeSelection,
                        context.linkSelection
                    );
                }
                
                if (self.onHover) {
                    self.onHover.call(this, event, d);
                }
            })
            .on('mouseleave', function(event, d) {
                // 清除高亮
                if (context && context.linkSelection) {
                    self.highlightManager.clearHighlight(
                        nodeSelection,
                        context.linkSelection
                    );
                }
                
                if (self.onHoverOut) {
                    self.onHoverOut.call(this, event, d);
                }
            });
    }
    
    /**
     * 清除所有高亮
     */
    clearAllHighlights(nodeSelection, linkSelection) {
        this.highlightManager.clearHighlight(nodeSelection, linkSelection);
    }
}

/**
 * 创建通用拖拽行为（适用于非力导向布局）
 */
export function createStaticDragBehavior(options = {}) {
    let startX, startY;
    
    return d3.drag()
        .on('start', function(event, d) {
            startX = d.x;
            startY = d.y;
            
            d3.select(this).raise(); // 将节点提到最前
            
            if (options.onStart) {
                options.onStart.call(this, event, d);
            }
        })
        .on('drag', function(event, d) {
            d.x = event.x;
            d.y = event.y;
            
            // 更新节点位置
            d3.select(this)
                .attr('transform', `translate(${d.x},${d.y})`);
            
            // 更新相关连接线
            if (options.updateLinks) {
                options.updateLinks(d);
            }
            
            if (options.onDrag) {
                options.onDrag.call(this, event, d);
            }
        })
        .on('end', function(event, d) {
            // 可选：添加吸附到网格的功能
            if (options.snapToGrid) {
                const gridSize = options.gridSize || 10;
                d.x = Math.round(d.x / gridSize) * gridSize;
                d.y = Math.round(d.y / gridSize) * gridSize;
                
                d3.select(this)
                    .transition()
                    .duration(200)
                    .attr('transform', `translate(${d.x},${d.y})`);
            }
            
            if (options.onEnd) {
                options.onEnd.call(this, event, d);
            }
        });
}