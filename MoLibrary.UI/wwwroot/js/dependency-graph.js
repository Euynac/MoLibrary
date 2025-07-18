// 依赖关系图可视化
let graph = null;
let container = null;
let d3 = null;

// 动态加载D3.js库
async function loadD3() {
    if (d3 === null) {
        try {
            // 检查是否已经加载到全局对象
            if (window.d3) {
                d3 = window.d3;
                return d3;
            }
            
            // 动态创建script标签加载D3.js
            const script = document.createElement('script');
            script.src = '/_content/MoLibrary.UI/lib/d3.min.js';
            script.type = 'text/javascript';
            
            // 返回Promise等待脚本加载完成
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

// 初始化依赖关系图
export async function initializeDependencyGraph(containerId, nodes, edges) {
    // 确保D3.js已加载
    await loadD3();
    container = document.getElementById(containerId);
    if (!container) return;

    // 清空容器
    container.innerHTML = '';

    // 创建SVG画布
    const svg = d3.select(container)
        .append('svg')
        .attr('width', '100%')
        .attr('height', '100%');

    // 创建箭头标记
    const defs = svg.append('defs');
    
    // 直接依赖箭头
    defs.append('marker')
        .attr('id', 'arrow-direct')
        .attr('viewBox', '0 0 10 10')
        .attr('refX', 20)
        .attr('refY', 3)
        .attr('markerWidth', 6)
        .attr('markerHeight', 6)
        .attr('orient', 'auto')
        .append('path')
        .attr('d', 'M0,0 L0,6 L9,3 z')
        .attr('fill', '#4CAF50');

    // 传递依赖箭头
    defs.append('marker')
        .attr('id', 'arrow-transitive')
        .attr('viewBox', '0 0 10 10')
        .attr('refX', 20)
        .attr('refY', 3)
        .attr('markerWidth', 6)
        .attr('markerHeight', 6)
        .attr('orient', 'auto')
        .append('path')
        .attr('d', 'M0,0 L0,6 L9,3 z')
        .attr('fill', '#9C27B0');

    // 循环依赖箭头
    defs.append('marker')
        .attr('id', 'arrow-circular')
        .attr('viewBox', '0 0 10 10')
        .attr('refX', 20)
        .attr('refY', 3)
        .attr('markerWidth', 6)
        .attr('markerHeight', 6)
        .attr('orient', 'auto')
        .append('path')
        .attr('d', 'M0,0 L0,6 L9,3 z')
        .attr('fill', '#FF5722');

    // 创建缩放行为
    const zoom = d3.zoom()
        .scaleExtent([0.1, 10])
        .on('zoom', (event) => {
            g.attr('transform', event.transform);
        });

    svg.call(zoom);

    // 创建主要的g元素
    const g = svg.append('g');

    // 创建力导向布局
    const simulation = d3.forceSimulation(nodes)
        .force('link', d3.forceLink(edges).id(d => d.id).distance(100))
        .force('charge', d3.forceManyBody().strength(-300))
        .force('center', d3.forceCenter(container.clientWidth / 2, container.clientHeight / 2))
        .force('collision', d3.forceCollide().radius(30));

    // 创建边
    const link = g.append('g')
        .selectAll('line')
        .data(edges)
        .enter().append('line')
        .attr('stroke', d => getEdgeColor(d.dependencyType))
        .attr('stroke-width', d => d.isPartOfCycle ? 3 : 2)
        .attr('stroke-dasharray', d => d.dependencyType === 'Transitive' ? '5,5' : null)
        .attr('marker-end', d => `url(#arrow-${d.dependencyType.toLowerCase()})`)
        .style('opacity', 0.7);

    // 创建节点
    const node = g.append('g')
        .selectAll('circle')
        .data(nodes)
        .enter().append('circle')
        .attr('r', 20)
        .attr('fill', d => getNodeColor(d))
        .attr('stroke', '#fff')
        .attr('stroke-width', 2)
        .style('cursor', 'pointer')
        .call(d3.drag()
            .on('start', dragstarted)
            .on('drag', dragged)
            .on('end', dragended));

    // 添加节点标签
    const label = g.append('g')
        .selectAll('text')
        .data(nodes)
        .enter().append('text')
        .text(d => d.label)
        .attr('font-size', 12)
        .attr('text-anchor', 'middle')
        .attr('dy', 35)
        .style('fill', '#333')
        .style('font-weight', 'bold')
        .style('pointer-events', 'none');

    // 添加交互
    node.on('mouseenter', function(event, d) {
        // 高亮相关的边
        link.style('opacity', edge => 
            edge.source.id === d.id || edge.target.id === d.id ? 1 : 0.3
        );
        
        // 高亮相关的节点
        node.style('opacity', n => 
            n.id === d.id || isConnected(d.id, n.id) ? 1 : 0.5
        );

        // 显示节点信息
        showNodeInfo(d);
    }).on('mouseleave', function() {
        // 重置样式
        link.style('opacity', 0.7);
        node.style('opacity', 1);
        
        // 清除节点信息
        hideNodeInfo();
    });

    // 更新位置
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

    // 保存图表实例
    graph = {
        svg,
        g,
        simulation,
        nodes,
        edges,
        link,
        node,
        label,
        zoom
    };

    // 辅助函数
    function getNodeColor(d) {
        if (d.isPartOfCycle) return '#F44336';
        if (d.isDisabled) return '#FF9800';
        return '#2196F3';
    }

    function getEdgeColor(type) {
        switch (type) {
            case 'Direct': return '#4CAF50';
            case 'Transitive': return '#9C27B0';
            case 'Circular': return '#FF5722';
            default: return '#999';
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
        const info = `模块: ${d.label}\n连接数: ${connected.length}\n循环依赖: ${d.isPartOfCycle ? '是' : '否'}`;
        
        // 这里可以调用Blazor组件的方法来显示信息
        // 暂时使用控制台输出
        console.log(info);
    }

    function hideNodeInfo() {
        // 清除节点信息
    }

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

// 改变布局
export async function changeLayout(layout) {
    if (!graph) return;
    await loadD3();

    const { simulation, nodes } = graph;
    const width = container.clientWidth;
    const height = container.clientHeight;

    switch (layout) {
        case 'force':
            simulation
                .force('link', d3.forceLink(graph.edges).id(d => d.id).distance(100))
                .force('charge', d3.forceManyBody().strength(-300))
                .force('center', d3.forceCenter(width / 2, height / 2))
                .force('collision', d3.forceCollide().radius(30));
            break;
        case 'hierarchical':
            const hierarchy = d3.hierarchy({ children: nodes });
            const treeLayout = d3.tree().size([width, height]);
            treeLayout(hierarchy);
            
            simulation
                .force('link', null)
                .force('charge', null)
                .force('center', null)
                .force('collision', null);
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
}

// 应用过滤器
export async function applyFilter(filter) {
    if (!graph) return;
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

    // 更新边的可见性
    link.style('display', d => visibleEdges.includes(d) ? 'block' : 'none');

    // 更新节点的可见性
    const visibleNodeIds = new Set();
    visibleEdges.forEach(edge => {
        visibleNodeIds.add(edge.source.id);
        visibleNodeIds.add(edge.target.id);
    });

    node.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
    label.style('display', d => visibleNodeIds.has(d.id) ? 'block' : 'none');
}

// 缩放控制
export async function zoomIn() {
    if (!graph) return;
    await loadD3();
    graph.svg.transition().call(graph.zoom.scaleBy, 1.2);
}

export async function zoomOut() {
    if (!graph) return;
    await loadD3();
    graph.svg.transition().call(graph.zoom.scaleBy, 1 / 1.2);
}

export async function resetZoom() {
    if (!graph) return;
    await loadD3();
    graph.svg.transition().call(graph.zoom.transform, d3.zoomIdentity);
}

// 导出图片
export async function exportGraph(filename) {
    if (!graph) return;
    await loadD3();

    const svgElement = graph.svg.node();
    const serializer = new XMLSerializer();
    const source = serializer.serializeToString(svgElement);
    
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    const image = new Image();
    
    image.onload = function() {
        canvas.width = image.width;
        canvas.height = image.height;
        context.drawImage(image, 0, 0);
        
        const link = document.createElement('a');
        link.download = filename;
        link.href = canvas.toDataURL();
        link.click();
    };
    
    image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(source);
}

// D3.js库将在使用时动态加载
console.log('Dependency graph module loaded. D3.js will be loaded dynamically when needed.'); 