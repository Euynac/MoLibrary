// Dependency graph visualization
let graph = null;
let container = null;
let d3 = null;

// Dynamically load the D3.js library
async function loadD3() {
    if (d3 === null) {
        try {
            // Check if it has been loaded into the global object
            if (window.d3) {
                d3 = window.d3;
                return d3;
            }
            
            // Dynamically create script tags to load D3.js
            const script = document.createElement('script');
            script.src = '/_content/Monica.UI/lib/d3.min.js';
            script.type = 'text/javascript';
            
            // Return Promise and wait for script loading to complete
            return new Promise((resolve, reject) => {
                script.onload = () => {
                    if (window.d3) {
                        d3 = window.d3;
                        resolve(d3);
                    } else {
                        reject(new Error('D3.js library not found after loading'));
                    }
                };
                script.onerror = () => {
                    reject(new Error('Failed to load D3.js library'));
                };
                document.head.appendChild(script);
            });
        } catch (error) {
            console.error('Failed to load D3.js:', error);
            throw error;
        }
    }
    return d3;
}

// Get the current topic
function isDarkMode() {
    // Check out MudBlazor’s Dark Mode
    return document.documentElement.classList.contains('mud-theme-dark') ||
           document.body.classList.contains('mud-theme-dark') ||
           document.body.classList.contains('dark-theme') || 
           window.matchMedia('(prefers-color-scheme: dark)').matches;
}

// Get theme-related colors
function getThemeColors() {
    const isDark = isDarkMode();
    return {
        nodeColors: {
            normal: isDark ? '#64B5F6' : '#2196F3',
            disabled: isDark ? '#FFB74D' : '#FF9800',
            cycle: isDark ? '#E57373' : '#F44336',
            text: isDark ? '#FFFFFF' : '#333333'
        },
        edgeColors: {
            direct: isDark ? '#81C784' : '#4CAF50',
            transitive: isDark ? '#BA68C8' : '#9C27B0',
            circular: isDark ? '#FF8A65' : '#FF5722',
            default: isDark ? '#BDBDBD' : '#999999'
        },
        background: isDark ? '#1E1E1E' : '#FFFFFF'
    };
}

