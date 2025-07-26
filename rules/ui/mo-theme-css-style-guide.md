# MoLibrary 主题 CSS 样式统一规范

## 概述

MoLibrary UI 模块采用分离式主题架构，将颜色管理与样式管理完全分离，确保主题的可扩展性和一致性。

## 架构原则

### 1. 双层分离架构

**C# 主题层 (ThemeProvider)**
- 负责颜色定义和 MudBlazor 主题配置
- 实现 `IThemeProvider` 接口
- 管理 `PaletteLight` 和 `PaletteDark` 颜色方案
- 设置字体、间距等布局属性

**CSS 样式层 (Theme CSS)**
- 负责组件样式、动画、圆角、阴影等视觉效果
- 使用 MudBlazor CSS 变量引用颜色
- 定义组件的交互效果和布局

### 2. 颜色管理规范

#### C# 层颜色定义
```csharp
public class ThemeExample : IThemeProvider
{
    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#667eea",
                Secondary = "#f093fb", 
                // ...其他颜色定义
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",
                Secondary = "#ff006e",
                // ...其他颜色定义
            }
        };
    }
}
```

#### CSS 层颜色引用
```css
.mud-button {
    background-color: var(--mud-palette-primary);
    color: var(--mud-palette-primary-text);
    border-color: var(--mud-palette-lines-inputs);
}
```

**禁止在 CSS 中硬编码颜色值，必须使用 MudBlazor 变量：**

详见 
[官方变量介绍]: ./mudblazor-css-variables.md



## 文件结构规范

```
MoLibrary.UI/
├── Themes/                          # C# 主题定义
│   ├── IThemeProvider.cs            # 主题提供者接口
│   ├── ThemeMudBlazorDefault.cs     # MudBlazor 原始主题
│   ├── ThemeMoLibraryDefault.cs     # MoLibrary 默认主题
│   ├── ThemeGlassmorphic.cs         # 毛玻璃主题
│   └── ThemeRegistry.cs             # 主题注册管理
└── wwwroot/css/
    ├── mo-theme-main.css            # 主入口文件
    ├── themes/                      # 主题样式文件
    │   ├── mo-theme-default.css     # 默认主题样式
    │   ├── mo-theme-glassmorphic.css # 毛玻璃主题样式
    │   └── mo-theme-mudblazor.css   # MudBlazor 原始样式 (空文件)
    ├── components/                  # 组件专用样式 (未来扩展)
    └── tokens/                      # 设计令牌 (未来扩展)
```

## CSS 变量命名规范

### 1. 组件样式变量
```css
:root {
    /* 按钮样式 */
    --mo-button-border-radius: 12px;
    --mo-button-padding-x: 16px;
    --mo-button-padding-y: 8px;
    --mo-button-font-weight: 500;
    --mo-button-box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    --mo-button-transition: all 0.3s ease;
    
    /* 卡片样式 */
    --mo-card-border-radius: 16px;
    --mo-card-padding: 24px;
    --mo-card-box-shadow: 0 4px 6px rgba(0,0,0,0.1);
    
    /* 输入框样式 */
    --mo-input-border-radius: 8px;
    --mo-input-padding: 12px 16px;
    --mo-input-border-width: 2px;
}
```

### 2. 动画和过渡变量
```css
:root {
    --mo-transition-fast: 150ms;
    --mo-transition-normal: 300ms;
    --mo-transition-slow: 450ms;
    --mo-animation-smooth: 400ms;
    --mo-animation-bounce: 600ms;
}
```

### 3. 间距变量
```css
:root {
    --mo-spacing-xs: 4px;
    --mo-spacing-sm: 8px;
    --mo-spacing-md: 16px;
    --mo-spacing-lg: 24px;
    --mo-spacing-xl: 32px;
}
```

## 主题样式实现规范

