---
description: 使用 Monica.Framework.UI 构建 UI 模块的规则和指南
globs: *.cs,*.razor
alwaysApply: false
---

# 变量定义

- `$ModuleName$`：功能名，使用 PascalCase。
- `$ModuleUIName$`：UI 模块名，格式为 `$ModuleName$UI`。
- `$UIFolderName$`：UI 功能目录名，格式为 `UI$ModuleName$`。
- `$PageName$`：页面名，格式为 `UI$ModuleName$Page` 或 `UI$ModuleName$ActionPage`。
- `$RouteURL$`：页面路由，使用 kebab-case，例如 `/$module-name$-manage`、`/$module-name$-monitor`、`/$module-name$-debug`。

# Monica.Framework.UI 模块规则

## 1. 总体定位

- `Monica.Framework.UI` 是 **composite UI module**。
- UI 项目负责页面、组件、对话框、页面状态、显示辅助逻辑。
- UI 直接注入基础设施模块公开的 Facade 或公共模型。
- 不要在 UI 项目中新建业务服务层来承接领域逻辑。
- 新的 Minimal API 入口应定义在源模块，而不是 UI 模块。

## 2. 目录结构

### 2.1 模块文件

- 文件位置：`Monica.Framework.UI/Modules/Module$ModuleUIName$.cs`
- 类命名：`Module$ModuleUIName$`
- `Modules/` 只放模块注册、依赖声明、UI 页面注册、必要的 UI 状态/支持类 DI 注册。

### 2.2 页面文件

- 文件位置：`Monica.Framework.UI/Pages/$PageName$.razor`
- 页面类型名与文件名保持一致。
- 页面路由常量统一命名为 `PAGE_URL`。

### 2.3 功能目录

```text
UI$ModuleName$/
├── Components/   # 可复用页面组件
├── Dialogs/      # 对话框组件
├── Models/       # 仅 UI 侧需要的 ViewModel / 配置模型
├── State/        # 页面状态、会话状态、浏览器状态
└── Support/      # Resolver / Formatter / Coordinator / Query support
```

规则：

- 默认优先使用 `Components/Dialogs/Models/State/Support`。
- 不再新增 `UI$ModuleName$/Services/`。
- 目标模块已有公共模型时，优先复用，不要在 UI 重复定义。

## 3. 命名约定

### 3.1 页面命名

- 单主页：`UI$ModuleName$Page`
- 明确动作页：`UI$ModuleName$MonitorPage`、`UI$ModuleName$ManagePage`、`UI$ModuleName$DebugPage`
- 页面路由常量：`PAGE_URL`

示例：

- `UIProjectUnitsPage`
- `UILoggingMonitorPage`
- `UIMapperDebugPage`

### 3.2 模块选项命名

- 页面开关统一命名为 `DisablePage`
- 不要重复写成 `DisableUIxxxPage`

### 3.3 组件命名

- 普通组件放在 `Components/`
- 对话框组件放在 `Dialogs/`
- 组件命名应表达具体职责，例如：
  - `ProjectUnitMonitor.razor`
  - `SubscriptionTable.razor`
  - `ProjectUnitDetailDialog.razor`

### 3.4 State / Support 命名

- `State/`：页面状态、筛选状态、轮询状态、缓存状态
- `Support/`：格式化、解析、协调、映射、查询辅助
- 避免继续使用含糊的 `Manager`、`Helper`、`Core`

## 4. UI 模块实现规范

### 4.1 模块实现模板

```csharp
public class Module$ModuleUIName$(Module$ModuleUIName$Option option)
    : MoModule<Module$ModuleUIName$, Module$ModuleUIName$Option, Module$ModuleUIName$Guide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<$ModuleName$PageState>();
        services.AddScoped<$ModuleName$Support>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.DisablePage)
        {
            DependsOnModule<Module$ModuleName$Guide>().Register();
            DependsOnModule<ModuleShellUIGuide>().Register()
                .RegisterUIComponents(p => p.RegisterLocalizedComponent<$PageName$>(
                    $PageName$.PAGE_URL,
                    "$ModuleName$",
                    Icons.Material.Filled.Settings,
                    "系统管理",
                    addToNav: true,
                    navOrder: 100));
        }
    }
}
```

### 4.2 页面模板

```razor
@attribute [Route(PAGE_URL)]
@inject $ModuleName$Facade Facade
@inject $ModuleName$PageState PageState

@code {
    public const string PAGE_URL = "/$route-url$";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await PageState.InitializeAsync();
            StateHasChanged();
        }
    }
}
```

## 5. 依赖注入与职责边界

### 5.1 允许的依赖方向

- `Page -> State`
- `Page -> Support`
- `Page / Component / Dialog -> Facade`
- `State -> Facade`
- `Support -> 公共模型 / Facade`

### 5.2 禁止的模式

- UI 页面直接调用私有服务实现业务逻辑
- 在 UI 项目中新增承担领域逻辑的 `Services/`
- 在 UI 模块中新增业务 Minimal API
- 页面内部维护复杂轮询、并发控制、状态机而不抽到 `State/`

## 6. 页面拆分原则

- 页面应是薄壳，只负责路由、生命周期、布局组合。
- 复杂区域提取到 `Components/`
- 对话框提取到 `Dialogs/`
- 页面级状态提取到 `State/`
- 格式化、解析、协调类提取到 `Support/`

建议：

- 页面 markup 控制在 100 到 200 行左右
- 页面 `@code` 控制在 50 到 150 行左右
- 超过后优先拆状态和组件，不要继续往页面里堆逻辑

## 7. 生命周期建议

- 耗时初始化优先放在 `OnAfterRenderAsync(bool firstRender)`
- `firstRender` 为 `true` 时只执行一次初始化
- JavaScript 互操作与初始加载放在相同初始化流程中统一管理

## 8. 迁移原则

- 老代码中的 `Services/` 迁移时，优先判断应该进入 `State/` 还是 `Support/`
- 对话框从 `Components/` 迁到 `Dialogs/`
- 仅 UI 使用的配置类从 `Components/` 迁到 `Models/`
- namespace 可在最后统一，不要求与目录迁移同步完成
