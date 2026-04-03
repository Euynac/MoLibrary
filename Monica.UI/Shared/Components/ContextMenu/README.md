# MoContextMenu 右键菜单组件

## 概述

MoContextMenu 是一个功能强大、易于使用的 Blazor 右键菜单组件，采用现代化设计模式，支持多级子菜单、图标、快捷键提示等丰富功能。

## 核心特性

- 🎨 **现代化UI**: 基于 MudBlazor 设计语言，支持明暗主题
- 🔧 **泛型支持**: 支持传递任何类型的上下文对象
- 🏗️ **构建器模式**: 流畅的API设计，易于创建复杂菜单结构
- 📱 **响应式**: 自动位置调整，防止菜单超出视窗
- 🎯 **多级菜单**: 支持无限层级的子菜单
- ⚡ **性能优化**: 智能的显示/隐藏延迟机制
- 🎮 **交互友好**: 优雅的鼠标交互体验

## 架构设计

### 组件结构

```
ContextMenu/
├── ContextMenuItem.cs          # 菜单项数据模型
├── ContextMenuBuilder.cs       # 菜单构建器（流畅API）
├── MoContextMenu.razor         # 主要组件
├── MoContextMenu.razor.css     # CSS隔离样式
└── README.md                   # 使用文档
```

### 核心类型

#### ContextMenuItem&lt;TItem&gt;

菜单项的数据模型，支持泛型上下文对象：

```csharp
public class ContextMenuItem<TItem>
{
    public string Text { get; set; }                    // 菜单项文本
    public string? Icon { get; set; }                   // 图标
    public bool Disabled { get; set; }                  // 是否禁用
    public bool IsDivider { get; set; }                 // 是否为分割线
    public Func<TItem?, Task>? OnClick { get; set; }    // 点击事件处理
    public string? ShortcutText { get; set; }           // 快捷键提示
    public List<ContextMenuItem<TItem>>? SubItems { get; set; } // 子菜单
    public bool HasSubMenu => SubItems?.Any() == true;  // 是否有子菜单
}
```

#### ContextMenuBuilder&lt;TItem&gt;

采用构建器模式的流畅API：

```csharp
public class ContextMenuBuilder<TItem>
{
    public ContextMenuBuilder<TItem> AddItem(string text, string? icon = null, 
        Func<TItem?, Task>? onClick = null, string? shortcut = null);
    public ContextMenuBuilder<TItem> AddSubMenu(string text, string? icon, 
        Action<ContextMenuBuilder<TItem>> configureSubMenu);
    public ContextMenuBuilder<TItem> AddDivider();
    public ContextMenuBuilder<TItem> AddItemIf(bool condition, string text, 
        string? icon = null, Func<TItem?, Task>? onClick = null, string? shortcut = null);
    public List<ContextMenuItem<TItem>> Build();
    public static ContextMenuBuilder<TItem> Create();
}
```

## 快速开始

### 1. 基本用法

```razor
@page "/context-menu-demo"
@using Monica.UI.Components.ContextMenu

<div @oncontextmenu="ShowContextMenu" @oncontextmenu:preventDefault="true">
    右键点击这里显示菜单
</div>

<MoContextMenu TItem="string" 
               Items="@_menuItems" 
               Visible="@_menuVisible"
               InitialX="@_menuX" 
               InitialY="@_menuY"
               ContextItem="@_contextData"
               OnItemClick="@OnMenuItemClick"
               OnClose="@CloseMenu" />

@code {
    private List<ContextMenuItem<string>> _menuItems = new();
    private bool _menuVisible = false;
    private double _menuX = 0;
    private double _menuY = 0;
    private string? _contextData = "示例数据";

    protected override void OnInitialized()
    {
        _menuItems = ContextMenuBuilder<string>.Create()
            .AddItem("编辑", Icons.Material.Filled.Edit, EditAsync, "Ctrl+E")
            .AddItem("删除", Icons.Material.Filled.Delete, DeleteAsync, "Del")
            .AddDivider()
            .AddItem("复制", Icons.Material.Filled.ContentCopy, CopyAsync, "Ctrl+C")
            .AddItem("粘贴", Icons.Material.Filled.ContentPaste, PasteAsync, "Ctrl+V")
            .Build();
    }

    private async Task ShowContextMenu(MouseEventArgs e)
    {
        _menuX = e.ClientX;
        _menuY = e.ClientY;
        _menuVisible = true;
        StateHasChanged();
    }

    private async Task CloseMenu()
    {
        _menuVisible = false;
        StateHasChanged();
    }

    private async Task OnMenuItemClick(ContextMenuItem<string> item)
    {
        // 菜单项点击处理
        Console.WriteLine($"点击了菜单项: {item.Text}");
    }

    private async Task EditAsync(string? context) => Console.WriteLine("编辑操作");
    private async Task DeleteAsync(string? context) => Console.WriteLine("删除操作");
    private async Task CopyAsync(string? context) => Console.WriteLine("复制操作");
    private async Task PasteAsync(string? context) => Console.WriteLine("粘贴操作");
}
```

