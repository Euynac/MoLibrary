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
 * 创建箭头标记
 * @param {Object} svg - SVG 元素
 * @param {string} id - 标记ID
 * @param {Object} options - 箭头配置
 */
export function createArrowMarker(svg, id = 'arrowhead', options = {}) {
    const defs = svg.select('defs').empty() 
        ? svg.append('defs') 
        : svg.select('defs');
    
    // 创建正常状态的箭头
    const marker = defs.append('marker')
        .attr('id', id)
        .attr('viewBox', '-0 -5 10 10')
        .attr('refX', options.refX || 9)  // 因为路径已经缩短，箭头不需要太多偏移
        .attr('refY', 0)
        .attr('orient', 'auto')
        .attr('markerWidth', options.size || 10)
        .attr('markerHeight', options.size || 10);
    
    marker.append('path')
        .attr('d', 'M 0,-5 L 10 ,0 L 0,5')
        .attr('fill', options.color || '#999')
        .attr('class', 'arrow-marker');
    
    // 创建高亮状态的箭头（相同大小和位置）
    const highlightMarker = defs.append('marker')
        .attr('id', `${id}-highlight`)
        .attr('viewBox', '-0 -5 10 10')
        .attr('refX', options.refX || 9)  // 保持相同位置
        .attr('refY', 0)
        .attr('orient', 'auto')
        .attr('markerWidth', options.size || 10)  // 保持相同大小
        .attr('markerHeight', options.size || 10);
    
    highlightMarker.append('path')
        .attr('d', 'M 0,-5 L 10 ,0 L 0,5')
        .attr('fill', '#2196F3')
        .attr('class', 'arrow-marker-highlight');
    
    return { marker, highlightMarker };
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
        
        // 创建箭头标记
        if (options.showArrows) {
            createArrowMarker(svg, 'arrowhead', {
                color: this.isDarkMode ? '#999' : '#666',
                highlightColor: '#2196F3'
            });
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