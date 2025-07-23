# Blazor UI 最佳实践

## 概述
本文档定义了在MoLibrary框架中开发Blazor UI的最佳实践，旨在提高代码质量、可维护性和用户体验。

## 1. 组件架构原则

### 1.1 组件层次结构
- **基础组件（Common）**: 可复用的原子组件
- **业务组件（Business）**: 特定功能的组合组件
- **页面组件（Pages）**: 完整的页面级组件

### 1.2 单一职责原则
- 每个组件只负责一个特定功能
- 复杂功能通过组合多个简单组件实现
- 避免在单个组件中混合展示逻辑和业务逻辑

### 1.3 组件通信
- 使用参数（Parameters）进行父子组件通信
- 使用事件回调（EventCallback）向上传递事件
- 复杂状态使用状态容器（State Container）管理

## 2. 生命周期最佳实践

### 2.1 避免在OnInitializedAsync中进行耗时操作
```csharp
// ❌ 错误示例
protected override async Task OnInitializedAsync()
{
    // 不要在这里进行耗时的数据加载
    await LoadLargeDataSetAsync();
    // 不要在这里进行JavaScript互操作
    await JSRuntime.InvokeVoidAsync("initializeChart");
}

// ✅ 正确示例
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // 在首次渲染后加载数据
        await LoadLargeDataSetAsync();
        // JavaScript互操作也应该在这里
        await JSRuntime.InvokeVoidAsync("initializeChart");
        StateHasChanged();
    }
}
```

### 2.2 使用CancellationToken管理异步操作
```csharp
@implements IAsyncDisposable

@code {
    private CancellationTokenSource? _cts;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _cts = new CancellationTokenSource();
            await LoadDataAsync(_cts.Token);
        }
    }
    
    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await DataService.GetDataAsync(cancellationToken);
            ProcessData(data);
        }
        catch (OperationCanceledException)
        {
            // 处理取消操作
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

## 3. 性能优化

### 3.1 使用@key优化列表渲染
```razor
@foreach (var item in Items)
{
    <div @key="item.Id">
        <ItemComponent Item="@item" />
    </div>
}
```

### 3.2 避免不必要的重渲染
```csharp
// 使用ShouldRender控制渲染
protected override bool ShouldRender()
{
    // 只在数据真正改变时才重新渲染
    return _hasDataChanged;
}
```

### 3.3 大数据集使用虚拟化
```razor
<MudVirtualize Items="@LargeDataSet" Context="item">
    <ItemTemplate>
        <ItemDisplay Item="@item" />
    </ItemTemplate>
</MudVirtualize>
```

## 4. 样式管理

### 4.1 使用CSS变量
```css
:root {
    --mo-primary-color: #7e6fff;
    --mo-spacing-unit: 8px;
    --mo-border-radius: 4px;
}

.mo-component {
    padding: calc(var(--mo-spacing-unit) * 2);
    border-radius: var(--mo-border-radius);
}
```

### 4.2 组件样式隔离
- 使用 `.razor.css` 文件进行组件样式隔离
- 避免使用全局样式选择器
- 使用BEM命名规范或CSS Modules

### 4.3 响应式设计
```css
.mo-container {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: var(--mo-spacing-unit);
}

@media (max-width: 768px) {
    .mo-container {
        grid-template-columns: 1fr;
    }
}
```

## 5. 状态管理

### 5.1 使用状态容器
```csharp
public class AppStateContainer
{
    private string _userName = string.Empty;
    
    public string UserName 
    { 
        get => _userName;
        set
        {
            _userName = value;
            NotifyStateChanged();
        }
    }
    
