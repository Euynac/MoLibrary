# MoLibrary

<p align="center">
  <img src="logo.svg" alt="MoLibrary Logo" width="200" />
</p>

<p align="center">
  <a href="https://github.com/Euynac/MoLibrary.Docs/actions"><img src="https://github.com/Euynac/MoLibrary.Docs/actions/workflows/static.yml/badge.svg" alt="Build Status"></a>
  <a href="https://www.nuget.org/packages?q=MoLibrary"><img src="https://img.shields.io/nuget/v/MoLibrary.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/Euynac/MoLibrary/blob/main/LICENSE"><img src="https://img.shields.io/github/license/Euynac/MoLibrary" alt="License"></a>
  <a href="https://molibrary.dpdns.org/"><img src="https://img.shields.io/badge/docs-online-brightgreen.svg" alt="Documentation"></a>
</p>

## 📖 概述

MoLibrary 是一个模块化的 .NET 基础设施库，允许您单独使用某个模块而无需引入整个框架。通过统一的注册和配置模式，MoLibrary 让您的开发体验更加一致和高效。

**[📚 在线文档](https://molibrary.dpdns.org/) • [🚀 快速开始](https://molibrary.dpdns.org/docs/intro) • [📝 博客](https://molibrary.dpdns.org/blog)**

## ✨ 特性

- **🧩 模块化设计**：每个组件都是独立的，您可以只使用所需的模块，无需引入整个框架
- **🔄 统一直觉的 API**：所有模块都遵循相同的注册和配置模式，上手简易
- **⚡ 高性能实现**：
  - 自动中间件注册，无需手动注册中间件
  - 防止重复注册，模块自动仅注册一次
  - 高性能服务注册，减少反射开销
  - 及时释放临时对象，减少内存占用
- **🔌 自动解决中间件顺序**：无需手动管理中间件的注册顺序
- **🔍 可视化依赖关系**：及时提醒可能的注册失败、误操作等
- **🔒 .NET 原生体验**：充分利用 C# 类型系统，提供强类型支持

## 📦 可用模块

MoLibrary 目前提供40+模块，以下是部分模块（待补充）

- **Core**：核心功能和基础设施
- **DomainDrivenDesign**：DDD 模式实现
- **Repository**：仓储模式实现
- **DependencyInjection**：增强的依赖注入功能
- **BackgroundJob**：后台任务处理
- **SignalR**：实时通信扩展
- **AutoModel**：自动模型映射和转换
- **Configuration**：配置管理
- **DataChannel**：数据通道
- **Tool**：常用工具和辅助功能

## 🚀 快速开始

### 安装

选择您需要的模块进行安装：

```bash
# 安装核心库
dotnet add package MoLibrary.Core

# 安装仓储模块
dotnet add package MoLibrary.Repository

# 安装依赖注入模块
dotnet add package MoLibrary.DependencyInjection

# 其他模块...
```

### 基本使用

MoLibrary 使用模块化的方式来注册和配置服务：

```csharp
builder.ConfigModuleConfigurationDashboard().AddMoConfigurationDashboardClient<DaprHttpForConnectServer, ProjectServiceInfo>(s =>
{
    s.ClientRetryTimes = 3;
    s.HeartbeatDuration = 10000;
    s.RetryDuration = 6000;
});
```

> 模块通常会返回一个 `ModuleGuide` 对象，用于进一步配置。



## 📚 核心概念

MoLibrary 的核心概念是 `MoModule`，作为库的核心注册机制，每个Library有一个或多个`Module`，每个`Module`组成如下：

1. `Module{ModuleName}Option`: 模块Option的设置
2. `Module{ModuleName}Guide`: 模块配置的向导类
3. `Module{ModuleName}`: 含有依赖注入的方式以及配置ASP.NET Core中间件等具体实现
4. `Module{ModuleName}BuilderExtensions`: 面向用户的扩展方法


## 🤝 贡献

我们欢迎任何形式的贡献！如果您想参与贡献，请：

1. Fork 本仓库
2. 创建您的功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交您的更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 开启一个 Pull Request

## 📄 许可证

该项目采用 MIT 许可证 - 详情请查看 [LICENSE](LICENSE) 文件。

## 📞 联系方式

- GitHub Issues: [https://github.com/Euynac/MoLibrary/issues](https://github.com/Euynac/MoLibrary/issues)
- GitHub Discussions: [https://github.com/Euynac/MoLibrary/discussions](https://github.com/Euynac/MoLibrary/discussions)
- 文档网站: [https://molibrary.dpdns.org/](https://molibrary.dpdns.org/)
