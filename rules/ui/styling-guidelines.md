# 样式系统规范

## 1. 样式架构概述

### 1.1 样式文件组织
```
MoLibrary.UI/
├── Styles/
│   ├── _variables.css          # CSS变量定义
│   ├── _mixins.scss           # SCSS混合（可选）
│   ├── _base.css              # 基础样式重置
│   ├── _components.css        # 组件通用样式
│   ├── _utilities.css         # 工具类
│   └── mo-theme.css          # 主题样式汇总
├── Components/
│   └── ComponentName/
│       └── ComponentName.razor.css  # 组件隔离样式
```

### 1.2 样式优先级
1. MudBlazor 默认样式（最低优先级）
2. 全局主题样式
3. 组件通用样式
4. 组件隔离样式
5. 内联样式（最高优先级，应避免使用）

## 2. CSS变量系统

### 2.1 颜色变量
```css
:root {
    /* 主题色 */
    --mo-color-primary: #7e6fff;
    --mo-color-primary-light: #a394ff;
    --mo-color-primary-dark: #5a3fcf;
    --mo-color-primary-contrast: #ffffff;
    
    /* 语义化颜色 */
    --mo-color-success: #3dcb6c;
    --mo-color-warning: #ffb545;
    --mo-color-error: #ff3f5f;
    --mo-color-info: #4a86ff;
    
    /* 中性色 */
    --mo-color-background: #1a1a27;
    --mo-color-surface: #1e1e2d;
    --mo-color-text-primary: #b2b0bf;
    --mo-color-text-secondary: #92929f;
    --mo-color-border: #33323e;
}

/* 亮色主题 */
[data-theme="light"] {
    --mo-color-background: #ffffff;
    --mo-color-surface: #f5f5f5;
    --mo-color-text-primary: #212121;
    --mo-color-text-secondary: #666666;
    --mo-color-border: #e0e0e0;
}
```

### 2.2 间距系统
```css
:root {
    /* 基础间距单位 */
    --mo-spacing-unit: 8px;
    
    /* 间距尺寸 */
    --mo-spacing-xs: calc(var(--mo-spacing-unit) * 0.5);   /* 4px */
    --mo-spacing-sm: var(--mo-spacing-unit);               /* 8px */
    --mo-spacing-md: calc(var(--mo-spacing-unit) * 2);     /* 16px */
    --mo-spacing-lg: calc(var(--mo-spacing-unit) * 3);     /* 24px */
    --mo-spacing-xl: calc(var(--mo-spacing-unit) * 4);     /* 32px */
    --mo-spacing-xxl: calc(var(--mo-spacing-unit) * 6);    /* 48px */
}
```

### 2.3 排版系统
```css
:root {
    /* 字体家族 */
    --mo-font-family-primary: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
    --mo-font-family-mono: "SF Mono", Monaco, "Cascadia Code", "Roboto Mono", Consolas, "Courier New", monospace;
    
    /* 字体大小 */
    --mo-font-size-xs: 0.75rem;    /* 12px */
    --mo-font-size-sm: 0.875rem;   /* 14px */
    --mo-font-size-base: 1rem;     /* 16px */
    --mo-font-size-lg: 1.125rem;   /* 18px */
    --mo-font-size-xl: 1.25rem;    /* 20px */
    --mo-font-size-2xl: 1.5rem;    /* 24px */
    --mo-font-size-3xl: 2rem;      /* 32px */
    
    /* 行高 */
    --mo-line-height-tight: 1.25;
    --mo-line-height-normal: 1.5;
    --mo-line-height-relaxed: 1.75;
    
    /* 字重 */
    --mo-font-weight-light: 300;
    --mo-font-weight-normal: 400;
    --mo-font-weight-medium: 500;
    --mo-font-weight-semibold: 600;
    --mo-font-weight-bold: 700;
}
```

### 2.4 边框和圆角
```css
:root {
    /* 边框宽度 */
    --mo-border-width-thin: 1px;
    --mo-border-width-medium: 2px;
    --mo-border-width-thick: 4px;
    
    /* 圆角半径 */
    --mo-radius-none: 0;
    --mo-radius-sm: 4px;
    --mo-radius-md: 8px;
    --mo-radius-lg: 12px;
    --mo-radius-xl: 16px;
    --mo-radius-full: 9999px;
}
```

### 2.5 阴影系统
```css
:root {
    /* 阴影 */
    --mo-shadow-xs: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
    --mo-shadow-sm: 0 2px 4px -1px rgba(0, 0, 0, 0.06);
    --mo-shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
    --mo-shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.1);
    --mo-shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
    --mo-shadow-inner: inset 0 2px 4px 0 rgba(0, 0, 0, 0.06);
}
```