    public event Action? OnChange;
    
    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

### 5.2 组件中使用状态容器
```csharp
@inject AppStateContainer AppState
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        AppState.OnChange += StateHasChanged;
    }
    
    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }
}
```

## 6. 错误处理

### 6.1 使用ErrorBoundary
```razor
<ErrorBoundary>
    <ChildContent>
        <ComplexComponent />
    </ChildContent>
    <ErrorContent Context="exception">
        <MudAlert Severity="Severity.Error">
            发生错误: @exception.Message
        </MudAlert>
    </ErrorContent>
</ErrorBoundary>
```

### 6.2 服务层错误处理
```csharp
public async Task<Res<TData>> GetDataAsync()
{
    try
    {
        var data = await FetchDataAsync();
        return Res.Ok(data);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "获取数据失败");
        return Res.Fail($"获取数据失败: {ex.Message}");
    }
}
```

## 7. 表单处理

### 7.1 使用EditForm和验证
```razor
<EditForm Model="@model" OnValidSubmit="@HandleValidSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />
    
    <MudTextField @bind-Value="model.Name" 
                  Label="名称" 
                  For="@(() => model.Name)" />
    
    <MudButton ButtonType="ButtonType.Submit" 
               Variant="Variant.Filled" 
               Color="Color.Primary">
        提交
    </MudButton>
</EditForm>
```

### 7.2 自定义验证
```csharp
public class CustomValidator : ComponentBase
{
    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }
    
    protected override void OnInitialized()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException($"{nameof(CustomValidator)} requires a cascading parameter of type {nameof(EditContext)}.");
        }
        
        CurrentEditContext.OnValidationRequested += ValidateModel;
    }
    
    private void ValidateModel(object? sender, ValidationRequestedEventArgs e)
    {
        // 自定义验证逻辑
    }
}
```

## 8. 可访问性（Accessibility）

### 8.1 使用语义化HTML
```razor
<nav aria-label="主导航">
    <ul>
        <li><a href="/">首页</a></li>
        <li><a href="/about">关于</a></li>
    </ul>
</nav>
```

### 8.2 提供键盘导航支持
```razor
<div @onkeydown="HandleKeyDown" tabindex="0">
    <!-- 可键盘导航的内容 -->
</div>

@code {
    private void HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowUp":
                // 处理向上导航
                break;
            case "ArrowDown":
                // 处理向下导航
                break;
        }
    }
}
```

## 9. 组件复用模式

### 9.1 泛型组件
```razor
@typeparam TItem

<div class="mo-list">
    @foreach (var item in Items)
    {
        @ItemTemplate(item)
    }
</div>

@code {
    [Parameter, EditorRequired] 
    public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
    
    [Parameter, EditorRequired] 
    public RenderFragment<TItem> ItemTemplate { get; set; } = null!;
}
```

### 9.2 组合优于继承
```razor
<!-- 基础卡片组件 -->
<MoCard>
    <Header>
        @HeaderContent
    </Header>
    <Body>
        @BodyContent
    </Body>
    <Footer>
        @FooterContent
    </Footer>
</MoCard>

<!-- 特定业务卡片 -->
<UserCard User="@user">
    <Actions>
        <MudButton>编辑</MudButton>
        <MudButton>删除</MudButton>
    </Actions>
</UserCard>
```

## 10. MudBlazor特定最佳实践

### 10.1 正确使用Icon属性
```razor
<!-- ✅ 正确：使用@前缀 -->
<MudIconButton Icon="@Icons.Material.Filled.Add" />

<!-- ❌ 错误：缺少@前缀 -->
<MudIconButton Icon="Icons.Material.Filled.Add" />
```

### 10.2 泛型组件显式指定类型
```razor
<!-- ✅ 正确：显式指定T类型 -->
<MudSwitch T="bool" @bind-Checked="@IsEnabled" />
<MudChip T="string" Value="@chipValue" />

<!-- ❌ 错误：未指定类型参数 -->
<MudSwitch @bind-Checked="@IsEnabled" />
```

### 10.3 使用MudBlazor主题系统
```csharp
// 在布局组件中配置主题
<MudThemeProvider Theme="@_theme" IsDarkMode="@_isDarkMode" />

@code {
    private MudTheme _theme = new()
    {
        Palette = new PaletteLight()
        {
            Primary = "#7e6fff",
            Secondary = "#ff4081"
        }
    };
}
```

## 总结
遵循这些最佳实践将帮助您构建高质量、可维护、性能优异的Blazor应用。记住，这些是指导原则，应根据具体项目需求灵活应用。