### 2. 多级子菜单

```csharp
_menuItems = ContextMenuBuilder<MyDataModel>.Create()
    .AddItem("新建", Icons.Material.Filled.Add)
    .AddSubMenu("导出", Icons.Material.Filled.FileDownload, builder =>
    {
        builder.AddItem("导出为PDF", Icons.Material.Filled.PictureAsPdf, ExportToPdfAsync)
               .AddItem("导出为Excel", Icons.Material.Filled.TableChart, ExportToExcelAsync)
               .AddDivider()
               .AddSubMenu("导出为图片", Icons.Material.Filled.Image, imageBuilder =>
               {
                   imageBuilder.AddItem("PNG格式", null, ExportToPngAsync)
                              .AddItem("JPEG格式", null, ExportToJpegAsync);
               });
    })
    .AddDivider()
    .AddItem("设置", Icons.Material.Filled.Settings, OpenSettingsAsync)
    .Build();
```

### 3. 条件性菜单项

```csharp
_menuItems = ContextMenuBuilder<User>.Create()
    .AddItem("查看详情", Icons.Material.Filled.Visibility, ViewDetailsAsync)
    .AddItemIf(user.CanEdit, "编辑", Icons.Material.Filled.Edit, EditUserAsync)
    .AddItemIf(user.CanDelete, "删除", Icons.Material.Filled.Delete, DeleteUserAsync)
    .AddDivider()
    .AddItemIf(user.IsActive, "停用", Icons.Material.Filled.Block, DeactivateUserAsync)
    .AddItemIf(!user.IsActive, "激活", Icons.Material.Filled.CheckCircle, ActivateUserAsync)
    .Build();
```

## 高级用法

### 1. 自定义菜单上下文

```csharp
public class FileItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsReadOnly { get; set; }
}

// 在组件中使用
<MoContextMenu TItem="FileItem" 
               Items="@_fileMenuItems" 
               Visible="@_menuVisible"
               ContextItem="@_selectedFile"
               OnItemClick="@OnFileMenuClick" />

@code {
    private FileItem? _selectedFile;
    
    private async Task OnFileMenuClick(ContextMenuItem<FileItem> item)
    {
        var file = _selectedFile; // 获取当前选中的文件
        // 根据菜单项和文件信息执行相应操作
    }
    
    private async Task DeleteFileAsync(FileItem? file)
    {
        if (file != null)
        {
            // 删除文件逻辑
            await FileService.DeleteAsync(file.Path);
        }
    }
}
```

### 2. 动态菜单构建

```csharp
private List<ContextMenuItem<Document>> BuildDocumentMenu(Document document)
{
    var builder = ContextMenuBuilder<Document>.Create();
    
    // 基础操作
    builder.AddItem("打开", Icons.Material.Filled.OpenInNew, OpenDocumentAsync);
    
    // 根据文档状态添加不同选项
    if (document.Status == DocumentStatus.Draft)
    {
        builder.AddItem("发布", Icons.Material.Filled.Publish, PublishDocumentAsync);
    }
    else if (document.Status == DocumentStatus.Published)
    {
        builder.AddItem("撤回", Icons.Material.Filled.Undo, UnpublishDocumentAsync);
    }
    
    builder.AddDivider();
    
    // 权限相关操作
    if (document.CanEdit)
    {
        builder.AddItem("编辑", Icons.Material.Filled.Edit, EditDocumentAsync);
    }
    
    if (document.CanShare)
    {
        builder.AddSubMenu("分享", Icons.Material.Filled.Share, shareBuilder =>
        {
            shareBuilder.AddItem("复制链接", Icons.Material.Filled.Link, CopyLinkAsync)
                       .AddItem("发送邮件", Icons.Material.Filled.Email, SendEmailAsync)
                       .AddItem("生成二维码", Icons.Material.Filled.QrCode, GenerateQrCodeAsync);
        });
    }
    
    if (document.CanDelete)
    {
        builder.AddDivider()
               .AddItem("删除", Icons.Material.Filled.Delete, DeleteDocumentAsync);
    }
    
    return builder.Build();
}
```

### 3. 集成到数据表格（推荐方式）

MudDataGrid 提供了内置的 `RowContextMenuClick` 参数，这是集成右键菜单的最佳方式：

