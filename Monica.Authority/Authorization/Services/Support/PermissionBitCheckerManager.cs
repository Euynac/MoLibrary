using System.Diagnostics.CodeAnalysis;
using Monica.Authority.Authorization.Abstractions;

namespace Monica.Authority.Authorization.Services.Support;

/// <summary>
/// Manager for binary permission bit checkers
/// </summary>
public class PermissionBitCheckerManager
{
    private static readonly Dictionary<Type, object> _dict = [];
    public static IPermissionBitChecker Singleton { get; internal set; } = null!;
    public static void AddChecker<TEnum>(IPermissionBitChecker<TEnum> checker) where TEnum : struct, Enum
    {
        _dict.Add(typeof(TEnum), checker);
    }

    public bool TryGetChecker<TEnum>([NotNullWhen(true)] out IPermissionBitChecker<TEnum>? checker) where TEnum : struct, Enum
    {
        checker = null;
        if (!_dict.TryGetValue(typeof(TEnum), out var obj)) return false;
        checker = (IPermissionBitChecker<TEnum>) obj;
        return true;
    }
}
