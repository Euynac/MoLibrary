using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Test.Monica.Localization;

/// <summary>
/// Returns localization keys as visible values so UI assertions stay deterministic.
/// </summary>
public class EchoStringLocalizer<T> : IStringLocalizer<T>
{
    /// <inheritdoc />
    public virtual LocalizedString this[string name] => new(name, name, true);

    /// <inheritdoc />
    public virtual LocalizedString this[string name, params object[] arguments]
        => new(name, $"{name} [{string.Join(", ", arguments.Select(static argument => argument?.ToString() ?? string.Empty))}]", true);

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        => Array.Empty<LocalizedString>();

    /// <inheritdoc />
    public IStringLocalizer WithCulture(CultureInfo culture) => this;
}