```razor
<MudDataGrid T="Product" Items="@_products" Hover="true" Dense="true" 
             RowContextMenuClick="OnRowContextMenuClick">
    <Columns>
        <PropertyColumn Property="x => x.Name" Title="产品名称" />
        <PropertyColumn Property="x => x.Price" Title="价格" />
        <TemplateColumn Title="状态">
            <CellTemplate>
                @{
                    var status = context.Item.Status;
                    var statusColor = status == ProductStatus.Active ? Color.Success : Color.Default;
                    var statusIcon = status == ProductStatus.Active ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Circle;
                }
                <MudChip T="string" Color="@statusColor" Size="Size.Small" Icon="@statusIcon">
                    @status.ToString()
                </MudChip>
            </CellTemplate>
        </TemplateColumn>
        <TemplateColumn Title="操作">
            <CellTemplate>
                <MudStack Row="true" Spacing="1">
                    <MudTooltip Text="编辑">
                        <MudIconButton Icon="@Icons.Material.Filled.Edit" 
                                       Size="Size.Small" 
                                       OnClick="@(() => EditProduct(context.Item))" />
                    </MudTooltip>
                    <MudTooltip Text="删除">
                        <MudIconButton Icon="@Icons.Material.Filled.Delete" 
                                       Size="Size.Small" 
                                       Color="Color.Error"
                                       OnClick="@(() => DeleteProduct(context.Item))" />
                    </MudTooltip>
                </MudStack>
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>

<!-- 右键菜单 -->
<MoContextMenu TItem="Product"
               Items="@_contextMenuItems"
               Visible="@_isContextMenuVisible"
               InitialX="@_contextMenuX"
               InitialY="@_contextMenuY"
               ContextItem="@_contextMenuItem"
               OnItemClick="@OnContextMenuItemClick"
               OnClose="@CloseContextMenu" />

@code {
    private List<Product> _products = new();
    
    // 右键菜单状态
    private bool _isContextMenuVisible = false;
    private double _contextMenuX = 0;
    private double _contextMenuY = 0;
    private Product? _contextMenuItem;
    private List<ContextMenuItem<Product>> _contextMenuItems = new();

    private Task OnRowContextMenuClick(DataGridRowClickEventArgs<Product> args)
    {
        // 根据选中的产品动态生成菜单项
        _contextMenuItems = BuildProductContextMenu(args.Item);
        _contextMenuItem = args.Item;
        _contextMenuX = args.MouseEventArgs.ClientX;
        _contextMenuY = args.MouseEventArgs.ClientY;
        _isContextMenuVisible = true;
        
        StateHasChanged();
        return Task.CompletedTask;
    }
    
    private Task OnContextMenuItemClick(ContextMenuItem<Product> item)
    {
        // 菜单项点击后菜单会自动关闭
        return Task.CompletedTask;
    }
    
    private Task CloseContextMenu()
    {
        _isContextMenuVisible = false;
        _contextMenuItem = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private List<ContextMenuItem<Product>> BuildProductContextMenu(Product product)
    {
        var builder = ContextMenuBuilder<Product>.Create()
            .AddItem("查看详情", Icons.Material.Filled.Info, async (item) =>
            {
                if (item != null)
                    await ShowProductDetailAsync(item);
            }, "Enter")
            .AddItem("编辑", Icons.Material.Filled.Edit, async (item) =>
            {
                if (item != null)
                    await EditProductAsync(item);
            }, "F2")
            .AddDivider();

        // 根据产品状态添加不同的操作
        if (product.Status == ProductStatus.Active)
        {
            builder.AddItem("停用", Icons.Material.Filled.Block, async (item) =>
            {
                if (item != null)
                    await DeactivateProductAsync(item);
            });
        }
        else
        {
            builder.AddItem("激活", Icons.Material.Filled.CheckCircle, async (item) =>
            {
                if (item != null)
                    await ActivateProductAsync(item);
            });
        }

        builder.AddSubMenu("高级操作", Icons.Material.Filled.MoreVert, advancedMenu =>
        {
            advancedMenu.AddItem("复制", Icons.Material.Filled.ContentCopy, async (item) =>
                        {
                            if (item != null)
                                await DuplicateProductAsync(item);
                        }, "Ctrl+D")
                       .AddItem("导出", Icons.Material.Filled.Download, async (item) =>
                        {
                            if (item != null)
                                await ExportProductAsync(item);
                        })
                       .AddDivider()
                       .AddItem("删除", Icons.Material.Filled.Delete, async (item) =>
                        {
                            if (item != null)
                                await DeleteProductAsync(item);
                        }, "Del");
        });

        return builder.Build();
    }

    private async Task ShowProductDetailAsync(Product product)
    {
        var parameters = new DialogParameters<ProductDetailDialog>
        {
            { x => x.Product, product }
        };

        var options = new DialogOptions 
        { 
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        await DialogService.ShowAsync<ProductDetailDialog>("产品详情", parameters, options);
    }
}
```

#### 关键优势：