### 2.6 动画和过渡
```css
:root {
    /* 过渡时长 */
    --mo-duration-fast: 150ms;
    --mo-duration-normal: 300ms;
    --mo-duration-slow: 500ms;
    
    /* 缓动函数 */
    --mo-ease-in: cubic-bezier(0.4, 0, 1, 1);
    --mo-ease-out: cubic-bezier(0, 0, 0.2, 1);
    --mo-ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
    
    /* 默认过渡 */
    --mo-transition-default: all var(--mo-duration-normal) var(--mo-ease-in-out);
}
```

## 3. 组件样式规范

### 3.1 基础组件样式
```css
/* 卡片组件 */
.mo-card {
    background-color: var(--mo-color-surface);
    border-radius: var(--mo-radius-lg);
    padding: var(--mo-spacing-lg);
    border: 1px solid var(--mo-color-border);
    transition: var(--mo-transition-default);
}

.mo-card:hover {
    transform: translateY(-2px);
    box-shadow: var(--mo-shadow-lg);
}

/* 按钮组件 */
.mo-button {
    padding: var(--mo-spacing-sm) var(--mo-spacing-md);
    border-radius: var(--mo-radius-md);
    font-weight: var(--mo-font-weight-medium);
    transition: var(--mo-transition-default);
    cursor: pointer;
    border: none;
    outline: none;
}

.mo-button--primary {
    background-color: var(--mo-color-primary);
    color: var(--mo-color-primary-contrast);
}

.mo-button--primary:hover {
    background-color: var(--mo-color-primary-dark);
}
```

### 3.2 组件状态样式
```css
/* 加载状态 */
.mo-loading {
    position: relative;
    overflow: hidden;
}

.mo-loading::after {
    content: "";
    position: absolute;
    top: 0;
    left: -100%;
    width: 100%;
    height: 100%;
    background: linear-gradient(
        90deg,
        transparent,
        rgba(255, 255, 255, 0.2),
        transparent
    );
    animation: mo-shimmer 1.5s infinite;
}

@keyframes mo-shimmer {
    0% { left: -100%; }
    100% { left: 100%; }
}

/* 禁用状态 */
.mo-disabled {
    opacity: 0.5;
    cursor: not-allowed;
    pointer-events: none;
}

/* 错误状态 */
.mo-error {
    border-color: var(--mo-color-error);
    color: var(--mo-color-error);
}
```

## 4. 响应式设计

### 4.1 断点定义
```css
:root {
    /* 断点 */
    --mo-breakpoint-xs: 0;
    --mo-breakpoint-sm: 600px;
    --mo-breakpoint-md: 960px;
    --mo-breakpoint-lg: 1280px;
    --mo-breakpoint-xl: 1920px;
}
```

### 4.2 响应式工具类
```css
/* 隐藏/显示 */
@media (max-width: 599px) {
    .mo-hide-xs { display: none !important; }
}

@media (min-width: 600px) and (max-width: 959px) {
    .mo-hide-sm { display: none !important; }
}

@media (min-width: 960px) and (max-width: 1279px) {
    .mo-hide-md { display: none !important; }
}

@media (min-width: 1280px) and (max-width: 1919px) {
    .mo-hide-lg { display: none !important; }
}

@media (min-width: 1920px) {
    .mo-hide-xl { display: none !important; }
}
```

### 4.3 响应式网格
```css
.mo-grid {
    display: grid;
    gap: var(--mo-spacing-md);
}

/* 默认：移动优先 */
.mo-grid {
    grid-template-columns: 1fr;
}

/* 平板 */
@media (min-width: 600px) {
    .mo-grid--2-cols { grid-template-columns: repeat(2, 1fr); }
    .mo-grid--3-cols { grid-template-columns: repeat(3, 1fr); }
}

/* 桌面 */
@media (min-width: 960px) {
    .mo-grid--4-cols { grid-template-columns: repeat(4, 1fr); }
    .mo-grid--6-cols { grid-template-columns: repeat(6, 1fr); }
}

/* 自适应网格 */
.mo-grid--auto {
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
}
```

## 5. 工具类

### 5.1 间距工具类
```css
/* 内边距 */
.mo-p-0 { padding: 0; }
.mo-p-xs { padding: var(--mo-spacing-xs); }
.mo-p-sm { padding: var(--mo-spacing-sm); }
.mo-p-md { padding: var(--mo-spacing-md); }
.mo-p-lg { padding: var(--mo-spacing-lg); }
.mo-p-xl { padding: var(--mo-spacing-xl); }

/* 外边距 */
.mo-m-0 { margin: 0; }
.mo-m-xs { margin: var(--mo-spacing-xs); }
.mo-m-sm { margin: var(--mo-spacing-sm); }
.mo-m-md { margin: var(--mo-spacing-md); }
.mo-m-lg { margin: var(--mo-spacing-lg); }
.mo-m-xl { margin: var(--mo-spacing-xl); }

/* 方向性间距 */
.mo-mt-md { margin-top: var(--mo-spacing-md); }
.mo-mr-md { margin-right: var(--mo-spacing-md); }
.mo-mb-md { margin-bottom: var(--mo-spacing-md); }
.mo-ml-md { margin-left: var(--mo-spacing-md); }
```

