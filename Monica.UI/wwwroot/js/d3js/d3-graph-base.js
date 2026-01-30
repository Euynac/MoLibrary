/**
 * D3.js 图形基础模块
 * 提供通用的图形初始化、缩放、拖拽等基础功能
 * 
 * @module d3-graph-base
 */

/**
 * 创建 SVG 画布
 * @param {string} containerId - 容器元素ID
 * @param {Object} options - 配置选项
 * @returns {Object} SVG 元素和相关配置
 */
export function createSvgCanvas(containerId, options = {}) {
    const container = document.getElementById(containerId);
    if (!container) {
        throw new Error(`Container with id '${containerId}' not found`);
    }

    // 清空容器
    container.innerHTML = '';
    
    const width = options.width || container.clientWidth;
    const height = options.height || container.clientHeight;

    // 创建 SVG
    const svg = d3.select(`#${containerId}`)
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .attr('viewBox', [0, 0, width, height]);

    // 创建主容器组
    const mainGroup = svg.append('g')
        .attr('class', 'graph-container');

    return { svg, mainGroup, width, height, container };
}

/**
 * 添加缩放和平移功能
 * @param {Object} svg - D3 SVG 选择
 * @param {Object} targetGroup - 目标组元素
 * @param {Object} options - 缩放选项
 * @returns {Object} zoom 行为对象
 */
export function addZoomBehavior(svg, targetGroup, options = {}) {
    const zoom = d3.zoom()
        .scaleExtent(options.scaleExtent || [0.1, 4])
        .on('zoom', (event) => {
            targetGroup.attr('transform', event.transform);
            if (options.onZoom) {
                options.onZoom(event);
            }
        });
    
    svg.call(zoom);
    
    // 添加点击空白处的事件处理
    svg.on('click', function(event) {
        // 如果点击的是 svg 背景
        if (event.target === this || event.target.tagName === 'svg') {
            if (options.onBackgroundClick) {
                options.onBackgroundClick(event);
            }
        }
    });
    
    return zoom;
}

/**
 * 创建现代化圆润箭头标记 - 使用MudBlazor颜色系统
 * @param {Object} svg - SVG 元素
 * @param {string} id - 标记ID
 * @param {Object} options - 箭头配置
 */
export function createArrowMarker(svg, id = 'arrowhead', options = {}) {
    const defs = svg.select('defs').empty() 
        ? svg.append('defs') 
        : svg.select('defs');
    
    // 为每个图表实例生成唯一的marker ID前缀
    const uniqueId = options.uniqueId || id;
    const outgoingId = `${uniqueId}-highlight-outgoing`;
    const incomingId = `${uniqueId}-highlight-incoming`;
    
    // 移除已存在的标记（只移除当前实例的）
    defs.selectAll(`#${uniqueId}, #${uniqueId}-highlight, #${outgoingId}, #${incomingId}`).remove();
    
    // 现代化箭头设计参数
    const arrowSize = options.size || 12;
    const viewBoxSize = arrowSize + 2; // 稍微大一点的viewBox以容纳圆润效果
    
    // 创建正常状态的箭头
    const marker = defs.append('marker')
        .attr('id', uniqueId)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    // 现代化圆润箭头路径 - 更流畅的曲线设计
    const arrowPath = `M1,${viewBoxSize/2-4} 
                      C1,${viewBoxSize/2-4} 3,${viewBoxSize/2-5} 5,${viewBoxSize/2-3}
                      L${arrowSize-2},${viewBoxSize/2-1}
                      C${arrowSize-1},${viewBoxSize/2-0.5} ${arrowSize-1},${viewBoxSize/2+0.5} ${arrowSize-2},${viewBoxSize/2+1}
                      L5,${viewBoxSize/2+3}
                      C3,${viewBoxSize/2+5} 1,${viewBoxSize/2+4} 1,${viewBoxSize/2+4} Z`;
    
    marker.append('path')
        .attr('d', arrowPath)
        .attr('fill', getArrowColor(options.isDarkMode, false))
        .attr('class', 'arrow-marker modern-arrow')
        .style('filter', 'drop-shadow(0 1px 2px rgba(0,0,0,0.1))'); // 轻微阴影增加立体感
    
    // 创建高亮状态的箭头（保持相同大小和位置）
    const highlightMarker = defs.append('marker')
        .attr('id', `${uniqueId}-highlight`)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)  // 保持相同大小
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    highlightMarker.append('path')
        .attr('d', arrowPath)
        .attr('fill', getArrowColor(options.isDarkMode, true))
        .attr('class', 'arrow-marker-highlight modern-arrow')
        .style('filter', 'drop-shadow(0 2px 4px rgba(33,150,243,0.3))'); // 高亮时的蓝色阴影
    
    // 创建出边高亮箭头（Info色系）
    const outgoingMarker = defs.append('marker')
        .attr('id', outgoingId)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    // 创建一个容器组来应用CSS变量
    const outgoingPath = outgoingMarker.append('path')
        .attr('d', arrowPath)
        .attr('class', 'arrow-marker-outgoing modern-arrow')
        .style('filter', 'drop-shadow(0 2px 4px rgba(25,118,210,0.3))');
    
    // 使用JavaScript获取计算后的CSS变量值（trim去除空格）
    const outgoingColor = options.isDarkMode ? 
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-info-lighten').trim() || '#29B6F6') :
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-info').trim() || '#1976D2');
    outgoingPath.attr('fill', outgoingColor);
    
    // 创建入边高亮箭头（Success色系）
    const incomingMarker = defs.append('marker')
        .attr('id', incomingId)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    const incomingPath = incomingMarker.append('path')
        .attr('d', arrowPath)
        .attr('class', 'arrow-marker-incoming modern-arrow')
        .style('filter', 'drop-shadow(0 2px 4px rgba(56,142,60,0.3))');
    
    // 使用JavaScript获取计算后的CSS变量值（trim去除空格）
    const incomingColor = options.isDarkMode ? 
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-success-lighten').trim() || '#66BB6A') :
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-success').trim() || '#43A047');
    incomingPath.attr('fill', incomingColor);
    
    return { 
        marker, 
        highlightMarker, 
        outgoingMarker, 
        incomingMarker,
        // 返回ID供其他模块使用
        markerId: uniqueId,
        highlightMarkerId: `${uniqueId}-highlight`,
        outgoingMarkerId: outgoingId,
        incomingMarkerId: incomingId
    };
}

