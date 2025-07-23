# MudBlazor v8.9.0 组件快速参考

本文档为Claude Code提供MudBlazor v8组件的快速查找和使用参考。

## 组件分类索引

### 1. 布局组件 (Layout)
- **MudContainer** - 响应式容器
- **MudGrid/MudItem** - 12列栅格系统
- **MudPaper** - Material Design纸张效果
- **MudCard** - 卡片容器
- **MudDrawer** - 侧边抽屉
- **MudLayout** - 页面布局框架
- **MudMainContent** - 主内容区域
- **MudSpacer** - 弹性空间
- **MudDivider** - 分割线
- **MudBreakpointProvider** - 响应式断点

### 2. 导航组件 (Navigation)
- **MudAppBar** - 应用栏
- **MudBreadcrumbs** - 面包屑导航
- **MudLink** - 链接
- **MudMenu** - 下拉菜单
- **MudNavMenu/MudNavLink** - 导航菜单
- **MudPagination** - 分页
- **MudTabs/MudTabPanel** - 标签页
- **MudStepper/MudStep** - 步骤条（v8新增）
- **MudSpeedDial** - 快速拨号按钮

### 3. 输入组件 (Inputs)
- **MudTextField** - 文本输入框
- **MudNumericField** - 数字输入框
- **MudSelect** - 下拉选择
- **MudAutocomplete** - 自动完成
- **MudCheckBox** - 复选框
- **MudRadio/MudRadioGroup** - 单选按钮
- **MudSwitch** - 开关
- **MudSlider** - 滑块
- **MudRating** - 评分
- **MudToggleGroup/MudToggleItem** - 切换组（v8新增）
- **MudColorPicker** - 颜色选择器
- **MudDatePicker** - 日期选择器
- **MudTimePicker** - 时间选择器
- **MudDateRangePicker** - 日期范围选择器
- **MudMask** - 输入掩码
- **MudFileUpload** - 文件上传

### 4. 数据展示 (Data Display)
- **MudTable** - 基础表格
- **MudDataGrid** - 高级数据网格（支持拖拽排序）
- **MudTreeView/MudTreeViewItem** - 树形视图
- **MudList/MudListItem** - 列表
- **MudChip/MudChipSet** - 芯片
- **MudBadge** - 徽章
- **MudAvatar** - 头像
- **MudTooltip** - 工具提示
- **MudCarousel** - 轮播图
- **MudTimeline** - 时间线
- **MudChat/MudChatBubble** - 聊天组件（v8新增）

### 5. 反馈组件 (Feedback)
- **MudAlert** - 警告提示
- **MudSnackbar** - 轻量提示条
- **MudDialog** - 对话框
- **MudProgressCircular** - 环形进度
- **MudProgressLinear** - 线性进度
- **MudSkeleton** - 骨架屏
- **MudOverlay** - 遮罩层
- **MudBackdrop** - 背景遮罩

### 6. 按钮组件 (Buttons)
- **MudButton** - 标准按钮
- **MudIconButton** - 图标按钮
- **MudFab** - 浮动操作按钮
- **MudButtonGroup** - 按钮组
- **MudToggleIconButton** - 切换图标按钮

### 7. 实用组件 (Utilities)
- **MudIcon** - 图标
- **MudText** - 文本排版
- **MudHidden** - 响应式隐藏
- **MudFocusTrap** - 焦点陷阱
- **MudVirtualize** - 虚拟滚动
- **MudSwipeArea** - 滑动区域
- **MudScrollToTop** - 滚动到顶部
- **MudMessageBox** - 消息框
- **MudContextualActionBar** - 上下文操作栏（v8新增）

## 常用组件代码示例

### 表单示例
```razor
<MudForm @ref="form" @bind-IsValid="@isValid">
    <MudTextField T="string" 
                  Label="用户名" 
                  @bind-Value="username"
                  Required="true"
                  RequiredError="用户名必填" />
    
    <MudTextField T="string" 
                  Label="密码" 
                  @bind-Value="password"
                  InputType="InputType.Password"
                  Required="true" />
    
    <MudButton ButtonType="ButtonType.Submit" 
               Variant="Variant.Filled" 
               Color="Color.Primary"
               Disabled="!isValid">
        提交
    </MudButton>
</MudForm>
```