// Initialize dependency graph
export async function initializeDependencyGraph(containerId, nodes, edges) {
    try {
        // Make sure D3.js is loaded
        await loadD3();
        container = document.getElementById(containerId);
        if (!container) {
            throw new Error(`Container with ID '${containerId}' not found`);
        }

        // Empty container
        container.innerHTML = '';

        // Get theme color
        const colors = getThemeColors();

        // Create SVG canvas
        const svg = d3.select(container)
            .append('svg')
            .attr('width', '100%')
            .attr('height', '100%')
            .style('background-color', colors.background);

        // Create arrow markers
        const defs = svg.append('defs');
        
        // direct dependency arrow
        defs.append('marker')
            .attr('id', 'arrow-direct')
            .attr('viewBox', '0 0 10 10')
            .attr('refX', 25)
            .attr('refY', 3)
            .attr('markerWidth', 8)
            .attr('markerHeight', 8)
            .attr('orient', 'auto')
            .append('path')
            .attr('d', 'M0,0 L0,6 L9,3 z')
            .attr('fill', colors.edgeColors.direct);

        // transitive dependency arrow
        defs.append('marker')
            .attr('id', 'arrow-transitive')
            .attr('viewBox', '0 0 10 10')
            .attr('refX', 25)
            .attr('refY', 3)
            .attr('markerWidth', 8)
            .attr('markerHeight', 8)
            .attr('orient', 'auto')
            .append('path')
            .attr('d', 'M0,0 L0,6 L9,3 z')
            .attr('fill', colors.edgeColors.transitive);

        // circular dependency arrow
        defs.append('marker')
            .attr('id', 'arrow-circular')
            .attr('viewBox', '0 0 10 10')
            .attr('refX', 25)
            .attr('refY', 3)
            .attr('markerWidth', 8)
            .attr('markerHeight', 8)
            .attr('orient', 'auto')
            .append('path')
            .attr('d', 'M0,0 L0,6 L9,3 z')
            .attr('fill', colors.edgeColors.circular);

        // Create zoom behavior
        const zoom = d3.zoom()
            .scaleExtent([0.1, 10])
            .on('zoom', (event) => {
                g.attr('transform', event.transform);
            });

        svg.call(zoom);

        // Create the main g element
        const g = svg.append('g');

        // Create a force-directed layout
        const simulation = d3.forceSimulation(nodes)
            .force('link', d3.forceLink(edges).id(d => d.id).distance(120))
            .force('charge', d3.forceManyBody().strength(-400))
            .force('center', d3.forceCenter(container.clientWidth / 2, container.clientHeight / 2))
            .force('collision', d3.forceCollide().radius(35));

        // Create edge
        const link = g.append('g')
            .selectAll('line')
            .data(edges)
            .enter().append('line')
            .attr('stroke', d => getEdgeColor(d.dependencyType, colors))
            .attr('stroke-width', d => d.isPartOfCycle ? 3 : 2)
            .attr('stroke-dasharray', d => d.dependencyType === 'Transitive' ? '5,5' : null)
            .attr('marker-end', d => `url(#arrow-${d.dependencyType.toLowerCase()})`)
            .style('opacity', 0.8);

        // Create node
        const node = g.append('g')
            .selectAll('circle')
            .data(nodes)
            .enter().append('circle')
            .attr('r', 25)
            .attr('fill', d => getNodeColor(d, colors))
            .attr('stroke', colors.background)
            .attr('stroke-width', 3)
            .style('cursor', 'move')
            .call(d3.drag()
                .on('start', function(event, d) {
                    if (!event.active) simulation.alphaTarget(0.3).restart();
                    d.fx = d.x;
                    d.fy = d.y;
                })
                .on('drag', function(event, d) {
                    d.fx = event.x;
                    d.fy = event.y;
                })
                .on('end', function(event, d) {
                    if (!event.active) simulation.alphaTarget(0);
                    // If not set to null, the position after dragging will be maintained.
                    // d.fx = null;
                    // d.fy = null;
                }));

        // Add node label
        const label = g.append('g')
            .selectAll('text')
            .data(nodes)
            .enter().append('text')
            .text(d => d.label)
            .attr('font-size', 12)
            .attr('text-anchor', 'middle')
            .attr('dy', 40)
            .style('fill', colors.nodeColors.text)
            .style('font-weight', 'bold')
            .style('pointer-events', 'none')
            .style('user-select', 'none');

        // Add interaction
        node.on('mouseenter', function(event, d) {
            // Highlight relevant edges
            link.style('opacity', edge => 
                edge.source.id === d.id || edge.target.id === d.id ? 1 : 0.3
            );
            
            // Highlight related nodes
            node.style('opacity', n => 
                n.id === d.id || isConnected(d.id, n.id) ? 1 : 0.5
            );

            // Show node information
            showNodeInfo(d);
        }).on('mouseleave', function() {
            // reset style
            link.style('opacity', 0.8);
            node.style('opacity', 1);
            
            // Clear node information
            hideNodeInfo();
        });

        // Update location
        simulation.on('tick', () => {
            link
                .attr('x1', d => d.source.x)
                .attr('y1', d => d.source.y)
                .attr('x2', d => d.target.x)
                .attr('y2', d => d.target.y);

            node
                .attr('cx', d => d.x)
                .attr('cy', d => d.y);

            label
                .attr('x', d => d.x)
                .attr('y', d => d.y);
        });

        // Save chart instance
        graph = {
            svg,
            g,
            simulation,
            nodes,
            edges,
            link,
            node,
            label,
            zoom,
            colors
        };

        // Monitor theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', updateTheme);
        
        // Monitor MudBlazor theme changes
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.type === 'attributes' && 
                    (mutation.attributeName === 'class' || mutation.attributeName === 'data-theme')) {
                    updateTheme();
                }
            });
        });
        
        observer.observe(document.documentElement, {
            attributes: true,
            attributeFilter: ['class', 'data-theme']
        });
        
        observer.observe(document.body, {
            attributes: true,
            attributeFilter: ['class', 'data-theme']
        });

        console.log('Dependency graph initialized successfully');
    } catch (error) {
        console.error('Failed to initialize dependency graph:', error);
        throw error;
    }

    // Helper function
    function getNodeColor(d, colors) {
        if (d.isPartOfCycle) return colors.nodeColors.cycle;
        if (d.isDisabled) return colors.nodeColors.disabled;
        return colors.nodeColors.normal;
    }

    function getEdgeColor(type, colors) {
        switch (type) {
            case 'Direct': return colors.edgeColors.direct;
            case 'Transitive': return colors.edgeColors.transitive;
            case 'Circular': return colors.edgeColors.circular;
            default: return colors.edgeColors.default;
        }
    }

    function isConnected(sourceId, targetId) {
        return edges.some(edge => 
            (edge.source.id === sourceId && edge.target.id === targetId) ||
            (edge.source.id === targetId && edge.target.id === sourceId)
        );
    }

    function showNodeInfo(d) {
        const connected = edges.filter(edge => 
            edge.source.id === d.id || edge.target.id === d.id
        );
        
        const directDependencies = connected.filter(edge => 
            edge.dependencyType === 'Direct' && edge.source.id === d.id
        );
        
        const transitiveDependencies = connected.filter(edge => 
            edge.dependencyType === 'Transitive' && edge.source.id === d.id
        );
        
        const dependedBy = connected.filter(edge => edge.target.id === d.id);
        
        const info = {
            module: d.label,
            directDependencies: directDependencies.length,
            transitiveDependencies: transitiveDependencies.length,
            dependedBy: dependedBy.length,
            isPartOfCycle: d.isPartOfCycle,
            isDisabled: d.isDisabled
        };
        
        // Create or update node information display
        updateNodeInfoDisplay(info);
    }

    function hideNodeInfo() {
        updateNodeInfoDisplay(null);
    }

    function updateNodeInfoDisplay(info) {
        // Try to find the element that shows the node details
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


}

