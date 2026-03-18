using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Monica.Authority.Identity.Abstractions;

namespace Monica.Authority.Identity.Services;

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
