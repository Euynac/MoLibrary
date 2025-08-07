---
description: 使用MoFramework构建UI模块的规则和指南
globs: *.cs,*.razor
alwaysApply: false
---

# 变量定义

> 变量代表特定上下文的参数，用 `$` 包围。

- `$ModuleName$` - 代表模块名称，必须使用PascalCase。
- `$ModuleUIName$` - UI模块名称，格式为 `$ModuleName$UI`。
- `$UIFolderName$` - UI文件夹名称，格式为 `UI$ModuleName$`。
- `$PageName$` - 页面名称，格式为 `UI$ModuleName$Page`。
- `$RouteURL$` - 页面路由URL，必须使用kebab-case，格式建议为 `/$module-name$-debug` 或 `/$module-name$-manage`。

# MoFramework UI模块规则

## 1. 模块文件结构

### 1.1 创建UI模块类文件
- 文件位置：`MoLibrary.FrameworkUI/Modules/$ModuleUIName$.cs`
- 文件命名：以模块名+UI的格式命名，如 `SignalrUI.cs`
- 类命名：`Module$ModuleUIName$`

### 1.2 创建UI模块文件夹
- 文件夹位置：`MoLibrary.FrameworkUI/$UIFolderName$/`
- 文件夹命名：UI+模块名，如 `UISignalr`
- 子文件夹结构：
  ```
  UI$ModuleName$/
  ├── Components/     # Blazor组件
  ├── Models/         # 数据模型
  └── Services/       # 业务服务
  ```

### 1.3 创建页面文件
- 文件位置：`MoLibrary.FrameworkUI/Pages/$PageName$.razor`
- 文件命名：UI+模块名+Page，如 `UISignalRPage.razor`

## 2. 代码结构规范

### 2.1 UI模块类实现
```csharp
public class Module$ModuleUIName$(Module$ModuleUIName$Option option)
    : MoModuleWithDependencies<Module$ModuleUIName$, Module$ModuleUIName$Option, Module$ModuleUIName$Guide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.$ModuleUIName$;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册相关服务
        services.AddScoped<$ModuleName$Service>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.Disable$ModuleName$Page)
        {
            DependsOnModule<Module$ModuleName$Guide>().Register();
            DependsOnModule<ModuleUICoreGuide>().Register()
                .RegisterUIComponents(p => p.RegisterComponent<$PageName$>(
                    $PageName$.$ModuleName$_DEBUG_URL, 
                    "$ModuleName$调试", 
                    Icons.Material.Filled.Settings, 
                    "系统管理", 
                    addToNav: true, 
                    navOrder: 100));
        }
    }
}
```

### 2.2 页面路由定义
```csharp
@attribute [Route($ModuleName$_DEBUG_URL)]

@code {
    public const string $ModuleName$_DEBUG_URL = "/$route-url$";
}
```

### 2.3 页面依赖注入
```csharp
@using MoLibrary.FrameworkUI.$UIFolderName$.Components
@using MoLibrary.FrameworkUI.$UIFolderName$.Services
@using MoLibrary.FrameworkUI.$UIFolderName$.Models
@inject $ModuleName$Service $ModuleName$Service
```

## 3. 模块化开发要求

### 3.1 组件拆分原则
- **单一职责**：每个组件只负责一个特定功能
- **可复用性**：通用功能抽离为独立组件
- **参数化**：通过参数控制组件行为和显示

### 3.2 组件命名规范
- 组件文件放在 `$UIFolderName$/Components/` 目录下
- 命名格式：`$ModuleName$$FunctionName$.razor`
- 示例：`SignalRConnectionConfig.razor`、`SignalRMessageLog.razor`

### 3.3 服务抽象原则
- 业务逻辑直接在Service层实现，不通过HTTP API调用
- Service文件放在 `$UIFolderName$/Services/` 目录下  
- 命名格式：`$ModuleName$Service.cs`
- 服务直接实现业务逻辑，通过依赖注入获取所需的其他服务
- 所有服务方法返回值必须不为空，使用`Res<T>`或`Res`类型
- 异常情况必须捕获并返回`Res.Fail`
- Blazor页面通过依赖注入直接使用服务

