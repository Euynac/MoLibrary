using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Monica.Configuration.Models;
using Monica.Configuration.UI.State;
using Monica.Configuration.UI.Support;

namespace Monica.Configuration.UI.Dialogs;

/// <summary>
/// Dialog backing logic for structured object, dictionary, and list configuration editing.
/// </summary>
public partial class ComplexValueEditorDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter, EditorRequired] public ConfigurationDefinition Definition { get; set; } = null!;
    [Parameter, EditorRequired] public ConfigurationNodeDefinition Node { get; set; } = null!;
    [Parameter] public ConfigurationEffectiveValue? EffectiveValue { get; set; }
    [Parameter] public IReadOnlyList<PendingChange> PendingChanges { get; set; } = [];

    private readonly List<PendingChange> _changes = [];
    private readonly List<FocusFrame> _navigationStack = [];
    private readonly Dictionary<string, string> _validationErrors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sensitiveDrafts = new(StringComparer.Ordinal);
    private JsonNode? _documentNode;
    private JsonNode? _originalDocumentNode;
    private ConfigurationNodeDefinition _focusNode = null!;
    private LogicalPath _focusPath = LogicalPath.Root;
    private EditorMode _mode = EditorMode.Visual;
    private string? _selectedEntryKey;
    private string _newEntryKey = string.Empty;
    private string? _newEntryError;
    private long? _valueVersion;
    private bool _startedWithScopedPendingChanges;

    private ConfigurationReloadBehavior EffectiveReloadBehavior =>
        Node.ReloadBehavior is { } behavior and not ConfigurationReloadBehavior.Inherit
            ? behavior
            : Definition.ReloadBehavior;

    private Severity ReloadSeverity => EffectiveReloadBehavior is ConfigurationReloadBehavior.RequiresRestart or ConfigurationReloadBehavior.StaticAfterStartup
        ? Severity.Warning
        : Severity.Info;

    private bool RequiresNewEntryKey =>
        _focusNode.NodeKind == ConfigurationNodeKind.Dictionary
        || _focusNode is { NodeKind: ConfigurationNodeKind.List, ListTemplate.SupportsPerItemMutation: true };

    private string CollectionKindLabel => _focusNode.NodeKind switch
    {
        ConfigurationNodeKind.Dictionary => L["Dialogs:ComplexEditor:CollectionKinds:Dictionary"],
        ConfigurationNodeKind.List => L["Dialogs:ComplexEditor:CollectionKinds:List"],
        _ => string.Empty
    };

    private string AddEntryLabel => _focusNode.NodeKind switch
    {
        ConfigurationNodeKind.Dictionary => L["Dialogs:ComplexEditor:Actions:AddKey"],
        ConfigurationNodeKind.List => L["Dialogs:ComplexEditor:Actions:AddItem"],
        _ => string.Empty
    };

    private string NewEntryKeyLabel => _focusNode.NodeKind == ConfigurationNodeKind.Dictionary
        ? L["Dialogs:ComplexEditor:NewKey"]
        : L["Dialogs:ComplexEditor:NewItemKey"];

    private IReadOnlyList<CollectionEntry> CurrentEntries => BuildEntries();

    private CollectionEntry? SelectedEntry => CurrentEntries.FirstOrDefault(entry => entry.SelectionKey == _selectedEntryKey);

    private ConfigurationNodeDefinition SelectedValueSchema => _focusNode.NodeKind switch
    {
        ConfigurationNodeKind.Dictionary => _focusNode.DictionaryTemplate!.ValueTemplate,
        ConfigurationNodeKind.List => _focusNode.ListTemplate!.ItemTemplate,
        _ => _focusNode
    };

    private IReadOnlyList<ConfigurationNodeDefinition> EditableScalarFields =>
        SelectedEntry is null
            ? []
            : SelectedValueSchema.NodeKind == ConfigurationNodeKind.Scalar
                ? [SelectedValueSchema]
                : SelectedValueSchema.Children.Where(child => child.NodeKind == ConfigurationNodeKind.Scalar).ToArray();

    private IReadOnlyList<ConfigurationNodeDefinition> NestedCollections =>
        SelectedEntry is null || SelectedValueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? []
            : SelectedValueSchema.Children
                .Where(child => child.NodeKind is ConfigurationNodeKind.Dictionary or ConfigurationNodeKind.List)
                .ToArray();

    private IReadOnlyList<ObjectFieldSection> NestedObjectSections =>
        SelectedEntry is null || SelectedValueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? []
            : SelectedValueSchema.Children
                .Where(child => child.NodeKind == ConfigurationNodeKind.Object)
                .Select(child => new ObjectFieldSection(
                    child,
                    SelectedEntry.Path.Append(new PropertySegment(child.Name)),
                    child.Children.Where(grandchild => grandchild.NodeKind == ConfigurationNodeKind.Scalar).ToArray()))
                .Where(section => section.ScalarFields.Count > 0)
                .ToArray();

    private string ActivePathLabel => (_mode == EditorMode.Patch ? LogicalPath.Root : _focusPath).Depth == 0 && _mode == EditorMode.Patch
        ? L["Dialogs:ComplexEditor:PendingMutationGroup"]
        : _focusPath.ToCanonicalString();

    private string StageButtonText => L["Dialogs:ComplexEditor:Actions:StageCount", _changes.Count];

    private bool CanStage => _changes.Count > 0 || _startedWithScopedPendingChanges;

    protected override void OnInitialized()
    {
        _valueVersion = EffectiveValue?.Version;
        _focusNode = Node;
        _focusPath = Node.RelativePath;
        _originalDocumentNode = ParseInitialDocument();
        _documentNode = ConfigurationPendingValueDocumentBuilder.ApplyPendingChanges(
            Node,
            Definition.DefinitionKey,
            Node.RelativePath,
            CloneNode(_originalDocumentNode) ?? DefaultJsonFor(Node),
            PendingChanges);
        _changes.AddRange(PendingChanges
            .Where(change => ConfigurationPendingValueDocumentBuilder.IsInScope(change, Definition.DefinitionKey, Node.RelativePath))
            .OrderBy(change => change.LogicalPath.ToCanonicalString(), StringComparer.Ordinal));
        _startedWithScopedPendingChanges = _changes.Count > 0;
        SelectFirstEntry();
    }

    private void SetMode(EditorMode mode)
    {
        _mode = mode;
    }

    private Variant ModeVariant(EditorMode mode)
    {
        return _mode == mode ? Variant.Filled : Variant.Outlined;
    }

    private Color ModeColor(EditorMode mode)
    {
        return _mode == mode ? Color.Primary : Color.Default;
    }

    private JsonNode ParseInitialDocument()
    {
        var source = string.IsNullOrWhiteSpace(EffectiveValue?.DisplayValue)
            ? DefaultJsonFor(Node).ToJsonString()
            : EffectiveValue.DisplayValue!;

        try
        {
            return JsonNode.Parse(source) ?? DefaultJsonFor(Node);
        }
        catch (JsonException)
        {
            return DefaultJsonFor(Node);
        }
    }

    private IReadOnlyList<CollectionEntry> BuildEntries()
    {
        var focusValue = ReadNode(_focusPath);
        return _focusNode.NodeKind switch
        {
            ConfigurationNodeKind.Dictionary when focusValue is JsonObject jsonObject => jsonObject
                .Select(pair => BuildDictionaryEntry(pair.Key, pair.Value))
                .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ConfigurationNodeKind.List when focusValue is JsonArray jsonArray => jsonArray
                .Select((item, index) => BuildListEntry(index, item))
                .ToArray(),
            _ => []
        };
    }

    private CollectionEntry BuildDictionaryEntry(string key, JsonNode? value)
    {
        var path = _focusPath.Append(new DictionaryKeySegment(key));
        var title = ReadPreferredTitle(value, key);
        return new CollectionEntry(key, title, key, path, string.Equals(_selectedEntryKey, key, StringComparison.Ordinal));
    }

    private CollectionEntry BuildListEntry(int index, JsonNode? value)
    {
        var keyPropertyName = _focusNode.ListTemplate?.ItemKeyPropertyName;
        var itemKey = !string.IsNullOrWhiteSpace(keyPropertyName)
            ? ReadObjectPropertyText(value, keyPropertyName!)
            : null;
        var selectionKey = string.IsNullOrWhiteSpace(itemKey) ? index.ToString(CultureInfo.InvariantCulture) : itemKey!;
        var path = string.IsNullOrWhiteSpace(itemKey)
            ? _focusPath.Append(new ListIndexSegment(index))
            : _focusPath.Append(new ListItemKeySegment(itemKey!));
        var title = ReadPreferredTitle(value, selectionKey);
        var subtitle = string.IsNullOrWhiteSpace(itemKey)
            ? L["Dialogs:ComplexEditor:IndexSubtitle", index]
            : $"#{itemKey}";

        return new CollectionEntry(selectionKey, title, subtitle, path, string.Equals(_selectedEntryKey, selectionKey, StringComparison.Ordinal));
    }

    private void SelectEntry(string key)
    {
        _selectedEntryKey = key;
        _validationErrors.Clear();
    }

    private void SelectFirstEntry()
    {
        _selectedEntryKey = CurrentEntries.FirstOrDefault()?.SelectionKey;
        _newEntryKey = string.Empty;
        _newEntryError = null;
        _validationErrors.Clear();
    }

    private void AddEntry()
    {
        _newEntryError = null;
        var key = _newEntryKey.Trim();
        if (RequiresNewEntryKey && string.IsNullOrWhiteSpace(key))
        {
            _newEntryError = L["Dialogs:ComplexEditor:Errors:KeyRequired"];
            return;
        }

        if (_focusNode.NodeKind == ConfigurationNodeKind.Dictionary)
        {
            AddDictionaryEntry(key);
            return;
        }

        if (_focusNode.NodeKind == ConfigurationNodeKind.List)
        {
            AddListEntry(key);
        }
    }

    private void AddDictionaryEntry(string key)
    {
        var dictionary = ReadNode(_focusPath) as JsonObject;
        if (dictionary is null)
        {
            _newEntryError = L["Dialogs:ComplexEditor:Errors:InvalidCollection"];
            return;
        }

        if (_focusNode.DictionaryTemplate?.DisallowColonInKey is true && key.Contains(':', StringComparison.Ordinal))
        {
            _newEntryError = L["Dialogs:ComplexEditor:Errors:ColonNotAllowed"];
            return;
        }

        if (dictionary.ContainsKey(key))
        {
            _newEntryError = L["Dialogs:ComplexEditor:Errors:DuplicateKey"];
            return;
        }

        var valueSchema = _focusNode.DictionaryTemplate!.ValueTemplate;
        var value = DefaultJsonFor(valueSchema);
        dictionary[key] = value;
        var path = _focusPath.Append(new DictionaryKeySegment(key));
        _selectedEntryKey = key;
        _newEntryKey = string.Empty;
        StageContainerSet(path, valueSchema, value);
    }

    private void AddListEntry(string key)
    {
        var list = ReadNode(_focusPath) as JsonArray;
        if (list is null)
        {
            _newEntryError = L["Dialogs:ComplexEditor:Errors:InvalidCollection"];
            return;
        }

        var itemSchema = _focusNode.ListTemplate!.ItemTemplate;
        var item = DefaultJsonFor(itemSchema);
        LogicalPath path;
        string selectionKey;
        if (_focusNode.ListTemplate.SupportsPerItemMutation)
        {
            if (ListContainsKey(list, key))
            {
                _newEntryError = L["Dialogs:ComplexEditor:Errors:DuplicateKey"];
                return;
            }

            SetObjectProperty(item, _focusNode.ListTemplate.ItemKeyPropertyName!, JsonValue.Create(key));
            path = _focusPath.Append(new ListItemKeySegment(key));
            selectionKey = key;
        }
        else
        {
            path = _focusPath.Append(new ListIndexSegment(list.Count));
            selectionKey = list.Count.ToString(CultureInfo.InvariantCulture);
        }

        list.Add(item);
        _selectedEntryKey = selectionKey;
        _newEntryKey = string.Empty;
        StageContainerSet(path, itemSchema, item);
    }

    private void RemoveSelectedEntry()
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        var oldValue = CloneNode(ReadNode(entry.Path));
        if (_focusNode.NodeKind == ConfigurationNodeKind.Dictionary && ReadNode(_focusPath) is JsonObject dictionary)
        {
            dictionary.Remove(entry.SelectionKey);
        }
        else if (_focusNode.NodeKind == ConfigurationNodeKind.List && ReadNode(_focusPath) is JsonArray list)
        {
            var index = FindListEntryIndex(list, entry.SelectionKey);
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }

        StageRemove(entry.Path, SelectedValueSchema, oldValue);
        SelectFirstEntry();
    }

    private void MoveSelectedListEntry(int direction)
    {
        if (_focusNode.NodeKind != ConfigurationNodeKind.List || SelectedEntry is null || ReadNode(_focusPath) is not JsonArray list)
        {
            return;
        }

        var index = FindListEntryIndex(list, SelectedEntry.SelectionKey);
        var next = index + direction;
        if (index < 0 || next < 0 || next >= list.Count)
        {
            return;
        }

        var item = list[index];
        list.RemoveAt(index);
        list.Insert(next, item);
        StageSet(_focusPath, _focusNode, CloneNode(ReadOriginalNode(_focusPath)), CloneNode(list));
    }

    private void OpenNestedCollection(ConfigurationNodeDefinition child)
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            return;
        }

        _navigationStack.Add(new FocusFrame(_focusNode, _focusPath, _selectedEntryKey));
        _focusNode = child;
        _focusPath = entry.Path.Append(new PropertySegment(child.Name));
        EnsureCollectionExists(_focusPath, child);
        SelectFirstEntry();
    }

    private void GoBack()
    {
        if (_navigationStack.Count == 0)
        {
            return;
        }

        var frame = _navigationStack[^1];
        _navigationStack.RemoveAt(_navigationStack.Count - 1);
        _focusNode = frame.Node;
        _focusPath = frame.Path;
        _selectedEntryKey = frame.SelectedEntryKey;
        _validationErrors.Clear();
    }

    private LogicalPath FieldPath(ConfigurationNodeDefinition field)
    {
        return SelectedEntry is null
            ? _focusPath
            : FieldPath(field, SelectedEntry.Path, SelectedValueSchema);
    }

    private static LogicalPath FieldPath(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        return ownerSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? ownerPath
            : ownerPath.Append(new PropertySegment(field.Name));
    }

    private JsonNode? FieldValue(ConfigurationNodeDefinition field)
    {
        if (SelectedEntry is null)
        {
            return null;
        }

        return FieldValue(field, SelectedEntry.Path, SelectedValueSchema);
    }

    private JsonNode? FieldValue(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        return ReadNode(FieldPath(field, ownerPath, ownerSchema));
    }

    private void OnTextFieldChanged(ConfigurationNodeDefinition field, string? value)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        OnTextFieldChanged(field, SelectedEntry.Path, SelectedValueSchema, value);
    }

    private void OnTextFieldChanged(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema,
        string? value)
    {
        value ??= string.Empty;
        var path = FieldPath(field, ownerPath, ownerSchema);
        if (field.IsSensitive && string.IsNullOrWhiteSpace(value))
        {
            _sensitiveDrafts.Remove(path.ToCanonicalString());
            ClearError(path);
            return;
        }

        if (!ValidateScalar(field, value, path))
        {
            if (field.IsSensitive)
            {
                _sensitiveDrafts[path.ToCanonicalString()] = value;
            }
            return;
        }

        SetFieldValue(field, ownerPath, ownerSchema, CreateTextValueNode(field, value));
    }

    private void OnNumberFieldChanged(ConfigurationNodeDefinition field, decimal? value)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        OnNumberFieldChanged(field, SelectedEntry.Path, SelectedValueSchema, value);
    }

    private void OnNumberFieldChanged(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema,
        decimal? value)
    {
        var path = FieldPath(field, ownerPath, ownerSchema);
        if (!ValidateScalar(field, value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, path))
        {
            return;
        }

        SetFieldValue(field, ownerPath, ownerSchema, value is null ? null : JsonValue.Create(value));
    }

    private void OnBoolFieldChanged(ConfigurationNodeDefinition field, bool value)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        OnBoolFieldChanged(field, SelectedEntry.Path, SelectedValueSchema, value);
    }

    private void OnBoolFieldChanged(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema,
        bool value)
    {
        SetFieldValue(field, ownerPath, ownerSchema, JsonValue.Create(value));
    }

    private void SetFieldValue(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema,
        JsonNode? value)
    {
        var path = FieldPath(field, ownerPath, ownerSchema);
        var oldValue = CloneNode(ReadOriginalNode(path));
        var valueForStorage = CloneNode(value);
        SetNodeValue(path, value);

        _sensitiveDrafts.Remove(path.ToCanonicalString());
        StageSet(path, field, oldValue, valueForStorage);
    }

    private void SetNodeValue(LogicalPath path, JsonNode? value)
    {
        var parentPath = new LogicalPath(path.Segments.Take(path.Depth - 1).ToArray());
        EnsureNodeExists(parentPath, ResolveSchemaForPath(parentPath));
        var parent = ReadNode(parentPath);
        var segment = path.Segments[^1];
        switch (parent)
        {
            case JsonObject jsonObject when segment is PropertySegment property:
                jsonObject[property.Name] = value;
                break;
            case JsonObject jsonObject when segment is DictionaryKeySegment dictionaryKey:
                jsonObject[dictionaryKey.Key] = value;
                break;
            case JsonArray jsonArray when segment is ListIndexSegment listIndex && listIndex.Index >= 0 && listIndex.Index < jsonArray.Count:
                jsonArray[listIndex.Index] = value;
                break;
            case JsonArray jsonArray when segment is ListItemKeySegment itemKey:
                var index = FindListEntryIndex(jsonArray, itemKey.ItemKey);
                if (index >= 0)
                {
                    jsonArray[index] = value;
                }
                break;
        }
    }

    private void StageSet(LogicalPath path, ConfigurationNodeDefinition schema, JsonNode? oldValue, JsonNode? newValue)
    {
        StageChange(ConfigurationMutationKind.Set, path, schema, oldValue, newValue);
    }

    private void StageContainerSet(LogicalPath path, ConfigurationNodeDefinition schema, JsonNode? newValue)
    {
        StageChange(ConfigurationMutationKind.Set, path, schema, null, CloneNode(newValue));
    }

    private void StageRemove(LogicalPath path, ConfigurationNodeDefinition schema, JsonNode? oldValue)
    {
        var existingNewItemIndex = _changes.FindIndex(change =>
            change.MutationKind == ConfigurationMutationKind.Set
            && change.OriginalValue is null
            && change.LogicalPath.Equals(path));
        if (existingNewItemIndex >= 0)
        {
            _changes.RemoveAt(existingNewItemIndex);
            RemoveDescendantChanges(path);
            return;
        }

        RemoveDescendantChanges(path);
        UpsertChange(BuildPendingChange(ConfigurationMutationKind.Remove, path, schema, oldValue, null));
    }

    private void StageChange(
        ConfigurationMutationKind kind,
        LogicalPath path,
        ConfigurationNodeDefinition schema,
        JsonNode? oldValue,
        JsonNode? newValue)
    {
        var coveringIndex = _changes.FindIndex(change =>
            change.MutationKind == ConfigurationMutationKind.Set
            && IsStrictAncestor(change.LogicalPath, path));

        if (coveringIndex >= 0)
        {
            var covering = _changes[coveringIndex];
            var coveringSchema = ResolveSchemaForPath(covering.LogicalPath) ?? schema;
            var coveringValue = CloneNode(ReadNode(covering.LogicalPath));
            _changes[coveringIndex] = covering with
            {
                NewValue = ToStoredValue(coveringValue),
                NewDisplayValue = DisplayJson(coveringValue, coveringSchema)
            };
            return;
        }

        if (kind == ConfigurationMutationKind.Remove || schema.NodeKind != ConfigurationNodeKind.Scalar)
        {
            RemoveDescendantChanges(path);
        }

        UpsertChange(BuildPendingChange(kind, path, schema, oldValue, newValue));
    }

    private PendingChange BuildPendingChange(
        ConfigurationMutationKind kind,
        LogicalPath path,
        ConfigurationNodeDefinition schema,
        JsonNode? oldValue,
        JsonNode? newValue)
    {
        return new PendingChange
        {
            DefinitionKey = Definition.DefinitionKey,
            DefinitionDisplayName = Definition.DisplayName,
            LogicalPath = path,
            NodeDisplayName = DisplayName(schema),
            MutationKind = kind,
            NewValue = kind == ConfigurationMutationKind.Remove ? ConfigurationStoredValue.Null : ToStoredValue(newValue),
            OriginalValue = oldValue is null ? null : ToStoredValue(oldValue),
            OriginalDisplayValue = DisplayJson(oldValue, schema),
            NewDisplayValue = kind == ConfigurationMutationKind.Remove ? L["Mutation:Kinds:Remove"] : DisplayJson(newValue, schema),
            ExpectedSchemaVersion = Definition.SchemaVersion,
            ExpectedValueVersion = _valueVersion,
            IsSensitive = schema.IsSensitive,
            NodeKind = schema.NodeKind,
            ValueKind = schema.ValueKind,
            ReloadBehavior = EffectiveReloadBehaviorFor(schema)
        }.WithTarget(Definition, EffectiveValue?.EffectiveSource);
    }

    private void UpsertChange(PendingChange change)
    {
        var index = _changes.FindIndex(candidate => candidate.LogicalPath.Equals(change.LogicalPath));
        if (index >= 0)
        {
            _changes[index] = change;
            return;
        }

        _changes.Add(change);
    }

    private void RemoveDescendantChanges(LogicalPath path)
    {
        _changes.RemoveAll(change => IsStrictAncestor(path, change.LogicalPath));
    }

    private void ResetVisualChanges()
    {
        _documentNode = CloneNode(_originalDocumentNode);
        _changes.Clear();
        _validationErrors.Clear();
        _sensitiveDrafts.Clear();
        _navigationStack.Clear();
        _focusNode = Node;
        _focusPath = Node.RelativePath;
        SelectFirstEntry();
    }

    private void Apply()
    {
        if (!CanStage)
        {
            return;
        }

        MudDialog.Close(DialogResult.Ok(_changes.ToArray()));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private bool ValidateScalar(ConfigurationNodeDefinition field, string value, LogicalPath path)
    {
        ClearError(path);
        if (field.ValidationRules.OfType<RequiredRule>().Any() && string.IsNullOrWhiteSpace(value))
        {
            SetError(path, L["State:Editor:Required"]);
            return false;
        }

        if (!ValidateScalarValueKind(field, value, path))
        {
            return false;
        }

        foreach (var rule in field.ValidationRules)
        {
            switch (rule)
            {
                case AllowedValuesRule allowedValuesRule when !string.IsNullOrWhiteSpace(value)
                                                              && !allowedValuesRule.Values.Contains(value, StringComparer.OrdinalIgnoreCase):
                    SetError(path, rule.ErrorMessage ?? L["State:Editor:InvalidPattern"]);
                    return false;
                case RegexRule regexRule when !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, regexRule.Pattern):
                    SetError(path, rule.ErrorMessage ?? L["State:Editor:InvalidPattern"]);
                    return false;
                case RangeRule rangeRule when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue):
                    if (rangeRule.Min is not null && decimalValue < rangeRule.Min || rangeRule.Max is not null && decimalValue > rangeRule.Max)
                    {
                        SetError(path, rule.ErrorMessage ?? L["State:Editor:OutOfRange"]);
                        return false;
                    }
                    break;
                case MaxLengthRule maxLengthRule when value.Length > maxLengthRule.Max:
                    SetError(path, rule.ErrorMessage ?? L["State:Editor:TooLong"]);
                    return false;
                case MinLengthRule minLengthRule when value.Length < minLengthRule.Min:
                    SetError(path, rule.ErrorMessage ?? L["State:Editor:TooShort"]);
                    return false;
            }
        }

        return true;
    }

    private bool ValidateScalarValueKind(
        ConfigurationNodeDefinition field,
        string value,
        LogicalPath path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (field.ValueKind is ConfigurationValueKind.TimeSpan or ConfigurationValueKind.DateTime
                    or ConfigurationValueKind.Integer or ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating
                && !field.IsNullable)
            {
                SetError(path, L["State:Editor:Required"]);
                return false;
            }

            return true;
        }

        switch (field.ValueKind)
        {
            case ConfigurationValueKind.TimeSpan when !ConfigurationScalarTextCodec.TryParseTimeSpan(value, out _):
                SetError(path, L["State:Editor:InvalidTimeSpan"]);
                return false;
            case ConfigurationValueKind.DateTime when !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _):
                SetError(path, L["ImportExport:Diagnostics:ExpectedDateTime"]);
                return false;
            case ConfigurationValueKind.Integer when !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                SetError(path, L["ImportExport:Diagnostics:ExpectedInteger"]);
                return false;
            case ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating
                when !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _):
                SetError(path, L["ImportExport:Diagnostics:ExpectedNumber"]);
                return false;
        }

        return true;
    }

    private bool HasError(LogicalPath path)
    {
        return _validationErrors.ContainsKey(path.ToCanonicalString());
    }

    private string? ErrorFor(LogicalPath path)
    {
        return _validationErrors.GetValueOrDefault(path.ToCanonicalString());
    }

    private void SetError(LogicalPath path, string error)
    {
        _validationErrors[path.ToCanonicalString()] = error;
    }

    private void ClearError(LogicalPath path)
    {
        _validationErrors.Remove(path.ToCanonicalString());
    }

    private string ReadText(ConfigurationNodeDefinition field)
    {
        if (SelectedEntry is null)
        {
            return string.Empty;
        }

        return ReadText(field, SelectedEntry.Path, SelectedValueSchema);
    }

    private string ReadText(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        var path = FieldPath(field, ownerPath, ownerSchema).ToCanonicalString();
        if (field.IsSensitive)
        {
            return _sensitiveDrafts.GetValueOrDefault(path) ?? string.Empty;
        }

        return ReadScalarAsString(FieldValue(field, ownerPath, ownerSchema)) ?? string.Empty;
    }

    private bool ReadBool(ConfigurationNodeDefinition field)
    {
        if (SelectedEntry is null)
        {
            return false;
        }

        return ReadBool(field, SelectedEntry.Path, SelectedValueSchema);
    }

    private bool ReadBool(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        var value = FieldValue(field, ownerPath, ownerSchema);
        if (value is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(document.RootElement.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private decimal? ReadDecimal(ConfigurationNodeDefinition field)
    {
        if (SelectedEntry is null)
        {
            return null;
        }

        return ReadDecimal(field, SelectedEntry.Path, SelectedValueSchema);
    }

    private decimal? ReadDecimal(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        var text = ReadScalarAsString(FieldValue(field, ownerPath, ownerSchema));
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static JsonNode? CreateTextValueNode(ConfigurationNodeDefinition field, string value)
    {
        if (field.ValueKind == ConfigurationValueKind.TimeSpan
            && ConfigurationScalarTextCodec.TryParseTimeSpan(value, out var timeSpan))
        {
            return JsonValue.Create(timeSpan.ToString("c", CultureInfo.InvariantCulture));
        }

        return JsonValue.Create(value);
    }

    private string? Placeholder(ConfigurationNodeDefinition field)
    {
        return field.IsSensitive ? L["State:Editor:SensitivePlaceholder"].Value : null;
    }

    private static bool IsNumeric(ConfigurationNodeDefinition field)
    {
        return field.ValueKind is ConfigurationValueKind.Integer or ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating;
    }

    private IReadOnlyList<string> EnumValues(ConfigurationNodeDefinition field)
    {
        return field.ValidationRules.OfType<AllowedValuesRule>().FirstOrDefault()?.Values
               ?? ExtractRegexEnumValues(field);
    }

    private static IReadOnlyList<string> ExtractRegexEnumValues(ConfigurationNodeDefinition field)
    {
        var pattern = field.ValidationRules.OfType<RegexRule>().FirstOrDefault()?.Pattern;
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.StartsWith("^(", StringComparison.Ordinal) || !pattern.EndsWith(")$", StringComparison.Ordinal))
        {
            return [];
        }

        return pattern[2..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private JsonNode? ReadNode(LogicalPath path)
    {
        return ReadNodeFrom(_documentNode, path);
    }

    private JsonNode? ReadOriginalNode(LogicalPath path)
    {
        return ReadNodeFrom(_originalDocumentNode, path);
    }

    private JsonNode? ReadNodeFrom(JsonNode? root, LogicalPath path)
    {
        var current = root;
        var currentSchema = Node;
        foreach (var segment in RelativeSegments(path))
        {
            if (current is null)
            {
                return null;
            }

            current = GetChild(current, currentSchema, segment);
            currentSchema = ResolveChildSchema(currentSchema, segment) ?? currentSchema;
        }

        return current;
    }

    private IEnumerable<ConfigurationPathSegment> RelativeSegments(LogicalPath path)
    {
        return path.Segments.Skip(Node.RelativePath.Depth);
    }

    private static JsonNode? GetChild(JsonNode current, ConfigurationNodeDefinition currentSchema, ConfigurationPathSegment segment)
    {
        return current switch
        {
            JsonObject jsonObject when segment is PropertySegment property => jsonObject[property.Name],
            JsonObject jsonObject when segment is DictionaryKeySegment dictionaryKey => jsonObject[dictionaryKey.Key],
            JsonArray jsonArray when segment is ListIndexSegment listIndex && listIndex.Index >= 0 && listIndex.Index < jsonArray.Count => jsonArray[listIndex.Index],
            JsonArray jsonArray when segment is ListItemKeySegment itemKey => FindArrayItemByKey(jsonArray, currentSchema, itemKey.ItemKey),
            _ => null
        };
    }

    private ConfigurationNodeDefinition? ResolveSchemaForPath(LogicalPath path)
    {
        var currentSchema = Node;
        foreach (var segment in RelativeSegments(path))
        {
            currentSchema = ResolveChildSchema(currentSchema, segment) ?? currentSchema;
        }

        return currentSchema;
    }

    private static ConfigurationNodeDefinition? ResolveChildSchema(ConfigurationNodeDefinition current, ConfigurationPathSegment segment)
    {
        return segment switch
        {
            PropertySegment property => current.Children.FirstOrDefault(child =>
                string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
            DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
            ListIndexSegment => current.ListTemplate?.ItemTemplate,
            ListItemKeySegment => current.ListTemplate?.ItemTemplate,
            _ => null
        };
    }

    private void EnsureCollectionExists(LogicalPath path, ConfigurationNodeDefinition schema)
    {
        EnsureNodeExists(path, schema);
    }

    private void EnsureNodeExists(LogicalPath path, ConfigurationNodeDefinition? schema)
    {
        if (ReadNode(path) is not null)
        {
            return;
        }

        if (path.Depth <= Node.RelativePath.Depth || schema is null)
        {
            return;
        }

        var parentPath = new LogicalPath(path.Segments.Take(path.Depth - 1).ToArray());
        EnsureNodeExists(parentPath, ResolveSchemaForPath(parentPath));

        var parent = ReadNode(parentPath);
        switch (parent)
        {
            case JsonObject parentObject when path.Segments[^1] is PropertySegment property:
                parentObject[property.Name] = DefaultJsonFor(schema);
                break;
            case JsonObject parentObject when path.Segments[^1] is DictionaryKeySegment dictionaryKey:
                parentObject[dictionaryKey.Key] = DefaultJsonFor(schema);
                break;
        }
    }

    private static JsonNode DefaultJsonFor(ConfigurationNodeDefinition schema)
    {
        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Object => DefaultObjectFor(schema),
            ConfigurationNodeKind.Dictionary => new JsonObject(),
            ConfigurationNodeKind.List => new JsonArray(),
            ConfigurationNodeKind.Scalar => DefaultScalarFor(schema),
            _ => JsonValue.Create(string.Empty)
        };
    }

    private static JsonNode DefaultObjectFor(ConfigurationNodeDefinition schema)
    {
        var jsonObject = new JsonObject();
        foreach (var child in schema.Children)
        {
            jsonObject[child.Name] = DefaultJsonFor(child);
        }

        return jsonObject;
    }

    private static JsonNode DefaultScalarFor(ConfigurationNodeDefinition schema)
    {
        return schema.ValueKind switch
        {
            ConfigurationValueKind.Boolean => JsonValue.Create(false),
            ConfigurationValueKind.Integer => JsonValue.Create(0),
            ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating => JsonValue.Create(0m),
            _ => JsonValue.Create(string.Empty)
        };
    }

    private bool ListContainsKey(JsonArray list, string key)
    {
        return FindListEntryIndex(list, key) >= 0;
    }

    private int FindListEntryIndex(JsonArray list, string key)
    {
        var keyPropertyName = _focusNode.ListTemplate?.ItemKeyPropertyName;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(keyPropertyName))
            {
                if (string.Equals(index.ToString(CultureInfo.InvariantCulture), key, StringComparison.Ordinal))
                {
                    return index;
                }

                continue;
            }

            if (string.Equals(ReadObjectPropertyText(list[index], keyPropertyName), key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static JsonNode? FindArrayItemByKey(JsonArray list, ConfigurationNodeDefinition listSchema, string key)
    {
        var keyPropertyName = listSchema.ListTemplate?.ItemKeyPropertyName;
        if (string.IsNullOrWhiteSpace(keyPropertyName))
        {
            return null;
        }

        return list.FirstOrDefault(item => string.Equals(ReadObjectPropertyText(item, keyPropertyName), key, StringComparison.Ordinal));
    }

    private static string? ReadObjectPropertyText(JsonNode? node, string propertyName)
    {
        return node is JsonObject jsonObject ? ReadScalarAsString(jsonObject[propertyName]) : null;
    }

    private string ReadPreferredTitle(JsonNode? node, string fallback)
    {
        return ReadObjectPropertyText(node, "DisplayName")
               ?? ReadObjectPropertyText(node, "Name")
               ?? ReadObjectPropertyText(node, _focusNode.ListTemplate?.ItemKeyPropertyName ?? string.Empty)
               ?? fallback;
    }

    private static string? ReadScalarAsString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.String => document.RootElement.GetString(),
            JsonValueKind.Number => document.RootElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => document.RootElement.GetRawText()
        };
    }

    private static void SetObjectProperty(JsonNode node, string propertyName, JsonNode? value)
    {
        if (node is JsonObject jsonObject)
        {
            jsonObject[propertyName] = value;
        }
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString());
    }

    private static ConfigurationStoredValue ToStoredValue(JsonNode? node)
    {
        return ConfigurationStoredValue.FromJson(node?.ToJsonString() ?? "null");
    }

    private string DisplayJson(JsonNode? node, ConfigurationNodeDefinition schema)
    {
        if (node is null)
        {
            return L["Value:States:Empty"];
        }

        if (schema.IsSensitive)
        {
            return L["State:Value:Sensitive"];
        }

        return schema.NodeKind == ConfigurationNodeKind.Scalar
            ? ReadScalarAsString(node) ?? L["Value:States:Empty"]
            : FormatJson(node);
    }

    private static string FormatJson(JsonNode? node)
    {
        using var document = JsonDocument.Parse(node?.ToJsonString() ?? "null");
        return JsonSerializer.Serialize(document.RootElement, ConfigurationJsonDisplayFormatter.ReadableJsonOptions);
    }

    private ConfigurationReloadBehavior EffectiveReloadBehaviorFor(ConfigurationNodeDefinition schema)
    {
        return schema.ReloadBehavior is { } behavior and not ConfigurationReloadBehavior.Inherit
            ? behavior
            : Definition.ReloadBehavior;
    }

    private string DisplayName(ConfigurationNodeDefinition schema)
    {
        return string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.Name : schema.DisplayName!;
    }

    private static bool IsStrictAncestor(LogicalPath ancestor, LogicalPath path)
    {
        return ancestor.Depth < path.Depth && IsPrefix(ancestor, path);
    }

    private static bool IsPrefix(LogicalPath ancestor, LogicalPath path)
    {
        if (ancestor.Depth > path.Depth)
        {
            return false;
        }

        for (var index = 0; index < ancestor.Depth; index++)
        {
            if (!ancestor.Segments[index].Equals(path.Segments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private string NestedCollectionIcon(ConfigurationNodeDefinition child)
    {
        return child.NodeKind == ConfigurationNodeKind.Dictionary
            ? Icons.Material.Filled.DataObject
            : Icons.Material.Filled.FormatListBulleted;
    }

    private enum EditorMode
    {
        Visual,
        Patch
    }

    private sealed record CollectionEntry(
        string SelectionKey,
        string Title,
        string Subtitle,
        LogicalPath Path,
        bool IsSelected);

    private sealed record ObjectFieldSection(
        ConfigurationNodeDefinition Node,
        LogicalPath Path,
        IReadOnlyList<ConfigurationNodeDefinition> ScalarFields);

    private sealed record FocusFrame(
        ConfigurationNodeDefinition Node,
        LogicalPath Path,
        string? SelectedEntryKey);
}
