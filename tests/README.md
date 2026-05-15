# Monica 单元测试体系

本目录承载 Monica 当前统一的单元测试基础设施、样板工程和执行约定。当前测试体系面向 Monica 框架内部开发，目标是让所有 `Monica.*` 项目都使用同一套目录结构、命名规则、断言习惯和执行命令，而不是各自演化出不同风格。

## 当前范围

- 当前版本覆盖基础单元测试与 Blazor 组件测试。
- 当前版本不包含真实外部依赖集成测试、端到端测试、浏览器自动化测试。
- 当前样板工程为 `Monica.JobScheduler` 与 `Monica.JobScheduler.UI`。

## 目录与命名规则

- 共享测试基建项目固定为仓库根目录 `Monica.UnitTests/`，项目名固定为 `Monica.UnitTests`。
- 可运行测试项目固定使用 `Test.Monica.*` 前缀，并与源项目一一对应：
  - `Monica.UI -> tests/Test.Monica.UI`
  - `Monica.JobScheduler -> tests/Test.Monica.JobScheduler`
  - `Monica.JobScheduler.UI -> tests/Test.Monica.JobScheduler.UI`
- 测试项目内部目录默认镜像源项目结构，例如源项目的 `Modules/`、`Facades/`、`Services/Support/`、`Pages/`、`Components/` 在测试项目中使用相同相对路径。
- 测试类命名固定为 `类名Tests`。
- 测试方法命名固定为 `Method_WhenCondition_ShouldExpectation`。

## 技术选型

- `xUnit v3`
  - 当前统一测试框架。
  - 与 `dotnet test` 和 IDE 兼容的 VSTest 路径一起使用。
- `AwesomeAssertions`
  - 作为 Fluent 风格断言库。
  - 选用它而不是 FluentAssertions v8+，是为了避免后续商业许可摩擦。
- `NSubstitute`
  - 作为默认 mock / substitute 框架。
  - 搭配 `NSubstitute.Analyzers.CSharp` 约束常见误用。
- `bUnit`
  - 作为 Blazor 组件测试框架。
  - 仅在 UI 测试项目中引用。
- `coverlet.collector`
  - 统一覆盖率收集器。

## 共享测试层 `Monica.UnitTests`

`Monica.UnitTests` 只放 Monica 测试基础设施，不放实际测试用例。当前主要职责如下：

- `Res` / `Res<T>` / `ResPaged<T>` 断言辅助。
- 模块注册测试的静态状态隔离与 reset helper。
- 通用本地化替身 `EchoStringLocalizer<T>`。
- UI 颜色与主题替身 `TestThemeState`。
- Application Service fast-path fixture。
- Sociable Application Testing host fixture 与默认 seam 替换。
- DbContext / repository 测试 fixture。

如果后续出现新的跨项目测试基础设施，应优先放到 `Monica.UnitTests`，而不是复制到各个测试项目。

## Monica 特有测试规则

### 1. 优先测 public surface

- 优先测试 `Modules/`、`Facades/`、公开模型、公开抽象。
- 只有当 public surface 无法有效覆盖关键行为时，才考虑 `InternalsVisibleTo`。
- `InternalsVisibleTo` 只允许一对一开放给对应测试项目，不允许全局 blanket 暴露。

### 2. `Res<T>` 必须显式断言

- 所有 facade 测试都必须断言 `Status`、`Message`、`Data`。
- 返回 `Res<string>` 的成功路径必须显式断言 `Data`，不能只断言 `Message`。
- 这是为了规避 `Res.Ok(string hint)` 非泛型重载与 `Res.Ok<T>(data)` 泛型重载的字符串重载陷阱。

### 3. 模块测试不是只看静态表

- 至少要覆盖 `Guide` / `Option` 配置行为、依赖声明或服务注册行为中的一种。
- 如果模块有明显的配置方法，例如 `UseInMemoryProvider()`、`UseSchedulerScope()`，要把这些方法作为优先测试点。

### 4. UI 测试只做组件级和页面壳层级

- UI 组件测试统一走 bUnit。
- UI 页面优先测试加载态、错误态、子组件组合和交互回调。
- 不在单元测试里引入真实浏览器和真实后端服务。

### 5. 默认禁止真实外部依赖

- 默认禁止真实网络、真实持久化和依赖 `Task.Delay` 的等待。
- 优先使用现有 `Fake`、`Memory`、`InProcess` provider。
- 对时间、随机数、ID 生成敏感的逻辑，优先通过 seam、固定输入或替身隔离。

## WSL 执行规则

本仓库运行在 WSL 下，`dotnet` 是通过 WSL 互操作调用的 Windows 可执行程序。执行 `dotnet build` 或 `dotnet test` 时必须使用 Windows 路径。

推荐命令：

```bash
dotnet test 'D:\Repositories\WorkTree1\MoLibrary\Monica.slnx' --results-directory 'D:\Repositories\WorkTree1\MoLibrary\tests\TestResults'
```

覆盖率命令：

```bash
dotnet test 'D:\Repositories\WorkTree1\MoLibrary\Monica.slnx' --collect:"XPlat Code Coverage" --results-directory 'D:\Repositories\WorkTree1\MoLibrary\tests\TestResults'
```

不要并行启动多个独立的 `dotnet build` / `dotnet test` 进程。需要并行时只使用单个 MSBuild 进程内部的并行能力。

## 新增测试项目时的步骤

1. 在 `tests/` 下创建 `Test.Monica.{ProjectName}`。
2. 引用 `Monica.UnitTests` 和对应的 `Monica.{ProjectName}` 源项目。
3. 按源项目目录结构建立测试目录。
4. 只在 UI 项目中额外引用 `bunit`。
5. 优先补三类测试：
   - `Modules/` 或 `Facades/` 的入口测试
   - 一个稳定的纯逻辑 / support / provider 测试
   - UI 项目再加一个组件或页面壳测试

## 当前样板说明

- `Test.Monica.UI`
  - 展示 Monica UI 基础服务的测试写法。
- `Test.Monica.JobScheduler`
  - 展示模块 guide、内存仓储、facade 聚合入口与 service 验证类的测试写法。
- `Test.Monica.JobScheduler.UI`
  - 展示 UI 模块服务注册、bUnit 组件测试、页面错误态测试与 UI support 类测试写法。
