# Monica

<p align="center">
  <img src="logo.png" alt="Monica Logo" width="200" />
</p>

<p align="center">
  <a href="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml"><img src="https://github.com/Tairitsua/Monica/actions/workflows/unit-tests.yml/badge.svg" alt="Unit Tests"></a>
  <a href="https://www.nuget.org/packages?q=Monica"><img src="https://img.shields.io/nuget/v/Monica.Core.svg" alt="NuGet"></a>
  <a href="https://github.com/Tairitsua/Monica/blob/main/LICENSE.txt"><img src="https://img.shields.io/github/license/Tairitsua/Monica" alt="License"></a>
  <a href="https://monica.dpdns.org/"><img src="https://img.shields.io/badge/docs-online-brightgreen.svg" alt="Documentation"></a>
  <a href="https://deepwiki.com/Tairitsua/Monica"><img src="https://deepwiki.com/badge.svg" alt="Ask DeepWiki"></a>
</p>

<p align="center">
  <a href="README.md">English</a> | 简体中文
</p>

<p align="center">
  <strong>AI agent 能遵循的架构，人类能检查的系统。</strong>
</p>

Monica 是面向可观测 .NET 后端的 agent-governed application architecture。它让开发者和编码 agent 共用同一套模块、DDD ProjectUnit、基础设施和运行时诊断语言，使生成的代码仍然结构可预期，运行中的系统仍然可理解。

> **候选版本**：Monica 1.0.0-rc.2 是用于验证和反馈的预发布版本。在 1.0.0 稳定版之前仍可能出现破坏性变更。

## 快速链接

- 文档站点：<https://monica.dpdns.org/>
- Monica.Docs 示例仓库：<https://github.com/Tairitsua/Monica.Docs>
- JobScheduler 指南：<https://monica.dpdns.org/markdown-docs?group=monica&document=modules%2Fjob-scheduler%2Findex.md&culture=zh-CN>
- 更新日志：[CHANGELOG.md](CHANGELOG.md)

## 为什么是 Monica

- AI 可以很快产出代码，但如果没有统一规格，代码会在规模增长后变得脆弱且难以观测。
- Monica 把基础设施本身变成规格：`AddMonica(...)` 先记录一个宿主的完整模块图，验证依赖与循环，再按确定性阶段应用注册。
- 每个组合都属于具体宿主；同一进程中的多个宿主不共享模块注册表、选项或 ProjectUnit 目录。
- 仓库里的 `.claude/skills/` 和 `.agents/skills/` 会在 AI 写代码之前先教它 Monica 的写法。

## 演示视频

这段一分钟演示覆盖 Monica 面向运维的几个核心界面：

- JobScheduler 健康仪表板、最近活动、作业定义和手动执行。
- Cron 表达式编辑，无需为后台作业手写管理页。
- ModuleSystem 对运行中宿主的性能视图和依赖视图。
- 运行时配置查看和主题切换。


https://github.com/user-attachments/assets/250e1e5f-0a78-4b8b-b832-756d682a01bd

## JobScheduler 代码示例

```csharp
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.AppName = "Orders";
        options.AppId = "orders";
    });

    monica.AddJobScheduler()
        .UseInMemoryMetadataRepository()
        .UseSchedulerScope("local-dev")
        .UseInMemoryProvider();

    monica.AddJobSchedulerUI();
});

var app = builder.Build();
app.UseMonica();
app.MapMonica();
app.Run();

[JobConfig(
    JobName = "Heartbeat",
    Description = "每五分钟写一次心跳日志。",
    CronSchedule = "0 */5 * * * *")]
public sealed class HeartbeatJob(ILogger<HeartbeatJob> logger) : RecurringJob
{
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Heartbeat job ran.");
        return Task.CompletedTask;
    }
}
```

- 定时作业和触发式作业使用同一套调度模型。
- 并发控制、僵尸检测和持久化选项都已内置。
- 需要浏览器运维界面时，在同一个 `AddMonica(...)` 回调中加入 `monica.AddJobSchedulerUI()`。UI 需要 ASP.NET Core Web 宿主来提供 Blazor 路由和静态资源。
- 最小可运行参考见 [`examples/JobSchedulerMinimal`](examples/JobSchedulerMinimal)，它演示了如何用最少 ASP.NET Core 宿主在 `/job-scheduler` 跑起 JobScheduler + JobScheduler UI。

