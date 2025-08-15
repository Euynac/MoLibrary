/**
 * 项目单元卡片渲染器
 * 专门用于渲染项目单元的复杂节点卡片
 * 
 * @module projectUnitCardRenderer
 */

import { getModernNodeStyle } from '../../MoLibrary.UI/js/d3js/d3-graph-base.js';

/**
 * 项目单元卡片渲染器类
 * 提供三层布局的现代化卡片式节点绘制
 */
export class ProjectUnitCardRenderer {
    constructor(isDarkMode = false, sizeConfig = null) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
        
        // 卡片布局配置
        this.config = {
            minWidth: sizeConfig?.minWidth || 180,
            maxWidth: sizeConfig?.maxWidth || 280,
            padding: 12,
            borderRadius: 0,  // 直角设计
            
            // 标题栏配置
            header: {
                height: 40,
                padding: 12,
                fontSize: '14px',
                fontWeight: '600'
            },
            
            // 内容区配置（显示方法信息等）
            content: {
                minHeight: 60,
                padding: 12,
                fontSize: '13px',
                lineHeight: 1.4,
                itemSpacing: 8
            },
            
            // 状态栏配置（显示Chips）
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
     * 计算项目单元卡片尺寸
     * @param {Object} nodeData - 节点数据
     * @returns {Object} 尺寸信息 {width, height}
     */
    calculateCardSize(nodeData) {
        const { config } = this;
        const contentItems = nodeData.metadata || [];
        
        // 计算标题宽度（包括图标空间）
        const titleWidth = this.estimateTextWidth(nodeData.title, config.header.fontSize, config.header.fontWeight) + 80;
        
        // 计算内容宽度 - 特别处理方法信息
        let maxContentWidth = config.minWidth;
        if (contentItems.length > 0) {
            contentItems.forEach(item => {
                const keyWidth = this.estimateTextWidth(`${item.key}: `, config.content.fontSize, '500');
                const valueWidth = this.estimateTextWidth(item.value, config.content.fontSize);
                
                // 对于方法签名，限制最大宽度
                let effectiveValueWidth = valueWidth;
                if (item.key === '方法' && valueWidth > 200) {
                    effectiveValueWidth = Math.min(valueWidth, 250);
                }
                
                const totalWidth = keyWidth + effectiveValueWidth + config.content.padding * 2;
                maxContentWidth = Math.max(maxContentWidth, totalWidth);
            });
        }
        
        // 根据内容复杂度动态调整最大宽度
        let dynamicMaxWidth = config.maxWidth;
        if (contentItems.length > 3) {
            dynamicMaxWidth = Math.min(config.maxWidth * 1.5, 400);
        }
        
        const width = Math.max(config.minWidth, Math.min(dynamicMaxWidth, Math.max(titleWidth, maxContentWidth)));
        
        // 计算高度
        const contentHeight = contentItems.length > 0 
            ? Math.max(
                config.content.minHeight,
                contentItems.length * (parseFloat(config.content.fontSize) * config.content.lineHeight + config.content.itemSpacing) + config.content.padding * 2
              )
            : config.content.minHeight;
        
        const footerHeight = this.calculateFooterHeight(nodeData, width);
        const height = config.header.height + contentHeight + footerHeight;
        
        return { width, height };
    }
    
    /**
     * 估算文本宽度
     */
    estimateTextWidth(text, fontSize, fontWeight = 'normal') {
        if (!this._measureCanvas) {
            this._measureCanvas = document.createElement('canvas');
            this._measureContext = this._measureCanvas.getContext('2d');
        }
        
        this._measureContext.font = `${fontWeight} ${fontSize} -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`;
        const metrics = this._measureContext.measureText(text);
        return metrics.width;
    }
    
    /**
     * 计算状态栏高度（支持多行chips）
     */
    calculateFooterHeight(nodeData, width) {
        const { config } = this;
        const chips = nodeData.chips || [];
        
        if (chips.length === 0) {
            return config.footer.height;
        }
        
        const availableWidth = width - config.footer.padding * 2;
        let currentRowWidth = 0;
        let rowCount = 1;
        const chipSpacing = 6;
        
        chips.forEach((chip, index) => {
            const hasIcon = chip.icon && chip.icon.trim() !== '';
            const chipWidth = this.estimateChipWidth(chip.text, hasIcon);
            
            if (index > 0) {
                currentRowWidth += chipSpacing;
            }
            
            if (currentRowWidth + chipWidth > availableWidth && index > 0) {
                rowCount++;
                currentRowWidth = chipWidth;
            } else {
                currentRowWidth += chipWidth;
            }
        });
        
        const chipsTotalHeight = rowCount * config.footer.chipHeight + (rowCount - 1) * 6;
        const verticalPadding = config.footer.padding * 2;
        
        return Math.max(config.footer.height, chipsTotalHeight + verticalPadding);
    }
    
    /**
     * 绘制项目单元卡片
     */
    drawCard(nodeElement, nodeData) {
        const { width, height } = this.calculateCardSize(nodeData);
        const { config, style } = this;
        
        nodeElement.selectAll('*').remove();
        
        const card = nodeElement.append('g')
            .attr('class', 'project-unit-card')
            .attr('data-alert-level', nodeData.alertLevel || 'none');
        
        // 添加阴影
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
        
        // 绘制卡片背景
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
        
        // 绘制三层结构
        this.drawHeader(card, nodeData, width, height, config.header.height);
        
        const contentItems = nodeData.metadata || [];
        const contentHeight = height - config.header.height - this.calculateFooterHeight(nodeData, width);
        const contentStartY = -height / 2 + config.header.height;
        this.drawContent(card, contentItems, width, contentHeight, contentStartY);
        
        const footerHeight = this.calculateFooterHeight(nodeData, width);
        const footerY = height / 2 - footerHeight;
        this.drawFooter(card, nodeData, width, footerHeight, footerY);
        
        // 存储尺寸信息
        nodeData._cardSize = { width, height };
        
        return card;
    }
    
    /**
     * 绘制标题栏
     */
    drawHeader(card, nodeData, width, cardHeight, headerHeight) {
        const { config, style } = this;
        const headerY = -cardHeight / 2;
        
        // 标题栏背景
        card.append('rect')
            .attr('class', 'card-header')
            .attr('width', width - style.strokeWidth * 2)
            .attr('height', headerHeight)
            .attr('x', -width / 2 + style.strokeWidth)
            .attr('y', headerY + style.strokeWidth)
            .attr('fill', style.headerColor);
        
        // 绘制图标和标题
        this.drawHeaderContent(card, nodeData, width, headerY, headerHeight);
    }
    
    /**
     * 绘制内容区 - 显示方法信息等元数据
     */
    drawContent(card, contentItems, width, height, startY) {
        const { config, style } = this;
        
        if (contentItems.length === 0) {
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
        
        const firstItemY = startY + config.content.padding + parseFloat(config.content.fontSize);
        const lineHeight = parseFloat(config.content.fontSize) * config.content.lineHeight + config.content.itemSpacing;
        
        contentItems.forEach((item, index) => {
            const y = firstItemY + index * lineHeight;
            
            const keyText = `${item.key}:`;
            const keyWidth = this.estimateTextWidth(keyText, config.content.fontSize, '500');
            
            // 键名
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
                .text(keyText);
            
            // 值（智能截断）
            const valueStartX = -width / 2 + config.content.padding + keyWidth + 5;
            let displayValue = item.value;
            
            // 对方法信息进行特殊处理
            if (item.key === '方法') {
                const colonIndex = item.value.indexOf(':');
                if (colonIndex > 0) {
                    const methodName = item.value.substring(0, colonIndex);
                    const description = item.value.substring(colonIndex + 1).trim();
                    
                    if (description.length > 30) {
                        displayValue = `${methodName}: ${description.substring(0, 27)}...`;
                    }
                } else if (item.value.length > 35) {
                    displayValue = item.value.substring(0, 32) + '...';
                }
            } else if (item.value.length > 40) {
                displayValue = item.value.substring(0, 37) + '...';
            }
            
            const valueText = card.append('text')
                .attr('class', 'content-value')
                .attr('x', valueStartX)
                .attr('y', y)
                .attr('text-anchor', 'start')
                .attr('fill', style.contentTextColor)
                .style('font-size', config.content.fontSize)
                .style('pointer-events', 'none')
                .text(displayValue);
            
            // 添加tooltip显示完整内容
            valueText.append('title').text(item.value);
        });
    }
    
    /**
     * 绘制状态栏 - 显示各种状态Chips
     */
    drawFooter(card, nodeData, width, footerHeight, y) {
        const { config, style } = this;
        
        // 状态栏背景
        card.append('rect')
            .attr('class', 'card-footer')
            .attr('width', width - style.strokeWidth * 2)
            .attr('height', footerHeight - style.strokeWidth)
            .attr('x', -width / 2 + style.strokeWidth)
            .attr('y', y)
            .attr('fill', style.footerColor);
        
        // 绘制chips
        this.drawChipsLayout(card, nodeData.chips || [], width, footerHeight, y);
    }
    
    /**
     * 计算chips的行分组
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
                rows.push(currentRow);
                currentRow = [chip];
                currentRowWidth = chipWidth;
            } else {
                currentRow.push(chip);
                currentRowWidth += needSpacing + chipWidth;
            }
        });
        
        if (currentRow.length > 0) {
            rows.push(currentRow);
        }
        
        return rows;
    }
    
    /**
     * 绘制chips布局
     */
    drawChipsLayout(container, chips, width, height, startY) {
        const { config } = this;
        
        if (!chips || chips.length === 0) return;
        
        const availableWidth = width - config.footer.padding * 2;
        const chipSpacing = 6;
        const rowSpacing = 6;
        
        const rows = this.calculateChipRows(chips, availableWidth, chipSpacing);
        const totalRowsHeight = rows.length * config.footer.chipHeight + (rows.length - 1) * rowSpacing;
        const verticalCenterOffset = (height - totalRowsHeight) / 2;
        
        rows.forEach((row, rowIndex) => {
            const rowY = startY + verticalCenterOffset + config.footer.chipHeight / 2 + 
                        rowIndex * (config.footer.chipHeight + rowSpacing);
            
            const rowTotalWidth = row.reduce((total, chip, index) => {
                const hasIcon = chip.icon && chip.icon.trim() !== '';
                return total + this.estimateChipWidth(chip.text, hasIcon) + (index > 0 ? chipSpacing : 0);
            }, 0);
            
            let currentX = width / 2 - config.footer.padding - rowTotalWidth;
            
            row.forEach((chip, chipIndex) => {
                const hasIcon = chip.icon && chip.icon.trim() !== '';
                const chipWidth = this.estimateChipWidth(chip.text, hasIcon);
                
                if (chipIndex > 0) {
                    currentX += chipSpacing;
                }
                
                this.drawChip(container, chip, currentX, rowY);
                currentX += chipWidth;
            });
        });
    }
    
    /**
     * 绘制单个Chip
     */
    drawChip(container, chipData, x, y) {
        const { config } = this;
        const hasIcon = chipData.icon && chipData.icon.trim() !== '';
        const iconSize = 14;
        const iconPadding = hasIcon ? 4 : 0;
        
        const chipWidth = this.estimateChipWidth(chipData.text, hasIcon);
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
        
        let textX = x + chipWidth / 2;
        
        // 绘制图标
        if (hasIcon) {
            const iconX = x + config.footer.chipPadding;
            this.drawChipIcon(container, chipData.icon, iconX, y, iconSize, colors.text);
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
     */
    getChipColors(colorName) {
        const isDark = this.isDarkMode;
        
        if (colorName && colorName.startsWith('#')) {
            return {
                background: colorName + '20',
                border: colorName,
                text: colorName
            };
        }
        
        // MudBlazor颜色映射
        const colorMap = {
            'primary': {
                background: isDark ? 'var(--mud-palette-primary-darken, #4a44bc)' : 'var(--mud-palette-primary-lighten, #a394f7)',
                border: 'var(--mud-palette-primary, #594ae2)',
                text: 'var(--mud-palette-primary-text, #ffffff)'
            },
            'info': {
                background: isDark ? 'var(--mud-palette-info-darken, #0c80df)' : 'var(--mud-palette-info-lighten, #47a7f5)',
                border: 'var(--mud-palette-info, #2196f3)',
                text: 'var(--mud-palette-info-text, #ffffff)'
            },
            'secondary': {
                background: isDark ? 'var(--mud-palette-secondary-darken, #ff1f69)' : 'var(--mud-palette-secondary-lighten, #ff66a1)',
                border: 'var(--mud-palette-secondary, #ff4081)',
                text: 'var(--mud-palette-secondary-text, #ffffff)'
            },
            'success': {
                background: isDark ? 'var(--mud-palette-success-darken, #00a343)' : 'var(--mud-palette-success-lighten, #00eb62)',
                border: 'var(--mud-palette-success, #00c853)',
                text: 'var(--mud-palette-success-text, #ffffff)'
            },
            'warning': {
                background: isDark ? 'var(--mud-palette-warning-darken, #d68100)' : 'var(--mud-palette-warning-lighten, #ffa724)',
                border: 'var(--mud-palette-warning, #ff9800)',
                text: 'var(--mud-palette-warning-text, #ffffff)'
            },
            'error': {
                background: isDark ? 'var(--mud-palette-error-darken, #f21c0d)' : 'var(--mud-palette-error-lighten, #f66055)',
                border: 'var(--mud-palette-error, #f44336)',
                text: 'var(--mud-palette-error-text, #ffffff)'
            }
        };
        
        return colorMap[colorName] || {
            background: isDark ? 'var(--mud-palette-dark-darken, #2e2e38)' : 'var(--mud-palette-dark-lighten, #575743)',
            border: 'var(--mud-palette-dark, #424242)',
            text: 'var(--mud-palette-dark-text, #ffffff)'
        };
    }
    
    /**
     * 绘制Chip图标
     */
    drawChipIcon(container, iconSvg, x, y, size, color) {
        const iconGroup = container.append('g')
            .attr('class', 'chip-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        this.renderMudBlazorIcon(iconGroup, iconSvg, color);
    }
    
    /**
     * 估算Chip宽度
     */
    estimateChipWidth(text, hasIcon = false) {
        const { config } = this;
        const textWidth = this.estimateTextWidth(text, config.footer.chipFontSize, '500');
        
        let width = config.footer.chipPadding;
        if (hasIcon) {
            width += 14 + 4; // iconSize + iconPadding
        }
        width += textWidth;
        width += config.footer.chipPadding;
        
        return width;
    }
    
    /**
     * 绘制标题栏内容
     */
    drawHeaderContent(container, nodeData, width, headerY, headerHeight) {
        const { config, style } = this;
        const leftPadding = config.header.padding;
        const iconSize = 16;
        const iconTextSpacing = 8;
        
        let currentX = -width / 2 + leftPadding;
        const centerY = headerY + style.strokeWidth + headerHeight / 2;
        
        // 绘制图标
        if (nodeData.icon) {
            this.drawHeaderIcon(container, nodeData.icon, currentX, centerY, iconSize);
            currentX += iconSize + iconTextSpacing;
        }
        
        // 绘制标题
        container.append('text')
            .attr('class', 'card-title')
            .attr('x', currentX)
            .attr('y', centerY + 1)
            .attr('text-anchor', 'start')
            .attr('dominant-baseline', 'middle')
            .attr('fill', style.headerTextColor)
            .style('font-size', config.header.fontSize)
            .style('font-weight', config.header.fontWeight)
            .style('pointer-events', 'none')
            .text(nodeData.title);
    }
    
    /**
     * 绘制标题栏图标
     */
    drawHeaderIcon(container, iconSvg, x, y, size) {
        const { style } = this;
        
        const iconGroup = container.append('g')
            .attr('class', 'header-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        this.renderMudBlazorIcon(iconGroup, iconSvg, style.headerTextColor);
    }
    
    /**
     * 渲染MudBlazor SVG图标
     */
    renderMudBlazorIcon(container, iconSvg, color) {
        if (!iconSvg || iconSvg.trim() === '') {
            container.append('circle')
                .attr('cx', 12)
                .attr('cy', 12)
                .attr('r', 2)
                .attr('fill', color)
                .style('pointer-events', 'none');
            return;
        }
        
        try {
            const parser = new DOMParser();
            const svgDoc = parser.parseFromString(`<svg xmlns="http://www.w3.org/2000/svg">${iconSvg}</svg>`, 'image/svg+xml');
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
     */
    updateTheme(isDarkMode) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
    }
}

/**
 * 创建项目单元卡片渲染器实例
 */
export function createProjectUnitCardRenderer(isDarkMode = false, sizeConfig = null) {
    return new ProjectUnitCardRenderer(isDarkMode, sizeConfig);
}