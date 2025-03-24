using System.Security.Claims;
using MoLibrary.Authority.Authorization;
using MoLibrary.Authority.Security;

namespace MoLibrary.Authority.Implements.Authorization;

public class MoPermissionChecker<TEnum>(IMoCurrentPrincipalAccessor accessor, IPermissionBitChecker<TEnum> checker)
    : IMoPermissionChecker where TEnum : struct, Enum
{
    public Task<bool> IsGrantedAsync(string name)
    {
        return IsGrantedAsync(accessor.Principal, name);
    }

    public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name)
    {
        return Task.FromResult(claimsPrincipal != null && checker.IsGranted(claimsPrincipal, name));
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        return IsGrantedAsync(accessor.Principal, names);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names)
    {
        if (claimsPrincipal == null) return Task.FromResult(new MultiplePermissionGrantResult(names, EPermissionGrantResult.Prohibited));
        var bits = checker.GetPermissionBits(claimsPrincipal);
        var result = new MultiplePermissionGrantResult();
        foreach (var name in names)
        {
            result.Result.Add(name,
                checker.IsInBits(bits, name) ? EPermissionGrantResult.Granted : EPermissionGrantResult.Prohibited);
        }

        return Task.FromResult(result);
    }
}