### 1. 主题类选择器结构
```css
/* 主题特定变量 */
:root[data-theme="theme-name-light"],
.mo-theme-name-light {
    --mo-custom-variable: value;
}

:root[data-theme="theme-name-dark"],
.mo-theme-name-dark {
    --mo-custom-variable: value;
}

/* 通用样式（适用于明暗两个模式） */
.mo-theme-name-light,
.mo-theme-name-dark {
    .mud-component {
        /* 样式定义 */
    }
}

/* 明亮模式专用样式 */
.mo-theme-name-light {
    .mud-component {
        /* 明亮模式特定样式 */
    }
}

/* 暗色模式专用样式 */
.mo-theme-name-dark {
    .mud-component {
        /* 暗色模式特定样式 */
    }
}
```

### 2. 组件样式覆盖规范

**按钮组件**
```css
.mud-button {
    border-radius: var(--mo-button-border-radius) !important;
    padding: var(--mo-button-padding-y) var(--mo-button-padding-x) !important;
    font-weight: var(--mo-button-font-weight) !important;
    transition: var(--mo-button-transition) !important;
    background-color: var(--mud-palette-surface);
    color: var(--mud-palette-text-primary);
}

.mud-button:hover {
    background-color: var(--mud-palette-action-default-hover);
    transform: translateY(-1px);
}
```

**卡片组件**
```css
.mud-card {
    border-radius: var(--mo-card-border-radius) !important;
    box-shadow: var(--mo-card-box-shadow) !important;
    background-color: var(--mud-palette-surface);
    border: 1px solid var(--mud-palette-lines-default);
}
```

**输入框组件**
```css
.mud-input-outlined .mud-input-outlined-border {
    border-color: var(--mud-palette-lines-inputs);
    border-radius: var(--mo-input-border-radius) !important;
}

.mud-input-outlined:hover .mud-input-outlined-border {
    border-color: var(--mud-palette-text-primary);
}

.mud-input-outlined.mud-input-focused .mud-input-outlined-border {
    border-color: var(--mud-palette-primary);
}
```

## 特殊效果实现规范

### 1. 毛玻璃效果 (Glassmorphic Theme)
```css
:root {
    --mo-glass-blur: 12px;
    --mo-glass-blur-heavy: 20px;
    --mo-glass-opacity: 0.85;
    --mo-glass-border-width: 1px;
}

.mo-glass-card {
    background: var(--mo-background-glass) !important;
    backdrop-filter: blur(var(--mo-glass-blur));
    -webkit-backdrop-filter: blur(var(--mo-glass-blur));
    border: var(--mo-glass-border-width) solid var(--mo-glass-border) !important;
    box-shadow: var(--mo-glass-shadow) !important;
}
```

### 2. 渐变背景实现
```css
.mo-theme-glassmorphic-light::before {
    content: '';
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    z-index: -10;
}
```

### 3. 霓虹发光效果 (Dark Mode)
```css
.mo-theme-dark {
    --mo-neon-text-shadow: 0 0 10px currentColor, 0 0 20px currentColor;
    --mo-neon-box-shadow: 0 0 20px var(--mud-palette-primary);
    
    .mud-button-filled-primary:hover {
        animation: mo-neon-pulse 2s ease-in-out infinite;
    }
}

@keyframes mo-neon-pulse {
    0%, 100% { 
        box-shadow: 0 0 20px var(--mud-palette-primary);
    }
    50% { 
        box-shadow: 0 0 40px var(--mud-palette-primary);
    }
}
```

## 响应式设计规范

### 1. 移动端适配
```css
@media (max-width: 960px) {
    .mo-theme-glassmorphic-light,
    .mo-theme-glassmorphic-dark {
        --mo-glass-blur: 8px;
        --mo-glass-blur-heavy: 15px;
        --mo-card-padding: 16px;
        --mo-dialog-padding: 24px;
    }
}
```

### 2. 无障碍支持
```css
@media (prefers-reduced-motion: reduce) {
    .mo-theme-* * {
        animation: none !important;
        transition: none !important;
    }
}
```

