/**
 * D3.js graphics basic module
 * Provides basic functions such as general graphics initialization, scaling, and dragging
 * 
 * @module d3-graph-base
 */

/**
 * Create SVG canvas
 * @param {string} containerId - container element ID
 * @param {Object} options - configuration options
 * @returns {Object} SVG elements and related configurations
 */
export function createSvgCanvas(containerId, options = {}) {
    const container = document.getElementById(containerId);
    if (!container) {
        throw new Error(`Container with id '${containerId}' not found`);
    }

    // Empty container
    container.innerHTML = '';
    
    const width = options.width || container.clientWidth;
    const height = options.height || container.clientHeight;

    // Create SVG
    const svg = d3.select(`#${containerId}`)
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .attr('viewBox', [0, 0, width, height]);

    // Create the main container group
    const mainGroup = svg.append('g')
        .attr('class', 'graph-container');

    return { svg, mainGroup, width, height, container };
}

/**
 * Add zoom and pan functionality
 * @param {Object} svg - D3 SVG selection
 * @param {Object} targetGroup - target group element
 * @param {Object} options - scaling options
 * @returns {Object} zoom behavior object
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
    
    // Add event handling for clicking on the blank space
    svg.on('click', function(event) {
        // If you click on the svg background
        if (event.target === this || event.target.tagName === 'svg') {
            if (options.onBackgroundClick) {
                options.onBackgroundClick(event);
            }
        }
    });
    
    return zoom;
}

/**
 * Create modern rounded arrow markers - using the MudBlazor color system
 * @param {Object} svg - SVG element
 * @param {string} id - tag ID
 * @param {Object} options - Arrow configuration
 */
