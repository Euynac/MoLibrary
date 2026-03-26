using System.Reflection;
using System.Security.Claims;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Annotations;
using Monica.Tool.Extensions;

namespace Monica.Authority.Authorization.Services.Support;

/// <summary>
/// Binary permission bit checker where the first enum member acts as a placeholder and is excluded from permissions
/// </summary>
public class PermissionBitChecker(PermissionBitCheckerManager manager) : IPermissionBitChecker
{
    private IPermissionBitChecker<TEnum> GetChecker<TEnum>() where TEnum : struct, Enum
    {
        if (!manager.TryGetChecker<TEnum>(out var checker))
        {
            throw new InvalidOperationException(
                $"Binary permission bit checker support for {typeof(TEnum).FullName} has not been loaded. Verify the module registration.");
        }
        return checker;
    }

    public bool IsInBits<TEnum>(string permissionBits, string permissionNameOfEnum) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsInBits(permissionBits, permissionNameOfEnum);
    }

    public bool IsInBits<TEnum>(string permissionBits, TEnum permissionEnum) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsInBits(permissionBits, permissionEnum);
    }

    public bool IsInBits<TEnum>(string permissionBits, params TEnum[] permissionEnums) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsInBits(permissionBits, permissionEnums);
    }

    public bool IsGranted<TEnum>(ClaimsPrincipal principal, string permissionNameOfEnum) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsGranted(principal, permissionNameOfEnum);
    }

    public bool IsGranted<TEnum>(ClaimsPrincipal principal, TEnum permissionEnum) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsGranted(principal, permissionEnum);
    }

    public bool IsGranted<TEnum>(ClaimsPrincipal principal, params TEnum[] permissionEnums) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().IsGranted(principal, permissionEnums);
    }

    public string GetPermissionBits<TEnum>(ClaimsPrincipal principal) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetPermissionBits(principal);
    }

    public string GetAdminPermissionBits<TEnum>() where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetAdminPermissionBits();
    }

    public Claim GetAdminClaim<TEnum>() where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetAdminClaim();
    }

    public string ToPermissionBits<TEnum>(List<TEnum> permissionEnums) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().ToPermissionBits(permissionEnums);
    }

    public List<TEnum> GrantedList<TEnum>(string permissionBits) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GrantedList(permissionBits);
    }

    public List<TEnum> GrantedList<TEnum>(ClaimsPrincipal principal) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GrantedList(principal);
    }

    public List<TEnum> GrantedList<TEnum>(string permissionBits, params TEnum[] permissionScope) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GrantedList(permissionBits, permissionScope);
    }

    public List<TEnum> GrantedList<TEnum>(ClaimsPrincipal principal, params TEnum[] permissionScope) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GrantedList(principal, permissionScope);
    }

    public Claim ToClaim<TEnum>(List<TEnum> permissionEnums) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().ToClaim(permissionEnums);
    }

    public Dictionary<TEnum, IPermissionBitData> GetAllBitData<TEnum>() where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetAllBitData();
    }

    public (TEnum, IPermissionBitData)? GetBitData<TEnum>(string key) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetBitData(key);
    }

    public IPermissionBitData? GetBitData<TEnum>(TEnum key) where TEnum : struct, Enum
    {
        return GetChecker<TEnum>().GetBitData(key);
    }
}
/// <summary>
/// Binary permission bit checker where the first enum member acts as a placeholder and is excluded from permissions
/// </summary>
/// <typeparam name="TEnum"></typeparam>
/// <param name="claimTypes"></param>
public class PermissionBitChecker<TEnum>(string claimTypes) : IPermissionBitChecker<TEnum> where TEnum : struct, Enum
{
    public bool IsInBits(string permissionBits, string permissionNameOfEnum)
    {
        return Enum.TryParse(permissionNameOfEnum, false, out TEnum permissionEnum) && IsInBits(permissionBits, permissionEnum);
    }

    public bool IsInBits(string permissionBits, TEnum permissionEnum)
    {
        var index = permissionEnum.ToInt() - 1;
        return permissionBits.ElementAtOrDefault(index) == '1';
    }

    public bool IsInBits(string permissionBits, params TEnum[] permissionEnums)
    {
        return permissionEnums.All(p => IsInBits(permissionBits, p));
    }