### 5.2 排版工具类
```css
/* 文本对齐 */
.mo-text-left { text-align: left; }
.mo-text-center { text-align: center; }
.mo-text-right { text-align: right; }
.mo-text-justify { text-align: justify; }

/* 字体大小 */
.mo-text-xs { font-size: var(--mo-font-size-xs); }
.mo-text-sm { font-size: var(--mo-font-size-sm); }
.mo-text-base { font-size: var(--mo-font-size-base); }
.mo-text-lg { font-size: var(--mo-font-size-lg); }
.mo-text-xl { font-size: var(--mo-font-size-xl); }

/* 字重 */
.mo-font-light { font-weight: var(--mo-font-weight-light); }
.mo-font-normal { font-weight: var(--mo-font-weight-normal); }
.mo-font-medium { font-weight: var(--mo-font-weight-medium); }
.mo-font-bold { font-weight: var(--mo-font-weight-bold); }

/* 文本装饰 */
.mo-truncate {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
```

### 5.3 布局工具类
```css
/* Flexbox */
.mo-flex { display: flex; }
.mo-flex-col { flex-direction: column; }
.mo-flex-wrap { flex-wrap: wrap; }

/* 对齐 */
.mo-items-start { align-items: flex-start; }
.mo-items-center { align-items: center; }
.mo-items-end { align-items: flex-end; }

.mo-justify-start { justify-content: flex-start; }
.mo-justify-center { justify-content: center; }
.mo-justify-end { justify-content: flex-end; }
.mo-justify-between { justify-content: space-between; }

/* Grid */
.mo-grid { display: grid; }
.mo-gap-sm { gap: var(--mo-spacing-sm); }
.mo-gap-md { gap: var(--mo-spacing-md); }
.mo-gap-lg { gap: var(--mo-spacing-lg); }
```

## 6. 最佳实践

### 6.1 命名规范
- 使用 BEM (Block Element Modifier) 命名法
- 所有自定义类名以 `mo-` 前缀开始
- 示例：`mo-card`, `mo-card__header`, `mo-card--large`

### 6.2 样式隔离
```css
/* 组件样式应该使用 .razor.css 文件 */
/* MyComponent.razor.css */
.component-wrapper {
    /* 组件特定样式 */
}

/* 避免使用全局选择器 */
/* ❌ 错误 */
div { margin: 0; }

/* ✅ 正确 */
.mo-component div { margin: 0; }
```

### 6.3 性能优化
```css
/* 使用 will-change 优化动画性能 */
.mo-animated {
    will-change: transform;
}

/* 动画完成后移除 */
.mo-animated:not(:hover) {
    will-change: auto;
}

/* 使用 GPU 加速 */
.mo-smooth {
    transform: translateZ(0);
    backface-visibility: hidden;
}
```

### 6.4 可访问性
```css
/* 确保足够的颜色对比度 */
.mo-text {
    color: var(--mo-color-text-primary);
    /* WCAG AA 标准: 4.5:1 对比度 */
}

/* 焦点样式 */
.mo-focusable:focus {
    outline: 2px solid var(--mo-color-primary);
    outline-offset: 2px;
}

/* 屏幕阅读器专用 */
.mo-sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
}
```

## 7. 与MudBlazor集成

### 7.1 覆盖MudBlazor样式
```css
/* 使用更具体的选择器覆盖 */
.mo-custom-button.mud-button {
    /* 自定义样式 */
}

/* 或使用CSS变量 */
.mo-theme {
    --mud-palette-primary: var(--mo-color-primary);
    --mud-palette-secondary: var(--mo-color-secondary);
}
```

### 7.2 扩展MudBlazor组件
```razor
<!-- 包装MudBlazor组件 -->
<div class="mo-enhanced-card">
    <MudCard>
        <!-- 内容 -->
    </MudCard>
</div>

<style>
.mo-enhanced-card .mud-card {
    border: 1px solid var(--mo-color-border);
    transition: var(--mo-transition-default);
}

.mo-enhanced-card .mud-card:hover {
    transform: translateY(-2px);
    box-shadow: var(--mo-shadow-lg);
}
</style>
```

这个样式系统规范提供了一个全面的CSS架构，确保样式的一致性、可维护性和可扩展性。通过使用CSS变量和工具类，可以快速构建美观且响应式的用户界面。