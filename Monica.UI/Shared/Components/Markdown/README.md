# MoMarkdown Markdown 渲染组件

## 概述

`MoMarkdown` 是 Monica.UI 中的统一 Markdown 渲染组件，基于 `MudBlazor.Markdown` 扩展，额外提供了以下能力：

- Mermaid fenced code block 渲染
- 本地图片与站内资源 URL 重写能力
- 跟随 Monica 主题切换的代码高亮主题
- 可扩展的 Markdown 样式包（`StyleKey`）

如果开发者要“美化 Markdown 显示”，通常不需要改渲染逻辑，重点是理解当前样式分层，然后在正确的位置改 CSS。

## 组件结构

```text
Markdown/
├── IMoMarkdownAssetResolver.cs
├── PassThroughMarkdownAssetResolver.cs
├── MoMarkdown.razor
├── MoMarkdown.razor.css
├── MoMudMarkdown.cs
├── MoMarkdownMermaidBlock.razor
├── MoMarkdownMermaidBlock.razor.css
└── README.md
```

相关静态资源：

```text
Monica.UI/wwwroot/css/
├── mo-theme-main.css
└── markdown/
    ├── mo-markdown-default.css
    ├── mo-markdown-deep-ocean.css
    └── mo-markdown-vintage-press.css

Monica.UI/wwwroot/js/
└── mo-markdown-mermaid.js
```

## 样式分层

### 1. 结构层：`MoMarkdown.razor.css`

这个文件负责“结构性样式”，适合放所有风格都需要的基础规则，例如：

- `.mud-markdown-body` 基础文本颜色
- `img` 的最大宽度、圆角、居中
- `pre` 的基础间距
- `table` 的宽度
- `.mud-markdown-code-highlight` 的圆角和裁剪

这类规则通常不应该写成具体主题风格，而是通用骨架。

### 2. 风格层：`wwwroot/css/markdown/mo-markdown-default.css`

这个文件负责 `StyleKey` 对应的样式包定义，目前默认样式包是：

```razor
<MoMarkdown Value="@content" StyleKey="default" />
```

组件内部会把 `StyleKey` 转成 host class：

```text
StyleKey="default" -> .mo-markdown-style-default
StyleKey="Editorial" -> .mo-markdown-style-editorial
```

当前 `StyleKey` 的标准入口就是：

- `.mo-markdown-style-default`
- `.mo-markdown-style-default .mud-markdown-body ...`
- `.mo-markdown-style-default .mo-markdown-mermaid-surface`

建议把“可复用的视觉 token”放在 CSS 变量里，例如：

```css
.mo-markdown-style-default {
    --mo-markdown-body-font-size: 0.98rem;
    --mo-markdown-body-line-height: 1.75;
    --mo-markdown-panel-background: var(--mud-palette-surface);
    --mo-markdown-panel-border: var(--mud-palette-lines-default);
    --mo-markdown-accent: var(--mud-palette-primary);
}
```

### 3. 主题覆盖层：`wwwroot/css/markdown/mo-markdown-*.css`

这些文件不是新的 `StyleKey`，而是“当前 Monica 主题”下对 Markdown 的附加覆盖。

例如：

- `mo-markdown-deep-ocean.css`
- `mo-markdown-vintage-press.css`

它们依赖 `MoThemeProvider` 输出的主题标记：

- `data-theme="deep-ocean-light"`
- `.mo-theme-deep-ocean-light`

典型写法：

```css
:root[data-theme="deep-ocean-light"] .mo-markdown-style-default,
.mo-theme-deep-ocean-light .mo-markdown-style-default {
    --mo-markdown-panel-background: color-mix(in srgb, var(--mud-palette-surface) 88%, var(--mud-palette-info));
}
```

这里要特别注意：

- `deep-ocean` 是应用主题名，不是 `StyleKey`
- `default` 是 Markdown 样式包名，不是应用主题名
- 一个应用主题可以覆盖多个 Markdown 样式包
- 一个 Markdown 样式包也可以在多个应用主题下表现不同

## 资源加载方式

Markdown 样式最终由 `Monica.UI/wwwroot/css/mo-theme-main.css` 统一引入：

```css
@import url('./markdown/mo-markdown-default.css');
@import url('./markdown/mo-markdown-deep-ocean.css');
@import url('./markdown/mo-markdown-vintage-press.css');
```

`MoApp.razor` 会加载 `mo-theme-main.css`，所以正常情况下不需要额外手工添加 Markdown CSS 引用。

## 快速开始

