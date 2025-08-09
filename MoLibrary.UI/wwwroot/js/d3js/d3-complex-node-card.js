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
    constructor(isDarkMode = false, sizeConfig = null) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
        
        // 卡片布局配置 - 使用传入的配置或默认值
        this.config = {
            minWidth: sizeConfig?.minWidth || 180,
            maxWidth: sizeConfig?.maxWidth || 280,
            padding: 12,
            borderRadius: 0,  // 改为直角
            
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
        const titleWidth = this.estimateTextWidth(nodeData.title, config.header.fontSize, config.header.fontWeight) + 50;
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
        
        // 计算状态栏高度（基于chips数量和换行）
        const footerHeight = this.calculateFooterHeight(nodeData, width);
        const height = config.header.height + contentHeight + footerHeight;
        
        return { width, height };
    }
    
    /**
     * 估算文本宽度（使用Canvas精确测量）
     * @param {string} text - 文本内容
     * @param {string} fontSize - 字体大小
     * @param {string} fontWeight - 字体粗细
     * @returns {number} 估算宽度
     */
    estimateTextWidth(text, fontSize, fontWeight = 'normal') {
        // 创建或复用Canvas上下文进行精确测量
        if (!this._measureCanvas) {
            this._measureCanvas = document.createElement('canvas');
            this._measureContext = this._measureCanvas.getContext('2d');
        }
        
        // 设置字体样式 - 使用系统字体堆栈
        this._measureContext.font = `${fontWeight} ${fontSize} -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`;
        
        // 测量文本宽度
        const metrics = this._measureContext.measureText(text);
        return metrics.width;
    }
    
    /**
     * 计算状态栏高度（支持多行chips）
     * @param {Object} nodeData - 节点数据
     * @param {number} width - 卡片宽度
     * @returns {number} 状态栏高度
     */
    calculateFooterHeight(nodeData, width) {
        const { config } = this;
        const chips = nodeData.chips || [];
        
        if (chips.length === 0) {
            return config.footer.height; // 最小高度
        }
        
        // 模拟chips布局，计算需要多少行
        const availableWidth = width - config.footer.padding * 2;
        let currentRowWidth = 0;
        let rowCount = 1;
        const chipSpacing = 6; // chips之间的间距
        
        chips.forEach((chip, index) => {
            const hasIcon = chip.icon && chip.icon.trim() !== '';
            const chipWidth = this.estimateChipWidth(chip.text, hasIcon);
            
            if (index > 0) {
                currentRowWidth += chipSpacing;
            }
            
            if (currentRowWidth + chipWidth > availableWidth && index > 0) {
                // 换行
                rowCount++;
                currentRowWidth = chipWidth;
            } else {
                currentRowWidth += chipWidth;
            }
        });
        
        // 计算总高度，确保上下margin相等
        // 基础padding(上下各8px) + chip总高度
        const chipsTotalHeight = rowCount * config.footer.chipHeight + (rowCount - 1) * 6;
        const verticalPadding = config.footer.padding * 2; // 上下padding
        
        return Math.max(config.footer.height, chipsTotalHeight + verticalPadding);
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
            .attr('class', 'complex-node-card')
            .attr('data-alert-level', nodeData.alertLevel || 'none');
        
        // 添加阴影效果（使用filter而不是drop-shadow以获得更好的性能）
        const shadowFilter = card.append('filter')
            .attr('id', `shadow-${nodeData.id || Math.random().toString(36).substr(2, 9)}`)
            .attr('x', '-50%')
            .attr('y', '-50%')
            .attr('width', '200%')
            .attr('height', '200%');
        
        shadowFilter.append('feDropShadow')
            .attr('dx', 0)
            .attr('dy', 2)
            .attr('stdDeviation', 3)
            .attr('flood-opacity', 0.15);
        
        // 绘制主卡片背景（作为整体边框）
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
            .style('filter', `url(#shadow-${nodeData.id || Math.random().toString(36).substr(2, 9)})`)
            .style('cursor', 'pointer');
        
        // 绘制标题栏
        this.drawHeader(card, nodeData, width, height, config.header.height);
        
        // 绘制内容区
        const contentItems = nodeData.metadata || [];
        const contentHeight = height - config.header.height - config.footer.height;
        const contentStartY = -height / 2 + config.header.height; // 内容区从标题栏下方开始
        this.drawContent(card, contentItems, width, contentHeight, contentStartY);
        
        // 绘制状态栏 - 使用动态计算的高度
        const footerHeight = this.calculateFooterHeight(nodeData, width);
        const footerY = height / 2 - footerHeight;
        this.drawFooter(card, nodeData, width, footerHeight, footerY);
        
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
        
        // 标题栏背景（内部填充，不覆盖边框）
        card.append('rect')
            .attr('class', 'card-header')
            .attr('width', width - style.strokeWidth * 2)  // 减去边框宽度
            .attr('height', headerHeight)
            .attr('x', -width / 2 + style.strokeWidth)  // 内缩边框宽度
            .attr('y', headerY + style.strokeWidth)  // 内缩边框宽度
            .attr('fill', style.headerColor);
        
        // 绘制Icon+标题（居左对齐）
        this.drawHeaderContent(card, nodeData, width, headerY, headerHeight);
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
     * @param {number} footerHeight - 状态栏高度（动态计算的实际高度）
     * @param {number} y - Y坐标位置
     */
    drawFooter(card, nodeData, width, footerHeight, y) {
        const { config, style } = this;
        
        // 状态栏背景（内部填充，不覆盖边框）
        card.append('rect')
            .attr('class', 'card-footer')
            .attr('width', width - style.strokeWidth * 2)  // 减去边框宽度
            .attr('height', footerHeight - style.strokeWidth)  // 减去底部边框宽度
            .attr('x', -width / 2 + style.strokeWidth)  // 内缩边框宽度
            .attr('y', y)
            .attr('fill', style.footerColor);
        
        // 绘制chips（支持多行布局）
        this.drawChipsLayout(card, nodeData.chips || [], width, footerHeight, y);
    }
    
    /**
     * 计算chips的行分组
     * @param {Array} chips - chips数组
     * @param {number} availableWidth - 可用宽度
     * @param {number} chipSpacing - chip间距
     * @returns {Array<Array>} 每行的chips数组
     */
    calculateChipRows(chips, availableWidth, chipSpacing) {
        const rows = [];
        let currentRow = [];
        let currentRowWidth = 0;
        
        chips.forEach((chip, index) => {
            const hasIcon = chip.icon && chip.icon.trim() !== '';
            const chipWidth = this.estimateChipWidth(chip.text, hasIcon);
            const needSpacing = currentRow.length > 0 ? chipSpacing : 0;
            
            if (currentRow.length > 0 && currentRowWidth + needSpacing + chipWidth > availableWidth) {
                // 需要换行，保存当前行并开始新行
                rows.push(currentRow);
                currentRow = [chip];
                currentRowWidth = chipWidth;
            } else {
                // 可以放在当前行
                currentRow.push(chip);
                currentRowWidth += needSpacing + chipWidth;
            }
        });
        
        // 添加最后一行
        if (currentRow.length > 0) {
            rows.push(currentRow);
        }
        
        return rows;
    }
    
    /**
     * 绘制chips布局（支持多行、居右对齐）
     * @param {Object} container - 容器元素
     * @param {Array} chips - chips数组
     * @param {number} width - 容器宽度
     * @param {number} height - 容器高度
     * @param {number} startY - 起始Y坐标
     */
    drawChipsLayout(container, chips, width, height, startY) {
        const { config } = this;
        
        if (!chips || chips.length === 0) return;
        
        const availableWidth = width - config.footer.padding * 2;
        const chipSpacing = 6; // chips之间的间距
        const rowSpacing = 6; // 行间距
        
        // 计算每行的chips分组
        const rows = this.calculateChipRows(chips, availableWidth, chipSpacing);
        
        // 计算所有行的总高度
        const totalRowsHeight = rows.length * config.footer.chipHeight + (rows.length - 1) * rowSpacing;
        
        // 计算垂直居中的起始位置
        const verticalCenterOffset = (height - totalRowsHeight) / 2;
        
        // 从上往下绘制每一行
        rows.forEach((row, rowIndex) => {
            // 垂直居中计算：使用居中偏移量
            const rowY = startY + verticalCenterOffset + config.footer.chipHeight / 2 + 
                        rowIndex * (config.footer.chipHeight + rowSpacing);
            
            // 计算该行的总宽度以实现右对齐
            const rowTotalWidth = row.reduce((total, chip, index) => {
                const hasIcon = chip.icon && chip.icon.trim() !== '';
                return total + this.estimateChipWidth(chip.text, hasIcon) + (index > 0 ? chipSpacing : 0);
            }, 0);
            
            // 从右边开始绘制（居右对齐）
            let currentX = width / 2 - config.footer.padding - rowTotalWidth;
            
            row.forEach((chip, chipIndex) => {
                const hasIcon = chip.icon && chip.icon.trim() !== '';
                const chipWidth = this.estimateChipWidth(chip.text, hasIcon);
                
                if (chipIndex > 0) {
                    currentX += chipSpacing;
                }
                
                // 绘制单个chip
                this.drawChip(container, chip, currentX, rowY);
                
                currentX += chipWidth;
            });
        });
    }
    
    /**
     * 绘制单个Chip - 模仿MudBlazor Chip样式
     * @param {Object} container - 容器元素
     * @param {Object} chipData - chip数据 {text, color, icon}
     * @param {number} x - X坐标
     * @param {number} y - Y坐标（中心点）
     */
    drawChip(container, chipData, x, y) {
        const { config } = this;
        const hasIcon = chipData.icon && chipData.icon.trim() !== '';
        const iconSize = 14; // chip icon 尺寸
        const iconPadding = hasIcon ? 4 : 0; // icon 和文字之间的间距
        
        const chipWidth = this.estimateChipWidth(chipData.text, hasIcon);
        
        // 获取chip颜色
        const colors = this.getChipColors(chipData.color);
        
        // Chip背景
        container.append('rect')
            .attr('class', `chip chip-${chipData.color}`)
            .attr('width', chipWidth)
            .attr('height', config.footer.chipHeight)
            .attr('x', x)
            .attr('y', y - config.footer.chipHeight / 2)
            .attr('rx', config.footer.chipBorderRadius)
            .attr('ry', config.footer.chipBorderRadius)
            .attr('fill', colors.background)
            .attr('stroke', colors.border)
            .attr('stroke-width', 1)
            .style('opacity', 0.9);
        
        let textX = x + chipWidth / 2; // 默认居中
        
        // 绘制图标（如果有）
        if (hasIcon) {
            const iconX = x + config.footer.chipPadding;
            this.drawChipIcon(container, chipData.icon, iconX, y, iconSize, colors.text);
            
            // 调整文字位置，紧跟在图标后面
            textX = iconX + iconSize + iconPadding;
        }
        
        // Chip文字
        container.append('text')
            .attr('class', 'chip-text')
            .attr('x', textX)
            .attr('y', y + 3)
            .attr('text-anchor', hasIcon ? 'start' : 'middle')
            .attr('fill', colors.text)
            .style('font-size', config.footer.chipFontSize)
            .style('font-weight', '500')
            .style('pointer-events', 'none')
            .text(chipData.text);
    }
    
    /**
     * 获取chip颜色配置
     * @param {string} colorName - 颜色名称或十六进制颜色
     * @returns {Object} 颜色配置
     */
    getChipColors(colorName) {
        const isDark = this.isDarkMode;
        
        // 如果是十六进制颜色，直接使用
        if (colorName && colorName.startsWith('#')) {
            return {
                background: colorName + '20', // 20% opacity for background
                border: colorName,
                text: colorName
            };
        }
        
        switch (colorName) {
            case 'primary':
                return {
                    background: isDark ? 'var(--mud-palette-primary-darken, #4a44bc)' : 'var(--mud-palette-primary-lighten, #a394f7)',
                    border: 'var(--mud-palette-primary, #594ae2)',
                    text: 'var(--mud-palette-primary-text, #ffffff)'
                };
            case 'info':
                return {
                    background: isDark ? 'var(--mud-palette-info-darken, #0c80df)' : 'var(--mud-palette-info-lighten, #47a7f5)',
                    border: 'var(--mud-palette-info, #2196f3)',
                    text: 'var(--mud-palette-info-text, #ffffff)'
                };
            case 'secondary':
                return {
                    background: isDark ? 'var(--mud-palette-secondary-darken, #ff1f69)' : 'var(--mud-palette-secondary-lighten, #ff66a1)',
                    border: 'var(--mud-palette-secondary, #ff4081)',
                    text: 'var(--mud-palette-secondary-text, #ffffff)'
                };
            case 'success':
                return {
                    background: isDark ? 'var(--mud-palette-success-darken, #00a343)' : 'var(--mud-palette-success-lighten, #00eb62)',
                    border: 'var(--mud-palette-success, #00c853)',
                    text: 'var(--mud-palette-success-text, #ffffff)'
                };
            case 'warning':
                return {
                    background: isDark ? 'var(--mud-palette-warning-darken, #d68100)' : 'var(--mud-palette-warning-lighten, #ffa724)',
                    border: 'var(--mud-palette-warning, #ff9800)',
                    text: 'var(--mud-palette-warning-text, #ffffff)'
                };
            case 'error':
                return {
                    background: isDark ? 'var(--mud-palette-error-darken, #f21c0d)' : 'var(--mud-palette-error-lighten, #f66055)',
                    border: 'var(--mud-palette-error, #f44336)',
                    text: 'var(--mud-palette-error-text, #ffffff)'
                };
            default:
                return {
                    background: isDark ? 'var(--mud-palette-dark-darken, #2e2e38)' : 'var(--mud-palette-dark-lighten, #575743)',
                    border: 'var(--mud-palette-dark, #424242)',
                    text: 'var(--mud-palette-dark-text, #ffffff)'
                };
        }
    }
    
    /**
     * 绘制Chip图标
     * @param {Object} container - 容器元素
     * @param {string} iconSvg - MudBlazor Icon SVG内容
     * @param {number} x - X坐标
     * @param {number} y - Y坐标（中心）
     * @param {number} size - 图标尺寸
     * @param {string} color - 图标颜色
     */
    drawChipIcon(container, iconSvg, x, y, size, color) {
        // 创建Icon容器
        const iconGroup = container.append('g')
            .attr('class', 'chip-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        // 解析并绘制MudBlazor SVG图标
        this.renderMudBlazorIcon(iconGroup, iconSvg, color);
    }
    
    /**
     * 估算Chip宽度
     * @param {string} text - Chip文本
     * @param {boolean} hasIcon - 是否有图标
     * @returns {number} 估算宽度
     */
    estimateChipWidth(text, hasIcon = false) {
        const { config } = this;
        const textWidth = this.estimateTextWidth(text, config.footer.chipFontSize, '500');
        
        // 基础宽度计算
        let width = config.footer.chipPadding; // 左边距
        
        // 如果有图标，增加图标占用的宽度
        if (hasIcon) {
            width += 14 + 4; // iconSize + iconPadding
        }
        
        // 添加文字宽度
        width += textWidth;
        
        // 添加右边距
        width += config.footer.chipPadding;
        
        return width;
    }
    
    /**
     * 绘制标题栏内容（Icon + 标题，居左对齐）
     * @param {Object} container - 容器元素
     * @param {Object} nodeData - 节点数据
     * @param {number} width - 标题栏宽度
     * @param {number} headerY - 标题栏Y坐标
     * @param {number} headerHeight - 标题栏高度
     */
    drawHeaderContent(container, nodeData, width, headerY, headerHeight) {
        const { config, style } = this;
        const leftPadding = config.header.padding;
        const iconSize = 16; // Icon尺寸
        const iconTextSpacing = 8; // Icon和文字之间的间距
        
        let currentX = -width / 2 + leftPadding;
        // 考虑边框内缩，准确计算中心Y坐标
        const centerY = headerY + style.strokeWidth + headerHeight / 2;
        
        // 绘制Icon（如果有）
        if (nodeData.icon) {
            this.drawHeaderIcon(container, nodeData.icon, currentX, centerY, iconSize);
            currentX += iconSize + iconTextSpacing;
        }
        
        // 绘制标题文字 - 使用dominant-baseline配合微调实现视觉居中
        container.append('text')
            .attr('class', 'card-title')
            .attr('x', currentX)
            .attr('y', centerY + 1) // 微调1px补偿字体基线
            .attr('text-anchor', 'start') // 左对齐
            .attr('dominant-baseline', 'middle') // 垂直居中
            .attr('fill', style.headerTextColor)
            .style('font-size', config.header.fontSize)
            .style('font-weight', config.header.fontWeight)
            .style('pointer-events', 'none')
            .text(nodeData.title);
    }
    
    /**
     * 绘制标题栏图标
     * @param {Object} container - 容器元素
     * @param {string} iconSvg - MudBlazor Icon SVG内容
     * @param {number} x - X坐标
     * @param {number} y - Y坐标（中心）
     * @param {number} size - 图标尺寸
     */
    drawHeaderIcon(container, iconSvg, x, y, size) {
        const { style } = this;
        
        // 创建Icon容器
        const iconGroup = container.append('g')
            .attr('class', 'header-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        // 解析并绘制MudBlazor SVG图标
        this.renderMudBlazorIcon(iconGroup, iconSvg, style.headerTextColor);
    }
    
    /**
     * 渲染MudBlazor SVG图标
     * @param {Object} container - 容器元素
     * @param {string} iconSvg - MudBlazor Icon SVG内容字符串
     * @param {string} color - 图标颜色
     */
    renderMudBlazorIcon(container, iconSvg, color) {
        if (!iconSvg || iconSvg.trim() === '') {
            // 如果没有图标，显示默认圆点
            container.append('circle')
                .attr('cx', 12)
                .attr('cy', 12)
                .attr('r', 2)
                .attr('fill', color)
                .style('pointer-events', 'none');
            return;
        }
        
        try {
            // MudBlazor Icons包含完整的SVG内容，需要解析
            const parser = new DOMParser();
            const svgDoc = parser.parseFromString(`<svg xmlns="http://www.w3.org/2000/svg">${iconSvg}</svg>`, 'image/svg+xml');
            
            // 查找所有path元素，但跳过fill="none"的占位path
            const paths = svgDoc.querySelectorAll('path:not([fill="none"])');
            
            paths.forEach(path => {
                const pathData = path.getAttribute('d');
                if (pathData) {
                    container.append('path')
                        .attr('d', pathData)
                        .attr('fill', color)
                        .style('pointer-events', 'none');
                }
            });
            
            // 如果没有找到有效的path，显示默认图标
            if (paths.length === 0) {
                container.append('circle')
                    .attr('cx', 12)
                    .attr('cy', 12)
                    .attr('r', 3)
                    .attr('fill', color)
                    .style('pointer-events', 'none');
            }
            
        } catch (error) {
            console.warn('Failed to parse MudBlazor icon:', error);
            // 解析失败时显示默认圆点
            container.append('circle')
                .attr('cx', 12)
                .attr('cy', 12)
                .attr('r', 3)
                .attr('fill', color)
                .style('pointer-events', 'none');
        }
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
 * @param {Object} sizeConfig - 尺寸配置 {minWidth, maxWidth, minHeight}
 * @returns {ComplexNodeCardRenderer} 渲染器实例
 */
export function createComplexNodeCardRenderer(isDarkMode = false, sizeConfig = null) {
    return new ComplexNodeCardRenderer(isDarkMode, sizeConfig);
}