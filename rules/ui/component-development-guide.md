# Blazor 组件开发指南

## 1. 组件文件结构

### 1.1 基础组件结构
```
MoLibrary.UI/
├── Components/
│   ├── Common/                 # 通用基础组件
│   │   ├── MoCard.razor       # 组件实现
│   │   ├── MoCard.razor.cs    # 代码后置（可选）
│   │   └── MoCard.razor.css   # 组件样式
│   ├── Business/              # 业务组件
│   └── Layout/                # 布局组件
```

### 1.2 组件命名规范
- 组件名以 `Mo` 前缀开始
- 使用 PascalCase 命名
- 名称应清晰表达组件用途
- 示例：`MoDataGrid`, `MoLoadingIndicator`, `MoUserProfile`

## 2. 组件模板结构

### 2.1 基础组件模板
```razor
@namespace MoLibrary.UI.Components.Common
@inherits MoComponentBase

<div class="@CssClass" @attributes="AdditionalAttributes">
    @if (Loading)
    {
        <MoLoadingState />
    }
    else if (!string.IsNullOrEmpty(ErrorMessage))
    {
        <MoErrorState Message="@ErrorMessage" OnRetry="@OnRetryCallback" />
    }
    else
    {
        @ChildContent
    }
</div>

@code {
    [Parameter] public bool Loading { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback OnRetryCallback { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter(CaptureUnmatchedValues = true)] 
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
    
    private string CssClass => CssBuilder.Default("mo-component")
        .AddClass(Class)
        .Build();
}
```

### 2.2 业务组件模板
```razor
@page "/module/{ModuleName}"
@using MoLibrary.UI.Components.Common
@inject IModuleService ModuleService
@inject ISnackbar Snackbar

<PageTitle>@ModuleName 管理</PageTitle>

<MoCard Loading="@_loading" ErrorMessage="@_errorMessage" OnRetryCallback="@LoadData">
    <MudText Typo="Typo.h4" Class="mb-4">@ModuleName 配置</MudText>
    
    @if (_moduleData != null)
    {
        <ModuleConfigForm Module="@_moduleData" OnSave="@SaveConfiguration" />
    }
</MoCard>

@code {
    [Parameter] public string ModuleName { get; set; } = string.Empty;
    
    private bool _loading = true;
    private string? _errorMessage;
    private ModuleData? _moduleData;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadData();
        }
    }
    
    private async Task LoadData()
    {
        _loading = true;
        _errorMessage = null;
        
        if ((await ModuleService.GetModuleDataAsync(ModuleName)).IsFailed(out var error, out var data))
        {
            _errorMessage = error.Message;
        }
        else
        {
            _moduleData = data;
        }
        
        _loading = false;
        StateHasChanged();
    }
    
    private async Task SaveConfiguration(ModuleData data)
    {
        if ((await ModuleService.SaveModuleDataAsync(data)).IsFailed(out var error))
        {
            Snackbar.Add($"保存失败: {error.Message}", Severity.Error);
        }
        else
        {
            Snackbar.Add("保存成功", Severity.Success);
        }
    }
}
```

## 3. 组件参数设计

### 3.1 必需参数
```csharp
// 使用 EditorRequired 标记必需参数
[Parameter, EditorRequired] 
public string Title { get; set; } = string.Empty;

[Parameter, EditorRequired] 
public RenderFragment ChildContent { get; set; } = null!;
```

### 3.2 可选参数与默认值
```csharp
[Parameter] public Color Color { get; set; } = Color.Primary;
[Parameter] public Size Size { get; set; } = Size.Medium;
[Parameter] public bool Disabled { get; set; }
```

### 3.3 事件回调
```csharp
[Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
[Parameter] public EventCallback<string> OnValueChanged { get; set; }

// 泛型事件回调
[Parameter] public EventCallback<TItem> OnItemSelected { get; set; }
```

## 4. 状态管理