### 1. 使用默认样式渲染 Markdown

```razor
<MoMarkdown Value="@content" />
```

### 2. 通过 `MudMarkdownStyling` 做轻量外观调整

这类设置适合表格密度、链接下划线、代码复制按钮等“行为级样式”，不要优先用 CSS 硬改。

```razor
<MoMarkdown Value="@content"
            Styling="@_styling" />

@code {
    private readonly MudMarkdownStyling _styling = new()
    {
        Table = { Dense = true, IsStriped = true, CellMinWidth = "120px" },
        Link = { Underline = false },
        CodeBlock = { CopyButton = true, CopyButtonText = "Copy" }
    };
}
```

### 3. 在页面中使用不同样式包

```razor
<MoMarkdown Value="@content" StyleKey="editorial" />
```

前提是你已经定义了 `.mo-markdown-style-editorial` 相关 CSS。

## 如何修改当前默认样式

如果只是希望调整现有 Markdown 外观，优先修改：

- `Monica.UI/wwwroot/css/markdown/mo-markdown-default.css`

这适合处理：

- 字体大小、行高、段落节奏
- 标题字重、字距、分割线
- 行内代码、引用块、链接颜色
- Mermaid 容器背景和边框

示例：

```css
.mo-markdown-style-default {
    --mo-markdown-body-font-size: 1rem;
    --mo-markdown-body-line-height: 1.85;
    --mo-markdown-accent: color-mix(in srgb, var(--mud-palette-primary) 82%, white);
}

.mo-markdown-style-default .mud-markdown-body h2 {
    margin-top: 2.25rem;
    padding-bottom: 0.4rem;
    letter-spacing: -0.03em;
}

.mo-markdown-style-default .mud-markdown-body blockquote {
    padding: 0.9rem 1rem;
}
```

如果调整的是所有主题都应该共享的基础布局，再改：

- `Monica.UI/Components/Markdown/MoMarkdown.razor.css`
- `Monica.UI/Components/Markdown/MoMarkdownMermaidBlock.razor.css`

## 如何新增一个 Markdown 样式包

如果你不想影响默认样式，而是要增加一种新的展示风格，建议新增独立样式包。

### 步骤 1：创建新 CSS 文件

示例：

- `Monica.UI/wwwroot/css/markdown/mo-markdown-editorial.css`

内容示例：

```css
.mo-markdown-style-editorial {
    --mo-markdown-body-max-width: 72ch;
    --mo-markdown-body-font-size: 1rem;
    --mo-markdown-body-line-height: 1.9;
    --mo-markdown-panel-background: var(--mud-palette-surface);
    --mo-markdown-panel-border: color-mix(in srgb, var(--mud-palette-primary) 18%, var(--mud-palette-lines-default));
    --mo-markdown-accent: var(--mud-palette-primary);
    --mo-markdown-muted: var(--mud-palette-text-secondary);
    --mo-markdown-inline-code-background: var(--mud-palette-action-disabled-background);
}

.mo-markdown-style-editorial .mud-markdown-body {
    max-width: var(--mo-markdown-body-max-width);
    margin-inline: auto;
    font-size: var(--mo-markdown-body-font-size);
    line-height: var(--mo-markdown-body-line-height);
}

.mo-markdown-style-editorial .mud-markdown-body h1,
.mo-markdown-style-editorial .mud-markdown-body h2 {
    letter-spacing: -0.03em;
}

.mo-markdown-style-editorial .mud-markdown-body blockquote {
    border-left: 4px solid var(--mo-markdown-accent);
    background: color-mix(in srgb, var(--mo-markdown-panel-background) 94%, transparent);
}

.mo-markdown-style-editorial .mo-markdown-mermaid-surface {
    background: var(--mo-markdown-panel-background);
    border-color: var(--mo-markdown-panel-border);
}
```

### 步骤 2：把新文件加入入口 CSS

修改：

- `Monica.UI/wwwroot/css/mo-theme-main.css`

添加：

```css
@import url('./markdown/mo-markdown-editorial.css');
```

### 步骤 3：在组件中使用新样式包

```razor
<MoMarkdown Value="@content" StyleKey="editorial" />
```

## 如何给特定应用主题增加 Markdown 覆盖

如果你要的不是新样式包，而是“同一套 Markdown 样式在不同 Monica 主题下有不同表现”，应该在主题覆盖文件里处理。

例如要让 `deep-ocean` 主题下的 `editorial` 样式更冷色：

