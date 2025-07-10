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
// 在其他模块的注册方法中
public void RegisterUIComponents(IUIComponentRegistry registry)
{
    // 注册页面
    registry.RegisterPage("/users", typeof(UserListPage), "用户管理", Icons.Material.Filled.Person, "系统管理");
    
    // 注册导航项
    registry.RegisterNavItem(new UINavItem 
    { 
        Text = "用户管理", 
        Href = "/users", 
        Icon = Icons.Material.Filled.Person,
        Order = 100
    });
    
    // 注册可重用组件
    registry.RegisterComponent<UserSelectorComponent>("UserSelector");
}
```

## 最佳实践

1. **组件命名**：使用清晰的命名约定，如 `{模块名}{功能}Component`
2. **组件隔离**：每个基础架构模块的组件应该放在独立的命名空间中
3. **样式管理**：使用 MudBlazor 的主题系统来保持一致的UI风格
4. **服务注入**：在组件中通过依赖注入获取所需的服务
5. **错误处理**：在组件中实现适当的错误处理和用户反馈

## 依赖项

- .NET 8.0
- Microsoft.AspNetCore.Components.Web
- MudBlazor 8.9.0
- MoLibrary.Core（内部依赖） 