### 4.1 组件内部状态
```csharp
@code {
    private bool _isExpanded;
    private string _searchText = string.Empty;
    private List<Item> _filteredItems = new();
    
    private void ToggleExpanded()
    {
        _isExpanded = !_isExpanded;
    }
    
    private void FilterItems(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? string.Empty;
        _filteredItems = Items.Where(i => i.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
```

### 4.2 级联参数
```razor
<!-- 父组件提供级联值 -->
<CascadingValue Value="@ThemeSettings">
    <ChildComponents />
</CascadingValue>

<!-- 子组件接收级联值 -->
@code {
    [CascadingParameter] 
    public ThemeSettings? ThemeSettings { get; set; }
}
```

## 5. 组件通信模式

### 5.1 父子组件通信
```razor
<!-- 父组件 -->
<ChildComponent Value="@parentValue" OnValueChanged="@HandleValueChange" />

@code {
    private string parentValue = "Initial";
    
    private void HandleValueChange(string newValue)
    {
        parentValue = newValue;
    }
}

<!-- 子组件 -->
@code {
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> OnValueChanged { get; set; }
    
    private async Task UpdateValue(string newValue)
    {
        await OnValueChanged.InvokeAsync(newValue);
    }
}
```

### 5.2 使用服务进行组件间通信
```csharp
// 通知服务
public class NotificationService
{
    public event Action<string>? OnNotification;
    
    public void Notify(string message)
    {
        OnNotification?.Invoke(message);
    }
}

// 发送组件
@inject NotificationService NotificationService

private void SendNotification()
{
    NotificationService.Notify("Hello from Component A");
}

// 接收组件
@inject NotificationService NotificationService
@implements IDisposable

protected override void OnInitialized()
{
    NotificationService.OnNotification += HandleNotification;
}

private void HandleNotification(string message)
{
    // 处理通知
    StateHasChanged();
}

public void Dispose()
{
    NotificationService.OnNotification -= HandleNotification;
}
```

## 6. 性能优化技巧

### 6.1 避免不必要的渲染
```csharp
private string _lastProcessedValue = string.Empty;

protected override bool ShouldRender()
{
    // 只在值真正改变时渲染
    if (_lastProcessedValue == CurrentValue)
    {
        return false;
    }
    
    _lastProcessedValue = CurrentValue;
    return true;
}
```

### 6.2 使用组件缓存
```razor
@if (_showComponent)
{
    <KeepAlive>
        <ExpensiveComponent />
    </KeepAlive>
}
```

### 6.3 延迟加载
```csharp
private bool _componentLoaded;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // 延迟加载重组件
        await Task.Delay(100);
        _componentLoaded = true;
        StateHasChanged();
    }
}
```

## 7. 可复用组件模式

### 7.1 插槽模式（Slot Pattern）
```razor
<!-- 定义组件 -->
<div class="mo-panel">
    <div class="mo-panel-header">
        @HeaderContent
    </div>
    <div class="mo-panel-body">
        @BodyContent
    </div>
    @if (FooterContent != null)
    {
        <div class="mo-panel-footer">
            @FooterContent
        </div>
    }
</div>

@code {
    [Parameter] public RenderFragment? HeaderContent { get; set; }
    [Parameter] public RenderFragment? BodyContent { get; set; }
    [Parameter] public RenderFragment? FooterContent { get; set; }
}

<!-- 使用组件 -->
<MoPanel>
    <HeaderContent>
        <h3>Panel Title</h3>
    </HeaderContent>
    <BodyContent>
        <p>Panel content goes here</p>
    </BodyContent>
    <FooterContent>
        <MudButton>Action</MudButton>
    </FooterContent>
</MoPanel>
```

