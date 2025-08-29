using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Providers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoLibrary.Configuration.Dashboard.Model;

/// <summary>
/// 配置状态管理器 - 统一管理所有配置状态和操作
/// </summary>
public class ConfigurationStateManager
{
    private readonly Dictionary<string, ConfigurationViewModel> _configurations = new();
    
    // 选择状态管理
    private SelectionState _selectionState = new();
    
    /// <summary>
    /// 初始化配置数据
    /// </summary>
    public void Initialize(List<DtoDomainConfigs> domainConfigs)
    {
        // 保存当前的选择状态
        var previousSelection = _selectionState.Clone();
        
        _configurations.Clear();
        
        foreach (var domain in domainConfigs)
        {
            foreach (var service in domain.Children)
            {
                foreach (var config in service.Children)
                {
                    var viewModel = new ConfigurationViewModel(config, service.AppId);
                    _configurations[config.Name] = viewModel;
                }
            }
        }
        
        // 尝试恢复选择状态
        RestoreSelectionState(domainConfigs, previousSelection);
    }
    
    /// <summary>
    /// 获取配置视图模型
    /// </summary>
    public ConfigurationViewModel? GetConfiguration(string configName)
    {
        return _configurations.TryGetValue(configName, out var config) ? config : null;
    }
    
    /// <summary>
    /// 更新配置项
    /// </summary>
    public void UpdateItem(string configName, string itemKey, object? newValue)
    {
        if (_configurations.TryGetValue(configName, out var config))
        {
            config.UpdateItem(itemKey, newValue);
        }
    }
    
    /// <summary>
    /// 撤销配置项修改
    /// </summary>
    public void UndoItem(string configName, string itemKey)
    {
        if (_configurations.TryGetValue(configName, out var config))
        {
            config.UndoItem(itemKey);
        }
    }
    
    /// <summary>
    /// 获取所有已修改的配置
    /// </summary>
    public List<ConfigurationViewModel> GetModifiedConfigurations()
    {
        return _configurations.Values.Where(c => c.HasModifications).ToList();
    }
    
    /// <summary>
    /// 生成更新请求
    /// </summary>
    public List<DtoUpdateConfig> BuildUpdateRequests()
    {
        var requests = new List<DtoUpdateConfig>();
        
        foreach (var config in GetModifiedConfigurations())
        {
            var configJson = new Dictionary<string, object?>();
            
            // 构建完整的配置JSON，包含所有项（修改的和未修改的）
            foreach (var item in config.Items)
            {
                configJson[item.OriginalItem.Name] = item.CurrentValue;
            }
            
            requests.Add(new DtoUpdateConfig
            {
                AppId = config.AppId,
                Key = config.ConfigName,
                Value = JsonSerializer.SerializeToNode(configJson, JsonFileProviderConventions.JsonSerializerOptions)
            });
        }
        
        return requests;
    }
    
    /// <summary>
    /// 清空所有修改
    /// </summary>
    public void ClearAllModifications()
    {
        foreach (var config in _configurations.Values)
        {
            config.ClearModifications();
        }
    }
    
    /// <summary>
    /// 获取API调用预览
    /// </summary>
    public string GetApiCallPreview(string configName)
    {
        if (!_configurations.TryGetValue(configName, out var config))
            return "{}";
            
        var configJson = new Dictionary<string, object?>();
        foreach (var item in config.Items)
        {
            configJson[item.OriginalItem.Name] = item.CurrentValue;
        }
        
        var request = new DtoUpdateConfig
        {
            AppId = config.AppId,
            Key = config.ConfigName,
            Value = JsonSerializer.SerializeToNode(configJson, JsonFileProviderConventions.JsonSerializerOptions)
        };
        
        var preview = new
        {
            Method = "POST",
            Endpoint = "/api/configuration/update",
            Headers = new { ContentType = "application/json" },
            Body = request
        };
        
        return JsonSerializer.Serialize(preview, JsonFileProviderConventions.JsonSerializerOptions);
    }
    
    #region 选择状态管理
    
    /// <summary>
    /// 获取当前选择状态
    /// </summary>
    public SelectionState GetSelectionState() => _selectionState;
    
    /// <summary>
    /// 更新选择状态
    /// </summary>
    public void UpdateSelection(string? domainName = null, string? serviceName = null, string? configName = null)
    {
        if (domainName != null) _selectionState.SelectedDomainName = domainName;
        if (serviceName != null) _selectionState.SelectedServiceName = serviceName;
        if (configName != null) _selectionState.SelectedConfigName = configName;
    }
    
    /// <summary>
    /// 清空选择状态
    /// </summary>
    public void ClearSelection()
    {
        _selectionState = new SelectionState();
    }
    