export function createArrowMarker(svg, id = 'arrowhead', options = {}) {
    const defs = svg.select('defs').empty() 
        ? svg.append('defs') 
        : svg.select('defs');
    
    // Generate a unique marker ID prefix for each chart instance
    const uniqueId = options.uniqueId || id;
    const outgoingId = `${uniqueId}-highlight-outgoing`;
    const incomingId = `${uniqueId}-highlight-incoming`;
    
    // Remove existing tags (only removes the current instance)
    defs.selectAll(`#${uniqueId}, #${uniqueId}-highlight, #${outgoingId}, #${incomingId}`).remove();
    
    // Modern arrow design parameters
    const arrowSize = options.size || 12;
    const viewBoxSize = arrowSize + 2; // 稍微大一点的viewBox以容纳圆润效果
    
    // Create a normal state arrow
    const marker = defs.append('marker')
        .attr('id', uniqueId)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    // Modern rounded arrow paths - smoother curved design
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
    
    // Creates a highlighted state arrow (keeping the same size and position)
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
    
    // Create an out-edge highlighted arrow (Info color system)
    const outgoingMarker = defs.append('marker')
        .attr('id', outgoingId)
        .attr('viewBox', `0 0 ${viewBoxSize} ${viewBoxSize}`)
        .attr('refX', options.refX || (arrowSize * 0.8))
        .attr('refY', viewBoxSize / 2)
        .attr('orient', 'auto')
        .attr('markerWidth', arrowSize)
        .attr('markerHeight', arrowSize)
        .attr('markerUnits', 'strokeWidth');
    
    // Create a container group to apply CSS variables
    const outgoingPath = outgoingMarker.append('path')
        .attr('d', arrowPath)
        .attr('class', 'arrow-marker-outgoing modern-arrow')
        .style('filter', 'drop-shadow(0 2px 4px rgba(25,118,210,0.3))');
    
    // Get calculated CSS variable values ​​using JavaScript (trim to remove spaces)
    const outgoingColor = options.isDarkMode ? 
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-info-lighten').trim() || '#29B6F6') :
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-info').trim() || '#1976D2');
    outgoingPath.attr('fill', outgoingColor);
    
    // Create an in-edge highlighted arrow (Success color system)
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
    
    // Get calculated CSS variable values ​​using JavaScript (trim to remove spaces)
    const incomingColor = options.isDarkMode ? 
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-success-lighten').trim() || '#66BB6A') :
        (getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-success').trim() || '#43A047');
    incomingPath.attr('fill', incomingColor);
    
    return { 
        marker, 
        highlightMarker, 
        outgoingMarker, 
        incomingMarker,
        // Return ID for use by other modules
        markerId: uniqueId,
        highlightMarkerId: `${uniqueId}-highlight`,
        outgoingMarkerId: outgoingId,
        incomingMarkerId: incomingId
    };
}

/**
 * Get arrow color - based on MudBlazor color system
 * @param {boolean} isDarkMode - whether it is dark mode
 * @param {boolean} isHighlight - whether it is highlighted
 * @returns {string} color value
 */
function getArrowColor(isDarkMode, isHighlight) {
    if (isHighlight) {
        // Use MudBlazor’s Primary color when highlighting
        return isDarkMode ? 'var(--mud-palette-primary-lighten, #9d7df7)' : 'var(--mud-palette-primary, #594ae2)';
    } else {
        // Normally use neutral colors
        return isDarkMode 
            ? 'var(--mud-palette-text-secondary, rgba(255,255,255,0.5))' 
            : 'var(--mud-palette-text-secondary, rgba(0,0,0,0.54))';
    }
}

/**
 * Get a modern connector style configuration - using the MudBlazor color system
 * @param {boolean} isDarkMode - whether it is dark mode
 * @param {boolean} isHighlight - whether it is highlighted
 * @param {Object} markerIds - custom marker IDs
 * @returns {Object} style configuration object
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
 * Get modern node style configuration - using MudBlazor color system
 * @param {boolean} isDarkMode - whether it is dark mode
 * @param {string} nodeType - node type
 * @returns {Object} node style configuration
 */
export function getModernNodeStyle(isDarkMode, nodeType = 'simple') {
    const baseStyle = {
        // Text Color - Use theme text color
        textColor: isDarkMode 
            ? 'var(--mud-palette-text-primary, rgba(255,255,255,0.7))' 
            : 'var(--mud-palette-text-primary, rgba(66,66,66,1))',
        
        // border color
        strokeColor: isDarkMode 
            ? 'var(--mud-palette-lines-default, rgba(255,255,255,0.12))' 
            : 'var(--mud-palette-lines-default, rgba(0,0,0,0.12))',
            
        strokeWidth: 2,
        
        // shadow effect
        filter: 'drop-shadow(0 2px 8px rgba(0,0,0,0.1))'
    };
    
    if (nodeType === 'complex') {
        return {
            ...baseStyle,
            // Complex nodes uniformly use the Surface color as the background
            backgroundColor: isDarkMode 
                ? 'var(--mud-palette-surface, rgba(55,55,64,1))' 
                : 'var(--mud-palette-surface, rgba(255,255,255,1))',
            
            // Use Primary color for title bar
            headerColor: isDarkMode 
                ? 'var(--mud-palette-primary, rgba(119,107,231,1))' 
                : 'var(--mud-palette-primary, rgba(89,74,226,1))',
                
            headerTextColor: 'var(--mud-palette-primary-text, rgba(255,255,255,1))',
            
            // Content area text color
            contentTextColor: baseStyle.textColor,
            
            // status bar background
            footerColor: isDarkMode 
                ? 'var(--mud-palette-background-gray, rgba(39,39,47,1))' 
                : 'var(--mud-palette-background-gray, rgba(245,245,245,1))'
        };
    }
    
    // Simple node styles - use type-dependent colors but adjust brightness to fit the theme
    return baseStyle;
}

/**
 * reset view
 * @param {Object} svg - SVG element
 * @param {Object} zoom - zoom behavior object
 * @param {number} duration - animation duration
 */
export function resetView(svg, zoom, duration = 750) {
    svg.transition()
        .duration(duration)
        .call(zoom.transform, d3.zoomIdentity);
}

/**
 * Scales the current zoom level by a multiplier.
 * @param {Object} svg - SVG element
 * @param {Object} zoom - D3 zoom behavior
 * @param {number} scaleBy - Zoom multiplier
 * @param {number} duration - Animation duration
 */
export function scaleView(svg, zoom, scaleBy, duration = 300) {
    svg.transition()
        .duration(duration)
        .call(zoom.scaleBy, scaleBy);
}

/**
 * Focus on specified location
 * @param {Object} svg - SVG element
 * @param {Object} zoom - zoom behavior object
 * @param {Object} position - target position {x, y}
 * @param {number} scale - scaling ratio
 * @param {Object} canvasSize - canvas size {width, height}
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
 * Create drag behavior
 * @param {Object} options - drag and drop configuration
 * @returns {Object} D3 dragging behavior
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
 * Basic graphics class
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
        
        // Add zoom behavior
        this.zoom = addZoomBehavior(svg, mainGroup, {
            scaleExtent: options.scaleExtent,
            onZoom: options.onZoom,
            onBackgroundClick: options.onBackgroundClick
        });
        
        // Create arrow markers (including all directional arrows)
        if (options.showArrows) {
            // Generate a unique ID for each chart instance
            const instanceId = `graph-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
            this.arrowMarkers = createArrowMarker(svg, 'arrowhead', {
                isDarkMode: this.isDarkMode,
                size: options.arrowSize || 12,
                uniqueId: instanceId
            });
            // Save marker IDs for use by other modules
            this.markerIds = this.arrowMarkers;
        }
    }
    
    resetView(duration = 750) {
        resetView(this.svg, this.zoom, duration);
    }

    zoomBy(scaleBy, duration = 300) {
        scaleView(this.svg, this.zoom, scaleBy, duration);
    }

    zoomIn(duration = 300) {
        this.zoomBy(1.2, duration);
    }

    zoomOut(duration = 300) {
        this.zoomBy(1 / 1.2, duration);
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
