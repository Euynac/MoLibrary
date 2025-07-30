# DataChannel 使用文档

## MessageDebuggerMiddleware 使用示例

MessageDebuggerMiddleware 是一个用于调试消息传输的中间件，可以监听和记录通过管道传输的消息内容。

### 基本使用

```csharp
// 创建消息调试器中间件
var debuggerMiddleware = new MessageDebuggerMiddleware();
debuggerMiddleware.Initialize();

// 添加到管道
dataPipeline.AddMiddleware(debuggerMiddleware);
```

### 自定义消息格式化

如果需要自定义消息格式化逻辑，可以继承 MessageDebuggerMiddleware 并重写 FormatMessage 方法：

```csharp
public class CustomMessageDebugger : MessageDebuggerMiddleware
{
    protected override string FormatMessage(object? data)
    {
        // 自定义格式化逻辑
        if (data is MyCustomType customData)
        {
            return $"CustomType: {customData.Id} - {customData.Name}";
        }
        
        return base.FormatMessage(data);
    }
}
```

### 自定义消息匹配逻辑

如果需要自定义消息匹配逻辑，可以重写 ShouldCapture 方法：

```csharp
public class RegexMessageDebugger : MessageDebuggerMiddleware
{
    protected override bool ShouldCapture(string formattedContent)
    {
        if (string.IsNullOrWhiteSpace(FilterKeyword))
        {
            return true;
        }

        // 使用正则表达式匹配
        return Regex.IsMatch(formattedContent, FilterKeyword, RegexOptions.IgnoreCase);
    }
}
```

### 在 UI 界面中使用

MessageDebuggerMiddleware 提供了内置的 UI 界面，用户可以在 DataChannel 管理界面中点击调试图标打开调试器：

1. 在管道中间件列表中，找到 MessageDebuggerMiddleware
2. 点击右侧的调试图标（虫子图标）
3. 在弹出的对话框中：
   - 输入要过滤的关键字（可选）
   - 设置队列长度（默认100）
   - 点击"开始监听"按钮
4. 监听期间，所有匹配的消息都会显示在列表中
5. 可以点击消息查看详细内容和元数据
6. 点击"停止监听"停止捕获消息
7. 关闭对话框后，临时队列会被清空

### API 参考

#### 属性

- `IsActive`: 获取或设置是否激活调试
- `FilterKeyword`: 获取或设置过滤关键字
- `MaxQueueSize`: 获取或设置队列最大长度（默认100）

#### 方法

- `Initialize()`: 初始化中间件
- `GetDebugMessages()`: 获取调试消息列表
- `ClearDebugMessages()`: 清空调试消息

#### 可重写方法

- `FormatMessage(object? data)`: 格式化消息内容
- `ShouldCapture(string formattedContent)`: 判断是否应该捕获消息

### 注意事项

1. MessageDebuggerMiddleware 仅在激活状态下才会处理消息，默认未激活
2. 消息队列有最大长度限制，超过限制会自动删除最旧的消息
3. 关闭 UI 界面后会自动停止监听并清空临时队列
4. 大量消息可能会影响性能，建议仅在调试时使用