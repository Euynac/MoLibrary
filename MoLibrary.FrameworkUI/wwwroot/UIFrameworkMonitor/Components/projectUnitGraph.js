let graphInstance = null;

export function initializeGraph(containerId, isDarkMode, dotNetRef) {
    const container = document.getElementById(containerId);
    if (!container) return;

    // Clear existing content
    container.innerHTML = '';
    
    const width = container.clientWidth;
    const height = container.clientHeight;

    // Create SVG
    const svg = d3.select(`#${containerId}`)
        .append('svg')
        .attr('width', width)
        .attr('height', height);

    // Add container groups
    const g = svg.append('g');
    
    // Add zoom behavior
    const zoom = d3.zoom()
        .scaleExtent([0.1, 4])
        .on('zoom', (event) => {
            g.attr('transform', event.transform);
        });
    
    svg.call(zoom);

    // Define arrow markers
    const defs = svg.append('defs');
    
    defs.append('marker')
        .attr('id', 'arrowhead')
        .attr('viewBox', '-0 -5 10 10')
        .attr('refX', 20)
        .attr('refY', 0)
        .attr('orient', 'auto')
        .attr('markerWidth', 8)
        .attr('markerHeight', 8)
        .append('path')
        .attr('d', 'M 0,-5 L 10 ,0 L 0,5')
        .attr('fill', isDarkMode ? '#999' : '#666');

    // Create force simulation
    const simulation = d3.forceSimulation()
        .force('link', d3.forceLink().id(d => d.id).distance(100))
        .force('charge', d3.forceManyBody().strength(-300))
        .force('center', d3.forceCenter(width / 2, height / 2))
        .force('collision', d3.forceCollide().radius(30));

    graphInstance = {
        svg,
        g,
        simulation,
        zoom,
        width,
        height,
        isDarkMode,
        dotNetRef,
        container
    };
}

export function updateGraph(data) {
    if (!graphInstance) return;
    
    const { g, simulation, isDarkMode, dotNetRef } = graphInstance;
    
    // Clear existing elements
    g.selectAll('.link').remove();
    g.selectAll('.node').remove();

    // Prepare data
    const nodes = data.nodes;
    const links = data.links;

    // Create links
    const link = g.append('g')
        .attr('class', 'links')
        .selectAll('line')
        .data(links)
        .enter().append('line')
        .attr('class', 'link')
        .attr('stroke', isDarkMode ? '#666' : '#999')
        .attr('stroke-opacity', 0.6)
        .attr('stroke-width', 2)
        .attr('marker-end', 'url(#arrowhead)');

    // Create node groups
    const node = g.append('g')
        .attr('class', 'nodes')
        .selectAll('g')
        .data(nodes)
        .enter().append('g')
        .attr('class', 'node')
        .call(d3.drag()
            .on('start', dragstarted)
            .on('drag', dragged)
            .on('end', dragended));

    // Add shapes based on node type
    node.each(function(d) {
        const nodeGroup = d3.select(this);
        const isComplex = isComplexUnit(d.typeValue);
        const color = getUnitColor(d.typeValue, isDarkMode);
        
        if (isComplex) {
            // Rectangle for complex units
            const rect = nodeGroup.append('rect')
                .attr('width', 120)
                .attr('height', 60)
                .attr('x', -60)
                .attr('y', -30)
                .attr('rx', 5)
                .attr('ry', 5)
                .attr('fill', color)
                .attr('stroke', isDarkMode ? '#fff' : '#333')
                .attr('stroke-width', 2)
                .style('cursor', 'pointer');
            
            // Add title text
            nodeGroup.append('text')
                .attr('dy', -5)
                .attr('text-anchor', 'middle')
                .attr('fill', '#fff')
                .style('font-size', '12px')
                .style('font-weight', 'bold')
                .style('pointer-events', 'none')
                .text(d.title);
            
            // Add type text
            nodeGroup.append('text')
                .attr('dy', 10)
                .attr('text-anchor', 'middle')
                .attr('fill', '#fff')
                .style('font-size', '10px')
                .style('pointer-events', 'none')
                .text(d.type);
            
            // Add dependency count if any
            if (d.dependencyCount > 0) {
                nodeGroup.append('text')
                    .attr('dy', 25)
                    .attr('text-anchor', 'middle')
                    .attr('fill', '#fff')
                    .style('font-size', '10px')
                    .style('pointer-events', 'none')
                    .text(`依赖: ${d.dependencyCount}`);
            }
        } else {
            // Circle for simple units
            nodeGroup.append('circle')
                .attr('r', 25)
                .attr('fill', color)
                .attr('stroke', isDarkMode ? '#fff' : '#333')
                .attr('stroke-width', 2)
                .style('cursor', 'pointer');
            
            // Add text
            nodeGroup.append('text')
                .attr('dy', 4)
                .attr('text-anchor', 'middle')
                .attr('fill', '#fff')
                .style('font-size', '11px')
                .style('font-weight', 'bold')
                .style('pointer-events', 'none')
                .text(d.title.length > 10 ? d.title.substring(0, 10) + '...' : d.title);
        }
        
        // Add hover effect
        nodeGroup.on('mouseover', function() {
            d3.select(this).select('circle, rect').attr('opacity', 0.8);
        }).on('mouseout', function() {
            d3.select(this).select('circle, rect').attr('opacity', 1);
        });
        
        // Add click handler
        nodeGroup.on('click', function(event) {
            event.stopPropagation();
            dotNetRef.invokeMethodAsync('OnNodeClick', d.id);
        });
        
        // Add right-click handler
        nodeGroup.on('contextmenu', function(event) {
            event.preventDefault();
            event.stopPropagation();
            dotNetRef.invokeMethodAsync('OnNodeRightClick', d.id, event.pageX, event.pageY);
        });
    });

    // Add tooltips
    node.append('title')
        .text(d => `${d.title}\n类型: ${d.type}\n依赖数: ${d.dependencyCount}`);

    // Update simulation
    simulation.nodes(nodes);
    simulation.force('link').links(links);
    simulation.alpha(1).restart();

    simulation.on('tick', () => {
        link
            .attr('x1', d => d.source.x)
            .attr('y1', d => d.source.y)
            .attr('x2', d => d.target.x)
            .attr('y2', d => d.target.y);

        node.attr('transform', d => `translate(${d.x},${d.y})`);
    });

    function dragstarted(event, d) {
        if (!event.active) simulation.alphaTarget(0.3).restart();
        d.fx = d.x;
        d.fy = d.y;
    }

    function dragged(event, d) {
        d.fx = event.x;
        d.fy = event.y;
    }

    function dragended(event, d) {
        if (!event.active) simulation.alphaTarget(0);
        d.fx = null;
        d.fy = null;
    }
}