1. **原生支持**: `RowContextMenuClick` 是 MudDataGrid 的原生功能
2. **更好的事件处理**: `DataGridRowClickEventArgs<T>` 提供完整的行数据和鼠标事件信息
3. **性能优化**: 不需要在每个单元格上绑定事件处理程序
4. **用户体验**: 整行都可以右键点击，更符合用户预期
5. **代码简洁**: 减少了模板代码和事件绑定

## 组件参数

### MoContextMenu 参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Items` | `List<ContextMenuItem<TItem>>` | `new()` | 菜单项列表 |
| `Visible` | `bool` | `false` | 是否显示菜单 |
| `InitialX` | `double` | `0` | 初始X坐标 |
| `InitialY` | `double` | `0` | 初始Y坐标 |
| `ZIndex` | `int` | `1300` | CSS z-index值 |
| `IsRootMenu` | `bool` | `true` | 是否为根菜单 |
| `ContextItem` | `TItem?` | `default` | 上下文对象 |
| `OnItemClick` | `EventCallback<ContextMenuItem<TItem>>` | - | 菜单项点击事件 |
| `OnClose` | `EventCallback` | - | 菜单关闭事件 |

## 样式定制

### CSS变量

组件使用 MudBlazor 的CSS变量系统，支持主题定制：

```css
:root {
    --mud-palette-surface: #ffffff;
    --mud-palette-action-default-hover: #f5f5f5;
    --mud-palette-text-primary: #212121;
    --mud-palette-text-secondary: #757575;
    --mud-palette-text-disabled: #bdbdbd;
    --mud-palette-divider: #e0e0e0;
}
```

### 自定义样式类

```css
/* 自定义菜单容器样式 */
.my-custom-context-menu .mo-context-menu {
    border-radius: 8px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
}

/* 自定义菜单项样式 */
.my-custom-context-menu .mo-context-menu-item {
    padding: 12px 20px;
    font-size: 14px;
}

/* 自定义图标颜色 */
.my-custom-context-menu .mo-menu-icon {
    color: #1976d2;
}
```

## 最佳实践

### 1. 性能优化

```csharp
// 缓存静态菜单项，避免重复构建
private static readonly List<ContextMenuItem<object>> _staticMenuItems = 
    ContextMenuBuilder<object>.Create()
        .AddItem("复制", Icons.Material.Filled.ContentCopy)
        .AddItem("粘贴", Icons.Material.Filled.ContentPaste)
        .Build();

// 动态菜单项仅在需要时构建
private List<ContextMenuItem<User>> BuildUserMenu(User user)
{
    // 根据用户状态动态构建菜单
}
```

### 2. 错误处理

```csharp
private async Task HandleMenuAction(User? user)
{
    try
    {
        if (user == null) return;
        
        // 执行菜单操作
        await UserService.UpdateAsync(user);
        
        // 显示成功消息
        Snackbar.Add("操作成功", Severity.Success);
    }
    catch (Exception ex)
    {
        // 错误处理
        Snackbar.Add($"操作失败: {ex.Message}", Severity.Error);
        Logger.LogError(ex, "菜单操作失败");
    }
}
```

### 3. 可访问性

```razor
<!-- 添加ARIA标签提升可访问性 -->
<div role="button" 
     tabindex="0"
     aria-label="右键打开菜单"
     @oncontextmenu="ShowMenu"
     @onkeydown="HandleKeyDown">
    内容区域
</div>

@code {
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // 支持键盘快捷键打开菜单
        if (e.Key == "F10" && e.ShiftKey)
        {
            await ShowMenu(new MouseEventArgs());
        }
    }
}
```

## 常见问题

### Q: 如何防止菜单超出屏幕边界？
A: 组件内置了自动位置调整功能，会检测屏幕边界并自动调整菜单位置。

### Q: 如何实现异步菜单项？
A: 在 `OnClick` 事件处理程序中使用 `async/await`：

```csharp
.AddItem("保存", Icons.Material.Filled.Save, async (context) =>
{
    await SaveDataAsync(context);
    Snackbar.Add("保存成功", Severity.Success);
})
```

### Q: 如何在菜单项中显示加载状态？
A: 可以通过动态更新菜单项的禁用状态和文本来实现：

```csharp
private async Task ExecuteWithLoading(ContextMenuItem<object> item, Func<Task> action)
{
    var originalText = item.Text;
    item.Text = "处理中...";
    item.Disabled = true;
    StateHasChanged();
    
    try
    {
        await action();
    }
    finally
    {
        item.Text = originalText;
        item.Disabled = false;
        StateHasChanged();
    }
}
```

## 更新日志

### v1.0.0
- 初始版本发布
- 支持基本的右键菜单功能
- 实现多级子菜单
- 添加构建器模式API
- 完善的交互体验和样式设计

---

*更多示例和高级用法请参考项目源码和单元测试。*