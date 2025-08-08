/**
 * D3.js 节点交互模块
 * 提供节点悬停、点击、高亮等交互功能
 * 
 * @module d3-node-interaction
 */

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
        
        // 找到相关的节点和连接
        const relatedNodes = new Set([nodeId]);
        const relatedLinks = new Set();
        
        links.forEach(link => {
            const sourceId = link.source.id || link.source;
            const targetId = link.target.id || link.target;
            
            if (sourceId === nodeId) {
                relatedNodes.add(targetId);
                relatedLinks.add(link);
            } else if (targetId === nodeId) {
                relatedNodes.add(sourceId);
                relatedLinks.add(link);
            }
        });
        
        // 应用高亮效果
        this.applyHighlight(relatedNodes, relatedLinks, nodeSelection, linkSelection);
    }
    
    /**
     * 应用高亮效果
     */
    applyHighlight(relatedNodes, relatedLinks, nodeSelection, linkSelection) {
        const self = this;
        
        // 高亮节点
        nodeSelection.each(function(d) {
            const node = d3.select(this);
            const isRelated = relatedNodes.has(d.id);
            
            node.select('circle, rect')
                .transition()
                .duration(200)
                .attr('opacity', isRelated ? self.normalOpacity : self.fadeOpacity)
                .attr('stroke-width', isRelated ? self.highlightStrokeWidth : self.normalStrokeWidth);
            
            node.select('text')
                .transition()
                .duration(200)
                .attr('opacity', isRelated ? 1 : self.fadeOpacity);
        });
        
        // 高亮连接
        linkSelection.each(function(d) {
            const link = d3.select(this);
            const isRelated = relatedLinks.has(d);
            
            link.transition()
                .duration(200)
                .attr('opacity', isRelated ? 1 : self.fadeOpacity)
                .attr('stroke-width', isRelated ? 3 : 2)
                .attr('marker-end', isRelated ? 'url(#arrowhead-highlight)' : 'url(#arrowhead)');
        });
        
        this.highlightedNodes = relatedNodes;
        this.highlightedLinks = relatedLinks;
    }
    
    /**
     * 清除高亮
     */
    clearHighlight(nodeSelection, linkSelection) {
        const self = this;
        
        // 恢复节点
        nodeSelection.each(function() {
            const node = d3.select(this);
            
            node.select('circle, rect')
                .transition()
                .duration(200)
                .attr('opacity', self.normalOpacity)
                .attr('stroke-width', self.normalStrokeWidth);
            
            node.select('text')
                .transition()
                .duration(200)
                .attr('opacity', 1);
        });
        
        // 恢复连接
        linkSelection
            .transition()
            .duration(200)
            .attr('opacity', self.normalOpacity)
            .attr('stroke-width', 2)
            .attr('marker-end', 'url(#arrowhead)');
        
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
        this.highlightManager = new NodeHighlightManager(options.highlightOptions || {});
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
                    
                    self.onRightClick.call(this, event, d, {
                        x: screenPt.x,
                        y: screenPt.y,
                        pageX: event.pageX,
                        pageY: event.pageY
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