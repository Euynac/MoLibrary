/**
 * Project unit card renderer
 * Complex node cards dedicated to rendering project units
 * 
 * @module projectUnitCardRenderer
 */

import { getModernNodeStyle } from '../../Monica.UI/js/d3js/d3-graph-base.js';

/**
 * Project unit card renderer class
 * Provides modern card-style node drawing with three-layer layout
 */
export class ProjectUnitCardRenderer {
    constructor(isDarkMode = false, sizeConfig = null) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
        
        // Card layout configuration
        this.config = {
            minWidth: sizeConfig?.minWidth || 180,
            maxWidth: sizeConfig?.maxWidth || 280,
            padding: 12,
            borderRadius: 0,  // 直角设计
            
            // Title bar configuration
            header: {
                height: 40,
                padding: 12,
                fontSize: '14px',
                fontWeight: '600'
            },
            
            // Content area configuration (display method information, etc.)
            content: {
                minHeight: 60,
                padding: 12,
                fontSize: '13px',
                lineHeight: 1.4,
                itemSpacing: 8
            },
            
            // Status bar configuration (showing Chips)
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
     * Calculate project unit card size
     * @param {Object} nodeData - node data
     * @returns {Object} size information {width, height}
     */
    calculateCardSize(nodeData) {
        const { config } = this;
        const contentItems = nodeData.metadata || [];
        
        // Calculate title width (including icon space)
        const titleWidth = this.estimateTextWidth(nodeData.title, config.header.fontSize, config.header.fontWeight) + 80;
        
        // Calculate content width - special handling information
        let maxContentWidth = config.minWidth;
        if (contentItems.length > 0) {
            contentItems.forEach(item => {
                const keyWidth = this.estimateTextWidth(`${item.key}: `, config.content.fontSize, '500');
                const valueWidth = this.estimateTextWidth(item.value, config.content.fontSize);
                
                // For method signatures, limit the maximum width
                let effectiveValueWidth = valueWidth;
                if (item.kind === 'method' && valueWidth > 200) {
                    effectiveValueWidth = Math.min(valueWidth, 250);
                }
                
                const totalWidth = keyWidth + effectiveValueWidth + config.content.padding * 2;
                maxContentWidth = Math.max(maxContentWidth, totalWidth);
            });
        }
        
        // Dynamically adjust the maximum width based on content complexity
        let dynamicMaxWidth = config.maxWidth;
        if (contentItems.length > 3) {
            dynamicMaxWidth = Math.min(config.maxWidth * 1.5, 400);
        }
        
        const width = Math.max(config.minWidth, Math.min(dynamicMaxWidth, Math.max(titleWidth, maxContentWidth)));
        
        // Calculate height
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
     * Estimate text width
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
     * Calculate status bar height (supports multi-line chips)
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
     * Draw project unit cards
     */
    drawCard(nodeElement, nodeData) {
        const { width, height } = this.calculateCardSize(nodeData);
        const { config, style } = this;
        
        nodeElement.selectAll('*').remove();
        
        const card = nodeElement.append('g')
            .attr('class', 'project-unit-card')
            .attr('data-alert-level', nodeData.alertLevel || 'none');
        
        // add shadow
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
        
        // Draw card background
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
        
        // Draw the three card sections.
        this.drawHeader(card, nodeData, width, height, config.header.height);
        
        const contentItems = nodeData.metadata || [];
        const contentHeight = height - config.header.height - this.calculateFooterHeight(nodeData, width);
        const contentStartY = -height / 2 + config.header.height;
        this.drawContent(card, nodeData, contentItems, width, contentHeight, contentStartY);
        
        const footerHeight = this.calculateFooterHeight(nodeData, width);
        const footerY = height / 2 - footerHeight;
        this.drawFooter(card, nodeData, width, footerHeight, footerY);
        
        // Store size information
        nodeData._cardSize = { width, height };
        
        return card;
    }
    
    /**
     * Draw title bar
     */
    drawHeader(card, nodeData, width, cardHeight, headerHeight) {
        const { config, style } = this;
        const headerY = -cardHeight / 2;
        
        // title bar background
        card.append('rect')
            .attr('class', 'card-header')
            .attr('width', width - style.strokeWidth * 2)
            .attr('height', headerHeight)
            .attr('x', -width / 2 + style.strokeWidth)
            .attr('y', headerY + style.strokeWidth)
            .attr('fill', style.headerColor);
        
        // Draw icons and titles
        this.drawHeaderContent(card, nodeData, width, headerY, headerHeight);
    }
    
    /**
     * Draw the content area and show metadata such as methods.
     */
    drawContent(card, nodeData, contentItems, width, height, startY) {
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
                .text(nodeData.noMetadataText || 'No metadata');
            return;
        }
        
        const firstItemY = startY + config.content.padding + parseFloat(config.content.fontSize);
        const lineHeight = parseFloat(config.content.fontSize) * config.content.lineHeight + config.content.itemSpacing;
        
        contentItems.forEach((item, index) => {
            const y = firstItemY + index * lineHeight;
            
            const keyText = `${item.key}:`;
            const keyWidth = this.estimateTextWidth(keyText, config.content.fontSize, '500');
            
            // Key name
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
            
            // value (smart truncation)
            const valueStartX = -width / 2 + config.content.padding + keyWidth + 5;
            let displayValue = item.value;
            
            // Special handling of method information
            if (item.kind === 'method') {
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
            
            // Add tooltip to display complete content
            valueText.append('title').text(item.value);
        });
    }
    
    /**
     * Draw status bar - display various status Chips
     */
    drawFooter(card, nodeData, width, footerHeight, y) {
        const { config, style } = this;
        
        // status bar background
        card.append('rect')
            .attr('class', 'card-footer')
            .attr('width', width - style.strokeWidth * 2)
            .attr('height', footerHeight - style.strokeWidth)
            .attr('x', -width / 2 + style.strokeWidth)
            .attr('y', y)
            .attr('fill', style.footerColor);
        
        // Draw chips
        this.drawChipsLayout(card, nodeData.chips || [], width, footerHeight, y);
    }
    
    /**
     * Calculate row grouping of chips
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
     * Draw chips layout
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
     * Draw a single Chip
     */
    drawChip(container, chipData, x, y) {
        const { config } = this;
        const hasIcon = chipData.icon && chipData.icon.trim() !== '';
        const iconSize = 14;
        const iconPadding = hasIcon ? 4 : 0;
        
        const chipWidth = this.estimateChipWidth(chipData.text, hasIcon);
        const colors = this.getChipColors(chipData.color);
        
        // Chip background
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
        
        // draw icon
        if (hasIcon) {
            const iconX = x + config.footer.chipPadding;
            this.drawChipIcon(container, chipData.icon, iconX, y, iconSize, colors.text);
            textX = iconX + iconSize + iconPadding;
        }
        
        // Chip text
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
     * Get chip color configuration
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
        
        // MudBlazor color mapping
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
     * Draw Chip Icon
     */
    drawChipIcon(container, iconSvg, x, y, size, color) {
        const iconGroup = container.append('g')
            .attr('class', 'chip-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        this.renderMudBlazorIcon(iconGroup, iconSvg, color);
    }
    
    /**
     * Estimate Chip Width
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
     * Draw title bar content
     */
    drawHeaderContent(container, nodeData, width, headerY, headerHeight) {
        const { config, style } = this;
        const leftPadding = config.header.padding;
        const iconSize = 16;
        const iconTextSpacing = 8;
        
        let currentX = -width / 2 + leftPadding;
        const centerY = headerY + style.strokeWidth + headerHeight / 2;
        
        // draw icon
        if (nodeData.icon) {
            this.drawHeaderIcon(container, nodeData.icon, currentX, centerY, iconSize);
            currentX += iconSize + iconTextSpacing;
        }
        
        // draw title
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
     * Draw title bar icon
     */
    drawHeaderIcon(container, iconSvg, x, y, size) {
        const { style } = this;
        
        const iconGroup = container.append('g')
            .attr('class', 'header-icon')
            .attr('transform', `translate(${x + size/2}, ${y}) scale(${size/24}) translate(-12, -12)`);
        
        this.renderMudBlazorIcon(iconGroup, iconSvg, style.headerTextColor);
    }
    
    /**
     * Rendering MudBlazor SVG icon
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
     * Update theme
     */
    updateTheme(isDarkMode) {
        this.isDarkMode = isDarkMode;
        this.style = getModernNodeStyle(isDarkMode, 'complex');
    }
}

/**
 * Create a project unit card renderer instance
 */
export function createProjectUnitCardRenderer(isDarkMode = false, sizeConfig = null) {
    return new ProjectUnitCardRenderer(isDarkMode, sizeConfig);
}