/**
 * 获取箭头颜色 - 基于MudBlazor颜色系统
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @param {boolean} isHighlight - 是否为高亮状态
 * @returns {string} 颜色值
 */
function getArrowColor(isDarkMode, isHighlight) {
    if (isHighlight) {
        // 高亮时使用MudBlazor的Primary色彩
        return isDarkMode ? 'var(--mud-palette-primary-lighten, #9d7df7)' : 'var(--mud-palette-primary, #594ae2)';
    } else {
        // 正常状态使用中性色彩
        return isDarkMode 
            ? 'var(--mud-palette-text-secondary, rgba(255,255,255,0.5))' 
            : 'var(--mud-palette-text-secondary, rgba(0,0,0,0.54))';
    }
}

/**
 * 获取现代化连接线样式配置 - 使用MudBlazor颜色系统
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @param {boolean} isHighlight - 是否为高亮状态
 * @param {Object} markerIds - 自定义marker IDs
 * @returns {Object} 样式配置对象
 */
export function getModernLinkStyle(isDarkMode, isHighlight = false, markerIds = null) {
    const normalMarkerId = markerIds?.markerId || 'arrowhead';
    const highlightMarkerId = markerIds?.highlightMarkerId || 'arrowhead-highlight';
    
    if (isHighlight) {
        return {
            stroke: isDarkMode ? 'var(--mud-palette-primary-lighten, #9d7df7)' : 'var(--mud-palette-primary, #594ae2)',
            strokeWidth: 3,
            strokeOpacity: 0.9,
            filter: 'drop-shadow(0 2px 6px rgba(33,150,243,0.25))',
            strokeLinecap: 'round',
            strokeLinejoin: 'round',
            markerEnd: `url(#${highlightMarkerId})`
        };
    } else {
        return {
            stroke: isDarkMode 
                ? 'var(--mud-palette-divider, rgba(255,255,255,0.12))' 
                : 'var(--mud-palette-divider, rgba(224,224,224,1))',
            strokeWidth: 2,
            strokeOpacity: isDarkMode ? 0.7 : 0.8,
            filter: 'drop-shadow(0 1px 3px rgba(0,0,0,0.1))',
            strokeLinecap: 'round',
            strokeLinejoin: 'round',
            markerEnd: `url(#${normalMarkerId})`
        };
    }
}

/**
 * 获取现代化节点样式配置 - 使用MudBlazor颜色系统
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @param {string} nodeType - 节点类型
 * @returns {Object} 节点样式配置
 */
