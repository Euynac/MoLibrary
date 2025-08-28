# DiffHighlight UI模块

文本差异对比高亮UI模块，提供可视化的文本差异对比功能。

## 功能特性

- 📊 实时文本差异对比
- 🎨 可视化差异高亮显示
- ⚙️ 多种对比模式（行级、字符级）
- 🔧 丰富的配置选项
- 📱 响应式设计
- 🚀 高性能渲染

## 使用方式

### 1. 注册UI模块

在 `Program.cs` 或应用配置中添加：

```csharp
builder.ConfigModuleDiffHighlightUI();
```

### 2. 访问页面

启动应用后访问：`/diff-highlight-debug`

### 3. 使用DiffViewer组件

```razor
@using MoLibrary.FrameworkUI.UIDiffHighlight.Components

<DiffViewer OriginText="@originText" 
           NewText="@newText" 
           AutoRefresh="true" />
```

## 组件说明

### DiffViewer 组件

可复用的差异对比查看器组件，支持：

- **参数配置**：
  - `OriginText`: 原始文本
  - `NewText`: 新文本  
  - `Options`: 对比选项
  - `AutoRefresh`: 是否自动刷新

- **方法**：
  - `RefreshDiffAsync()`: 手动刷新差异对比

### UIDiffHighlightPage 页面

完整的差异对比调试页面，包含：

- 配置面板（对比模式、输出格式等）
- 文本输入区域
- 实时差异对比结果显示
- 示例数据加载

## 配置选项

支持的对比配置：

- **对比模式**: 行级、字符级
- **输出格式**: HTML、Markdown、纯文本
- **上下文行数**: 显示变更前后的上下文
- **忽略选项**: 忽略空白字符、忽略大小写
- **性能限制**: 最大字符差异长度

## 样式定制

组件使用CSS隔离，可通过以下类进行样式定制：

- `.diff-viewer`: 主容器
- `.diff-line--added`: 新增行样式
- `.diff-line--deleted`: 删除行样式
- `.diff-line--modified`: 修改行样式

## 技术特点

- 基于Myers差异算法
- 支持离线运行（无CDN依赖）
- 使用MudBlazor UI组件库
- 完整的错误处理和用户反馈
- 响应式设计，支持移动端