### 3.4 数据模型管理
- 页面相关的数据模型放在 `$UIFolderName$/Models/` 目录下
- 使用强类型模型，避免动态类型
- 模型命名要清晰表达其用途
- （重要！）目标模块中已经有模型定义的，直接复用而不是重新创建模型类，减少重复管理，仅创建必要的模型类。

## 4. 开发最佳实践

### 4.1 代码复用
- 相似功能的组件要抽象出公共基类或接口
- 通用的UI交互逻辑封装成可复用的服务
- 统一的样式和主题使用MudBlazor组件库

### 4.2 性能优化
- 大组件拆分为小组件，减少重渲染范围
- 使用`@key`指令优化列表渲染
- 避免在模板中进行复杂计算

### 4.3 错误处理
- 实现统一的错误处理机制
- 使用ISnackbar显示用户友好的错误信息
- 记录详细的调试日志

### 4.4 Blazor生命周期最佳实践
- **避免在OnInitializedAsync中进行耗时操作**：接口初始化和JavaScript互操作不应放在OnInitializedAsync中，这会导致页面阻塞和空白等待
- **使用OnAfterRenderAsync进行初始化**：将耗时的接口调用和JavaScript互操作放在OnAfterRenderAsync(bool firstRender)中
- **确保只在首次渲染时执行**：使用firstRender参数确保初始化逻辑只在首次渲染时执行一次
- **示例代码**：
  ```csharp
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
      if (firstRender)
      {
          await LoadDataAsync(); // 耗时的接口调用
          await JSRuntime.InvokeVoidAsync("initializeComponent"); // JavaScript互操作
      }
  }
  ```

## 5. 服务层和Minimal API集成模式

### 5.1 服务层开发规范
服务层是业务逻辑的核心实现，直接提供功能给Blazor页面使用：

#### 5.1.1 服务实现模式
```csharp
/// <summary>
/// $ModuleName$服务，实现核心业务逻辑
/// </summary>
public class $ModuleName$Service
{
    // 通过依赖注入获取所需服务
    private readonly ILogger<$ModuleName$Service> _logger;
    private readonly IOtherService _otherService;

    public $ModuleName$Service(ILogger<$ModuleName$Service> logger, IOtherService otherService)
    {
        _logger = logger;
        _otherService = otherService;
    }

    /// <summary>
    /// 业务方法实现
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>返回值不为空，错误时返回Res.Fail</returns>
    public async Task<Res<TResponse>> GetDataAsync(TRequest parameter)
    {
        try
        {
            // 直接实现业务逻辑
            var result = await DoBusinessLogic(parameter);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "业务操作失败");
            return Res.Fail($"操作失败: {ex.Message}");
        }
    }

    private async Task<TResponse> DoBusinessLogic(TRequest parameter)
    {
        // 具体业务逻辑实现
        // ...
        return result;
    }
}
```

### 5.2 Minimal API重构规范

当目标模块中包含Minimal API定义时，需要按以下规范重构为Service层模式：

#### 5.2.1 Minimal API重构原则
- **Service类必须定义在源模块**：Service类要定义在目标源模块而不是UI模块，此时无需再定义UI模块层服务，直接使用源模块的Service类。
- **业务逻辑迁移**：将Minimal API中的业务逻辑完全迁移到Service类中
- **接口抽象**：为Service类定义接口，支持依赖注入和测试
- **模型复用**：尽可能复用源模块的数据模型，避免重复定义
- 如果源模块的minimal API使用了匿名类返回的，Service类中实现要定义新的Dto类并返回，不能直接返回匿名类。


#### 5.2.2 重构示例

**重构前的Minimal API：**
```csharp
endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish", 
    async ([FromRoute] string eventKey, 
          [FromServices] IMoDistributedEventBus eventBus, 
          [FromServices] IGlobalJsonOption jsonOption, 
          [FromBody] JsonNode eventContent, 
          HttpResponse response, 
          HttpContext context) =>
{
    if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } e)
    {
        var json = eventContent.ToString();
        var eventToPublish = JsonSerializer.Deserialize(json, e.Type, jsonOption.GlobalOptions)!;
        await eventBus.PublishAsync(e.Type, eventToPublish);
        return Res.Ok(eventToPublish).AppendMsg($"已发布{eventKey}信息").GetResponse();
    }

    return Res.Fail($"获取{eventKey}相关单元信息失败").GetResponse();
});
```

