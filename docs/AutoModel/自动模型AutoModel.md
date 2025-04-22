# 目标
1. 面向接口，支持依赖注入
2. 开箱即用
3. 无需耦合实体代码


## 用法

目前`MoCrudAppService`中有使用到该技术，对于查询请求提供`Filter`，`Select`，`Fuzzy`等字段。

提供

## 语法

`<FieldName1> <Condition1> "<Value1>" [or|and|&&|||] <FieldName2> <Condition2> "<Value2>"`
- FieldName: **字段名**（默认是实体属性名）
- Condition：**条件关键字**，目前包含`in`, `like`, `=`, `>`, `<`, `>=`, `<=`, `!=`, `explike`
- Value: **字段比较值**。以`"`双引号包裹。内部语法根据条件关键字、字段类型变化而变化。

### 字段名

目前可用字段名即实体属性名

### 条件关键字

以下是有特殊功能的条件关键字

#### like
1. 字段类型为字符串时
- `test`代表`%test%`，查询包含`test`的
- `test%`代表以`test`开头的，即与数据库模糊相同（当含有`%`原样输出）

#### explike
与：`&`
或：`|`
非：`!`, `！`
模糊：`%`
括号：`(`，`)`，`（`, `）`
1. 字段类型为字符串时
- `ABC` 字符串包含 `ABC`
- `!ABC` 字符串不包含 `ABC`
- `ABC & CD` 字符串包含 `ABC` 且包含 `CD`
- `ABC | CD` 字符串包含 `ABC` 或包含 `CD`
- `ABC%` 字符串以`ABC`开头
- `A%BC` 字符串以`A`开头`BC`结尾
- 高阶：
	- `(AB%C | B C) & (!CE% & !CD)`  字符串相似`AB%C`或包含`B C`，而且字符串不以`CE`开头且不包含`CD`

#### in
1. 字段类型为字符串时
- `佛山,深圳` 字符串等于佛山或等于深圳
2. 字段类型为时间时（暂未实现）
- `EOBT in "[now, now+30min]"`


### 字段比较值

内部会将传入的字符串比较值转为实体字段相关类型进行比较。
目前支持的类型：`double`,`int`,`long`,`Enum`,`string`,`boolean`,`DateTime`,`DateOnly`,`TimeOnly`,`Guid`,`TimeSpan`


#### DateTime，DateOnly类型
支持格式为`yyyy-MM-dd`, `yyyyMMdd`, `MMdd`, `yyyy-MM-dd HHmmss`, `yyMMdd`
以及时间表达式：`Now`, `Now+30min`

##### 时间表达式
当前时间：`now`
增加某时间长度后的时间：`now + 60min`
减去某时间长度后的时间：`now - 3.6h`, `yyyy-MM-dd + 60min`
支持连续加减
- 仅支持`+`或`-`

#### TimeSpan类型
支持格式为`HH:mm:ss`, `HHmm`
以及特殊时间长度：
- 分钟(minutes)：`1min`
- 小时(hour)：`3.6h`
- 秒(second)：`114s`
- 天数(day)：`1d`


# 开发使用

### 结合仓储层


```cs
 protected IAutoModelDbOperator<PlanNoticeMsg> _autoDb =>
     LazyServiceProvider.LazyGetService<IAutoModelDbOperator<PlanNoticeMsg>>()!;

 protected IRepositoryPlanNoticeMsg _msg;

 var query = await _msg.GetQueryableAsync(true);
 _autoDb.ApplyFilter(query, $"""
                            {nameof(PlanNoticeMsg.MsgContent)} like "CBD"
                            """);
 query.ToListAsync();
```


### 自定义字段Query组装器（暂未实现）


```cs
CustomFor(nameof(Entity.FieldName), (input, querable))
```

### 前端请求类（暂未实现）

可以以传统方式编写请求类，前端不必拼接语句
```cs


```




# 引擎核心设计

## 引擎语法设计

本引擎本着简单易用的目的，设计了一个简明扼要的`Query DSL`语法。因该`DSL`面向实体，实体中有相关的属性数据，面向实体属性的筛选，表达式语法规则即：

`<实体属性名> <条件关键字> "<属性比较值>"`

例如航班计划实体`FlightPlan`，其中含有`Callsign`的实体属性，那么如果筛选航班计划中`Callsign`包含`3001`字符串的实体，表达式为：

`Callsign like "3001"`

本引擎把以上一个表达式称为一个`Token`。另外，也支持括号及且（`&&`）、或（`||`）的关系表达式，如：

`Token1 && (Token2 || Token3)`

对于条件关键字，目前支持`in`, `like`, `explike`, `>`, `<`, `>=`, `<=`, `=`, `!=`等条件，可由开发者自行扩展。对于属性比较值，语法及效果根据的条件关键字、实体属性的类型变化而变化。

## 引擎的模块划分及逻辑关系

引擎使用面向接口、依赖注入的方式实现，具体实现可供开发者进行替换或改造，主要分为应用接口与内部实现接口。应用接口面向开发者，即提供开箱即用的接口。内部实现主要代表引擎内部所有构建块，面向需求更复杂的开发者，可供灵活扩展引擎，实现不同的需求。引擎命名为`AutoModel`，模块整体架构如图所示：
![](attachments/Pasted%20image%2020250408100554.png)


应用接口上，主要提供两种应用接口。一是适用于数据库的引擎功能接口，可以适用于`LINQ to SQL`的表达式生成，将生成的过滤表达式运用于`ORM`框架，实现针对数据库的动态过滤。二是适用于内存的引擎功能接口，可以适用于实体`Lambda`表达式生成，生成`Predicate<TEntity>`的方法委托，即传入为实体类型，返回值为布尔类型的函数指针。该方法委托可用于内存中`IEnumerable<TEntity>`的类型筛选，即任何实体支持迭代的类型的筛选。

内部实现上，其中三种蓝色部分的接口用于`Query DSL`字符串表达式的解析。剩余两种紫色部分的接口，快照用于缓存实体配 置及相关信息构建，类型转换器用于字符串过滤值至代码托管类型转换。

## 引擎交互流程

引擎交互流程通过一个例子进行解释：
![](attachments/Pasted%20image%2020250408100610.png)
其中表达式：

`Callsign like "CCA%" && CreateTime >= "2024-12-25" && (EOBT > "now – 60min") || ATOT < "now + 3d")`

表达式标准化器用于将表达式标准化为调用核心引擎的语法，当前实现的引擎为`Dynamic LINQ`引擎。表达式实体字段`Token`解析器用于对整个表达式进行`Token`化分析。示例表达式可以被拆分为四个`Token`，对于每个`Token`进行进一步的信息填充。

以`Callsign like"CCA%"` 为例，识别实体属性名为`Callsign`，条件关键字为`like`，属性比较值为`CCA%`。根据实体快照，对`Token`的三个部分进行检查验证，明确是否为有效的`Token`。最终会生成四个经过验证的`Token`，通过`Token`生成器生成适用于调用核心引擎的调用方式，进而作用于数据库或内存实体表中进行筛选。
