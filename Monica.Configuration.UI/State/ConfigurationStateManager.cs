using System.Text.Json;
using Monica.Configuration.Models;
using Monica.Configuration.Providers.JsonFile;

namespace Monica.Configuration.UI.State;

/// <summary>
/// Configuration Status Manager - Unified management of all configuration status and operations
/// </summary>
public class ConfigurationStateManager
{
    private readonly Dictionary<string, ConfigurationViewModel> _configurations = new();
    
    // Select status management
    private SelectionState _selectionState = new();
    
    /// <summary>
    /// Initialize configuration data
    /// </summary>
    public void Initialize(List<ConfigurationDomainGroup> domainConfigs)
    {
        // Save current selection state
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
        
        // Try to restore selection state
        RestoreSelectionState(domainConfigs, previousSelection);
    }
    
    /// <summary>
    /// Get configuration view model
    /// </summary>
    public ConfigurationViewModel? GetConfiguration(string configName)
    {
        return _configurations.GetValueOrDefault(configName);
    }
    
    /// <summary>
    /// Update configuration items
    /// </summary>
    public void UpdateItem(string configName, string itemKey, object? newValue)
    {
        if (_configurations.TryGetValue(configName, out var config))
        {
            config.UpdateItem(itemKey, newValue);
        }
    }
    
    /// <summary>
    /// Undo configuration item modification
    /// </summary>
    public void UndoItem(string configName, string itemKey)
    {
        if (_configurations.TryGetValue(configName, out var config))
        {
            config.UndoItem(itemKey);
        }
    }
    
    /// <summary>
    /// Get all modified configurations
    /// </summary>
    public List<ConfigurationViewModel> GetModifiedConfigurations()
    {
        return _configurations.Values.Where(c => c.HasModifications).ToList();
    }
    
    /// <summary>
    /// Generate update request
    /// </summary>
    public List<ConfigurationUpdateRequest> BuildUpdateRequests()
    {
        var requests = new List<ConfigurationUpdateRequest>();
        
        foreach (var config in GetModifiedConfigurations())
        {
            var configJson = new Dictionary<string, object?>();
            
            // Build the complete configuration JSON, including all items (modified and unmodified)
            foreach (var item in config.Items)
            {
                configJson[item.OriginalItem.Name] = item.CurrentValue;
            }
            
            requests.Add(new ConfigurationUpdateRequest
            {
                AppId = config.AppId,
                Key = config.ConfigName,
                Value = JsonSerializer.SerializeToNode(configJson, JsonFileConventions.JsonSerializerOptions)
            });
        }
        
        return requests;
    }
    
    /// <summary>
    /// Clear all changes
    /// </summary>
    public void ClearAllModifications()
    {
        foreach (var config in _configurations.Values)
        {
            config.ClearModifications();
        }
    }
    
    /// <summary>
    /// Get API call preview
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
        
        var request = new ConfigurationUpdateRequest
        {
            AppId = config.AppId,
            Key = config.ConfigName,
            Value = JsonSerializer.SerializeToNode(configJson, JsonFileConventions.JsonSerializerOptions)
        };
        
        var preview = new
        {
            Method = "POST",
            Endpoint = "/api/configuration/update",
            Headers = new { ContentType = "application/json" },
            Body = request
        };
        
        return JsonSerializer.Serialize(preview, JsonFileConventions.JsonSerializerOptions);
    }
    
    #region 选择状态管理
    
    /// <summary>
    /// Get the current selection status
    /// </summary>
    public SelectionState GetSelectionState() => _selectionState;
    
    /// <summary>
    /// Update selection status
    /// </summary>
    public void UpdateSelection(string? domainName = null, string? serviceName = null, string? configName = null)
    {
        if (domainName != null) _selectionState.SelectedDomainName = domainName;
        if (serviceName != null) _selectionState.SelectedServiceName = serviceName;
        if (configName != null) _selectionState.SelectedConfigName = configName;
    }
    
    /// <summary>
    /// Clear selection status
    /// </summary>
    public void ClearSelection()
    {
        _selectionState = new SelectionState();
    }
    
    /// <summary>
    /// Restore selection state
    /// </summary>
    private void RestoreSelectionState(List<ConfigurationDomainGroup> domainConfigs, SelectionState previousSelection)
    {
        if (string.IsNullOrEmpty(previousSelection.SelectedDomainName)) 
            return;
            
        // Try to find the previously selected domain
        var domain = domainConfigs.FirstOrDefault(d => d.Name == previousSelection.SelectedDomainName);
        if (domain == null) return;
        
        _selectionState.SelectedDomainName = domain.Name;
        
        // Try restoring service options
        if (!string.IsNullOrEmpty(previousSelection.SelectedServiceName))
        {
            var service = domain.Children.FirstOrDefault(s => s.Name == previousSelection.SelectedServiceName);
            if (service != null)
            {
                _selectionState.SelectedServiceName = service.Name;
                
                // Try to restore configuration class selection
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
/// Configure view model
/// </summary>
public class ConfigurationViewModel
{
    public string ConfigName { get; }
    public string AppId { get; }
    public List<ConfigurationItemViewModel> Items { get; }
    
    public bool HasModifications => Items.Any(i => i.IsModified);
    public int ModificationCount => Items.Count(i => i.IsModified);
    
    public ConfigurationViewModel(ConfigurationSnapshot originalConfig, string appId)
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
/// Configuration item view model
/// </summary>
public class ConfigurationItemViewModel
{
    public ConfigurationOptionSnapshot OriginalItem { get; }
    private object? _currentValue;
    private bool _isModified;
    
    public object? CurrentValue 
    { 
        get => _currentValue; 
        private set => _currentValue = value; 
    }
    
    public bool IsModified => _isModified;
    
    public ConfigurationItemViewModel(ConfigurationOptionSnapshot originalItem)
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
    /// Get raw JSON
    /// </summary>
    public string GetOriginalJson()
    {
        return JsonSerializer.Serialize(OriginalItem.Value, JsonFileConventions.JsonSerializerOptions);
    }
    
    /// <summary>
    /// Get the current JSON
    /// </summary>
    public string GetCurrentJson()
    {
        return JsonSerializer.Serialize(CurrentValue, JsonFileConventions.JsonSerializerOptions);
    }
    
    private static bool ValuesEqual(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        
        // Deep comparison using JSON serialization
        try
        {
            var json1 = JsonSerializer.Serialize(value1, JsonFileConventions.JsonSerializerOptions);
            var json2 = JsonSerializer.Serialize(value2, JsonFileConventions.JsonSerializerOptions);
            return json1 == json2;
        }
        catch
        {
            return value1.Equals(value2);
        }
    }
}

/// <summary>
/// Select state model
/// </summary>
public class SelectionState
{
    public string? SelectedDomainName { get; set; }
    public string? SelectedServiceName { get; set; }
    public string? SelectedConfigName { get; set; }
    
    /// <summary>
    /// Clone selection status
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