```css
:root[data-theme="deep-ocean-light"] .mo-markdown-style-editorial,
:root[data-theme="deep-ocean-dark"] .mo-markdown-style-editorial,
.mo-theme-deep-ocean-light .mo-markdown-style-editorial,
.mo-theme-deep-ocean-dark .mo-markdown-style-editorial {
    --mo-markdown-panel-background: color-mix(in srgb, var(--mud-palette-surface) 86%, var(--mud-palette-info));
    --mo-markdown-panel-border: color-mix(in srgb, var(--mud-palette-info) 35%, var(--mud-palette-lines-default));
}
```

这种写法建议放到：

- `Monica.UI/wwwroot/css/markdown/mo-markdown-deep-ocean.css`

## 如何在消费页面局部覆盖样式

如果你不想修改 `Monica.UI` 的公共样式，而是只在某个页面里局部调整，可以在消费组件中包一层容器，再用 CSS isolation 的 `::deep` 覆盖。

### Razor

```razor
<div class="article-preview">
    <MoMarkdown Value="@content" StyleKey="default" />
</div>
```

### `.razor.css`

```css
.article-preview ::deep .mo-markdown-style-default .mud-markdown-body {
    max-width: 78ch;
    margin-inline: auto;
}

.article-preview ::deep .mo-markdown-style-default .mud-markdown-body h1 {
    font-size: 2.4rem;
}

.article-preview ::deep .mo-markdown-style-default .mud-markdown-body img {
    box-shadow: 0 16px 40px color-mix(in srgb, var(--mud-palette-text-primary) 12%, transparent);
}
```

注意：

- 不要在 `.razor` 文件里直接写 `<style>`
- 覆盖 `MoMarkdown` 这种子组件输出的 DOM 时，需要 `::deep`
- 如果是公共能力，优先沉淀到 `Monica.UI` 自己的 Markdown CSS 文件中

## Mermaid 与代码块的美化入口

### Mermaid 容器外观

Mermaid 图表外围容器主要由以下文件控制：

- `Monica.UI/Components/Markdown/MoMarkdownMermaidBlock.razor.css`
- `Monica.UI/wwwroot/css/markdown/mo-markdown-default.css`

推荐调整：

- 外边距
- 内边距
- 面板背景
- 边框颜色
- 圆角

### Mermaid 图表配色

如果要改 SVG 图表本身的颜色映射，不应只改 CSS，应该看：

- `Monica.UI/wwwroot/js/mo-markdown-mermaid.js`

这里通过 `themeVariables` 把 Monica 主题色传给 Mermaid。适合修改：

- 节点主色
- 文本色
- 边框色
- 饼图颜色组
- 字体族

### 代码块主题

`MoMarkdownThemeManager` 会根据当前 Monica 主题切换代码高亮主题。如果要改代码块高亮方案，优先检查：

- `Monica.UI/Components/MoMarkdownThemeManager.razor`
- `Monica.UI/Themes/ThemeRegistry.*`

如果只是调整代码块容器的圆角、边框、外边距，改 CSS 即可；如果要改语法高亮配色，应该改主题配置。

## 推荐修改顺序

当你准备“美化 Markdown”时，建议按这个顺序判断：

1. 先确认是改通用骨架，还是改具体风格
2. 通用骨架改 `MoMarkdown.razor.css`
3. 默认风格改 `mo-markdown-default.css`
4. 某个 Monica 主题下的差异改 `mo-markdown-<theme>.css`
5. 某个页面独有的需求用容器 `::deep` 局部覆盖
6. Mermaid 图本身配色改 `mo-markdown-mermaid.js`

## 常用选择器

- `.mo-markdown-style-default`
- `.mud-markdown-body`
- `.mud-markdown-body h1` ~ `.mud-markdown-body h6`
- `.mud-markdown-body blockquote`
- `.mud-markdown-body code:not(.hljs)`
- `.mud-markdown-body img`
- `.mud-markdown-body table`
- `.mud-markdown-code-highlight`
- `.mo-markdown-mermaid-surface`

## 最佳实践

- 尽量先改 CSS 变量，再改具体选择器，便于不同主题复用
- 优先使用 `var(--mud-palette-*)` 与 `var(--mud-default-borderradius)`，保持与 Monica 主题体系一致
- `StyleKey` 建议使用小写 kebab-case，避免产生难以预期的 host class
- 新增 Markdown 样式包后，要记得把 CSS 文件加入 `mo-theme-main.css`
- 如果某个主题没有专门的 Markdown 覆盖文件，组件会自动退回公共样式包，不会影响渲染
