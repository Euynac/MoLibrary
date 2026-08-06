using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Monica.Core.Modularity.Diagnostics.Annotations;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Services.Support;

/// <summary>
/// Projects finalized option objects into a bounded immutable graph without retaining live configuration objects.
/// </summary>
internal sealed class ModuleOptionDiagnosticsProjector
{
    private const int MAX_DEPTH = 4;
    private const int MAX_ENTRIES = 200;
    private const int MAX_COLLECTION_ITEMS = 20;
    private const int MAX_VALUE_LENGTH = 256;
    private static readonly ConcurrentDictionary<Type, ImmutableArray<OptionPropertyDescriptor>> PROPERTY_CACHE = new();

    internal ModuleOptionDiagnostics Project(
        ModuleKey moduleKey,
        string? requestedProfileName,
        string? effectiveProfileName,
        ModuleOptionProfileResolution profileResolution,
        Type optionType,
        object options,
        ModuleOptionDiagnosticsExposureMode exposureMode,
        ModuleOptionDiagnosticsPolicyDefinition policy)
    {
        ArgumentNullException.ThrowIfNull(optionType);
        ArgumentNullException.ThrowIfNull(options);
        if (exposureMode is not ModuleOptionDiagnosticsExposureMode.Redacted
            and not ModuleOptionDiagnosticsExposureMode.RevealSensitive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exposureMode),
                exposureMode,
                "Module option diagnostics exposure mode must be a defined value.");
        }

        if (!optionType.IsInstanceOfType(options))
        {
            throw new InvalidOperationException(
                $"Module option instance is not assignable to {optionType.FullName}.");
        }

        var context = new ProjectionContext(optionType.Assembly, exposureMode, policy);
        context.ActiveReferences.Add(options);
        var entries = ProjectProperties(options, optionType, string.Empty, 0, false, context).Children;

        return new ModuleOptionDiagnostics
        {
            ModuleKey = moduleKey,
            OptionTypeName = optionType.FullName ?? optionType.Name,
            RequestedProfileName = requestedProfileName,
            ProfileName = effectiveProfileName,
            ProfileResolution = profileResolution,
            ExposureMode = exposureMode,
            IsFinalized = true,
            Entries = entries,
            SensitiveEntryCount = context.SensitiveEntryCount,
            IsTruncated = context.IsTruncated,
            ContainsRevealedSensitiveValues = context.ContainsRevealedSensitiveValues
        };
    }

    private static PropertyProjection ProjectProperties(
        object source,
        Type sourceType,
        string parentPath,
        int depth,
        bool inheritedSensitivity,
        ProjectionContext context)
    {
        var entries = ImmutableArray.CreateBuilder<ModuleOptionDiagnosticEntry>();
        var wasInterrupted = false;
        foreach (var descriptor in GetPublicProperties(sourceType))
        {
            var property = descriptor.Property;
            var path = JoinPath(parentPath, property.Name);
            if (!context.TryReservePropertyEntry(depth))
            {
                wasInterrupted = true;
                break;
            }

            var isSensitive = inheritedSensitivity
                              || context.Policy.SensitivePaths.Contains(path)
                              || property.IsDefined(
                                  typeof(ModuleOptionDiagnosticsSensitiveAttribute),
                                  inherit: true)
                              || IsSensitiveName(property.Name, property.PropertyType);
            if (descriptor.BackingField is null)
            {
                context.RegisterSensitiveEntry(isSensitive);
                entries.Add(CreateEntry(
                    property.Name,
                    path,
                    property.PropertyType,
                    ModuleOptionDiagnosticValueKind.Unsupported,
                    isSensitive: isSensitive));
                continue;
            }

            object? value;
            try
            {
                value = descriptor.BackingField.GetValue(source);
            }
            catch
            {
                context.RegisterSensitiveEntry(isSensitive);
                entries.Add(CreateEntry(
                    property.Name,
                    path,
                    property.PropertyType,
                    ModuleOptionDiagnosticValueKind.Unavailable,
                    isSensitive: isSensitive));
                continue;
            }

            isSensitive |= IsSensitiveAddress(property.Name, value);
            entries.Add(ProjectValue(
                property.Name,
                path,
                property.PropertyType,
                value,
                depth,
                isSensitive,
                context));
        }

        if (wasInterrupted)
        {
            context.MarkTruncated();
        }

        return new PropertyProjection(entries.ToImmutable(), wasInterrupted);
    }

    private static ModuleOptionDiagnosticEntry ProjectValue(
        string name,
        string path,
        Type declaredType,
        object? value,
        int depth,
        bool isSensitive,
        ProjectionContext context)
    {
        context.RegisterSensitiveEntry(isSensitive);

        if (value is null)
        {
            return CreateEntry(
                name,
                path,
                declaredType,
                isSensitive && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted
                    ? ModuleOptionDiagnosticValueKind.Presence
                    : ModuleOptionDiagnosticValueKind.Value,
                isPresent: isSensitive && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted
                    ? false
                    : null,
                isNull: !isSensitive || context.ExposureMode != ModuleOptionDiagnosticsExposureMode.Redacted,
                isSensitive: isSensitive);
        }

        var runtimeType = value.GetType();
        if (IsRuntimeBoundaryType(declaredType) || IsRuntimeBoundaryType(runtimeType))
        {
            return CreateEntry(
                name,
                path,
                declaredType,
                isSensitive && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted
                    ? ModuleOptionDiagnosticValueKind.Presence
                    : ModuleOptionDiagnosticValueKind.Unsupported,
                isPresent: true,
                isSensitive: isSensitive);
        }

        if (TryFormatScalar(value, out var scalar))
        {
            if (isSensitive && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted)
            {
                if (TryGetSensitiveAddress(name, value, out var protectedAddress))
                {
                    var (boundedAddress, addressWasTruncated) = Bound(protectedAddress);
                    return CreateEntry(
                        name,
                        path,
                        declaredType,
                        ModuleOptionDiagnosticValueKind.Value,
                        value: boundedAddress,
                        isTruncated: addressWasTruncated,
                        isSensitive: true);
                }

                return CreateEntry(
                    name,
                    path,
                    declaredType,
                    ModuleOptionDiagnosticValueKind.Presence,
                    isPresent: IsPresent(value),
                    isSensitive: true);
            }

            var (bounded, isTruncated) = Bound(scalar);
            if (isSensitive
                && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.RevealSensitive
                && IsPresent(value))
            {
                context.ContainsRevealedSensitiveValues = true;
            }

            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Value,
                value: bounded,
                isTruncated: isTruncated,
                isSensitive: isSensitive);
        }

        bool isSafeCollection;
        int count;
        IEnumerable items;
        try
        {
            isSafeCollection = TryGetSafeCollection(value, out count, out items);
        }
        catch (Exception)
        {
            context.MarkTruncated();
            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Unavailable,
                isTruncated: true,
                isSensitive: isSensitive);
        }

        if (isSafeCollection)
        {
            if (isSensitive && context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted)
            {
                return CreateEntry(
                    name,
                    path,
                    declaredType,
                    ModuleOptionDiagnosticValueKind.Count,
                    count: count,
                    isSensitive: true);
            }

            if (depth >= MAX_DEPTH)
            {
                if (count > 0)
                {
                    context.MarkTruncated();
                }

                return CreateEntry(
                    name,
                    path,
                    declaredType,
                    ModuleOptionDiagnosticValueKind.Collection,
                    count: count,
                    isTruncated: count > 0,
                    isSensitive: isSensitive);
            }

            var collection = ProjectCollectionItems(path, items, depth + 1, isSensitive, context);
            var isTruncated = collection.WasInterrupted || count > collection.Children.Length;
            if (isTruncated)
            {
                context.MarkTruncated();
            }

            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Collection,
                count: count,
                isTruncated: isTruncated,
                isSensitive: isSensitive,
                children: collection.Children);
        }

        if (depth >= MAX_DEPTH || !CanInspectObject(runtimeType, context.RootOptionAssembly))
        {
            if (depth >= MAX_DEPTH)
            {
                context.MarkTruncated();
            }

            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Unsupported,
                isPresent: true,
                isTruncated: depth >= MAX_DEPTH,
                isSensitive: isSensitive);
        }

        if (!runtimeType.IsValueType && !context.ActiveReferences.Add(value))
        {
            context.MarkTruncated();
            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Unsupported,
                isPresent: true,
                isTruncated: true,
                isSensitive: isSensitive);
        }

        try
        {
            var projection = ProjectProperties(value, runtimeType, path, depth + 1, isSensitive, context);
            return CreateEntry(
                name,
                path,
                declaredType,
                ModuleOptionDiagnosticValueKind.Object,
                isTruncated: projection.WasInterrupted,
                isSensitive: isSensitive,
                children: projection.Children);
        }
        finally
        {
            if (!runtimeType.IsValueType)
            {
                context.ActiveReferences.Remove(value);
            }
        }
    }

    private static CollectionProjection ProjectCollectionItems(
        string parentPath,
        IEnumerable items,
        int depth,
        bool inheritedSensitivity,
        ProjectionContext context)
    {
        var children = ImmutableArray.CreateBuilder<ModuleOptionDiagnosticEntry>();
        var index = 0;
        var wasInterrupted = false;
        var readFailed = false;
        var failureEntryReserved = false;
        IEnumerator? enumerator = null;
        try
        {
            enumerator = items.GetEnumerator();
            while (index < MAX_COLLECTION_ITEMS)
            {
                bool hasNext;
                try
                {
                    hasNext = enumerator.MoveNext();
                }
                catch (Exception)
                {
                    wasInterrupted = true;
                    readFailed = true;
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                if (!context.TryReserveEntry())
                {
                    wasInterrupted = true;
                    break;
                }

                failureEntryReserved = true;
                try
                {
                    var item = enumerator.Current;
                    if (TryReadDictionaryEntry(item, out var key, out var dictionaryValue))
                    {
                        var classificationKey = FormatCollectionKey(key, index);
                        var keyText = context.ExposureMode == ModuleOptionDiagnosticsExposureMode.Redacted
                            ? index.ToString(CultureInfo.InvariantCulture)
                            : classificationKey;
                        var itemPath = $"{parentPath}[{keyText}]";
                        var itemType = dictionaryValue?.GetType() ?? typeof(object);
                        children.Add(ProjectValue(
                            $"[{keyText}]",
                            itemPath,
                            itemType,
                            dictionaryValue,
                            depth,
                            inheritedSensitivity || IsSensitiveName(classificationKey, itemType),
                            context));
                    }
                    else
                    {
                        var itemPath = $"{parentPath}[{index}]";
                        var itemType = item?.GetType() ?? typeof(object);
                        children.Add(ProjectValue(
                            $"[{index}]",
                            itemPath,
                            itemType,
                            item,
                            depth,
                            inheritedSensitivity,
                            context));
                    }

                    failureEntryReserved = false;
                    index++;
                }
                catch (Exception)
                {
                    wasInterrupted = true;
                    readFailed = true;
                    break;
                }
            }
        }
        catch (Exception)
        {
            wasInterrupted = true;
            readFailed = true;
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception)
                {
                    wasInterrupted = true;
                    readFailed = true;
                }
            }
        }

        if (wasInterrupted)
        {
            context.MarkTruncated();
        }

        if (readFailed)
        {
            var unavailable = CreateEntry(
                "[unavailable]",
                $"{parentPath}[unavailable]",
                typeof(object),
                ModuleOptionDiagnosticValueKind.Unavailable,
                isTruncated: true,
                isSensitive: inheritedSensitivity);
            if (failureEntryReserved
                || (children.Count < MAX_COLLECTION_ITEMS && context.TryReserveEntry()))
            {
                children.Add(unavailable);
            }
            else if (children.Count > 0)
            {
                children[^1] = unavailable;
            }
        }

        return new CollectionProjection(children.ToImmutable(), wasInterrupted);
    }

    private static ImmutableArray<OptionPropertyDescriptor> GetPublicProperties(Type type) =>
        PROPERTY_CACHE.GetOrAdd(type, static sourceType => sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property =>
                (property.GetMethod is { IsPublic: true } || property.SetMethod is { IsPublic: true })
                && property.GetIndexParameters().Length == 0)
            .OrderBy(static property => property.DeclaringType == property.ReflectedType ? 1 : 0)
            .ThenBy(static property => property.MetadataToken)
            .Select(static property => new OptionPropertyDescriptor(
                property,
                FindAutoPropertyBackingField(property)))
            .ToImmutableArray());

    private static FieldInfo? FindAutoPropertyBackingField(PropertyInfo property)
    {
        var declaringType = property.DeclaringType;
        if (declaringType is null)
        {
            return null;
        }

        var backingField = declaringType.GetField(
            $"<{property.Name}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return backingField?.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) == true
            ? backingField
            : null;
    }

    private static bool TryFormatScalar(object value, out string representation)
    {
        switch (value)
        {
            case string text:
                representation = text;
                return true;
            case char character:
                representation = character.ToString();
                return true;
            case bool flag:
                representation = flag ? "true" : "false";
                return true;
            case Enum enumValue:
                representation = enumValue.ToString();
                return true;
            case DateTime dateTime:
                representation = dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return true;
            case DateTimeOffset dateTimeOffset:
                representation = dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                return true;
            case DateOnly dateOnly:
                representation = dateOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeOnly timeOnly:
                representation = timeOnly.ToString("O", CultureInfo.InvariantCulture);
                return true;
            case TimeSpan timeSpan:
                representation = timeSpan.ToString("c", CultureInfo.InvariantCulture);
                return true;
            case Guid guid:
                representation = guid.ToString("D", CultureInfo.InvariantCulture);
                return true;
            case Uri uri:
                representation = uri.ToString();
                return true;
            case TimeZoneInfo timeZone:
                representation = timeZone.Id;
                return true;
            case Type type:
                representation = type.FullName ?? type.Name;
                return true;
            case Version version:
                representation = version.ToString();
                return true;
            case IPAddress address:
                representation = address.ToString();
                return true;
            case IFormattable formattable when IsNumericType(value.GetType()):
                representation = formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
                return true;
            default:
                representation = string.Empty;
                return false;
        }
    }

    private static bool TryGetSafeCollection(object value, out int count, out IEnumerable items)
    {
        if (value is Array array && value is not byte[] && value is not char[])
        {
            count = array.Length;
            items = array;
            return true;
        }

        var runtimeType = value.GetType();
        var assemblyName = runtimeType.Assembly.GetName().Name ?? string.Empty;
        if (value is ICollection collection
            && (assemblyName.StartsWith("System.", StringComparison.Ordinal)
                || assemblyName is "System.Private.CoreLib" or "mscorlib"))
        {
            count = collection.Count;
            items = collection;
            return true;
        }

        if (value is IEnumerable enumerable
            && (assemblyName.StartsWith("System.", StringComparison.Ordinal)
                || assemblyName is "System.Private.CoreLib" or "mscorlib"))
        {
            var collectionInterface = runtimeType.GetInterfaces().FirstOrDefault(static contract =>
                contract.IsGenericType
                && contract.GetGenericTypeDefinition() is var definition
                && (definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>)));
            if (collectionInterface?.GetProperty(nameof(ICollection<object>.Count))?.GetValue(value) is int genericCount)
            {
                count = genericCount;
                items = enumerable;
                return true;
            }
        }

        count = 0;
        items = Array.Empty<object>();
        return false;
    }

    private static bool TryReadDictionaryEntry(object? item, out object? key, out object? value)
    {
        if (item is DictionaryEntry entry)
        {
            key = entry.Key;
            value = entry.Value;
            return true;
        }

        if (item is not null)
        {
            var type = item.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                key = type.GetProperty(nameof(KeyValuePair<object, object>.Key))!.GetValue(item);
                value = type.GetProperty(nameof(KeyValuePair<object, object>.Value))!.GetValue(item);
                return true;
            }
        }

        key = null;
        value = null;
        return false;
    }

    private static bool CanInspectObject(Type type, Assembly rootOptionAssembly)
    {
        if (IsRuntimeBoundaryType(type) || typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }

        if (GetPublicProperties(type).IsEmpty)
        {
            return false;
        }

        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        return type.Assembly == rootOptionAssembly
               || assemblyName.StartsWith("Monica.", StringComparison.Ordinal)
               || HasConfigurationTypeName(type.Name);
    }

    private static bool HasConfigurationTypeName(string typeName) =>
        typeName.EndsWith("Option", StringComparison.Ordinal)
        || typeName.EndsWith("Options", StringComparison.Ordinal)
        || typeName.EndsWith("Settings", StringComparison.Ordinal)
        || typeName.EndsWith("Configuration", StringComparison.Ordinal);

    private static bool IsRuntimeBoundaryType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return typeof(Delegate).IsAssignableFrom(type)
               || typeof(Stream).IsAssignableFrom(type)
               || typeof(Task).IsAssignableFrom(type)
               || typeof(IServiceProvider).IsAssignableFrom(type)
               || IsKeyMaterialContainer(type)
               || type.FullName?.StartsWith("System.Security.Cryptography.", StringComparison.Ordinal) == true
               || type.FullName?.StartsWith("System.Security.Cryptography.X509Certificates.", StringComparison.Ordinal) == true;
    }

    private static bool IsKeyMaterialContainer(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return type.GetElementType() is var elementType
                   && (elementType == typeof(byte) || elementType == typeof(char));
        }

        return TryGetGenericContract(type, typeof(IEnumerable<>), out var arguments)
               && arguments[0] is var itemType
               && (itemType == typeof(byte) || itemType == typeof(char));
    }

    private static bool IsSensitiveName(string name, Type declaredType)
    {
        var canonicalName = string.Concat(name.Where(char.IsLetterOrDigit));
        if (!HasSensitiveName(canonicalName))
        {
            return false;
        }

        return IsKeyMaterialContainer(declaredType) || CanContainSensitiveText(declaredType);
    }

    private static bool HasSensitiveName(string canonicalName) =>
        EndsWithEither(canonicalName, "Password", "Passwords")
        || EndsWithEither(canonicalName, "Passphrase", "Passphrases")
        || EndsWithEither(canonicalName, "Secret", "Secrets")
        || EndsWithEither(canonicalName, "Token", "Tokens")
        || EndsWithEither(canonicalName, "ApiKey", "ApiKeys")
        || EndsWithEither(canonicalName, "AccessKey", "AccessKeys")
        || EndsWithEither(canonicalName, "SecretKey", "SecretKeys")
        || EndsWithEither(canonicalName, "PrivateKey", "PrivateKeys")
        || EndsWithEither(canonicalName, "SigningKey", "SigningKeys")
        || EndsWithEither(canonicalName, "EncryptionKey", "EncryptionKeys")
        || EndsWithEither(canonicalName, "SecurityKey", "SecurityKeys")
        || EndsWithEither(canonicalName, "KeyMaterial", "KeyMaterials")
        || EndsWithEither(canonicalName, "ConnectionString", "ConnectionStrings")
        || EndsWithEither(canonicalName, "Authorization", "Authorizations")
        || EndsWithEither(canonicalName, "Cookie", "Cookies")
        || EndsWithEither(canonicalName, "Credential", "Credentials")
        || EndsWithEither(canonicalName, "SystemPrompt", "SystemPrompts")
        || canonicalName.EndsWith("ClientSecretOptions", StringComparison.OrdinalIgnoreCase);

    private static bool EndsWithEither(string value, string singular, string plural) =>
        value.EndsWith(singular, StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(plural, StringComparison.OrdinalIgnoreCase);

    private static bool CanContainSensitiveText(Type declaredType)
    {
        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (type == typeof(string) || type == typeof(Uri) || type == typeof(object))
        {
            return true;
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return elementType is not null
                   && elementType != typeof(byte)
                   && elementType != typeof(char)
                   && CanContainSensitiveText(elementType);
        }

        if (TryGetGenericContract(type, typeof(IDictionary<,>), out var dictionaryArguments)
            || TryGetGenericContract(type, typeof(IReadOnlyDictionary<,>), out dictionaryArguments))
        {
            return CanContainSensitiveText(dictionaryArguments[0])
                   || CanContainSensitiveText(dictionaryArguments[1]);
        }

        if (TryGetGenericContract(type, typeof(IEnumerable<>), out var enumerableArguments))
        {
            return CanContainSensitiveText(enumerableArguments[0]);
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            return true;
        }

        return !type.IsValueType;
    }

    private static bool TryGetGenericContract(Type type, Type genericDefinition, out Type[] arguments)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
        {
            arguments = type.GetGenericArguments();
            return true;
        }

        var contract = type.GetInterfaces().FirstOrDefault(candidate =>
            candidate.IsGenericType && candidate.GetGenericTypeDefinition() == genericDefinition);
        if (contract is not null)
        {
            arguments = contract.GetGenericArguments();
            return true;
        }

        arguments = [];
        return false;
    }

    private static bool IsSensitiveAddress(string name, object? value) =>
        TryGetSensitiveAddress(name, value, out _);

    private static bool TryGetSensitiveAddress(string name, object? value, out string protectedAddress)
    {
        if (value is Uri configuredUri && IsSensitiveUri(configuredUri))
        {
            protectedAddress = SanitizeUri(configuredUri);
            return true;
        }

        if (value is string text
            && IsAddressName(name))
        {
            if (Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var parsed)
                && IsSensitiveUri(parsed))
            {
                protectedAddress = SanitizeUri(parsed);
                return true;
            }

            if (TrySanitizeScpCredential(text, out protectedAddress))
            {
                return true;
            }
        }

        protectedAddress = string.Empty;
        return false;
    }

    private static bool IsAddressName(string name) =>
        name.EndsWith("Url", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("Uri", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase);

    private static bool IsSensitiveUri(Uri uri) =>
        uri.IsAbsoluteUri
            ? !string.IsNullOrEmpty(uri.UserInfo)
              || !string.IsNullOrEmpty(uri.Query)
              || !string.IsNullOrEmpty(uri.Fragment)
            : uri.ToString().Contains('?', StringComparison.Ordinal)
              || uri.ToString().Contains('#', StringComparison.Ordinal);

    private static bool TrySanitizeScpCredential(string value, out string protectedAddress)
    {
        protectedAddress = string.Empty;
        // SCP-like Git addresses are not URI user-info, so detect their user:password@host shape separately.
        if (value.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        var atIndex = value.IndexOf('@', StringComparison.Ordinal);
        var credentialSeparatorIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (credentialSeparatorIndex <= 0 || atIndex <= credentialSeparatorIndex + 1)
        {
            return false;
        }

        var userName = value[..credentialSeparatorIndex];
        var password = value[(credentialSeparatorIndex + 1)..atIndex];
        if (userName.Any(char.IsWhiteSpace)
            || password.Any(character => char.IsWhiteSpace(character) || character is '/' or '\\'))
        {
            return false;
        }

        protectedAddress = $"{userName}:redacted{value[atIndex..]}";
        return true;
    }

    private static string SanitizeUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            var text = uri.ToString();
            var queryIndex = text.IndexOf('?', StringComparison.Ordinal);
            var fragmentIndex = text.IndexOf('#', StringComparison.Ordinal);
            if (queryIndex >= 0 && (fragmentIndex < 0 || queryIndex < fragmentIndex))
            {
                return $"{text[..queryIndex]}?redacted";
            }

            return fragmentIndex < 0 ? text : text[..fragmentIndex];
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.IsNullOrEmpty(uri.Query)
                ? string.Empty
                : string.Join('&', uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(static part => $"{part.Split('=', 2)[0]}=redacted")),
            Fragment = string.Empty
        };
        return builder.Uri.ToString();
    }

    private static bool IsPresent(object? value) => value switch
    {
        null => false,
        string text => !string.IsNullOrWhiteSpace(text),
        Array array => array.Length != 0,
        ICollection collection => collection.Count != 0,
        _ => true
    };

    private static bool IsNumericType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(byte)
               || type == typeof(sbyte)
               || type == typeof(short)
               || type == typeof(ushort)
               || type == typeof(int)
               || type == typeof(uint)
               || type == typeof(long)
               || type == typeof(ulong)
               || type == typeof(float)
               || type == typeof(double)
               || type == typeof(decimal);
    }

    private static string FormatCollectionKey(object? key, int fallbackIndex)
    {
        if (key is null)
        {
            return fallbackIndex.ToString(CultureInfo.InvariantCulture);
        }

        var text = key switch
        {
            string stringKey => stringKey,
            Enum enumKey => enumKey.ToString(),
            Guid guidKey => guidKey.ToString("D", CultureInfo.InvariantCulture),
            IFormattable formattable when IsNumericType(key.GetType()) =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => fallbackIndex.ToString(CultureInfo.InvariantCulture)
        } ?? fallbackIndex.ToString(CultureInfo.InvariantCulture);
        return Bound(text).Value;
    }

    private static (string Value, bool IsTruncated) Bound(string value)
    {
        if (value.Length <= MAX_VALUE_LENGTH)
        {
            return (value, false);
        }

        return (value[..MAX_VALUE_LENGTH], true);
    }

    private static string JoinPath(string parentPath, string name) =>
        parentPath.Length == 0 ? name : $"{parentPath}.{name}";

    private static ModuleOptionDiagnosticEntry CreateEntry(
        string name,
        string path,
        Type type,
        ModuleOptionDiagnosticValueKind kind,
        string? value = null,
        bool? isPresent = null,
        int? count = null,
        bool isNull = false,
        bool isTruncated = false,
        bool isSensitive = false,
        ImmutableArray<ModuleOptionDiagnosticEntry> children = default) => new()
        {
            Name = name,
            Path = path,
            TypeName = FormatTypeName(type),
            Kind = kind,
            Value = value,
            IsPresent = isPresent,
            Count = count,
            IsNull = isNull,
            IsTruncated = isTruncated,
            IsSensitive = isSensitive,
            Children = children.IsDefault ? [] : children
        };

    private static string FormatTypeName(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        return underlyingType is null
            ? type.FullName ?? type.Name
            : $"{underlyingType.FullName ?? underlyingType.Name}?";
    }

    private sealed class ProjectionContext(
        Assembly rootOptionAssembly,
        ModuleOptionDiagnosticsExposureMode exposureMode,
        ModuleOptionDiagnosticsPolicyDefinition policy)
    {
        internal Assembly RootOptionAssembly { get; } = rootOptionAssembly;

        internal ModuleOptionDiagnosticsExposureMode ExposureMode { get; } = exposureMode;

        internal ModuleOptionDiagnosticsPolicyDefinition Policy { get; } = policy;

        internal HashSet<object> ActiveReferences { get; } = new(ReferenceEqualityComparer.Instance);

        internal int EntryCount { get; private set; }

        internal int SensitiveEntryCount { get; set; }

        internal bool IsTruncated { get; private set; }

        internal bool ContainsRevealedSensitiveValues { get; set; }

        internal void RegisterSensitiveEntry(bool isSensitive)
        {
            if (isSensitive)
            {
                SensitiveEntryCount++;
            }
        }

        internal bool TryReservePropertyEntry(int depth)
        {
            // Root properties are the configuration catalog and must never disappear behind a value-projection bound.
            if (depth == 0)
            {
                if (EntryCount >= MAX_ENTRIES)
                {
                    IsTruncated = true;
                }

                EntryCount++;
                return true;
            }

            return TryReserveEntry();
        }

        internal bool TryReserveEntry()
        {
            if (EntryCount >= MAX_ENTRIES)
            {
                IsTruncated = true;
                return false;
            }

            EntryCount++;
            return true;
        }

        internal void MarkTruncated() => IsTruncated = true;
    }

    private readonly record struct CollectionProjection(
        ImmutableArray<ModuleOptionDiagnosticEntry> Children,
        bool WasInterrupted);

    private readonly record struct PropertyProjection(
        ImmutableArray<ModuleOptionDiagnosticEntry> Children,
        bool WasInterrupted);

    private readonly record struct OptionPropertyDescriptor(
        PropertyInfo Property,
        FieldInfo? BackingField);
}
