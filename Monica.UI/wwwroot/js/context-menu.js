window.MoContextMenu = (function() {
    'use strict';
    
    // Get window size
    function getWindowSize() {
        return {
            width: window.innerWidth,
            height: window.innerHeight
        };
    }
    
    // Get element bounds
    function getElementBounds(element) {
        if (!element) return { width: 0, height: 0, x: 0, y: 0 };
        
        var rect = element.getBoundingClientRect();
        return {
            width: rect.width,
            height: rect.height,
            x: rect.left,
            y: rect.top
        };
    }
    
    // Calculate menu width
    function calculateMenuWidth(menuItems) {
        // Create a temporary hidden element to measure text width
        var tempDiv = document.createElement('div');
        tempDiv.style.position = 'absolute';
        tempDiv.style.visibility = 'hidden';
        tempDiv.style.whiteSpace = 'nowrap';
        tempDiv.style.fontSize = '0.875rem'; // 与菜单文字相同的字体大小
        tempDiv.style.fontFamily = getComputedStyle(document.body).fontFamily;
        document.body.appendChild(tempDiv);
        
        var maxWidth = 0;
        
        for (var i = 0; i < menuItems.length; i++) {
            var item = menuItems[i];
            if (item.isDivider) continue;
            
            // Calculate icon width (18px + 16px margin)
            var iconWidth = 34;
            
            // Measure text width
            tempDiv.textContent = item.text || '';
            var textWidth = tempDiv.offsetWidth;
            
            // Calculate shortcut key or arrow width
            var rightContentWidth = 0;
            if (item.hasSubMenu) {
                rightContentWidth = 16; // 箭头图标宽度
            } else if (item.shortcutText) {
                tempDiv.textContent = item.shortcutText;
                rightContentWidth = tempDiv.offsetWidth;
            }
            
            // Total width = icon width + text width + right content width + padding + 5rem (80px)
            var totalWidth = iconWidth + textWidth + rightContentWidth + 32 + 80; // 32px是左右内边距
            
            if (totalWidth > maxWidth) {
                maxWidth = totalWidth;
            }
        }
        
        document.body.removeChild(tempDiv);
        
        // Make sure to stay within the minimum and maximum width
        return Math.max(200, Math.min(400, maxWidth));
    }
    
    // Public API
    return {
        getWindowSize: getWindowSize,
        getElementBounds: getElementBounds,
        calculateMenuWidth: calculateMenuWidth
    };
})();