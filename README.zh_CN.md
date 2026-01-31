# Monica

<p align="center">
  <img src="logo.jpeg" alt="Monica Logo" width="200" />
</p>


<p align="center">
  <a href="https://github.com/molloryn/Monica.Docs/actions"><img src="https://github.com/molloryn/Monica.Docs/actions/workflows/static.yml/badge.svg" alt="Build Status"></a>
  <a href="https://www.nuget.org/packages?q=Monica"><img src="https://img.shields.io/nuget/v/Monica.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/molloryn/Monica/blob/main/LICENSE"><img src="https://img.shields.io/github/license/molloryn/Monica" alt="License"></a>
  <a href="https://monica.dpdns.org/"><img src="https://img.shields.io/badge/docs-online-brightgreen.svg" alt="Documentation"></a>
</p>

> ⚠️ **开发状态**：Monica 目前处于内部开发阶段，正在快速迭代中。API 可能会发生破坏性变更。文档正在积极完善中。目前不建议在生产环境中使用。

面向前沿应用的模块化 .NET 基础设施。一个全面的框架，提供 30+ 个独立模块，涵盖从核心基础设施到 AI 集成的方方面面。

## 语言

[English](README.md) | 简体中文

## 📖 概述

Monica 是一个模块化的 .NET 基础设施库，专为灵活性和性能而设计。每个模块都可以独立使用，无需引入整个框架。通过统一的注册和配置模式，Monica 提供一致且高效的开发体验。

