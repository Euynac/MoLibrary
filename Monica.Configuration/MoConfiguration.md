
这是独立自主研发的配置注册方式。集成`dapr`热配置，集成`ASP.NET Core Configuration `以及 应用`Option Pattern`，用法与原生一致。



## 1. 定义配置
当前额外定义了两种标签`[Configuration]`和`[OptionSetting]`
```cs
[Configuration]
public class AppSettings
{
	[OptionSetting(Title = "程序名")]
    [MaxLength(10), DefaultValue("Unknown")]
    public string AppName { get; set; }
    [ConfigurationKeyName("AppVersion")]
    public int Version { get; set; }
    [MaxLength(5), RegularExpression("[a-zA-Z]+")]
    public string HotConfigTest { get; set; }

}
```

标签的作用是用于自动注册配置类到程序，无需手动配置。


## 2. 获取配置

一般用法是与ASP.NET Core的Option Pattern原生用法一致。

Option Pattern是框架已经注册了如下三种形式的配置接口：
1. **IOption\<T\>** 是只读取一次的。
2. **IOptionsSnapshot\<T\>** 类似于服务的Scope，下一次请求会重新读取配置。（无法注册到单例生命周期服务）
3. **IOptionsMonitor\<T\>** 则会即时读取配置。且支持配置更改通知（注册它的`OnChange`事件实现。

> 注意，热配置是Snapshot及Monitor，需要相关的配置源Provider支持TraceTokenChange才可以，比如自带的文件类）


另外也可以通过自行实现的全局配置管理器 `HotConfiguration` 获取配置。

### 支持热配置的配置数据源

| 数据源  | 是否支持热配置 | 备注  |
| :--- | :------ | :-- |
| 环境变量 | 不支持     |     |
| 文件   | 支持      |     |
|      |         |     |
|      |         |     |
|      |         |     |

## 热配置设置

### 配置服务

`ConfigurationAssemblyLocation` 设置除了项目`Entry`程序集外额外扫描的程序集。

```cs
 builder.Services.AddMoConfiguration(configuration, setting =>
 {
     setting.Logger = GlobalLog.Logger;
     setting.ErrorOnNoTagConfigAttribute = true;
     setting.EnableLoggingWithoutOptionSetting = true;
     setting.ErrorOnNoTagOptionAttribute = true;
     //setting.EnableConfigRegisterLogging = true;
     setting.ConfigurationAssemblyLocation = [nameof(ProtocolPlatform), "Service.Domain", "DataExchange"];
     setting.GenerateFileForEachOption = true;
     setting.GenerateOptionFileParentDirectory = configDirectory;
     setting.SetOtherSourceAction = manager =>
     {
         manager.AddJsonFile(
             GeneralExtensions.GetRelativePathInRunningPath($"{configDirectory}/global-appsettings.json"), false,
             true);
         manager.AddJsonFile(
             GeneralExtensions.GetRelativePathInRunningPath("appsettings.json"), true,
             true);
     };
 });

```



日志
[Dynamically replace the contents of a C# method? - Stack Overflow](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method)
### 


### 原生选项标签
- **ConfigurationKeyName**： 使用`[ConfigurationKeyName("xxx")]`标签来设定别名。如若Configuration类未设置Section，而又需要针对单个选型设置Secition，可以使用如下格式路由名：`ConfigSectionName:OptionName`，多层可以用多个Section。
- **MaxLength**：【验证】限制最大长度
- **RegularExpression**：【验证】值必须满足此正则表达式

## 配置源

默认情况下，ASP.NET Core web apps 自动读取下列配置（优先级由高到低）：
1. Command-line arguments
2. Non-prefixed environment variables
3. User secrets when the app runs in the Development environment
4. `appsettings.{Environment}.json`
5. `appsettings.json`
6. Host configuration sources, 这些是框架预定义的有前缀的环境变量，详见官网Configuration章节。

实际上优先级就是读取的顺序，后面的读取重复的会覆盖前面的配置。