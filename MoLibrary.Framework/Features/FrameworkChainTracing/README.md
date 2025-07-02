# ChainTracking Provider 使用文档

本文档介绍基于新的 MoChainTracing 系统的两个链路追踪 Provider。

## 概述

基于全新的 ChainTracking 模块，我们重新实现了两个链路追踪Provider，用于替换原有的旧版本：

1. **ChainTrackingProviderInvocationInterceptor** - 替换 `InvocationChainRecorderMoInterceptor`
2. **ChainTrackingProviderRepositoryEfCoreInterceptor** - 替换 `RepositoryChainEfCoreRecorderInterceptor`

## 主要改进

### ChainTrackingProviderInvocationInterceptor

#### 新功能和改进：
- **优化的调用判断逻辑**：不再使用命名判断，直接检查返回类型是否实现 `IServiceResponse` 接口
- **更好的异常处理**：完整的异常处理流程，自动创建错误响应
- **详细的调用信息记录**：记录方法名、参数信息等详细信息
- **自动响应链路附加**：自动为响应对象附加调用链信息
- **使用 ChainTracingScope**：简化了代码逻辑，自动管理调用链生命周期

#### 配置示例：

```csharp
// 在 DI 容器中注册
services.AddScoped<ChainTrackingProviderInvocationInterceptor>();

// 配置拦截器（具体方式取决于你的拦截器配置系统）
services.AddInterceptor<ChainTrackingProviderInvocationInterceptor>();
```

### ChainTrackingProviderRepositoryEfCoreInterceptor

#### 新功能和改进：
- **智能 SQL 解析**：自动识别 SQL 命令类型（SELECT, INSERT, UPDATE, DELETE 等）
- **表名提取**：从 SQL 语句中智能提取表名信息
- **详细的执行信息记录**：记录执行时间、影响行数、参数信息等
- **完善的异常处理**：记录数据库异常和取消操作
- **线程安全的 TraceId 映射**：使用 `ConcurrentDictionary` 管理命令和调用链的映射关系

#### 配置示例：

```csharp
// 在 DbContext 配置中添加拦截器
services.AddDbContext<YourDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(serviceProvider.GetRequiredService<ChainTrackingProviderRepositoryEfCoreInterceptor>());
});

// 在 DI 容器中注册
services.AddScoped<ChainTrackingProviderRepositoryEfCoreInterceptor>();
```

## 使用前提

确保已经正确配置了 ChainTracking 模块：

```csharp
// 在 Program.cs 或 Startup.cs 中
builder.ConfigModuleChainTracing(options =>
{
    options.Enabled = true;
    options.UseMiddleware = true;
    options.EnableActionFilter = false; // 根据需要启用
    options.MaxChainDepth = 50;
    options.MaxNodeCount = 1000;
});
```

## 调用链信息结构

新的 Provider 会在调用链中记录以下信息：

### 方法调用链信息：
- **Handler**: 服务类名
- **Operation**: 方法名或请求类型名
- **ExtraInfo**: 
  - `MethodName`: 实际方法名
  - `ParameterCount`: 参数个数
  - `Arguments`: 前3个参数的概要信息

### 数据库调用链信息：
- **Handler**: "Database"
- **Operation**: "SELECT(TableName)" 或 "INSERT(TableName)" 等
- **ExtraInfo**:
  - `CommandTimeout`: 命令超时时间
  - `ParameterCount`: 参数个数
  - `CommandText`: SQL语句（截断后）
  - `Parameters`: 前10个参数的详细信息
  - `Duration`: 执行时间（毫秒）
  - `Status`: 执行状态
  - `Result`: 执行结果信息

## 性能考虑

- 新的 Provider 使用了更高效的数据结构和算法
- 减少了不必要的字符串操作和反射调用
- 使用线程安全的集合类型避免锁竞争
- 智能的信息截断避免内存泄漏

## 迁移指南

### 从旧版本迁移：

1. **移除旧的拦截器配置**
2. **添加新的 Provider 配置**
3. **确保 ChainTracking 模块已正确配置**
4. **测试调用链信息的正确性**

### 注意事项：

- 新版本的调用链信息结构可能与旧版本不同，需要更新相关的日志解析逻辑
- 确保所有需要记录调用链的服务方法返回类型都实现了 `IServiceResponse` 接口
- 数据库拦截器现在会自动记录所有数据库操作，如果不需要可以通过配置禁用

## 故障排查

### 常见问题：

1. **调用链信息未记录**
   - 检查 ChainTracking 模块是否正确配置
   - 确认方法返回类型是否实现 `IServiceResponse`
   - 检查 DI 容器中是否正确注册了 Provider

2. **性能问题**
   - 检查 `MaxChainDepth` 和 `MaxNodeCount` 配置
   - 考虑禁用不必要的调用链记录

3. **内存泄漏**
   - 确保 `_commandTraceMap` 中的条目被正确清理
   - 检查是否有异常情况导致调用链未正常结束

## API 参考

详细的 API 文档请参考：
- `IMoChainTracing` 接口文档
- `ChainTracingExtensions` 扩展方法文档
- `MoChainContext` 和 `MoChainNode` 数据模型文档 