**[📚 在线文档](https://monica.dpdns.org/) • [🚀 快速开始](https://monica.dpdns.org/docs/intro) • [📝 博客](https://monica.dpdns.org/blog)**

## ✨ 特性

- **🧩 真正的模块化**：每个组件都是独立的 - 只使用您需要的模块，无需引入整个框架
- **🔄 统一直觉的 API**：所有模块都遵循相同的注册和配置模式，使用 `Mo.Add*()` 约定
- **⚡ 高性能**：
  - 自动中间件注册，无需手动配置
  - 防止重复注册 - 模块自动仅注册一次
  - 优化的服务注册，减少反射开销
  - 及时释放临时对象，最小化内存占用
- **🔌 自动中间件解析**：无需手动管理中间件注册顺序
- **🔍 依赖关系可视化**：主动警告潜在的注册失败和配置错误
- **🎯 源代码生成器**：代码生成减少样板代码并提高性能
- **🖥️ 全面的仪表板**：内置监控和管理 UI
- **🌐 分布式优先设计**：原生支持分布式系统和微服务
- **🔒 强类型**：充分利用 C# 类型系统实现编译时安全

## 📦 可用模块

Monica 提供 30+ 个按类别组织的模块。标有 ⭐ 的模块是常用的核心模块。

### 核心基础设施
- **Core** ⭐ - 基础类型、工具和基础设施
- **Tool** ⭐ - 常用工具和辅助函数
- **DependencyInjection** - 增强的依赖注入功能

### 领域驱动设计
- **DomainDrivenDesign** - DDD 模式实现和基类
- **AutoController** ⭐ - 从服务自动生成 API 控制器
- **AutoModel** - 自动模型映射和转换

### 数据访问
- **Repository** ⭐ - 仓储模式实现，集成 EF Core
- **StateStore** - 状态管理和持久化

### 后台处理
- **JobScheduler** ⭐ - 后台作业调度和执行，支持定时和触发作业

### 配置
- **Configuration** ⭐ - 增强的配置管理，支持验证和热重载

### 通信
- **DataChannel** - 数据流和基于通道的通信
- **EventBus** - 事件驱动架构支持
- **SignalR** - 实时通信扩展
- **Dapr** ⭐ - Dapr 集成，用于分布式应用

### 分布式系统
- **RegisterCentre** - 服务注册和发现
- **Locker** - 分布式锁机制
- **Resilience** - 弹性模式（重试、熔断器等）

### 安全
- **Authority** - 身份验证和授权基础设施

### AI 集成
- **AI** ⭐ - AI 服务集成和抽象

### 监控与可观测性
- **Logging** - 增强的日志功能
- **Profiling** - 性能分析和诊断
- **Framework** - 框架级监控和指标

### UI 组件
- **UI** ⭐ - Blazor UI 组件和工具（基于 MudBlazor）
- **Framework.UI** - 框架 UI 仪表板和管理面板

### 实用工具
- **Office** - Office 文档处理（Excel、Word 等）
- **Validation** - 增强的验证框架

> 📚 有关详细的模块描述、配置选项和使用示例，请参阅[在线文档](https://monica.dpdns.org/)。

## 🚀 快速开始

### 安装

通过 NuGet 安装您需要的模块：

```bash
# 安装核心库
dotnet add package Monica.Core

# 安装仓储模块
dotnet add package Monica.Repository

# 安装作业调度器
dotnet add package Monica.JobScheduler

# 根据需要安装其他模块...
```

### 基本使用

Monica 使用统一的模块化模式来注册和配置服务。所有模块都遵循 `Mo.Add*()` 约定：

```csharp
using Monica;

var builder = WebApplication.CreateBuilder(args);

// 使用统一的 Mo.Add*() 模式注册模块
Mo.AddJobScheduler(o =>
{
    o.RecurringJobDebugMode = true;
    o.TriggeredJobDebugMode = true;
})
.UseEfCoreMetadataRepository();

Mo.AddConfiguration(o =>
{
    o.EnableHotReload = true;
    o.ValidateOnStartup = true;
});

var app = builder.Build();
app.Run();
```

> 💡 模块通常会返回一个 `ModuleGuide` 对象，用于通过流式 API 链进行进一步配置。

## 📚 核心概念

### 模块模式

Monica 的架构围绕 `MoModule` 概念构建。每个模块由四个组件组成：

1. **`Module{Name}Option`** - 模块的配置选项
2. **`Module{Name}Guide`** - 用于额外配置的流式 API 向导
3. **`Module{Name}`** - 核心实现，包含依赖注入和中间件设置
4. **`Module{Name}BuilderExtensions`** - 面向用户的扩展方法（即 `Mo.Add*()` 方法）


### 模块注册

所有模块都遵循一致的注册模式：

```csharp
Mo.Add{ModuleName}(options =>
{
    // 配置模块选项
})
.Use{Feature}()  // 可选：启用特定功能
.With{Provider}(); // 可选：配置提供程序
```

## 🏗️ 架构亮点

- **自动中间件注册**：中间件组件根据依赖关系自动按正确顺序注册
- **智能依赖解析**：框架分析模块依赖关系并提供潜在问题的警告
- **性能优化**：减少反射使用、优化服务注册和高效的资源管理
- **仪表板集成**：许多模块包含用于监控和管理的内置仪表板
- **可扩展性**：遵循相同模式，易于使用自定义模块进行扩展

## 🛠️ 技术栈

- **.NET 10.0** - 最新的 .NET 运行时
- **ASP.NET Core** - Web 框架
- **Entity Framework Core** - 数据访问 ORM
- **MudBlazor** - Blazor UI 组件库
- **Mapster** - 对象映射
- **MediatR** - 中介者模式实现
- **Dapr** - 分布式应用运行时
- **Serilog** - 结构化日志
- **FluentValidation** - 验证框架
- **Polly** - 弹性和瞬态故障处理

## 📖 文档

- **[在线文档](https://monica.dpdns.org/)** - 全面的指南和 API 参考
- **模块 README** - 每个模块在其目录中都包含详细文档
- **Claude Code 技能** - 使用 `/mo-development` 和 `/mo-ui-development` 技能获取开发指导

## ⚠️ 开发状态

**重要**：Monica 目前处于活跃的内部开发阶段：

- 🚧 **快速迭代**：API 可能会在没有通知的情况下发生破坏性变更
- 📝 **文档**：正在积极改进和扩展
- 🔬 **内部使用**：目前专为内部项目设计
- ⚠️ **未准备好生产**：目前不建议在生产环境中使用
- 🔄 **无向后兼容性**：在此阶段不保证向后兼容性

我们建议在 Monica 正式稳定版本发布之前，不要在生产环境中使用。

## 🤝 贡献

我们欢迎贡献！要贡献：

1. Fork 本仓库
2. 创建您的功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交您的更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 开启一个 Pull Request

## 📄 许可证

该项目采用 MIT 许可证 - 详情请查看 [LICENSE](LICENSE) 文件。

## 📞 联系方式

- **GitHub Issues**：[https://github.com/molloryn/Monica/issues](https://github.com/molloryn/Monica/issues)
- **GitHub Discussions**：[https://github.com/molloryn/Monica/discussions](https://github.com/molloryn/Monica/discussions)
- **文档网站**：[https://monica.dpdns.org/](https://monica.dpdns.org/)
