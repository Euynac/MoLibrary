/**
 * D3.js 力导向布局模块
 * 提供力导向图的布局算法和配置
 * 
 * @module d3-force-layout
 */

/**
 * 力导向布局配置
 */
export class ForceLayoutConfig {
    constructor(options = {}) {
        this.linkDistance = options.linkDistance || 150;
        this.chargeStrength = options.chargeStrength || -300;
        this.collisionRadius = options.collisionRadius || 50;
        this.centerX = options.centerX || 0;
        this.centerY = options.centerY || 0;
        this.alphaDecay = options.alphaDecay || 0.0228; // 默认值
        this.velocityDecay = options.velocityDecay || 0.4; // 默认值
    }
}

/**
 * 创建力导向模拟器
 * @param {Object} config - 力导向配置
 * @returns {Object} D3 力导向模拟器
 */
export function createForceSimulation(config = new ForceLayoutConfig()) {
    const simulation = d3.forceSimulation()
        .alphaDecay(config.alphaDecay)
        .velocityDecay(config.velocityDecay);
    
    // 设置各种力
    simulation
        .force('link', d3.forceLink()
            .id(d => d.id)
            .distance(config.linkDistance))
        .force('charge', d3.forceManyBody()
            .strength(config.chargeStrength))
        .force('center', d3.forceCenter(config.centerX, config.centerY))
        .force('collision', d3.forceCollide()
            .radius(config.collisionRadius));
    
    return simulation;
}

/**
 * 更新力导向参数
 * @param {Object} simulation - 力导向模拟器
 * @param {string} forceName - 力的名称
 * @param {Object} params - 参数配置
 */
export function updateForceParameter(simulation, forceName, params) {
    const force = simulation.force(forceName);
    if (!force) return;
    
    switch (forceName) {
        case 'link':
            if (params.distance !== undefined) {
                force.distance(params.distance);
            }
            break;
        case 'charge':
            if (params.strength !== undefined) {
                force.strength(params.strength);
            }
            break;
        case 'collision':
            if (params.radius !== undefined) {
                force.radius(params.radius);
            }
            break;
        case 'center':
            if (params.x !== undefined && params.y !== undefined) {
                simulation.force('center', d3.forceCenter(params.x, params.y));
            }
            break;
    }
    
    // 重新加热模拟
    simulation.alpha(0.3).restart();
}

/**
 * 力导向拖拽行为
 */
export class ForceDragBehavior {
    constructor(simulation, options = {}) {
        this.simulation = simulation;
        this.options = options;
        this.keepFixed = options.keepFixed || false; // 是否保持固定
    }
    
    createDrag() {
        const self = this;
        
        return d3.drag()
            .on('start', function(event, d) {
                if (!event.active) {
                    self.simulation.alphaTarget(0.3).restart();
                }
                // 双击释放固定
                if (event.sourceEvent && event.sourceEvent.detail === 2) {
                    d.fx = null;
                    d.fy = null;
                } else {
                    d.fx = d.x;
                    d.fy = d.y;
                }
                
                if (self.options.onStart) {
                    self.options.onStart.call(this, event, d);
                }
            })
            .on('drag', function(event, d) {
                d.fx = event.x;
                d.fy = event.y;
                
                if (self.options.onDrag) {
                    self.options.onDrag.call(this, event, d);
                }
            })
            .on('end', function(event, d) {
                if (!event.active) {
                    self.simulation.alphaTarget(0);
                }
                
                // 如果不保持固定，释放节点
                if (!self.keepFixed) {
                    d.fx = null;
                    d.fy = null;
                }
                
                if (self.options.onEnd) {
                    self.options.onEnd.call(this, event, d);
                }
            });
    }
}

/**
 * 力导向布局管理器
 */
export class ForceLayoutManager {
    constructor(width, height, options = {}) {
        this.width = width;
        this.height = height;
        
        // 创建配置
        this.config = new ForceLayoutConfig({
            centerX: width / 2,
            centerY: height / 2,
            ...options
        });
        
        // 创建模拟器
        this.simulation = createForceSimulation(this.config);
        
        // 创建拖拽行为
        this.dragBehavior = new ForceDragBehavior(this.simulation, {
            keepFixed: options.keepFixed || false,
            onStart: options.onDragStart,
            onDrag: options.onDrag,
            onEnd: options.onDragEnd
        });
    }
    
    /**
     * 设置数据
     */
    setData(nodes, links) {
        this.simulation.nodes(nodes);
        if (this.simulation.force('link')) {
            this.simulation.force('link').links(links);
        }
        return this;
    }
    
    /**
     * 更新连接距离
     */
    updateLinkDistance(distance) {
        this.config.linkDistance = distance;
        updateForceParameter(this.simulation, 'link', { distance });
        return this;
    }
    
    /**
     * 更新斥力强度
     */
    updateChargeStrength(strength) {
        this.config.chargeStrength = strength;
        updateForceParameter(this.simulation, 'charge', { strength });
        return this;
    }
    
    /**
     * 设置是否保持节点固定
     */
    setKeepFixed(keepFixed) {
        this.dragBehavior.keepFixed = keepFixed;
        return this;
    }
    
    /**
     * 释放所有固定节点
     */
    releaseAllFixed(nodes) {
        nodes.forEach(node => {
            node.fx = null;
            node.fy = null;
        });
        this.simulation.alpha(0.3).restart();
        return this;
    }
    
    /**
     * 开始模拟
     */
    start(onTick) {
        this.simulation.on('tick', onTick);
        this.simulation.alpha(1).restart();
        return this;
    }
    
    /**
     * 停止模拟
     */
    stop() {
        this.simulation.stop();
        return this;
    }
    
    /**
     * 获取拖拽行为
     */
    getDragBehavior() {
        return this.dragBehavior.createDrag();
    }
    
    /**
     * 销毁
     */
    dispose() {
        this.stop();
        this.simulation = null;
    }
}