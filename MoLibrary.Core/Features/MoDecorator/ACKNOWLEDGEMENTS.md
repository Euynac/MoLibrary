# MoDecorator - 鸣谢

## 致谢

MoDecorator 功能的实现基于优秀的开源项目 [Scrutor](https://github.com/khellang/Scrutor)。我们在此向 Scrutor 项目的所有贡献者表示衷心的感谢。

## 关于 Scrutor

**Scrutor** 是一个为 Microsoft.Extensions.DependencyInjection 提供程序集扫描和装饰扩展的开源库。

- **项目主页**: https://github.com/khellang/Scrutor
- **作者**: Kristian Hellang ([@khellang](https://github.com/khellang))
- **许可证**: MIT License

### Scrutor 的核心功能

- 程序集扫描和服务自动注册
- 服务装饰器模式支持
- 泛型类型装饰
- 条件注册和过滤

## MoDecorator 的扩展

基于 Scrutor 的优秀设计，MoDecorator 在原有功能基础上增加了：

### InterfaceProxyDecorationStrategy

新增的 `InterfaceProxyDecorationStrategy` 策略允许：

- 装饰所有实现指定接口的服务
- 支持泛型接口的代理装饰
- 批量应用横切关注点（如日志、缓存、授权等）

### 使用示例

```csharp
// 装饰所有实现 IRepository 接口的服务
services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();

// 使用委托装饰
services.DecorateInterfaceProxy<IService>(service => 
    new LoggingDecorator<IService>(service));

// 装饰泛型接口
services.DecorateInterfaceProxy(typeof(IHandler<>), typeof(LoggingHandler<>));
```

## 开源精神

我们深信开源协作的力量。通过基于 Scrutor 的优秀设计进行扩展，我们希望：

1. **传承优秀设计** - 保持 Scrutor 简洁优雅的 API 设计
2. **扩展功能边界** - 为特定场景提供更强大的装饰能力
3. **回馈社区** - 将改进和扩展贡献给更广泛的开发者社区

## 贡献者

感谢以下 Scrutor 项目的主要贡献者：

- **Kristian Hellang** - 项目创始人和主要维护者
- **Thorben Wenzel** - 重要贡献者
- **Dan Hartley** - 核心功能开发
- 以及所有其他贡献者

## 许可声明

MoDecorator 继承了 Scrutor 的 MIT 许可证精神，致力于为 .NET 生态系统提供更好的依赖注入装饰解决方案。

---

**MoLibrary Team**  
如有任何问题或建议，请通过 GitHub Issues 与我们联系。 