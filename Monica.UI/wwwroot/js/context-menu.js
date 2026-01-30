window.MoContextMenu = (function() {
    'use strict';
    
    // 获取窗口大小
    function getWindowSize() {
        return {
            width: window.innerWidth,
            height: window.innerHeight
        };
    }
    
    // 获取元素边界
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
    
    // 计算菜单宽度
    function calculateMenuWidth(menuItems) {
        // 创建一个临时的隐藏元素来测量文字宽度
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
            
            // 计算图标宽度 (18px + 16px margin)
            var iconWidth = 34;
            
            // 测量文字宽度
            tempDiv.textContent = item.text || '';
            var textWidth = tempDiv.offsetWidth;
            
            // 计算快捷键或箭头宽度
            var rightContentWidth = 0;
            if (item.hasSubMenu) {
                rightContentWidth = 16; // 箭头图标宽度
            } else if (item.shortcutText) {
                tempDiv.textContent = item.shortcutText;
                rightContentWidth = tempDiv.offsetWidth;
            }
            
            // 总宽度 = 图标宽度 + 文字宽度 + 右侧内容宽度 + 内边距 + 5rem(80px)
            var totalWidth = iconWidth + textWidth + rightContentWidth + 32 + 80; // 32px是左右内边距
            
            if (totalWidth > maxWidth) {
                maxWidth = totalWidth;
            }
        }
        
        document.body.removeChild(tempDiv);
        
        // 确保在最小和最大宽度范围内
        return Math.max(200, Math.min(400, maxWidth));
    }
    
    // 公开的API
    return {
        getWindowSize: getWindowSize,
        getElementBounds: getElementBounds,
        calculateMenuWidth: calculateMenuWidth
    };
})();