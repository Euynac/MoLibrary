# 目标

- 开发模板化
- 可管理及配置适配器，查看状态信息
- 易扩展不同的通信能力
- 易扩展不同的数据转换能力
- 可对数据处理流程进行中间件式的配置
- 可对通信能力进行附加功能扩展：如解决粘包、重试、定时触发、主备检测等
# 支持场景

- 连接TCP端口，可配置粘包解决方法，将数据包融合后统一处理。
		通过TCP通信能力核心支持粘包功能的Metadata配置。或通过Endpoint中间件形式支持粘包解决。
- 从指定HTTP接口，定时或通过接口触发获取信息，写入数据库表
		新增"Endpoint"的中间件类型，相对于消息Transform的中间件。可访问到Input Endpoint和Output Endpoint。
		Endpoint中间件可以有HTTP接口触发、定时器触发中间件，不过新增这种可能就会使得Pipe通信属性多一个方向（因为可以传递主动拉取的事件消息了）
- 从指定文件类型读取信息
		瞬时生命周期Channel，用完即断开连接Dispose，下次需要重新Init。
		相对的是常规的LongRunning生命周期，它是单例的，默认仅一次Init。

# 规划

#### 需求

需要与不同系统交换不同信息，每个信息之间格式定义不一致，而且通信方式也不一样：比如连接方式有TCP、ActiveMQ、UDP、串口、WebSocket等等，要对各个系统开发接口程序，难以统一管理。

以适配器模式作为接口接入模式，开发适配器微服务。适配器将通信能力、数据协议转换解耦，保持系统纯净。适配器开发需要模板化，这里以数据管道（Data Pipeline）模式为参考设计开发出轻量的ETL框架

#### 设计

- 以消息通路（DataChannel）为单位，构建与目标系统/资源的消息传递连接。
- 消息通路以双向管道模式实现。
- 消息通路消息传递以上下文模式传递，自由新增管道中间件，如可配置数据协议转换中间件，将目标系统的格式转换为指定格式。
- 消息通路可以统一管理，查看状态等各种信息。
- Pipe+Middleware+Endpoint Build后成为Pipeline，是未激活状态，init后变为DataChannel。

#### 双向管道模式

管道有两个入口，即两个管道Endpoint，也就会有两个消息传递方向。但是并不一定该管道就支持双向通信，还是要根据管道Endpoint支持的收发方式确定管道通信方向属性。

管道拥有仅Inner to Outer（Output）方向以及仅Outer to Inner（Input）方向，以及双向通信（Output and Input）属性。
Input方向一般代表从 对端系统 到 本系统 的方向，即消息首先来源于对端系统，经过通信核心，进入管道。
Output方向一般代表从 本系统 到 对端系统 的方向，即消息首先来源于本系统，进入管道，再经过通信核心，进入对端系统。

管道Endpoint实现IPipeEndpoint，有收与发两种接口。

配置管道Endpoint后，可自动根据管道Endpoint支持的收发方式，计算出管道支持的通信方向属性。

管道可以拥有中间件，消息通过DataContext进行传输。中间件应有Input和Output两种方向的Context处理逻辑。

#### 管道终端

DataContext头：
```json
{
  "operation": "get", //必须
  "metadata": {
    "path": "/things/1234"  //根据Endpoint支持不同而不同
  },
  "data": ""
}
```

#### 通信核心
ICommunicationCore
通信能力抽象为MQ、数据库、TCP、UDP、Serial、WebSocket、File等。将通信所需参数抽象为通信核心Metadata（CommunicationMetadata）
仅需配置通信核心Metadata，即可获得相应的通信能力，直接从目标建立连接，获取信息。

通信核心实现了管道Endpoint接口（IPipeEndpoint），可作为管道Endpoint消息入口。


#### 配置过程

1. 配置通信核心metadata。
2. 将通信核心metadata传入通信核心工厂，生成通信核心实例
3. Builder模式构造Pipe，传入通信核心实例
4. 配置中间件添加。
5. 注册管道。

注册后在AppBuild后阶段才开始解析中间件，实例化管道。这时候才可以运用依赖注入的功能。
将面向接口的核心及中间件所依赖项解析。

IDataChannelManager获取指定管道名消息通路实例。


## Dashboard

### 看板接口
- 适配器列表
- 适配器状态

### 适配器接口
- 获取当前适配器基础信息：数据协议核心、通信能力核心、目标系统
- 可供热配置的配置项修改
- 错误日志：重连等
- 测试目标系统连接

## 通信能力核心

#### HTTP

#### TCP

#### UDP

#### MQ
使用Dapr Binding实现。

#### WebSocket


## 数据转换核心