    /// <summary>
    /// 恢复选择状态
    /// </summary>
    private void RestoreSelectionState(List<DtoDomainConfigs> domainConfigs, SelectionState previousSelection)
    {
        if (string.IsNullOrEmpty(previousSelection.SelectedDomainName)) 
            return;
            
        // 尝试找到之前选择的域
        var domain = domainConfigs.FirstOrDefault(d => d.Name == previousSelection.SelectedDomainName);
        if (domain == null) return;
        
        _selectionState.SelectedDomainName = domain.Name;
        
        // 尝试恢复服务选择
        if (!string.IsNullOrEmpty(previousSelection.SelectedServiceName))
        {
            var service = domain.Children.FirstOrDefault(s => s.Name == previousSelection.SelectedServiceName);
            if (service != null)
            {
                _selectionState.SelectedServiceName = service.Name;
                
                // 尝试恢复配置类选择
                if (!string.IsNullOrEmpty(previousSelection.SelectedConfigName))
                {
                    var config = service.Children.FirstOrDefault(c => c.Name == previousSelection.SelectedConfigName);
                    if (config != null)
                    {
                        _selectionState.SelectedConfigName = config.Name;
                    }
                }
            }
        }
    }
    
    #endregion
}

/// <summary>
/// 配置视图模型
/// </summary>
public class ConfigurationViewModel
{
    public string ConfigName { get; }
    public string AppId { get; }
    public List<ConfigurationItemViewModel> Items { get; }
    
    public bool HasModifications => Items.Any(i => i.IsModified);
    public int ModificationCount => Items.Count(i => i.IsModified);
    
    public ConfigurationViewModel(DtoConfig originalConfig, string appId)
    {
        ConfigName = originalConfig.Name;
        AppId = appId;
        Items = originalConfig.Items.Select(item => new ConfigurationItemViewModel(item)).ToList();
    }
    
    public void UpdateItem(string itemKey, object? newValue)
    {
        var item = Items.FirstOrDefault(i => i.OriginalItem.Key == itemKey);
        item?.UpdateValue(newValue);
    }
    
    public void UndoItem(string itemKey)
    {
        var item = Items.FirstOrDefault(i => i.OriginalItem.Key == itemKey);
        item?.UndoModification();
    }
    
    public void ClearModifications()
    {
        foreach (var item in Items)
        {
            item.UndoModification();
        }
    }
    
    public List<ConfigurationItemViewModel> GetModifiedItems()
    {
        return Items.Where(i => i.IsModified).ToList();
    }
}

/// <summary>
/// 配置项视图模型
/// </summary>
public class ConfigurationItemViewModel
{
    public DtoOptionItem OriginalItem { get; }
    private object? _currentValue;
    private bool _isModified;
    
    public object? CurrentValue 
    { 
        get => _currentValue; 
        private set => _currentValue = value; 
    }
    
    public bool IsModified => _isModified;
    
    public ConfigurationItemViewModel(DtoOptionItem originalItem)
    {
        OriginalItem = originalItem;
        _currentValue = originalItem.Value;
        _isModified = false;
    }
    
    public void UpdateValue(object? newValue)
    {
        _currentValue = newValue;
        _isModified = !ValuesEqual(_currentValue, OriginalItem.Value);
    }
    
    public void UndoModification()
    {
        _currentValue = OriginalItem.Value;
        _isModified = false;
    }
    
    /// <summary>
    /// 获取原始JSON
    /// </summary>
    public string GetOriginalJson()
    {
        return JsonSerializer.Serialize(OriginalItem.Value, JsonFileProviderConventions.JsonSerializerOptions);
    }
    
    /// <summary>
    /// 获取当前JSON
    /// </summary>
    public string GetCurrentJson()
    {
        return JsonSerializer.Serialize(CurrentValue, JsonFileProviderConventions.JsonSerializerOptions);
    }
    
    private static bool ValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        
        // 使用JSON序列化进行深度比较
        try
        {
            var json1 = JsonSerializer.Serialize(value1, JsonFileProviderConventions.JsonSerializerOptions);
            var json2 = JsonSerializer.Serialize(value2, JsonFileProviderConventions.JsonSerializerOptions);
            return json1 == json2;
        }
        catch
        {
            return value1.Equals(value2);
        }
    }
}

/// <summary>
/// 选择状态模型
/// </summary>
public class SelectionState
{
    public string? SelectedDomainName { get; set; }
    public string? SelectedServiceName { get; set; }
    public string? SelectedConfigName { get; set; }
    
    /// <summary>
    /// 克隆选择状态
    /// </summary>
    public SelectionState Clone()
    {
        return new SelectionState
        {
            SelectedDomainName = SelectedDomainName,
            SelectedServiceName = SelectedServiceName,
            SelectedConfigName = SelectedConfigName
        };
    }
}