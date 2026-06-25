using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Models;
using Monica.Configuration.UI.State;
using Monica.Configuration.UI.Support;

namespace Monica.Configuration.UI.Dialogs;

/// <summary>
/// Dialog backing logic for structured object, dictionary, and list configuration editing.
/// </summary>
public partial class ComplexValueEditorDialog : IAsyncDisposable
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Parameter, EditorRequired] public ConfigurationDefinition Definition { get; set; } = null!;
    [Parameter, EditorRequired] public ConfigurationNodeDefinition Node { get; set; } = null!;
    [Parameter] public ConfigurationEffectiveValue? EffectiveValue { get; set; }
    [Parameter] public IReadOnlyList<PendingChange> PendingChanges { get; set; } = [];

    private readonly List<PendingChange> _changes = [];
    private readonly List<FocusFrame> _navigationStack = [];
    private readonly Dictionary<string, ScalarValidationError> _validationErrors = new(StringComparer.Ordinal);
    private JsonNode? _documentNode;
    private JsonNode? _originalDocumentNode;
    private ConfigurationNodeDefinition _focusNode = null!;
    private LogicalPath _focusPath = LogicalPath.Root;
    private EditorMode _mode = EditorMode.Visual;
    private string? _selectedEntryKey;
    private string _newEntryKey = string.Empty;
    private string? _newEntryError;
    private string? _pendingScrollEntryKey;
    private IJSObjectReference? _jsModule;
    private long? _valueVersion;
    private bool _startedWithScopedPendingChanges;

    private ConfigurationReloadBehavior EffectiveReloadBehavior => Node.ResolveEffectiveReloadBehavior(Definition);

    private Severity ReloadSeverity => EffectiveReloadBehavior.RequiresProcessRestart()
        ? Severity.Warning
        : Severity.Info;

    private string ReloadBehaviorMessage =>
        ConfigurationReloadBehaviorFormatter.Description(EffectiveReloadBehavior, L);

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

    private string ActivePathLabel => (_mode == EditorMode.Patch ? LogicalPath.Root : _focusPath).Depth == 0 && _mode == EditorMode.Patch
        ? L["Dialogs:ComplexEditor:PendingMutationGroup"]
        : _focusPath.ToCanonicalString();

    private string StageButtonText => L["Dialogs:ComplexEditor:Actions:StageCount", _changes.Count];

    private bool CanStage => (_changes.Count > 0 || _startedWithScopedPendingChanges) && _validationErrors.Count == 0;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_mode != EditorMode.Visual || string.IsNullOrWhiteSpace(_pendingScrollEntryKey))
        {
            return;
        }

        var entryKey = _pendingScrollEntryKey;
        _pendingScrollEntryKey = null;

        try
        {
            var module = await GetJsModuleAsync();
            await module.InvokeVoidAsync("scrollElementIntoView", EntryElementId(entryKey));
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void SetMode(EditorMode mode)
    {
        _mode = mode;
    }

    private async Task<IJSObjectReference> GetJsModuleAsync()
    {
        _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Monica.Configuration.UI/js/configuration-ui.js");
        return _jsModule;
    }

    private Variant ModeVariant(EditorMode mode)
    {
        return _mode == mode ? Variant.Filled : Variant.Outlined;
    }

    private Color ModeColor(EditorMode mode)
    {
        return _mode == mode ? Color.Primary : Color.Default;
    }

    private ConfigurationNodeDefinition ValueSchemaFor(CollectionEntry? entry)
    {
        return _focusNode.NodeKind switch
        {
            ConfigurationNodeKind.Dictionary => _focusNode.DictionaryTemplate!.ValueTemplate,
            ConfigurationNodeKind.List => _focusNode.ListTemplate!.ItemTemplate,
            _ => _focusNode
        };
    }

    private IReadOnlyList<ConfigurationNodeDefinition> EditableScalarFieldsFor(CollectionEntry? entry)
    {
        if (entry is null)
        {
            return [];
        }

        var valueSchema = ValueSchemaFor(entry);
        return valueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? [valueSchema]
            : valueSchema.Children.Where(child => child.NodeKind == ConfigurationNodeKind.Scalar).ToArray();
    }

    private IReadOnlyList<ConfigurationNodeDefinition> NestedCollectionsFor(CollectionEntry? entry)
    {
        if (entry is null)
        {
            return [];
        }

        var valueSchema = ValueSchemaFor(entry);
        return valueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? []
            : valueSchema.Children
                .Where(child => child.NodeKind is ConfigurationNodeKind.Dictionary or ConfigurationNodeKind.List)
                .Where(child => !child.IsScalarCollection())
                .ToArray();
    }

    private IReadOnlyList<ScalarCollectionSection> ScalarCollectionSectionsFor(CollectionEntry? entry)
    {
        if (entry is null)
        {
            return [];
        }

        var valueSchema = ValueSchemaFor(entry);
        return valueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? []
            : ScalarCollectionSectionsFor(valueSchema.Children, entry.Path);
    }

    private static IReadOnlyList<ScalarCollectionSection> ScalarCollectionSectionsFor(
        IReadOnlyList<ConfigurationNodeDefinition> children,
        LogicalPath ownerPath)
    {
        return children
            .Where(child => child.IsScalarCollection())
            .Select(child => new ScalarCollectionSection(
                child,
                ownerPath.Append(new PropertySegment(child.Name))))
            .ToArray();
    }

    private IReadOnlyList<ObjectFieldSection> NestedObjectSectionsFor(CollectionEntry? entry)
    {
        if (entry is null)
        {
            return [];
        }

        var valueSchema = ValueSchemaFor(entry);
        return valueSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? []
            : valueSchema.Children
                .Where(child => child.NodeKind == ConfigurationNodeKind.Object)
                .Select(child =>
                {
                    var objectPath = entry.Path.Append(new PropertySegment(child.Name));
                    return new ObjectFieldSection(
                        child,
                        objectPath,
                        child.Children.Where(grandchild => grandchild.NodeKind == ConfigurationNodeKind.Scalar).ToArray(),
                        ScalarCollectionSectionsFor(child.Children, objectPath));
                })
                .Where(section => section.ScalarFields.Count > 0 || section.ScalarCollections.Count > 0)
                .ToArray();
    }

    private string EntryEditorClass(CollectionEntry entry)
    {
        return entry.IsSelected
            ? "configuration-entry-editor-card configuration-entry-editor-card-selected"
            : "configuration-entry-editor-card";
    }

    private string EntryElementId(string selectionKey)
    {
        var identity = $"{_focusPath.ToCanonicalString()}::{selectionKey}";
        return $"configuration-entry-editor-{Convert.ToHexString(Encoding.UTF8.GetBytes(identity))}";
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
        var subtitle = string.Equals(title, key, StringComparison.Ordinal) ? string.Empty : key;
        return new CollectionEntry(key, title, subtitle, path, string.Equals(_selectedEntryKey, key, StringComparison.Ordinal));
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
        _pendingScrollEntryKey = key;
    }

    private void SelectFirstEntry()
    {
        _selectedEntryKey = CurrentEntries.FirstOrDefault()?.SelectionKey;
        _pendingScrollEntryKey = _selectedEntryKey;
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
        _pendingScrollEntryKey = key;
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
        _pendingScrollEntryKey = selectionKey;
        _newEntryKey = string.Empty;
        StageContainerSet(path, itemSchema, item);
    }

    private void RemoveEntry(CollectionEntry entry)
    {
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

        RemoveErrorsInScope(entry.Path);
        StageRemove(entry.Path, ValueSchemaFor(entry), oldValue);
        if (string.Equals(_selectedEntryKey, entry.SelectionKey, StringComparison.Ordinal) || SelectedEntry is null)
        {
            SelectFirstEntry();
        }
    }

    private void MoveListEntry(CollectionEntry entry, int direction)
    {
        if (_focusNode.NodeKind != ConfigurationNodeKind.List || ReadNode(_focusPath) is not JsonArray list)
        {
            return;
        }

        var index = FindListEntryIndex(list, entry.SelectionKey);
        var next = index + direction;
        if (index < 0 || next < 0 || next >= list.Count)
        {
            return;
        }

        var item = list[index];
        list.RemoveAt(index);
        list.Insert(next, item);
        _selectedEntryKey = _focusNode.ListTemplate?.SupportsPerItemMutation is true
            ? entry.SelectionKey
            : next.ToString(CultureInfo.InvariantCulture);
        _pendingScrollEntryKey = _selectedEntryKey;
        StageSet(_focusPath, _focusNode, CloneNode(ReadOriginalNode(_focusPath)), CloneNode(list));
    }

    private void OpenNestedCollection(CollectionEntry entry, ConfigurationNodeDefinition child)
    {
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

    private static LogicalPath FieldPath(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema)
    {
        return ownerSchema.NodeKind == ConfigurationNodeKind.Scalar
            ? ownerPath
            : ownerPath.Append(new PropertySegment(field.Name));
    }

    private Task OnScalarFieldChanged(
        ConfigurationNodeDefinition field,
        LogicalPath ownerPath,
        ConfigurationNodeDefinition ownerSchema,
        ConfigurationScalarEditResult result)
    {
        var path = FieldPath(field, ownerPath, ownerSchema);
        var displayValue = result.DisplayValue ?? string.Empty;
        if (field.IsSensitive && string.IsNullOrWhiteSpace(displayValue))
        {
            ClearError(path);
            return Task.CompletedTask;
        }

        if (!result.IsValid || result.StoredValue is null)
        {
            SetError(path, new ScalarValidationError(
                field.IsSensitive ? L["State:Value:Sensitive"].Value : displayValue,
                result.ValidationError ?? L["State:Editor:InvalidPattern"]));
            return Task.CompletedTask;
        }

        ClearError(path);
        SetFieldValue(field, ownerPath, ownerSchema, JsonNode.Parse(result.StoredValue.Json));
        return Task.CompletedTask;
    }

    private Task OnScalarCollectionChanged(
        ConfigurationNodeDefinition collection,
        LogicalPath path,
        ConfigurationScalarCollectionEditResult result)
    {
        if (!result.IsValid || result.StoredValue is null)
        {
            SetError(path, new ScalarValidationError(
                IsSensitiveNode(collection)
                    ? L["State:Value:Sensitive"].Value
                    : result.InvalidDisplayValue ?? result.DisplayValue ?? string.Empty,
                result.ValidationError ?? L["State:Editor:InvalidPattern"]));
            return Task.CompletedTask;
        }

        ClearError(path);
        SetCollectionValue(collection, path, JsonNode.Parse(result.StoredValue.Json));
        return Task.CompletedTask;
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

        StageSet(path, field, oldValue, valueForStorage);
    }

    private void SetCollectionValue(ConfigurationNodeDefinition collection, LogicalPath path, JsonNode? value)
    {
        var oldValue = CloneNode(ReadOriginalNode(path));
        var valueForStorage = CloneNode(value);
        SetNodeValue(path, value);

        StageSet(path, collection, oldValue, valueForStorage);
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
            IsSensitive = IsSensitiveNode(schema),
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

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            await _jsModule.DisposeAsync();
        }
    }

    private ConfigurationValidationIssue? ValidationIssueFor(ConfigurationNodeDefinition field, LogicalPath path)
    {
        return _validationErrors.TryGetValue(path.ToCanonicalString(), out var error)
            ? new ConfigurationValidationIssue
            {
                DefinitionKey = Definition.DefinitionKey,
                DefinitionDisplayName = Definition.DisplayName,
                LogicalPath = path,
                NodeDisplayName = DisplayName(field),
                InvalidDisplayValue = error.DisplayValue,
                ValidationError = error.Message,
                IsSensitive = IsSensitiveNode(field),
                ValidationRules = ValidationRulesFor(field)
            }
            : null;
    }

    private static bool ScalarEditorShowsInlineError(ConfigurationNodeDefinition field)
    {
        return field.ValueKind == ConfigurationValueKind.TimeSpan;
    }

    private bool IsFieldModified(LogicalPath path)
    {
        return _changes.Any(change => change.LogicalPath.Equals(path)) || IsValueModified(path);
    }

    private bool IsValueModified(LogicalPath path)
    {
        return !JsonNode.DeepEquals(ReadOriginalNode(path), ReadNode(path));
    }

    private ConfigurationNodeDefinition ScalarEditorNode(ConfigurationNodeDefinition field, LogicalPath path)
    {
        return field.RelativePath.Equals(path)
            ? field
            : field with
            {
                RelativePath = path,
                ConfigurationPath = null
            };
    }

    private ConfigurationEffectiveValue ScalarEditorEffectiveValue(ConfigurationNodeDefinition field, LogicalPath path)
    {
        return new ConfigurationEffectiveValue
        {
            DefinitionKey = Definition.DefinitionKey,
            LogicalPath = path,
            ConfigurationPath = field.ConfigurationPath,
            DisplayValue = field.IsSensitive ? null : ReadScalarAsString(ReadNode(path)),
            IsSensitive = field.IsSensitive,
            Version = _valueVersion,
            EffectiveSource = EffectiveValue?.EffectiveSource
        };
    }

    private ConfigurationNodeDefinition ScalarCollectionEditorNode(ConfigurationNodeDefinition collection, LogicalPath path)
    {
        return collection.RelativePath.Equals(path)
            ? collection
            : collection with
            {
                RelativePath = path,
                ConfigurationPath = null
            };
    }

    private ConfigurationEffectiveValue ScalarCollectionEffectiveValue(ConfigurationNodeDefinition collection, LogicalPath path)
    {
        var value = ReadNode(path) ?? DefaultJsonFor(collection);
        return new ConfigurationEffectiveValue
        {
            DefinitionKey = Definition.DefinitionKey,
            LogicalPath = path,
            ConfigurationPath = collection.ConfigurationPath,
            DisplayValue = IsSensitiveNode(collection) ? null : value.ToJsonString(),
            IsSensitive = IsSensitiveNode(collection),
            Version = _valueVersion,
            EffectiveSource = EffectiveValue?.EffectiveSource
        };
    }

    private PendingChange? ScalarCollectionPendingChange(ConfigurationNodeDefinition collection, LogicalPath path)
    {
        var exactChange = _changes.FirstOrDefault(change => change.LogicalPath.Equals(path));
        if (exactChange is not null)
        {
            return exactChange;
        }

        if (!IsFieldModified(path))
        {
            return null;
        }

        return BuildPendingChange(
            ConfigurationMutationKind.Set,
            path,
            collection,
            CloneNode(ReadOriginalNode(path)),
            CloneNode(ReadNode(path)) ?? DefaultJsonFor(collection));
    }

    private void SetError(LogicalPath path, ScalarValidationError error)
    {
        _validationErrors[path.ToCanonicalString()] = error;
    }

    private void ClearError(LogicalPath path)
    {
        _validationErrors.Remove(path.ToCanonicalString());
    }

    private void RemoveErrorsInScope(LogicalPath path)
    {
        var keys = _validationErrors.Keys
            .Where(key => IsPrefix(path, LogicalPath.Parse(key)))
            .ToArray();
        foreach (var key in keys)
        {
            _validationErrors.Remove(key);
        }
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

        if (IsSensitiveNode(schema))
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
        return schema.ResolveEffectiveReloadBehavior(Definition);
    }

    private string DisplayName(ConfigurationNodeDefinition schema)
    {
        return string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.Name : schema.DisplayName!;
    }

    private static bool IsSensitiveNode(ConfigurationNodeDefinition schema)
    {
        return schema.IsSensitive
               || schema.ListTemplate?.ItemTemplate.IsSensitive is true
               || schema.DictionaryTemplate?.ValueTemplate.IsSensitive is true;
    }

    private static IReadOnlyList<ConfigurationValidationRule> ValidationRulesFor(ConfigurationNodeDefinition schema)
    {
        return schema.ListTemplate?.ItemTemplate.ValidationRules
               ?? schema.DictionaryTemplate?.ValueTemplate.ValidationRules
               ?? schema.ValidationRules;
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
        IReadOnlyList<ConfigurationNodeDefinition> ScalarFields,
        IReadOnlyList<ScalarCollectionSection> ScalarCollections);

    private sealed record ScalarCollectionSection(
        ConfigurationNodeDefinition Node,
        LogicalPath Path);

    private sealed record FocusFrame(
        ConfigurationNodeDefinition Node,
        LogicalPath Path,
        string? SelectedEntryKey);

    private sealed record ScalarValidationError(string DisplayValue, string Message);
}