export function getModernNodeStyle(isDarkMode, nodeType = 'simple') {
    const baseStyle = {
        // 文本颜色 - 使用主题文本颜色
        textColor: isDarkMode 
            ? 'var(--mud-palette-text-primary, rgba(255,255,255,0.7))' 
            : 'var(--mud-palette-text-primary, rgba(66,66,66,1))',
        
        // 边框颜色
        strokeColor: isDarkMode 
            ? 'var(--mud-palette-lines-default, rgba(255,255,255,0.12))' 
            : 'var(--mud-palette-lines-default, rgba(0,0,0,0.12))',
            
        strokeWidth: 2,
        
        // 阴影效果
        filter: 'drop-shadow(0 2px 8px rgba(0,0,0,0.1))'
    };
    
    if (nodeType === 'complex') {
        return {
            ...baseStyle,
            // 复杂节点统一使用Surface色作为背景
            backgroundColor: isDarkMode 
                ? 'var(--mud-palette-surface, rgba(55,55,64,1))' 
                : 'var(--mud-palette-surface, rgba(255,255,255,1))',
            
            // 标题栏使用Primary色
            headerColor: isDarkMode 
                ? 'var(--mud-palette-primary, rgba(119,107,231,1))' 
                : 'var(--mud-palette-primary, rgba(89,74,226,1))',
                
            headerTextColor: 'var(--mud-palette-primary-text, rgba(255,255,255,1))',
            
            // 内容区文本颜色
            contentTextColor: baseStyle.textColor,
            
            // 状态栏背景
            footerColor: isDarkMode 
                ? 'var(--mud-palette-background-gray, rgba(39,39,47,1))' 
                : 'var(--mud-palette-background-gray, rgba(245,245,245,1))'
        };
    }
    
    // 简单节点样式 - 使用类型相关颜色但调整亮度适应主题
    return baseStyle;
}

/**
 * 重置视图
 * @param {Object} svg - SVG 元素
 * @param {Object} zoom - zoom 行为对象
 * @param {number} duration - 动画持续时间
 */
export function resetView(svg, zoom, duration = 750) {
    svg.transition()
        .duration(duration)
        .call(zoom.transform, d3.zoomIdentity);
}

/**
 * 聚焦到指定位置
 * @param {Object} svg - SVG 元素
 * @param {Object} zoom - zoom 行为对象
 * @param {Object} position - 目标位置 {x, y}
 * @param {number} scale - 缩放比例
 * @param {Object} canvasSize - 画布尺寸 {width, height}
 */
export function focusOnPosition(svg, zoom, position, scale = 1.5, canvasSize) {
    const { width, height } = canvasSize;
    
    svg.transition()
        .duration(750)
        .call(
            zoom.transform,
            d3.zoomIdentity
                .translate(width / 2, height / 2)
                .scale(scale)
                .translate(-position.x, -position.y)
        );
}

/**
 * 创建拖拽行为
 * @param {Object} options - 拖拽配置
 * @returns {Object} D3 拖拽行为
 */
export function createDragBehavior(options = {}) {
    return d3.drag()
        .on('start', function(event, d) {
            if (options.onStart) {
                options.onStart.call(this, event, d);
            }
        })
        .on('drag', function(event, d) {
            d.x = event.x;
            d.y = event.y;
            if (options.onDrag) {
                options.onDrag.call(this, event, d);
            }
        })
        .on('end', function(event, d) {
            if (options.onEnd) {
                options.onEnd.call(this, event, d);
            }
        });
}

/**
 * 基础图形类
 */
export class GraphBase {
    constructor(containerId, options = {}) {
        const { svg, mainGroup, width, height, container } = createSvgCanvas(containerId, options);
        
        this.svg = svg;
        this.mainGroup = mainGroup;
        this.width = width;
        this.height = height;
        this.container = container;
        this.isDarkMode = options.isDarkMode || false;
        
        // 添加缩放行为
        this.zoom = addZoomBehavior(svg, mainGroup, {
            scaleExtent: options.scaleExtent,
            onZoom: options.onZoom,
            onBackgroundClick: options.onBackgroundClick
        });
        
        // 创建箭头标记（包括所有方向性箭头）
        if (options.showArrows) {
            // 为每个图表实例生成唯一ID
            const instanceId = `graph-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
            this.arrowMarkers = createArrowMarker(svg, 'arrowhead', {
                isDarkMode: this.isDarkMode,
                size: options.arrowSize || 12,
                uniqueId: instanceId
            });
            // 保存marker IDs供其他模块使用
            this.markerIds = this.arrowMarkers;
        }
    }
    
    resetView(duration = 750) {
        resetView(this.svg, this.zoom, duration);
    }
    
    focusOnPosition(position, scale = 1.5) {
        focusOnPosition(this.svg, this.zoom, position, scale, {
            width: this.width,
            height: this.height
        });
    }
    
    dispose() {
        if (this.container) {
            this.container.innerHTML = '';
        }
    }
}