**重构后的Minimal API：**
```csharp
// 映射POST端点
endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish", 
    async ([FromRoute] string eventKey, 
          [FromServices] IDomainEventService domainEventService,
          [FromBody] JsonNode eventContent) =>
    {
        // 直接调用Service处理业务逻辑并返回结果
        return await domainEventService.PublishDomainEventAsync(eventKey, eventContent);
    })
```

**对应的Service接口和实现（定义在源模块中）：**
```csharp
// 接口定义
public interface IDomainEventService
{
    Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent);
}

// 实现类（必须定义在源模块中）
public class DomainEventService(IMoDistributedEventBus eventBus, IGlobalJsonOption jsonOption) : IDomainEventService
{
    public async Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        // 检查是否能获取到对应的事件单元
        if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } unitEvent)
        {
            var json = eventContent.ToString();
            var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonOption.GlobalOptions)!;
            
            await eventBus.PublishAsync(unitEvent.Type, eventToPublish);
            
            return Res.Ok(eventToPublish)
                      .AppendMsg($"已发布{eventKey}信息");
        }
        return Res.Fail($"获取{eventKey}相关单元信息失败");
    }
}
```

#### 5.2.3 数据模型管理原则
- **优先复用源模块模型**：尽可能使用源模块已定义的数据模型
- **组合而非重定义**：如需新模型，采用组合方式组合源模块模型
- **最小化模型创建**：仅在必要时创建新的数据传输对象

### 5.3 Blazor页面调用服务规范

#### 5.3.1 服务注入和调用
```csharp
// 在Blazor页面中注入服务
@inject $ModuleName$Service $ModuleName$Service

@code {
    private async Task LoadDataAsync()
    {
        // 使用IsFailed方法检查结果并获取数据或错误
        if ((await $ModuleName$Service.GetDataAsync(parameter)).IsFailed(out var error, out var data))
        {
            // 处理错误情况
            Snackbar.Add($"操作失败: {error}", Severity.Error);
            return;
        }

        // 处理成功情况，data不为null
        ProcessData(data);
    }

    private void ProcessData(TResponse data)
    {
        // 处理数据，data保证不为null
        // ...
    }
}
```

#### 5.3.2 重要规则
- 所有服务方法的返回值必须不为空
- 成功时返回`Res.Ok(data)`，失败时返回`Res.Fail(errorMessage)`，没有泛型类型的`Res.Fail<T>`以及`OK<T>`这种方法，因为本身有隐式转换！
- 记得必须引用using MoLibrary.Tool.MoResponse，否则会报错
- 异常情况必须捕获并返回`Res.Fail`
- 调用方使用`IsFailed(out var error, out var data)`模式检查结果
- 成功时`data`保证不为null，失败时`error`包含错误信息

#### 5.3.3 依赖注入配置
在模块的`ConfigureServices`方法中注册服务：
```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<$ModuleName$Service>();
}
```

### 5.4 数据模型管理
- 数据模型位置：`$UIFolderName$/Models/`
- 命名约定：`$ModuleName$Response`、`$ModuleName$Request`等
- 用于服务层和Controller之间的数据传输

## 6. 示例参考

以UISignalR模块为例：
- 模块文件：`Modules/SignalrUI.cs`
- UI文件夹：`UISignalr/`
- 页面文件：`Pages/UISignalRPage.razor`
- 组件：`UISignalr/Components/SignalRConnectionConfig.razor`等
- 服务：`UISignalr/Services/SignalRService.cs`

以UISystemInfo模块为例（带Controller集成）：
- 模块文件：`Modules/SystemInfoUI.cs`
- UI文件夹：`UISystemInfo/`
- 页面文件：`Pages/UISystemInfoPage.razor`
- 服务：`UISystemInfo/Services/SystemInfoService.cs` (直接实现业务逻辑)
- Controller：`UISystemInfo/Controllers/ModuleSystemInfoController.cs` (依赖服务实现)
- 模型：`UISystemInfo/Models/SystemInfoResponse.cs`