### 数据表格示例
```razor
<MudDataGrid T="Person" 
             Items="@people"
             Filterable="true"
             SortMode="@SortMode.Multiple"
             DragDropColumnReordering="true">
    <Columns>
        <PropertyColumn Property="x => x.Name" Title="姓名" />
        <PropertyColumn Property="x => x.Age" Title="年龄" />
        <PropertyColumn Property="x => x.Email" Title="邮箱" />
        <TemplateColumn Title="操作">
            <CellTemplate>
                <MudIconButton Icon="@Icons.Material.Filled.Edit" 
                               Size="Size.Small"
                               OnClick="@(() => EditPerson(context.Item))" />
            </CellTemplate>
        </TemplateColumn>
    </Columns>
</MudDataGrid>
```

### 对话框示例
```razor
@code {
    private async Task ShowDialogAsync()
    {
        var parameters = new DialogParameters<MyDialog>
        {
            { x => x.ContentText, "确定要删除吗？" },
            { x => x.ButtonText, "删除" },
            { x => x.Color, Color.Error }
        };

        var options = new DialogOptions 
        { 
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small
        };

        var dialog = await DialogService.ShowAsync<MyDialog>("确认删除", parameters, options);
        var result = await dialog.Result;
        
        if (!result.Canceled)
        {
            // 执行删除操作
        }
    }
}
```

## 组件通用属性

### 所有MudComponent都支持
- **Class** - CSS类名
- **Style** - 内联样式
- **UserAttributes** - 自定义HTML属性
- **@ref** - 组件引用

### 表单组件通用属性
- **@bind-Value** - 双向绑定
- **Label** - 标签
- **Placeholder** - 占位符
- **Required** - 必填
- **RequiredError** - 必填错误消息
- **Disabled** - 禁用
- **ReadOnly** - 只读
- **Error** - 是否有错误
- **ErrorText** - 错误文本
- **HelperText** - 帮助文本
- **Variant** - 变体（Text/Filled/Outlined）
- **Margin** - 边距（None/Dense/Normal）

### 颜色属性值
- Primary
- Secondary
- Tertiary
- Info
- Success
- Warning
- Error
- Dark
- Light
- Transparent
- Inherit
- Surface

### 尺寸属性值
- Small
- Medium
- Large

### 变体属性值
- Text
- Filled
- Outlined

## 搜索技巧

### 查找组件实现
```bash
# 查找组件定义
Glob: "**/Mud{ComponentName}.razor"
Glob: "**/Mud{ComponentName}.razor.cs"

# 查找组件测试
Glob: "**/{ComponentName}Tests.cs"

# 查找组件样式
Glob: "**/_mud{componentname}.scss"
```

### 查找组件用法
```bash
# 在文档中查找示例
Glob: "**/Examples/{ComponentName}*.razor"

# 在测试中查找用法
Grep: "<Mud{ComponentName}"
```

## 重要提示

1. **组件命名规则**：所有组件都以"Mud"前缀开始
2. **参数绑定**：使用@bind-Value进行双向绑定
3. **事件回调**：使用EventCallback<T>类型
4. **异步操作**：优先使用Async后缀的方法
5. **样式定制**：通过Class和Style属性，或使用主题系统

## 快速定位组件

如果不确定组件位置，使用以下路径模式：
- 组件定义：`src/MudBlazor/Components/{Category}/Mud{ComponentName}.razor`
- 组件逻辑：`src/MudBlazor/Components/{Category}/Mud{ComponentName}.razor.cs`
- 组件测试：`src/MudBlazor.UnitTests/Components/{ComponentName}Tests.cs`
- 组件样式：`src/MudBlazor/Styles/components/_{componentname}.scss`

Categories包括：
- AppBar, Avatar, Badge, Breadcrumbs, Button, Card, Carousel, Chart, Checkbox, Chip, ColorPicker, DataGrid, DatePicker, Dialog, Divider, Drawer, ExpansionPanel, Field, FileUpload, Form, Grid, Hidden, Highlighter, Icon, Input, Layout, Link, List, Menu, MessageBox, NavMenu, Overlay, Pagination, Paper, Popover, Progress, Radio, Rating, ScrollToTop, Select, Skeleton, Slider, Snackbar, SpeedDial, Stepper, SwipeArea, Switch, Table, Tabs, TextField, Timeline, TimePicker, ToggleButton, Tooltip, TreeView, Typography, Virtualize