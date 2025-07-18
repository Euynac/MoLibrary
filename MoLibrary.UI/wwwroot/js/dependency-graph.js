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

// 获取当前主题
function isDarkMode() {
    // 检查 MudBlazor 的暗黑模式
    return document.documentElement.classList.contains('mud-theme-dark') ||
           document.body.classList.contains('mud-theme-dark') ||
           document.body.classList.contains('dark-theme') || 
           window.matchMedia('(prefers-color-scheme: dark)').matches;
}

// 获取主题相关的颜色
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

// 初始化依赖关系图
export async function initializeDependencyGraph(containerId, nodes, edges) {
    try {
        // 确保D3.js已加载
        await loadD3();
        container = document.getElementById(containerId);
        if (!container) {
            throw new Error(`Container with ID '${containerId}' not found`);
        }

        // 清空容器
        container.innerHTML = '';

        // 获取主题颜色
        const colors = getThemeColors();

        // 创建SVG画布
        const svg = d3.select(container)
            .append('svg')
            .attr('width', '100%')
            .attr('height', '100%')
            .style('background-color', colors.background);

        // 创建箭头标记
        const defs = svg.append('defs');
        
        // 直接依赖箭头
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

        // 传递依赖箭头
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

        // 循环依赖箭头
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
            .force('link', d3.forceLink(edges).id(d => d.id).distance(120))
            .force('charge', d3.forceManyBody().strength(-400))
            .force('center', d3.forceCenter(container.clientWidth / 2, container.clientHeight / 2))
            .force('collision', d3.forceCollide().radius(35));

        // 创建边
        const link = g.append('g')
            .selectAll('line')
            .data(edges)
            .enter().append('line')
            .attr('stroke', d => getEdgeColor(d.dependencyType, colors))
            .attr('stroke-width', d => d.isPartOfCycle ? 3 : 2)
            .attr('stroke-dasharray', d => d.dependencyType === 'Transitive' ? '5,5' : null)
            .attr('marker-end', d => `url(#arrow-${d.dependencyType.toLowerCase()})`)
            .style('opacity', 0.8);

        // 创建节点
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
                    // 不设置为null，保持拖动后的位置
                    // d.fx = null;
                    // d.fy = null;
                }));

        // 添加节点标签
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
            link.style('opacity', 0.8);
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
            zoom,
            colors
        };

        // 监听主题变化
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', updateTheme);
        
        // 监听MudBlazor主题变化
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

    // 辅助函数
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
        
        // 创建或更新节点信息显示
        updateNodeInfoDisplay(info);
    }

    function hideNodeInfo() {
        updateNodeInfoDisplay(null);
    }

    function updateNodeInfoDisplay(info) {
        // 尝试找到显示节点详情的元素
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

// 更新主题
function updateTheme() {
    if (!graph) return;
    
    const colors = getThemeColors();
    graph.colors = colors;
    
    // 更新SVG背景
    graph.svg.style('background-color', colors.background);
    
    // 更新节点颜色
    graph.node.attr('fill', d => {
        if (d.isPartOfCycle) return colors.nodeColors.cycle;
        if (d.isDisabled) return colors.nodeColors.disabled;
        return colors.nodeColors.normal;
    });
    graph.node.attr('stroke', colors.background);
    
    // 更新边颜色
    graph.link.attr('stroke', d => {
        switch (d.dependencyType) {
            case 'Direct': return colors.edgeColors.direct;
            case 'Transitive': return colors.edgeColors.transitive;
            case 'Circular': return colors.edgeColors.circular;
            default: return colors.edgeColors.default;
        }
    });
    
    // 更新标签颜色
    graph.label.style('fill', colors.nodeColors.text);
    
    // 更新箭头颜色
    graph.svg.select('#arrow-direct path').attr('fill', colors.edgeColors.direct);
    graph.svg.select('#arrow-transitive path').attr('fill', colors.edgeColors.transitive);
    graph.svg.select('#arrow-circular path').attr('fill', colors.edgeColors.circular);
}

// 改变布局
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

// 应用过滤器
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
    } catch (error) {
        console.error('Failed to apply filter:', error);
    }
}

// 缩放控制
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

// 导出图片
export async function exportGraph(filename) {
    if (!graph) {
        console.error('Graph not initialized');
        return;
    }
    
    try {
        await loadD3();

        const svgElement = graph.svg.node();
        const svgData = new XMLSerializer().serializeToString(svgElement);
        
        // 创建完整的SVG内容，包含样式
        const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
        const svgUrl = URL.createObjectURL(svgBlob);
        
        // 创建canvas来转换为PNG
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const img = new Image();
        
        img.onload = function() {
            canvas.width = img.naturalWidth || 800;
            canvas.height = img.naturalHeight || 600;
            
            // 设置白色背景
            ctx.fillStyle = graph.colors.background;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            
            // 绘制图像
            ctx.drawImage(img, 0, 0);
            
            // 创建下载链接
            const link = document.createElement('a');
            link.download = filename || 'dependency-graph.png';
            link.href = canvas.toDataURL('image/png');
            link.click();
            
            // 清理资源
            URL.revokeObjectURL(svgUrl);
        };
        
        img.onerror = function() {
            console.error('Failed to load SVG image for export');
            // 备选方案：直接下载SVG
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

// 辅助函数
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

// 导出主题更新函数
export function refreshTheme() {
    updateTheme();
}

// D3.js库将在使用时动态加载
console.log('Enhanced dependency graph module loaded. D3.js will be loaded dynamically when needed.'); 