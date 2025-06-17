using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MoLibrary.Authority.Security;

namespace MoLibrary.Authority.Implements.Security;

public class MoCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor, IMoSystemUserManager systemUser) : IMoCurrentPrincipalAccessor
{
    private static readonly AsyncLocal<ClaimsPrincipal> _currentPrincipal = new();

    protected virtual ClaimsPrincipal? GetClaimsPrincipal()
    {
        return httpContextAccessor.HttpContext?.User ?? Thread.CurrentPrincipal as ClaimsPrincipal;
    }

    // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
    public ClaimsPrincipal Principal => _currentPrincipal.Value ?? GetClaimsPrincipal() ?? systemUser.GetCurSystemUserPrinciple();
}
