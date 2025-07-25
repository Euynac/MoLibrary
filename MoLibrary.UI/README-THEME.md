# MoLibrary UI 主题系统使用指南

## 概述

MoLibrary UI模块提供了一个基于CSS变量的统一样式系统，可以轻松定制MudBlazor组件的外观，并支持日夜间模式切换。

## 快速开始

### 1. 在应用中引入主题

在你的 `App.razor` 或主布局文件中：

```razor
@using MoLibrary.UI.Components.Layout
@using MoLibrary.UI.Services

<MoThemeProvider>
    <!-- 你的应用内容 -->
    <Router AppAssembly="@typeof(App).Assembly">
        <!-- ... -->
    </Router>
</MoThemeProvider>
```

### 2. 在 `Program.cs` 中注册服务

```csharp
// 注册主题服务
builder.Services.AddSingleton<MoThemeService>();
```

### 3. 在 HTML 中引入CSS

在 `index.html` (Blazor WebAssembly) 或 `_Host.cshtml` (Blazor Server) 中：

```html
<!-- MudBlazor CSS -->
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

<!-- MoLibrary 主题 CSS -->
<link href="_content/MoLibrary.UI/css/mo-theme-main.css" rel="stylesheet" />
```

## 主题切换

### 添加主题切换按钮

```razor
@inject MoThemeService ThemeService

<MudIconButton Icon="@Icons.Material.Filled.Brightness4" 
               Color="Color.Inherit" 
               OnClick="@(() => ThemeService.ToggleTheme())" />
```

### 程序化切换主题

```csharp
@code {
    [Inject] private MoThemeService ThemeService { get; set; }
    
    private void SetDarkMode(bool isDark)
    {
        ThemeService.IsDarkMode = isDark;
    }
}
```

## 自定义样式

### 方法1：修改CSS变量

创建一个新的CSS文件来覆盖默认变量：

```css
/* custom-theme.css */
:root {
    /* 自定义主色调 */
    --mo-primary-main: #e91e63;
    --mo-primary-light: #f06292;
    --mo-primary-dark: #c2185b;
    
    /* 自定义组件样式 */
    --mo-button-border-radius: 20px;
    --mo-card-border-radius: 16px;
    --mo-card-padding: 32px;
}
```

### 方法2：创建新主题

1. 创建新的主题CSS文件：

```css
/* mo-theme-brand.css */
:root[data-theme="brand"] {
    /* 品牌色 */
    --mo-primary-main: #ff6b6b;
    --mo-secondary-main: #4ecdc4;
    
    /* 自定义背景 */
    --mo-background-default: #f7f7f7;
    --mo-background-paper: #ffffff;
}
```

2. 在 `mo-theme-main.css` 中引入：

```css
@import url('./themes/mo-theme-brand.css');
```

### 方法3：扩展 MoThemeService

```csharp
public class CustomThemeService : MoThemeService
{
    protected override MudTheme CreateMoTheme()
    {
        var theme = base.CreateMoTheme();
        
        // 自定义调整
        theme.PaletteLight.Primary = "#ff6b6b";
        theme.LayoutProperties.DefaultBorderRadius = "20px";
        
        return theme;
    }
}
```

## CSS变量参考

### 颜色变量
- `--mo-primary-main/light/dark` - 主色调
- `--mo-secondary-main/light/dark` - 次要色调
- `--mo-success/error/warning/info-main/light/dark` - 状态颜色
- `--mo-background-default/paper` - 背景色
- `--mo-text-primary/secondary/disabled` - 文本颜色

### 组件样式变量
- `--mo-button-border-radius` - 按钮圆角
- `--mo-button-padding-x/y` - 按钮内边距
- `--mo-card-border-radius` - 卡片圆角
- `--mo-card-padding` - 卡片内边距
- `--mo-input-border-radius` - 输入框圆角
- `--mo-dialog-border-radius` - 对话框圆角

### 间距变量
- `--mo-spacing-xs` (4px)
- `--mo-spacing-sm` (8px)
- `--mo-spacing-md` (16px)
- `--mo-spacing-lg` (24px)
- `--mo-spacing-xl` (32px)

### 动画变量
- `--mo-transition-fast` (150ms)
- `--mo-transition-normal` (300ms)
- `--mo-transition-slow` (450ms)

## 最佳实践

1. **使用CSS变量而非硬编码值**
   ```css
   /* 好 */
   .my-custom-card {
       border-radius: var(--mo-card-border-radius);
       padding: var(--mo-spacing-lg);
   }
   
   /* 避免 */
   .my-custom-card {
       border-radius: 12px;
       padding: 24px;
   }
   ```

2. **遵循命名约定**
   - 使用 `--mo-` 前缀定义自定义变量
   - 使用 `--mud-` 前缀覆盖MudBlazor变量

3. **主题切换性能优化**
   - 在 `mo-theme-main.css` 中已经排除了表格、数据网格等组件的过渡动画
   - 对于自定义组件，使用 `.no-transition` 类禁用过渡效果

4. **响应式设计**
   ```css
   @media (max-width: 600px) {
       :root {
           --mo-card-padding: 16px;
           --mo-spacing-lg: 16px;
       }
   }
   ```

## 示例：创建完整的品牌主题

```css
/* mo-theme-corporate.css */
:root {
    /* 企业品牌色 */
    --mo-corporate-blue: #003366;
    --mo-corporate-gold: #ffcc00;
    --mo-corporate-gray: #666666;
    
    /* 应用到主题 */
    --mo-primary-main: var(--mo-corporate-blue);
    --mo-secondary-main: var(--mo-corporate-gold);
    
    /* 专业的圆角设计 */
    --mo-button-border-radius: 4px;
    --mo-card-border-radius: 8px;
    
    /* 紧凑的间距 */
    --mo-button-padding-x: 12px;
    --mo-button-padding-y: 6px;
    
    /* 专业的阴影 */
    --mo-button-box-shadow: none;
    --mo-card-box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

/* 企业风格的按钮 */
.corporate-theme .mud-button-filled-primary {
    background: linear-gradient(135deg, var(--mo-corporate-blue), #004080);
    border: 1px solid var(--mo-corporate-blue);
}

.corporate-theme .mud-button-filled-primary:hover {
    background: var(--mo-corporate-blue);
    transform: none;
    box-shadow: 0 2px 8px rgba(0,0,0,0.2);
}
```

## 故障排除

1. **样式没有生效**
   - 确保CSS文件正确引入
   - 检查CSS变量名称是否正确
   - 使用浏览器开发工具检查CSS变量值

2. **主题切换不工作**
   - 确保 `MoThemeService` 已注册
   - 检查 `MoThemeProvider` 是否正确包装应用

3. **与其他CSS框架冲突**
   - 使用更具体的选择器
   - 调整CSS加载顺序
   - 使用 `!important`（谨慎使用）