    public bool IsGranted(ClaimsPrincipal principal, string permissionNameOfEnum)
    {
        var bits = GetPermissionBits(principal);
        return IsInBits(bits, permissionNameOfEnum);
    }

    public bool IsGranted(ClaimsPrincipal principal, TEnum permissionEnum)
    {
        var bits = GetPermissionBits(principal);
        return IsInBits(bits, permissionEnum);
    }

    public bool IsGranted(ClaimsPrincipal principal, params TEnum[] permissionEnums)
    {
        var bits = GetPermissionBits(principal);
        return IsInBits(bits, permissionEnums);
    }

    public string GetPermissionBits(ClaimsPrincipal principal)
    {
        return principal.Claims.FirstOrDefault(p => p.Type == claimTypes)?.Value ?? "";
    }

    public string GetAdminPermissionBits()
    {
        var totalCount = Enum.GetValues<TEnum>().Length - 1;
        return totalCount == 0 ? "" : Enumerable.Repeat("1", totalCount).StringJoin("");
    }

    public Claim GetAdminClaim()
    {
        return new Claim(claimTypes, GetAdminPermissionBits());
    }

    public string ToPermissionBits(List<TEnum> permissionEnums)
    {
        var totalCount = Enum.GetValues<TEnum>().Length - 1;
        var bits = new int[totalCount];
        Array.Fill(bits, 0);
        foreach (var @enum in permissionEnums)
        {
            var index = @enum.ToInt() - 1;
            bits[index] = 1;
        }

        var bitsString = bits.StringJoin("");
        return bitsString;
    }

    public List<TEnum> GrantedList(string permissionBits)
    {
        return GrantedList(permissionBits, Enum.GetValues<TEnum>());
    }

    public List<TEnum> GrantedList(ClaimsPrincipal principal)
    {
        return GrantedList(GetPermissionBits(principal));
    }

    public List<TEnum> GrantedList(string permissionBits, params TEnum[] permissionScope)
    {
        var values = new List<TEnum>();
        foreach (var @enum in permissionScope)
        {
            if (@enum.ToInt() is { } index and not 0)
            {
                var bit = permissionBits.ElementAtOrDefault(index - 1);
                if (bit == default) return values;
                if (bit == '1') values.Add(@enum);
            }
        }
        return values;
    }

    public List<TEnum> GrantedList(ClaimsPrincipal principal, params TEnum[] permissionScope)
    {
        return GrantedList(GetPermissionBits(principal), permissionScope);
    }

    public Claim ToClaim(List<TEnum> permissionEnums)
    {
        return new Claim(claimTypes, ToPermissionBits(permissionEnums));
    }

    public Dictionary<TEnum, IPermissionBitData> GetAllBitData()
    {
        var dict = new Dictionary<TEnum, IPermissionBitData>();
        var enumValues = Enum.GetValues<TEnum>();
        foreach (var e in enumValues)
        {
            var data = GetAttribute(e);
            if (data == null) continue;
            dict.Add(e, data);
        }
        return dict;
    }

    private IPermissionBitData? GetAttribute(TEnum e)
    {
        return typeof(TEnum).GetField(e.ToString())!.GetCustomAttributes().OfType<IPermissionBitData>().FirstOrDefault();
    }

    public (TEnum, IPermissionBitData)? GetBitData(string key)
    {
        if (!Enum.TryParse(key, false, out TEnum permissionEnum)) return null;
        if (permissionEnum.ToInt() == 0) return null;
        var data = GetAttribute(permissionEnum);
        if (data == null)
            throw new InvalidOperationException(
                $"Enum value {typeof(TEnum).Name}.{permissionEnum} is missing the {nameof(IPermissionBitData)} attribute, so permission bit metadata cannot be read.");
        return (permissionEnum, data);
    }

    public IPermissionBitData? GetBitData(TEnum key)
    {
        if (key.ToInt() == 0) return null;
        var data = GetAttribute(key);
        if (data == null)
            throw new InvalidOperationException(
                $"Enum value {typeof(TEnum).Name}.{key} is missing the {nameof(IPermissionBitData)} attribute, so permission bit metadata cannot be read.");
        return data;
    }
}