## 宿主级配置

应用身份和模块系统默认值在同一个宿主组合边界中配置：

```csharp
builder.AddMonica(monica =>
{
    monica.ConfigureApplication(options =>
    {
        options.AppName = "My Application";
        options.AppId = "my-application";
    });

    monica.ConfigureModuleSystem(options =>
    {
        options.DefaultApiGroupName = "Core";
    });
});
```

回调返回后模块图即被封闭，保留下来的 Guide 不能再修改组合。

## 仪表板、主题与国际化

Monica.UI 之上还提供多个运维型 Blazor UI：JobScheduler、Configuration、DependencyInjection、ProjectUnits、ModuleSystem、AI 等。UI 层共用同一套主题契约，支持多个可切换主题，并内置 `en-US` + `zh-CN` 本地化资源。

## 随仓库交付的 Agent Skills

仓库已经内置了可直接使用的 skill pack，位于 `.claude/skills/` 和 `.agents/skills/`。

- 框架入口：`monica-framework`、`monica-development`、`monica-architecture`、`monica-ui-development`、`monica-ui-design`、`monica-ui-audit`、`monica-docs-authoring`、`monica-requirement-design`、`monica-unit-testing`、`monica-ui-bridge-debug`
- 基于 Monica 的应用系统入口：`monica-application`、`monica-application-microservice`、`monica-application-modular-monolith`、`monica-application-project-unit-development`
- 辅助工作流：`code-simplifier`、`playwright-cli`、`supervise-subagents`、`third-party-source-catalog`

## 包成熟度

| 层级 | 承诺 | 代表能力 |
|---|---|---|
| **Stable** | 1.0 支持的应用主路径 | Core、ProjectUnits、WebApi、Configuration、Repository、JobScheduler、OpenTelemetry、UI |
| **Integrations** | 围绕外部系统的版本化适配器 | EF Core、Kafka、Redis/StackExchange、Dapr、SignalR |
| **Labs** | 保持快速演进的实验能力 | AI/RAG/MCP、DataChannel、DevOps/Profiling、Office、Experimental |

Stable 不依赖 Labs，Integration 也始终是按需引入的 provider-specific 包；CI 会同时验证包分类完整性和 maturity tier 的单向依赖规则。

## 架构速览

### 模块模式

- `Module{Name}Option`：公开配置入口
- `Module{Name}Guide`：链式补充配置
- `Module{Name}`：模块实现本体
- `Module{Name}BuilderExtensions`：`monica.Add{Name}()` 入口

### ProjectUnit 模式

用于约束 AI 能写什么的类型化 DDD 单元：`ApplicationService`、`RequestDto`、`DomainService`、`Entity`、`Repository`、`DomainEvent`、`DomainEventHandler`、`LocalEventHandler`、`Configuration`、`RecurringJob`、`TriggeredJob`。

ProjectUnit 的详细约定可以在 `monica-application-project-unit-development` 和 Monica.Docs 的概念页里继续查看。

## 技术栈

- [.NET 10](https://github.com/dotnet/runtime)
- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [MudBlazor](https://github.com/MudBlazor/MudBlazor)
- [Mapster](https://github.com/MapsterMapper/Mapster)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Dapr](https://github.com/dapr/dapr)
- [Serilog](https://github.com/serilog/serilog)
- [FluentValidation](https://github.com/FluentValidation/FluentValidation)
- [Polly](https://github.com/App-vNext/Polly)

## 贡献

1. Fork 仓库。
2. 创建分支。
3. 提交修改。
4. 提交 Pull Request。

完整贡献和发布说明见 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 鸣谢

Monica 的一小部分模块实现思路参考了 [ABP Framework](https://github.com/abpframework/abp)，该项目采用 LGPL-3.0 许可证。任何直接改编的代码都会保留其原始声明和许可证条款。Monica 是独立项目，与 ABP 没有关联关系。

## 许可证

MIT License。见 [LICENSE.txt](LICENSE.txt)。

## 联系方式

- Issues：<https://github.com/Tairitsua/Monica/issues>
- Discussions：<https://github.com/Tairitsua/Monica/discussions>
- 文档站点：<https://monica.dpdns.org/>
- 安全：[SECURITY.md](SECURITY.md)