// Update theme
function updateTheme() {
    if (!graph) return;
    
    const colors = getThemeColors();
    graph.colors = colors;
    
    // Update SVG background
    graph.svg.style('background-color', colors.background);
    
    // Update node color
    graph.node.attr('fill', d => {
        if (d.isPartOfCycle) return colors.nodeColors.cycle;
        if (d.isDisabled) return colors.nodeColors.disabled;
        return colors.nodeColors.normal;
    });
    graph.node.attr('stroke', colors.background);
    
    // Update edge color
    graph.link.attr('stroke', d => {
        switch (d.dependencyType) {
            case 'Direct': return colors.edgeColors.direct;
            case 'Transitive': return colors.edgeColors.transitive;
            case 'Circular': return colors.edgeColors.circular;
            default: return colors.edgeColors.default;
        }
    });
    
    // Update label color
    graph.label.style('fill', colors.nodeColors.text);
    
    // Update arrow color
    graph.svg.select('#arrow-direct path').attr('fill', colors.edgeColors.direct);
    graph.svg.select('#arrow-transitive path').attr('fill', colors.edgeColors.transitive);
    graph.svg.select('#arrow-circular path').attr('fill', colors.edgeColors.circular);
}

// Change layout
export async function changeLayout(layout) {
    if (!graph) return;
    
    try {
        await loadD3();

        const { simulation, nodes } = graph;
        const width = container.clientWidth;
        const height = container.clientHeight;

        switch (layout) {
            case 'force':
                simulation
                    .force('link', d3.forceLink(graph.edges).id(d => d.id).distance(120))
                    .force('charge', d3.forceManyBody().strength(-400))
                    .force('center', d3.forceCenter(width / 2, height / 2))
                    .force('collision', d3.forceCollide().radius(35));
                break;
            case 'hierarchical':
                simulation
                    .force('link', d3.forceLink(graph.edges).id(d => d.id).distance(80))
                    .force('charge', d3.forceManyBody().strength(-200))
                    .force('center', d3.forceCenter(width / 2, height / 2))
                    .force('collision', d3.forceCollide().radius(35))
                    .force('y', d3.forceY(height / 2).strength(0.1));
                break;
            case 'circular':
                const angleStep = (2 * Math.PI) / nodes.length;
                const radius = Math.min(width, height) / 3;
                
                nodes.forEach((node, i) => {
                    const angle = i * angleStep;
                    node.fx = width / 2 + radius * Math.cos(angle);
                    node.fy = height / 2 + radius * Math.sin(angle);
                });
                
                simulation
                    .force('link', null)
                    .force('charge', null)
                    .force('center', null)
                    .force('collision', null);
                break;
        }

        simulation.alpha(1).restart();
    } catch (error) {
        console.error('Failed to change layout:', error);
    }
}

