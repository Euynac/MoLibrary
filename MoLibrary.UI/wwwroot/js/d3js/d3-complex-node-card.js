/**
 * D3.js 复杂节点卡片绘制模块
 * 提供现代化的卡片式节点绘制功能，支持三层布局和自适应大小
 * 
 * @module d3-complex-node-card
 */

import { getModernNodeStyle } from './d3-graph-base.js';

/**
 * 复杂节点卡片绘制器
 */
export class ComplexNodeCardRenderer {
    constructor(isDarkMode = false) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
        
        // 卡片布局配置
        this.config = {
            minWidth: 180,
            maxWidth: 280,
            padding: 12,
            borderRadius: 8,
            
            // 标题栏配置
            header: {
                height: 40,
                padding: 12,
                fontSize: '14px',
                fontWeight: '600'
            },
            
            // 内容区配置
            content: {
                minHeight: 60,
                padding: 12,
                fontSize: '13px',
                lineHeight: 1.4,
                itemSpacing: 8
            },
            
            // 状态栏配置
            footer: {
                height: 32,
                padding: 8,
                chipHeight: 20,
                chipPadding: 8,
                chipFontSize: '11px',
                chipBorderRadius: 10
            }
        };
    }
    
    /**
     * 计算节点卡片尺寸
     * @param {Object} nodeData - 节点数据
     * @returns {Object} 尺寸信息 {width, height}
     */
    calculateCardSize(nodeData) {
        const { config } = this;
        const contentItems = nodeData.metadata || [];
        
        // 计算宽度 - 基于标题长度和内容宽度
        const titleWidth = this.estimateTextWidth(nodeData.title, config.header.fontSize, config.header.fontWeight);
        const maxContentWidth = Math.max(
            ...contentItems.map(item => this.estimateTextWidth(`${item.key}: ${item.value}`, config.content.fontSize))
        );
        
        const requiredWidth = Math.max(titleWidth, maxContentWidth) + config.padding * 2;
        const width = Math.max(config.minWidth, Math.min(config.maxWidth, requiredWidth));
        
        // 计算高度 - 标题栏 + 内容区 + 状态栏
        const contentHeight = contentItems.length > 0 
            ? Math.max(
                config.content.minHeight,
                contentItems.length * (parseFloat(config.content.fontSize) * config.content.lineHeight + config.content.itemSpacing) + config.content.padding * 2
              )
            : config.content.minHeight; // 没有数据时使用最小高度
        
        const height = config.header.height + contentHeight + config.footer.height;
        
        return { width, height };
    }
    
    /**
     * 估算文本宽度（近似值）
     * @param {string} text - 文本内容
     * @param {string} fontSize - 字体大小
     * @param {string} fontWeight - 字体粗细
     * @returns {number} 估算宽度
     */
    estimateTextWidth(text, fontSize, fontWeight = 'normal') {
        const baseWidth = parseFloat(fontSize) * 0.6; // 近似比例
        const weightMultiplier = fontWeight === '600' || fontWeight === 'bold' ? 1.1 : 1;
        return text.length * baseWidth * weightMultiplier;
    }
    
    /**
     * 绘制复杂节点卡片
     * @param {Object} nodeElement - D3选择的节点元素
     * @param {Object} nodeData - 节点数据
     */
    drawCard(nodeElement, nodeData) {
        const { width, height } = this.calculateCardSize(nodeData);
        const { config, style } = this;
        
        // 清除现有内容
        nodeElement.selectAll('*').remove();
        
        // 创建卡片主容器
        const card = nodeElement.append('g')
            .attr('class', 'complex-node-card');
        
        // 绘制主卡片背景
        const cardBackground = card.append('rect')
            .attr('class', 'card-background')
            .attr('width', width)
            .attr('height', height)
            .attr('x', -width / 2)
            .attr('y', -height / 2)
            .attr('rx', config.borderRadius)
            .attr('ry', config.borderRadius)
            .attr('fill', style.backgroundColor)
            .attr('stroke', style.strokeColor)
            .attr('stroke-width', style.strokeWidth)
            .style('filter', style.filter)
            .style('cursor', 'pointer');
        
        // 绘制标题栏
        this.drawHeader(card, nodeData, width, height, config.header.height);
        
        // 绘制内容区
        const contentItems = nodeData.metadata || [];
        const contentHeight = height - config.header.height - config.footer.height;
        const contentStartY = -height / 2 + config.header.height; // 内容区从标题栏下方开始
        this.drawContent(card, contentItems, width, contentHeight, contentStartY);
        
        // 绘制状态栏
        const footerY = height / 2 - config.footer.height;
        this.drawFooter(card, nodeData, width, config.footer.height, footerY);
        
        // 存储尺寸信息供其他模块使用
        nodeData._cardSize = { width, height };
        
        return card;
    }
    
    /**
     * 绘制标题栏
     * @param {Object} card - 卡片容器
     * @param {Object} nodeData - 节点数据
     * @param {number} width - 卡片宽度
     * @param {number} cardHeight - 整个卡片高度
     * @param {number} headerHeight - 标题栏高度
     */
    drawHeader(card, nodeData, width, cardHeight, headerHeight) {
        const { config, style } = this;
        const headerY = -cardHeight / 2; // 标题栏在卡片顶部
        
        // 标题栏背景
        card.append('rect')
            .attr('class', 'card-header')
            .attr('width', width)
            .attr('height', headerHeight)
            .attr('x', -width / 2)
            .attr('y', headerY)
            .attr('rx', config.borderRadius)
            .attr('ry', config.borderRadius)
            .attr('fill', style.headerColor);
        
        // 隐藏标题栏下方的圆角（只保留上方圆角）
        card.append('rect')
            .attr('width', width)
            .attr('height', config.borderRadius)
            .attr('x', -width / 2)
            .attr('y', headerY + headerHeight - config.borderRadius)
            .attr('fill', style.headerColor);
        
        // 标题文字
        card.append('text')
            .attr('class', 'card-title')
            .attr('x', 0)
            .attr('y', headerY + headerHeight / 2 + 5)
            .attr('text-anchor', 'middle')
            .attr('fill', style.headerTextColor)
            .style('font-size', config.header.fontSize)
            .style('font-weight', config.header.fontWeight)
            .style('pointer-events', 'none')
            .text(nodeData.title);
    }
    
    /**
     * 绘制内容区
     * @param {Object} card - 卡片容器
     * @param {Array} contentItems - 内容项目列表
     * @param {number} width - 卡片宽度
     * @param {number} height - 内容区高度
     * @param {number} startY - 内容区起始Y坐标
     */
    drawContent(card, contentItems, width, height, startY) {
        const { config, style } = this;
        
        if (contentItems.length === 0) {
            // 如果没有内容，显示占位文本（在内容区中央）
            card.append('text')
                .attr('class', 'content-placeholder')
                .attr('x', 0)
                .attr('y', startY + height / 2)
                .attr('text-anchor', 'middle')
                .attr('fill', style.contentTextColor)
                .style('font-size', config.content.fontSize)
                .style('opacity', 0.6)
                .style('pointer-events', 'none')
                .text('暂无元数据');
            return;
        }
        
        // 绘制内容项
        const firstItemY = startY + config.content.padding + parseFloat(config.content.fontSize);
        const lineHeight = parseFloat(config.content.fontSize) * config.content.lineHeight + config.content.itemSpacing;
        
        contentItems.forEach((item, index) => {
            const y = firstItemY + index * lineHeight;
            
            // 键名（左对齐）
            card.append('text')
                .attr('class', 'content-key')
                .attr('x', -width / 2 + config.content.padding)
                .attr('y', y)
                .attr('text-anchor', 'start')
                .attr('fill', style.contentTextColor)
                .style('font-size', config.content.fontSize)
                .style('font-weight', '500')
                .style('opacity', 0.8)
                .style('pointer-events', 'none')
                .text(`${item.key}:`);
            
            // 值（右对齐）
            card.append('text')
                .attr('class', 'content-value')
                .attr('x', width / 2 - config.content.padding)
                .attr('y', y)
                .attr('text-anchor', 'end')
                .attr('fill', style.contentTextColor)
                .style('font-size', config.content.fontSize)
                .style('pointer-events', 'none')
                .text(item.value);
        });
    }
    
    /**
     * 绘制状态栏
     * @param {Object} card - 卡片容器
     * @param {Object} nodeData - 节点数据
     * @param {number} width - 卡片宽度
     * @param {number} height - 状态栏高度
     * @param {number} y - Y坐标位置
     */
    drawFooter(card, nodeData, width, height, y) {
        const { config, style } = this;
        
        // 状态栏背景
        card.append('rect')
            .attr('class', 'card-footer')
            .attr('width', width)
            .attr('height', height)
            .attr('x', -width / 2)
            .attr('y', y)
            .attr('rx', config.borderRadius)
            .attr('ry', config.borderRadius)
            .attr('fill', style.footerColor);
        
        // 隐藏状态栏上方的圆角（只保留下方圆角）
        card.append('rect')
            .attr('width', width)
            .attr('height', config.borderRadius)
            .attr('x', -width / 2)
            .attr('y', y)
            .attr('fill', style.footerColor);
        
        // 绘制类型Chip
        this.drawTypeChip(card, nodeData.type, -width / 2 + config.footer.padding, y + height / 2);
        
        // 如果有依赖数量，绘制依赖Chip
        if (nodeData.dependencyCount > 0) {
            const chipWidth = this.estimateChipWidth(`依赖 ${nodeData.dependencyCount}`);
            const chipX = width / 2 - config.footer.padding - chipWidth / 2;
            this.drawDependencyChip(card, nodeData.dependencyCount, chipX, y + height / 2);
        }
    }
    
    /**
     * 绘制类型Chip - 模仿MudBlazor Chip样式
     * @param {Object} container - 容器元素
     * @param {string} type - 类型文本
     * @param {number} x - X坐标
     * @param {number} y - Y坐标
     */
    drawTypeChip(container, type, x, y) {
        const { config } = this;
        const chipWidth = this.estimateChipWidth(type);
        
        // Chip背景
        container.append('rect')
            .attr('class', 'type-chip')
            .attr('width', chipWidth)
            .attr('height', config.footer.chipHeight)
            .attr('x', x)
            .attr('y', y - config.footer.chipHeight / 2)
            .attr('rx', config.footer.chipBorderRadius)
            .attr('ry', config.footer.chipBorderRadius)
            .attr('fill', this.isDarkMode ? 'var(--mud-palette-primary-darken, #4a44bc)' : 'var(--mud-palette-primary-lighten, #a394f7)')
            .attr('stroke', 'var(--mud-palette-primary, #594ae2)')
            .attr('stroke-width', 1)
            .style('opacity', 0.9);
        
        // Chip文字
        container.append('text')
            .attr('class', 'type-chip-text')
            .attr('x', x + chipWidth / 2)
            .attr('y', y + 3)
            .attr('text-anchor', 'middle')
            .attr('fill', 'var(--mud-palette-primary-text, #ffffff)')
            .style('font-size', config.footer.chipFontSize)
            .style('font-weight', '500')
            .style('pointer-events', 'none')
            .text(type);
    }
    
    /**
     * 绘制依赖数量Chip
     * @param {Object} container - 容器元素
     * @param {number} count - 依赖数量
     * @param {number} x - X坐标
     * @param {number} y - Y坐标
     */
    drawDependencyChip(container, count, x, y) {
        const { config } = this;
        const text = `依赖 ${count}`;
        const chipWidth = this.estimateChipWidth(text);
        
        // Chip背景
        container.append('rect')
            .attr('class', 'dependency-chip')
            .attr('width', chipWidth)
            .attr('height', config.footer.chipHeight)
            .attr('x', x - chipWidth / 2)
            .attr('y', y - config.footer.chipHeight / 2)
            .attr('rx', config.footer.chipBorderRadius)
            .attr('ry', config.footer.chipBorderRadius)
            .attr('fill', this.isDarkMode ? 'var(--mud-palette-info-darken, #0c80df)' : 'var(--mud-palette-info-lighten, #47a7f5)')
            .attr('stroke', 'var(--mud-palette-info, #2196f3)')
            .attr('stroke-width', 1)
            .style('opacity', 0.9);
        
        // Chip文字
        container.append('text')
            .attr('class', 'dependency-chip-text')
            .attr('x', x)
            .attr('y', y + 3)
            .attr('text-anchor', 'middle')
            .attr('fill', 'var(--mud-palette-info-text, #ffffff)')
            .style('font-size', config.footer.chipFontSize)
            .style('font-weight', '500')
            .style('pointer-events', 'none')
            .text(text);
    }
    
    /**
     * 估算Chip宽度
     * @param {string} text - Chip文本
     * @returns {number} 估算宽度
     */
    estimateChipWidth(text) {
        const { config } = this;
        const textWidth = this.estimateTextWidth(text, config.footer.chipFontSize, '500');
        return textWidth + config.footer.chipPadding * 2;
    }
    
    /**
     * 更新主题
     * @param {boolean} isDarkMode - 是否为暗色模式
     */
    updateTheme(isDarkMode) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
    }
}

/**
 * 创建复杂节点卡片渲染器实例
 * @param {boolean} isDarkMode - 是否为暗色模式
 * @returns {ComplexNodeCardRenderer} 渲染器实例
 */
export function createComplexNodeCardRenderer(isDarkMode = false) {
    return new ComplexNodeCardRenderer(isDarkMode);
}