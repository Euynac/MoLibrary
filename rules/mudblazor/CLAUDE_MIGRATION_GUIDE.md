# MudBlazor v8.9.0 迁移与API引导文档

本文档为Claude Code提供MudBlazor v8版本的关键API变化和新特性指南，帮助理解和使用最新版本的正确方式。

## 重要：版本差异说明

Claude Code的本地知识库基于v7.x版本，但当前代码库是v8.9.0。本文档列出了关键差异，遇到不确定的API时请参考此文档。

## 1. 异步API迁移（最重要）

### DialogService
```csharp
// ❌ v7 旧API（已废弃）
var dialog = DialogService.Show<MyDialog>();
var dialog = DialogService.Show<MyDialog>("Title", parameters);
var dialog = DialogService.Show(typeof(MyDialog));

// ✅ v8 新API（必须使用）
var dialog = await DialogService.ShowAsync<MyDialog>();
var dialog = await DialogService.ShowAsync<MyDialog>("Title", parameters);
var dialog = await DialogService.ShowAsync(typeof(MyDialog));
```

### MudDataGrid
```csharp
// ❌ v7 旧API（已废弃）
dataGrid.ExpandAllGroups();
dataGrid.CollapseAllGroups();
dataGrid.SetSelectedItem(item);
dataGrid.SetSelectedItems(items);

// ✅ v8 新API（必须使用）
await dataGrid.ExpandAllGroupsAsync();
await dataGrid.CollapseAllGroupsAsync();
await dataGrid.SetSelectedItemAsync(item);
await dataGrid.SetSelectedItemsAsync(items);
```

### MudThemeProvider
```csharp
// ❌ v7 旧API（已废弃）
var isDarkMode = await themeProvider.GetSystemPreference();
await themeProvider.WatchSystemPreference(OnSystemPreferenceChanged);
await themeProvider.SystemPreferenceChanged(isDarkMode);

// ✅ v8 新API（必须使用）
var isDarkMode = await themeProvider.GetSystemDarkModeAsync();
await themeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChanged);
await themeProvider.SystemDarkModeChangedAsync(isDarkMode);
```

## 2. 新组件使用指南

### MudToggleGroup（新）
```razor
<MudToggleGroup T="string" @bind-Value="selectedValue">
    <MudToggleItem Value="@("option1")">选项1</MudToggleItem>
    <MudToggleItem Value="@("option2")">选项2</MudToggleItem>
    <MudToggleItem Value="@("option3")">选项3</MudToggleItem>
</MudToggleGroup>
```

### MudStepper（新）
```razor
<MudStepper @ref="stepper">
    <MudStep Title="第一步">
        <ChildContent>步骤1内容</ChildContent>
    </MudStep>
    <MudStep Title="第二步">
        <ChildContent>步骤2内容</ChildContent>
    </MudStep>
    <MudStep Title="完成">
        <ChildContent>完成内容</ChildContent>
    </MudStep>
</MudStepper>
```

### MudDataGrid拖拽功能（新）
```razor
<MudDataGrid T="MyModel" 
             Items="@items"
             DragDropColumnReordering="true"
             ColumnsPanelReordering="true"
             DragIndicatorIcon="@Icons.Material.Filled.DragIndicator"
             DropAllowedClass="drop-allowed"
             DropNotAllowedClass="drop-not-allowed">
    <!-- 列定义 -->
</MudDataGrid>
```

## 3. 参数状态管理新模式

### ParameterState使用（推荐）
```csharp
public partial class MyComponent : MudComponentBase
{
    private readonly ParameterState<string> _valueState;
    
    public MyComponent()
    {
        // v8新的参数注册方式
        _valueState = RegisterParameter(nameof(Value))
            .WithParameter(() => Value)
            .WithEventCallback(() => ValueChanged)
            .WithChangeHandler(OnValueChanged);
    }
    
    [Parameter] public string Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    
    private Task OnValueChanged()
    {
        // 处理值变化
        return Task.CompletedTask;
    }
}
```

### 双向绑定更新值
```csharp
// ❌ 错误方式（不要直接设置参数）
Value = newValue;

// ✅ 正确方式（使用ParameterState）
await _valueState.SetValueAsync(newValue);
// 或
await ValueChanged.InvokeAsync(newValue);
```

## 4. 本地化支持

### 配置本地化
```csharp
// Program.cs
builder.Services.AddMudServices(config =>
{
    // v8新增：本地化拦截器
    config.Services.AddSingleton<ILocalizationInterceptor, CustomLocalizationInterceptor>();
});
```

### 自定义本地化拦截器
```csharp
public class CustomLocalizationInterceptor : ILocalizationInterceptor
{
    public string Intercept(string key, params object[] arguments)
    {
        // 自定义本地化逻辑
        return GetLocalizedString(key, arguments);
    }
}
```

## 5. 主题系统增强

### 分离的调色板
```csharp
var theme = new MudTheme
{
    // v8新增：分离的亮色和暗色调色板
    PaletteLight = new PaletteLight
    {
        Primary = Colors.Blue.Default,
        Secondary = Colors.Green.Default
    },
    PaletteDark = new PaletteDark
    {
        Primary = Colors.Blue.Lighten1,
        Secondary = Colors.Green.Lighten1
    }
};
```

## 6. 性能优化建议

### 使用正确的基类
```csharp
// 对于有状态管理需求的组件
public partial class MyComponent : ComponentBaseWithState
{
    // 自动享受v8的状态管理优化
}

// 对于简单组件
public partial class SimpleComponent : MudComponentBase
{
    // 标准组件基类
}
```

### Trimming支持
```xml
<!-- 项目文件中启用trimming -->
<PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>link</TrimMode>
</PropertyGroup>
```

## 7. 搜索关键词指南

当Claude Code需要查找特定功能时，使用以下关键词：

- **异步方法**：搜索 "Async" 后缀
- **废弃API**：搜索 "[Obsolete"
- **新组件**：查看 src/MudBlazor/Components 目录
- **参数管理**：搜索 "ParameterState" 或 "RegisterParameter"
- **本地化**：搜索 "ILocalizationInterceptor"
- **主题**：搜索 "PaletteLight" 或 "PaletteDark"
- **拖拽**：搜索 "DragDrop" 或 "Reordering"

## 8. 常见错误排查

### 错误：方法不存在
如果遇到方法不存在错误，检查是否应该使用异步版本：
- `Show` → `ShowAsync`
- `Close` → `CloseAsync`
- `Expand` → `ExpandAsync`

### 错误：参数设置无效
确保不在参数setter中包含逻辑，使用OnParametersSetAsync代替。

### 错误：主题不生效
检查是否使用了新的PaletteLight/PaletteDark而不是旧的Palette。

## 9. 快速参考

### 必须记住的规则
1. **所有UI操作使用异步方法**
2. **不在参数setter中写逻辑**
3. **使用ParameterState管理双向绑定**
4. **组件必须支持RTL**
5. **新组件使用v8基类**

### 版本检查
```csharp
// 如果需要检查MudBlazor版本
var assembly = typeof(MudComponentBase).Assembly;
var version = assembly.GetName().Version;
// 当前应该是 8.9.0.x
```

---

**提示**：如果Claude Code在编码时遇到不确定的API，应该：
1. 首先检查是否有Async版本
2. 使用Grep搜索相关组件的最新实现
3. 查看该组件的单元测试了解正确用法
4. 参考此文档的示例代码