### 7.2 渲染委托模式
```razor
@typeparam TItem

<div class="mo-list">
    @foreach (var item in Items)
    {
        <div class="mo-list-item" @onclick="() => HandleItemClick(item)">
            @ItemTemplate(item)
        </div>
    }
</div>

@code {
    [Parameter, EditorRequired] 
    public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
    
    [Parameter, EditorRequired] 
    public RenderFragment<TItem> ItemTemplate { get; set; } = null!;
    
    [Parameter] 
    public EventCallback<TItem> OnItemClick { get; set; }
    
    private async Task HandleItemClick(TItem item)
    {
        await OnItemClick.InvokeAsync(item);
    }
}
```

## 8. 错误处理和验证

### 8.1 输入验证
```razor
<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    
    <MudTextField @bind-Value="model.Email" 
                  Label="邮箱"
                  For="@(() => model.Email)"
                  Immediate="true"
                  Validation="@(new EmailAddressAttribute())" />
    
    <MudButton ButtonType="ButtonType.Submit" 
               Disabled="@(!context.IsModified() || !context.Validate())">
        提交
    </MudButton>
</EditForm>
```

### 8.2 错误边界
```razor
<ErrorBoundary @ref="errorBoundary">
    <ChildContent>
        <ComplexComponent />
    </ChildContent>
    <ErrorContent Context="exception">
        <MudAlert Severity="Severity.Error" Class="mb-4">
            <MudText>组件发生错误</MudText>
            <MudText Typo="Typo.body2">@exception.Message</MudText>
            <MudButton Color="Color.Primary" 
                      Variant="Variant.Text" 
                      OnClick="@(() => errorBoundary?.Recover())">
                重试
            </MudButton>
        </MudAlert>
    </ErrorContent>
</ErrorBoundary>

@code {
    private ErrorBoundary? errorBoundary;
}
```

## 9. 组件测试指南

### 9.1 组件可测试性设计
```csharp
// 使用依赖注入而不是直接实例化
[Inject] private IDataService DataService { get; set; } = null!;

// 提供测试钩子
[Parameter] public bool SkipAnimation { get; set; }

// 暴露内部状态用于测试
public bool IsLoading => _isLoading;
public string? LastError => _lastError;
```

### 9.2 组件文档
```csharp
/// <summary>
/// 显示模块信息的卡片组件
/// </summary>
/// <example>
/// <code>
/// <MoModuleCard Module="@module" 
///               ShowActions="true"
///               OnEdit="@HandleEdit"
///               OnDelete="@HandleDelete" />
/// </code>
/// </example>
public partial class MoModuleCard : ComponentBase
{
    /// <summary>
    /// 要显示的模块信息
    /// </summary>
    [Parameter, EditorRequired] 
    public ModuleInfo Module { get; set; } = null!;
    
    /// <summary>
    /// 是否显示操作按钮
    /// </summary>
    [Parameter] 
    public bool ShowActions { get; set; } = true;
}
```

## 10. 组件生命周期钩子使用

### 10.1 完整生命周期示例
```csharp
public class LifecycleComponent : ComponentBase, IAsyncDisposable
{
    private Timer? _timer;
    
    // 1. 设置参数后，渲染前
    protected override void OnInitialized()
    {
        // 初始化组件状态
        // 订阅事件
    }
    
    // 2. 参数设置/更新后
    protected override void OnParametersSet()
    {
        // 响应参数变化
        // 验证参数
    }
    
    // 3. 决定是否渲染
    protected override bool ShouldRender()
    {
        // 性能优化
        return base.ShouldRender();
    }
    
    // 4. 渲染后
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 首次渲染后的初始化
            // JavaScript 互操作
            // 启动定时器
            _timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
    }
    
    // 5. 组件销毁
    public async ValueTask DisposeAsync()
    {
        // 清理资源
        _timer?.Dispose();
        // 取消订阅
        // 释放非托管资源
    }
    
    private void TimerCallback(object? state)
    {
        // 定时器回调逻辑
        InvokeAsync(StateHasChanged);
    }
}
```

这个组件开发指南提供了创建高质量、可维护的Blazor组件所需的核心知识和模式。遵循这些指南将帮助您构建一致、可复用的组件库。