## 性能优化规范

### 1. 过渡动画优化
```css
/* 禁用在性能敏感组件上的过渡 */
.mud-table *,
.mud-data-grid *,
.mud-treeview *,
.no-transition,
.no-transition * {
    transition: none !important;
}
```

### 2. 硬件加速
```css
.mud-card:hover {
    transform: translateY(-1px) translateZ(0); /* 强制硬件加速 */
    will-change: transform; /* 提示浏览器优化 */
}
```

## 实用工具类规范

### 1. 毛玻璃工具类
```css
.mo-glass-heavy {
    backdrop-filter: blur(var(--mo-glass-blur-heavy)) !important;
    -webkit-backdrop-filter: blur(var(--mo-glass-blur-heavy)) !important;
}

.mo-glass-light {
    backdrop-filter: blur(5px) !important;
    -webkit-backdrop-filter: blur(5px) !important;
}
```

### 2. 渐变工具类
```css
.mo-gradient-bg {
    background: var(--mo-button-gradient) !important;
}

.mo-glow {
    box-shadow: var(--mo-hover-glow) !important;
}
```

## 新主题创建流程

### 1. C# 主题类
```csharp
public class ThemeCustom : IThemeProvider
{
    public string Name => "custom";
    public string DisplayName => "自定义主题";
    public string Description => "主题描述";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight() { /* 颜色定义 */ },
            PaletteDark = new PaletteDark() { /* 颜色定义 */ },
            LayoutProperties = new LayoutProperties() { /* 布局属性 */ }
        };
    }
}
```

### 2. CSS 样式文件
```css
/* mo-theme-custom.css */
:root[data-theme="custom-light"],
.mo-theme-custom-light {
    /* 明亮模式变量 */
}

:root[data-theme="custom-dark"], 
.mo-theme-custom-dark {
    /* 暗色模式变量 */
}

.mo-theme-custom-light,
.mo-theme-custom-dark {
    /* 通用组件样式 */
}
```

### 3. 主题注册
```csharp
// 在 ThemeRegistry.cs 中注册
private static void RegisterDefaultThemes()
{
    RegisterTheme(new ThemeCustom());
}
```

### 4. CSS 导入
```css
/* 在 mo-theme-main.css 中导入 */
@import url('./themes/mo-theme-custom.css');
```

## 最佳实践

### 1. 颜色一致性
- ✅ 使用 `var(--mud-palette-primary)` 
- ❌ 使用 `#667eea`

### 2. 样式覆盖
- ✅ 使用 `!important` 确保样式优先级
- ✅ 使用 CSS 变量提高可维护性
- ❌ 使用内联样式

### 3. 性能考虑
- ✅ 合理使用过渡动画
- ✅ 避免在大量元素上使用复杂动画
- ✅ 使用硬件加速

### 4. 兼容性
- ✅ 提供 `-webkit-` 前缀支持
- ✅ 支持 `prefers-reduced-motion`
- ✅ 移动端适配

## 注意事项

1. **!important 使用**：仅在覆盖 MudBlazor 默认样式时使用
2. **CSS 隔离**：优先使用 CSS isolation 而非 `<style>` 标签
3. **主题切换**：确保所有样式都能正确响应主题切换
4. **浏览器兼容**：特殊效果（如毛玻璃）需要添加浏览器前缀
5. **深色模式**：每个主题必须同时支持 Light 和 Dark 模式

## 调试和测试

### 1. 主题切换测试
确保所有组件在主题切换时正确应用样式。

### 2. 响应式测试
验证在不同屏幕尺寸下的表现。

### 3. 无障碍测试
确保动画和颜色对比度符合无障碍标准。

### 4. 性能测试
监控 CSS 动画对页面性能的影响。

---

*此规范确保 MoLibrary 主题系统的一致性、可维护性和可扩展性。所有新增主题都应遵循此规范进行开发。*