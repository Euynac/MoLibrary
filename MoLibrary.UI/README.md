# MoLibrary.UI - UI基础架构类库

## 概述

`MoLibrary.UI` 是一个基于 MudBlazor 的 Razor 类库，为其他基础架构模块提供UI界面支持。该模块采用模块化设计，允许其他基础架构项目引用并注册自己的UI组件。

## 功能特性

- 基于 MudBlazor 的现代化UI框架
- 模块化组件注册机制
- 可重用的布局和组件
- 类库模式，无需独立的入口程序
- 完整的文档注释支持

## 安装使用

### 1. 在其他基础架构模块中引用

在你的基础架构模块项目文件中添加项目引用：

```xml
<ProjectReference Include="..\MoLibrary.UI\MoLibrary.UI.csproj" />
```

### 2. 在Web应用中配置UI模块

在你的ASP.NET Core应用的 `Program.cs` 中：

```csharp
using MoLibrary.UI.Modules;

var builder = WebApplication.CreateBuilder(args);

// 配置UI核心模块
builder.ConfigModuleUICore(options =>
{
    // 可选：配置UI模块选项
});

var app = builder.Build();

// 添加UI中间件
app.Services.GetRequiredService<ModuleUICoreGuide>()
    .AddUIMiddlewares()
    .MapRazorComponents<App>(); // App是你的应用根组件

app.Run();
```

### 3. 在基础架构模块中注册UI组件

#### 方式1：单个组件注册

```csharp
// 在你的模块配置中
public override void ConfigureServices(IServiceCollection services)
{
    // 注册单个UI组件
    services.RegisterUIComponent<MyCustomComponent>(
        "MyComponent", 
        "这是我的自定义组件");
}
```

#### 方式2：批量组件注册

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    // 批量注册UI组件
    services.RegisterUIComponents(
        (typeof(UserListComponent), "UserList", "用户列表组件"),
        (typeof(UserEditComponent), "UserEdit", "用户编辑组件"),
        (typeof(UserDetailComponent), "UserDetail", "用户详情组件")
    );
}
```

#### 方式3：配置模块UI

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.ConfigureModuleUI(config =>
    {
        config.ModuleName = "用户管理模块";
        config.Description = "提供用户管理相关功能";
        config.Icon = "fas fa-users";
        config.Version = "1.0.0";
        config.CustomStyles.Add("primary-color", "#1976d2");
    });
}
```

### 4. 创建Blazor组件

在你的基础架构模块中创建 `.razor` 组件：

```razor
@* Components/UserListComponent.razor *@
@using MudBlazor

<MudContainer>
    <MudText Typo="Typo.h4">用户列表</MudText>
    
    <MudDataGrid Items="@users" Filterable="true" SortMode="@SortMode.Multiple">
        <Columns>
            <MudColumn T="User" Field="Name" Title="姓名" />
            <MudColumn T="User" Field="Email" Title="邮箱" />
            <MudColumn T="User" Field="CreateTime" Title="创建时间" />
        </Columns>
    </MudDataGrid>
</MudContainer>

@code {
    private List<User> users = new();
    
    protected override async Task OnInitializedAsync()
    {
        // 加载用户数据
        users = await LoadUsersAsync();
    }
    
    private async Task<List<User>> LoadUsersAsync()
    {
        // 实现数据加载逻辑
        return new List<User>();
    }
}
```

### 5. 获取已注册的组件

```csharp
@inject IUIComponentRegistry UIRegistry

@code {
    private void ShowRegisteredComponents()
    {
        var components = UIRegistry.GetRegisteredComponents();
        foreach (var component in components)
        {
            Console.WriteLine($"组件: {component.Name}, 类型: {component.ComponentType.Name}");
        }
    }
}
```

## 项目结构

```
MoLibrary.UI/
├── Components/           # 可重用组件
│   ├── Layout/          # 布局组件
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/           # 示例页面组件
│   └── _Imports.razor   # 组件导入
├── Modules/             # 模块配置
│   ├── UICore.cs        # 核心模块定义
│   └── UIModuleExtensions.cs # 扩展方法
└── MoLibrary.UI.csproj  # 项目文件
```

## 最佳实践

1. **组件命名**：使用清晰的命名约定，如 `{模块名}{功能}Component`
2. **组件隔离**：每个基础架构模块的组件应该放在独立的命名空间中
3. **样式管理**：使用 MudBlazor 的主题系统来保持一致的UI风格
4. **服务注入**：在组件中通过依赖注入获取所需的服务
5. **错误处理**：在组件中实现适当的错误处理和用户反馈

## 注意事项

- 该项目是一个 Razor 类库，不包含应用入口程序
- 使用该库的应用需要自己提供 `App.razor` 根组件
- 确保在应用中正确配置了 MudBlazor 的CSS和JS资源
- 组件注册应该在模块的 `ConfigureServices` 方法中进行

## 依赖项

- .NET 8.0
- Microsoft.AspNetCore.Components.Web
- MudBlazor 8.9.0
- MoLibrary.Core（内部依赖） 