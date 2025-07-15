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

```cs
 builder.ConfigModuleUICore().RegisterUIComponents(registry =>
    {
        // 注册SignalR调试组件，同时添加到导航菜单
        registry.RegisterComponent<SignalRDebug>("debug", "SignalR调试", Icons.Material.Filled.ManageAccounts, "SignalR调试", addToNav: true, navOrder: 100);
    });
```

## 最佳实践

1. **组件命名**：使用清晰的命名约定，如 `{模块名}{功能}Component`
2. **组件隔离**：每个基础架构模块的组件应该放在独立的命名空间中
3. **样式管理**：使用 MudBlazor 的主题系统来保持一致的UI风格
4. **服务注入**：在组件中通过依赖注入获取所需的服务
5. **错误处理**：在组件中实现适当的错误处理和用户反馈

## 静态资源配置

### Razor类库静态资源访问

由于本项目是Razor类库，静态资源访问需要特殊配置：

#### 项目配置 (MoLibrary.UI.csproj)
```xml
<PropertyGroup>
  <!-- 启用静态Web资源支持 -->
  <StaticWebAssetProjectMode>Default</StaticWebAssetProjectMode>
  <!-- 确保生成静态Web资源清单 -->
  <GenerateStaticWebAssetsManifest>true</GenerateStaticWebAssetsManifest>
</PropertyGroup>
```

#### 在Web应用程序中的配置
```cs
// 在Program.cs或Startup.cs中
builder.WebHost.UseStaticWebAssets();
app.UseStaticFiles();
```

#### 静态资源访问路径
- **wwwroot文件夹中的资源**：`/_content/MoLibrary.UI/[相对路径]`
- **JavaScript文件**：`/_content/MoLibrary.UI/js/filename.js`
- **CSS文件**：`/_content/MoLibrary.UI/css/filename.css`

#### 在Razor组件中引用JavaScript模块
```cs
// 正确的引用方式
jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "/_content/MoLibrary.UI/js/module.js");

// 错误的引用方式（仅适用于Web应用程序）
jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/module.js");
```

### 离线静态资源管理

为了支持离线使用，所有第三方JavaScript库都应该下载到本地：

1. **下载依赖库**：将第三方库文件保存到`wwwroot/lib/`目录
2. **按需加载**：在组件中按正确顺序加载依赖项
3. **版本管理**：在README中记录使用的库版本

## 依赖项

- .NET 8.0
- Microsoft.AspNetCore.Components.Web
- MudBlazor 8.9.0
- MoLibrary.Core（内部依赖） 