export function resetView() {
    if (!graphInstance) return;
    
    const { svg, width, height } = graphInstance;
    
    svg.transition()
        .duration(750)
        .call(graphInstance.zoom.transform, d3.zoomIdentity);
}

export function focusOnNode(nodeId) {
    if (!graphInstance) return;
    
    const { svg, g, width, height, zoom } = graphInstance;
    
    const node = g.selectAll('.node').filter(d => d.id === nodeId);
    if (node.empty()) return;
    
    const nodeData = node.datum();
    const scale = 1.5;
    
    svg.transition()
        .duration(750)
        .call(
            zoom.transform,
            d3.zoomIdentity
                .translate(width / 2, height / 2)
                .scale(scale)
                .translate(-nodeData.x, -nodeData.y)
        );
    
    // Highlight the focused node
    node.select('circle, rect')
        .transition()
        .duration(300)
        .attr('stroke-width', 4)
        .transition()
        .delay(300)
        .duration(300)
        .attr('stroke-width', 2);
}

export function dispose() {
    if (graphInstance) {
        graphInstance.simulation.stop();
        graphInstance = null;
    }
}

// Helper functions
function isComplexUnit(typeValue) {
    // Complex types that need rectangle display
    const complexTypes = [
        9,  // ApplicationService
        11, // DomainService
        18, // BackgroundWorker
        19, // BackgroundJob
        45, // HttpApi
        49, // GrpcApi
    ];
    return complexTypes.includes(typeValue);
}

function getUnitColor(typeValue, isDarkMode) {
    const colors = {
        // Core domain types
        9: '#2196F3',   // ApplicationService - Blue
        11: '#4CAF50',  // DomainService - Green
        16: '#FF9800',  // Repository - Orange
        
        // Event types
        20: '#9C27B0',  // DomainEvent - Purple
        23: '#673AB7',  // DomainEventHandler - Deep Purple
        28: '#3F51B5',  // LocalEventHandler - Indigo
        
        // Background types
        37: '#00BCD4',  // BackgroundWorker - Cyan
        39: '#009688',  // BackgroundJob - Teal
        
        // API types
        45: '#F44336',  // HttpApi - Red
        49: '#E91E63',  // GrpcApi - Pink
        
        // Other types
        65: '#795548',  // Entity - Brown
        69: '#607D8B',  // RequestDto - Blue Grey
        
        // Default
        0: '#9E9E9E'    // None/Unknown - Grey
    };
    
    let color = colors[typeValue] || colors[0];
    
    // Adjust for dark mode
    if (isDarkMode) {
        // Lighten colors for dark mode
        color = d3.color(color).brighter(0.3).toString();
    }
    
    return color;
}