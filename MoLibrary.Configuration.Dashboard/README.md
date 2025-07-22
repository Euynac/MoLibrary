# Configuration Dashboard UI Module

这个模块为MoLibrary配置管理系统提供了完整的Web UI界面，包含配置管理、历史查看、实时监控等功能。

## 功能特性

### 🚀 主要功能
- **配置列表管理**: 查看、编辑、删除配置项
- **历史记录追踪**: 查看配置变更历史和版本信息
- **实时状态监控**: 监控配置状态和应用健康度
- **类型化编辑**: 根据配置类型提供相应的编辑组件
- **搜索和筛选**: 快速定位特定配置项
- **批量操作**: 支持批量更新和回滚操作

### 🎨 UI特性
- 基于MudBlazor的现代化界面设计
- 响应式布局，支持移动端访问
- 实时数据刷新和自动更新
- 直观的时间线历史展示
- 丰富的统计图表和仪表板

## 快速开始

### 1. 添加模块依赖

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 配置核心模块
builder.ConfigMoConfiguration(options =>
{
    // 基础配置选项
});

// 配置Dashboard
builder.ConfigMoConfigurationDashboard(options =>
{
    options.ThisIsDashboard = true; // 设置为配置中心
});

// 配置UI模块
builder.ConfigMoConfigurationUI()
    .SetPageTitle("配置管理中心")
    .EnableRealTimeUpdates(true)
    .ConfigureHistory(showHistory: true, retentionDays: 180)
    .ConfigurePermissions(allowEdit: true, allowRollback: true);

var app = builder.Build();
app.Run();
```

### 2. 访问UI界面

启动应用程序后，访问 `/configuration-manage` 路径即可打开配置管理界面。

## API接口

### Configuration Controller
- `GET /api/ModuleConfiguration/status` - 获取配置状态
- `GET /api/ModuleConfiguration/debug` - 获取调试信息
- `GET /api/ModuleConfiguration/providers` - 获取配置提供者

### Configuration Dashboard Controller  
- `GET /api/configuration/status` - 获取所有配置状态
- `GET /api/configuration/option/status` - 获取特定配置状态
- `GET /api/configuration/history` - 获取配置历史
- `POST /api/configuration/update` - 更新配置
- `POST /api/configuration/rollback` - 回滚配置

### Configuration Client Controller
- `POST /api/option/update` - 热更新配置

## 架构说明

### 模块结构
```
MoLibrary.Configuration.Dashboard/
├── Controllers/                    # API控制器
│   ├── ConfigurationDashboardController.cs
│   ├── ConfigurationClientController.cs
├── UIConfiguration/               # UI组件和服务
│   ├── Components/               # Blazor组件
│   │   ├── ConfigurationList.razor
│   │   ├── ConfigurationEditor.razor
│   │   ├── ConfigurationHistory.razor
│   │   └── ConfigurationStatus.razor
│   ├── Models/                   # 视图模型
│   │   └── ConfigurationViewModels.cs
│   └── Services/                 # 业务服务
│       └── ConfigurationService.cs
├── Pages/                        # 页面
│   └── UIConfigurationPage.razor
└── Modules/                      # 模块定义
    ├── ModuleConfigurationUI.cs
    ├── ModuleConfigurationUIOption.cs
    ├── ModuleConfigurationUIGuide.cs
    └── ModuleConfigurationUIBuilderExtensions.cs
```

### 服务层设计
- `ConfigurationService`: 核心业务逻辑实现
- 所有方法返回`Res<T>`类型，保证非空返回值
- 统一异常处理和错误日志记录
- 支持事务处理和工作单元模式

### 组件设计原则
- 单一职责：每个组件专注特定功能
- 可复用性：通用功能抽离为独立组件
- 参数化：通过参数控制组件行为和显示
- 事件驱动：组件间通过事件进行通信

## 配置选项

### ModuleConfigurationUIOption

```csharp
public class ModuleConfigurationUIOption : MoModuleControllerOption<ModuleConfigurationUI>
{
    // 是否禁用配置管理页面
    public bool DisableConfigurationPage { get; set; } = false;
    
    // 页面标题
    public string PageTitle { get; set; } = "配置管理";
    
    // 是否启用实时更新
    public bool EnableRealTimeUpdates { get; set; } = true;
    
    // 默认页面大小
    public int DefaultPageSize { get; set; } = 20;
    
    // 是否显示历史记录
    public bool ShowHistory { get; set; } = true;
    
    // 历史记录保留天数
    public int HistoryRetentionDays { get; set; } = 180;
    
    // 是否允许配置编辑
    public bool AllowEdit { get; set; } = true;
    
    // 是否允许配置回滚
    public bool AllowRollback { get; set; } = true;
}
```

## 使用示例

### 基础使用
```csharp
// 最简配置
builder.ConfigMoConfigurationUI();
```

### 高级配置
```csharp
// 完整配置
builder.ConfigMoConfigurationUI()
    .DisableConfigurationPage(false)
    .SetPageTitle("企业配置管理平台")
    .EnableRealTimeUpdates(true)
    .SetDefaultPageSize(50)
    .ConfigureHistory(showHistory: true, retentionDays: 365)
    .ConfigurePermissions(allowEdit: true, allowRollback: false);
```

### 编程式配置
```csharp
builder.ConfigMoConfigurationUI(options =>
{
    options.PageTitle = "配置管理系统";
    options.EnableRealTimeUpdates = false;
    options.DefaultPageSize = 10;
    options.ShowHistory = false;
    options.AllowRollback = false;
});
```

## 注意事项

1. **依赖关系**: 需要先配置Configuration和ConfigurationDashboard模块
2. **权限控制**: 生产环境建议关闭编辑和回滚功能
3. **性能考虑**: 大量配置项时建议调整页面大小
4. **安全性**: 配置接口应配置适当的授权策略
5. **监控**: 建议启用实时更新以便及时发现配置问题

## 更新日志

### v1.0.0
- ✅ 初始版本发布
- ✅ 支持基础配置管理功能
- ✅ 实现历史记录查看
- ✅ 添加实时状态监控
- ✅ 完整的UI组件系统