// Apply filter
export async function applyFilter(filter) {
    if (!graph) return;
    
    try {
        await loadD3();

        const { link, node, label, edges } = graph;

        let visibleEdges = edges;
        
        switch (filter) {
            case 'direct':
                visibleEdges = edges.filter(e => e.dependencyType === 'Direct');
                break;
            case 'cycle':
                visibleEdges = edges.filter(e => e.isPartOfCycle);
                break;
            case 'all':
            default:
                visibleEdges = edges;
                break;
        }

        // Update edge visibility
        link.style('display', d => visibleEdges.includes(d) ? 'block' : 'none');

        // Update node visibility
        const visibleNodeIds = new Set();
        visibleEdges.forEach(edge => {
            visibleNodeIds.add(edge.source.id);
            visibleNodeIds.add(edge.target.id);
        });

        node.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
        label.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
    } catch (error) {
        console.error('Failed to apply filter:', error);
    }
}

// Zoom control
export async function zoomIn() {
    if (!graph) return;
    
    try {
        await loadD3();
        graph.svg.transition().duration(300).call(graph.zoom.scaleBy, 1.2);
    } catch (error) {
        console.error('Failed to zoom in:', error);
    }
}

export async function zoomOut() {
    if (!graph) return;
    
    try {
        await loadD3();
        graph.svg.transition().duration(300).call(graph.zoom.scaleBy, 1 / 1.2);
    } catch (error) {
        console.error('Failed to zoom out:', error);
    }
}

export async function resetZoom() {
    if (!graph) return;
    
    try {
        await loadD3();
        graph.svg.transition().duration(500).call(graph.zoom.transform, d3.zoomIdentity);
    } catch (error) {
        console.error('Failed to reset zoom:', error);
    }
}

// Export pictures
export async function exportGraph(filename) {
    if (!graph) {
        console.error('Graph not initialized');
        return;
    }
    
    try {
        await loadD3();

        const svgElement = graph.svg.node();
        const svgData = new XMLSerializer().serializeToString(svgElement);
        
        // Create complete SVG content, including styles
        const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
        const svgUrl = URL.createObjectURL(svgBlob);
        
        // Create canvas to convert to PNG
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const img = new Image();
        
        img.onload = function() {
            canvas.width = img.naturalWidth || 800;
            canvas.height = img.naturalHeight || 600;
            
            // Set white background
            ctx.fillStyle = graph.colors.background;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            
            // draw image
            ctx.drawImage(img, 0, 0);
            
            // Create download link
            const link = document.createElement('a');
            link.download = filename || 'dependency-graph.png';
            link.href = canvas.toDataURL('image/png');
            link.click();
            
            // Clean up resources
            URL.revokeObjectURL(svgUrl);
        };
        
        img.onerror = function() {
            console.error('Failed to load SVG image for export');
            // Alternative: Download the SVG directly
            const link = document.createElement('a');
            link.download = (filename || 'dependency-graph') + '.svg';
            link.href = svgUrl;
            link.click();
            URL.revokeObjectURL(svgUrl);
        };
        
        img.src = svgUrl;
        
    } catch (error) {
        console.error('Failed to export graph:', error);
    }
}

// Helper function
function getNodeColor(d, colors) {
    if (d.isPartOfCycle) return colors.nodeColors.cycle;
    if (d.isDisabled) return colors.nodeColors.disabled;
    return colors.nodeColors.normal;
}

function getEdgeColor(type, colors) {
    switch (type) {
        case 'Direct': return colors.edgeColors.direct;
        case 'Transitive': return colors.edgeColors.transitive;
        case 'Circular': return colors.edgeColors.circular;
        default: return colors.edgeColors.default;
    }
}

// Export theme update function
export function refreshTheme() {
    updateTheme();
}

// The D3.js library will be dynamically loaded when used
console.log('Enhanced dependency graph module loaded. D3.js will be loaded dynamically when needed.'); 