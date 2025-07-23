# UI模块结构指南

## 概述
本文档定义了MoLibrary框架中UI模块的标准结构和组织方式。

## 1. 模块文件结构

### 1.1 UI模块目录结构
```
MoLibrary.FrameworkUI/
├── Modules/                    # UI模块定义
│   └── {ModuleName}UI.cs      # 模块类文件
├── UI{ModuleName}/            # 模块实现目录
│   ├── Components/            # Blazor组件
│   ├── Models/               # 数据模型
│   ├── Services/             # 业务服务
│   └── Controllers/          # API控制器（可选）
├── Pages/                     # 页面组件
│   └── UI{ModuleName}Page.razor
└── wwwroot/                   # 静态资源
    ├── js/                   # JavaScript文件
    └── lib/                  # 第三方库
```

### 1.2 命名规范

#### 模块命名
- UI模块名称：`{ModuleName}UI` (如 `SignalrUI`)
- 模块类名：`Module{ModuleName}UI`
- 文件夹名：`UI{ModuleName}` (如 `UISignalr`)

#### 页面命名
- 页面组件：`UI{ModuleName}Page.razor`
- 路由URL：使用kebab-case，如 `/signalr-debug`

#### 组件命名
- 格式：`{ModuleName}{FunctionName}.razor`
- 示例：`SignalRConnectionConfig.razor`

## 2. 模块类实现

### 2.1 基本结构
```csharp
public class Module{ModuleName}UI(Module{ModuleName}UIOption option)
    : MoModuleWithDependencies<Module{ModuleName}UI, Module{ModuleName}UIOption, Module{ModuleName}UIGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.{ModuleName}UI;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册服务
        services.AddScoped<{ModuleName}Service>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.Disable{ModuleName}Page)
        {
            // 声明依赖的模块
            DependsOnModule<Module{ModuleName}Guide>().Register();
            
            // 注册UI组件
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<UI{ModuleName}Page>(
                    UI{ModuleName}Page.ROUTE_URL, 
                    "{ModuleName}管理", 
                    Icons.Material.Filled.Settings, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 100));
        }
    }
}
```

### 2.2 模块选项
```csharp
public class Module{ModuleName}UIOption : MoModuleOption<Module{ModuleName}UI>
{ 
    /// <summary>
    /// 是否禁用{ModuleName}页面
    /// </summary>
    public bool Disable{ModuleName}Page { get; set; }
    
    /// <summary>
    /// 其他配置选项
    /// </summary>
    public string DefaultSetting { get; set; } = "default";
}
```

## 3. 页面组件结构

### 3.1 页面基本模板
```razor
@page "/module-name"
@attribute [Route(ROUTE_URL)]
@using MoLibrary.FrameworkUI.UI{ModuleName}.Components
@using MoLibrary.FrameworkUI.UI{ModuleName}.Services
@using MoLibrary.FrameworkUI.UI{ModuleName}.Models
@inject {ModuleName}Service Service
@inject ISnackbar Snackbar

<PageTitle>{ModuleName}管理</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h3" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.Settings" Class="mr-2" />
        {ModuleName}管理
    </MudText>
    
    <!-- 页面内容 -->
    @if (_loading)
    {
        <MudProgressCircular Indeterminate="true" />
    }
    else
    {
        <!-- 主要内容区域 -->
    }
</MudContainer>

@code {
    public const string ROUTE_URL = "/module-name";
    
    private bool _loading = true;
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadDataAsync();
        }
    }
    
    private async Task LoadDataAsync()
    {
        _loading = true;
        // 加载数据逻辑
        _loading = false;
        StateHasChanged();
    }
}
```

## 4. 服务层架构

### 4.1 服务实现规范
```csharp
/// <summary>
/// {ModuleName}服务
/// </summary>
public class {ModuleName}Service
{
    private readonly ILogger<{ModuleName}Service> _logger;
    
    public {ModuleName}Service(ILogger<{ModuleName}Service> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 获取数据
    /// </summary>
    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        try
        {
            var result = await ProcessDataAsync(request);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取数据失败");
            return Res.Fail($"操作失败: {ex.Message}");
        }
    }
}
```

### 4.2 服务注册
在模块的 `ConfigureServices` 方法中注册：
```csharp
services.AddScoped<{ModuleName}Service>();
```

## 5. 控制器集成（可选）

### 5.1 何时需要控制器
- 需要为外部系统提供API接口
- 需要支持非Blazor客户端访问
- 需要RESTful API支持

### 5.2 控制器实现
```csharp
[ApiController]
public class Module{ModuleName}Controller : MoModuleControllerBase
{
    private readonly {ModuleName}Service _service;
    
    public Module{ModuleName}Controller({ModuleName}Service service)
    {
        _service = service;
    }
    
    [HttpGet("api/module/{id}")]
    public async Task<IActionResult> GetData(int id)
    {
        var result = await _service.GetDataAsync(id);
        return result.GetResponse(this);
    }
}
```

### 5.3 控制器注册
如果模块需要Controller支持，选项类需要继承 `MoModuleControllerOption`：
```csharp
public class Module{ModuleName}UIOption : MoModuleControllerOption<Module{ModuleName}UI>
{ 
    // 配置选项
}
```

在 `ClaimDependencies` 中注册：
```csharp
DependsOnModule<ModuleControllersGuide>().Register()
    .RegisterMoControllers<Module{ModuleName}Controller>(Option);
```

## 6. 组件组织原则

### 6.1 组件分类
- **展示组件**：只负责UI展示，通过参数接收数据
- **容器组件**：包含业务逻辑，管理状态
- **布局组件**：定义页面结构和布局

### 6.2 组件通信
- 使用参数（Parameters）传递数据
- 使用事件回调（EventCallback）向上传递事件
- 复杂场景使用服务或状态容器

### 6.3 组件复用
- 抽取通用功能为独立组件
- 使用泛型组件提高灵活性
- 通过插槽（RenderFragment）支持内容定制

## 7. 最佳实践

### 7.1 模块独立性
- 每个UI模块应该是独立的功能单元
- 避免模块间的紧密耦合
- 通过依赖注入管理模块间依赖

### 7.2 代码组织
- 相关功能的文件放在同一目录
- 使用命名空间反映目录结构
- 保持文件和类的单一职责

### 7.3 资源管理
- 静态资源放在 `wwwroot` 目录
- JavaScript文件使用模块化组织
- CSS使用组件隔离样式

## 8. 示例参考

### UISignalR模块
```
MoLibrary.FrameworkUI/
├── Modules/SignalrUI.cs
├── UISignalr/
│   ├── Components/
│   │   ├── SignalRConnectionConfig.razor
│   │   ├── SignalRConnectionStatus.razor
│   │   └── SignalRMessageLog.razor
│   ├── Models/
│   │   └── SignalRModels.cs
│   └── Services/
│       └── SignalRDebugService.cs
└── Pages/UISignalRPage.razor
```

### UISystemInfo模块（带Controller）
```
MoLibrary.FrameworkUI/
├── Modules/SystemInfoUI.cs
├── UISystemInfo/
│   ├── Controllers/
│   │   └── ModuleSystemInfoController.cs
│   ├── Models/
│   │   └── SystemInfoResponse.cs
│   └── Services/
│       └── SystemInfoService.cs
└── Pages/UISystemInfoPage.razor
```