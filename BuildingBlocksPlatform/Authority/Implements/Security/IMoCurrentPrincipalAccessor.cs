using System.Security.Claims;
using BuildingBlocksPlatform.Authority.Security;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.Authority.Implements.Security;

public class MoCurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor, IMoSystemUserManager systemUser) : IMoCurrentPrincipalAccessor
{
    private readonly AsyncLocal<ClaimsPrincipal> _currentPrincipal = new();

    protected virtual ClaimsPrincipal? GetClaimsPrincipal()
    {
        return httpContextAccessor.HttpContext?.User ?? Thread.CurrentPrincipal as ClaimsPrincipal;
    }

    // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
    public ClaimsPrincipal Principal => _currentPrincipal.Value ?? GetClaimsPrincipal() ?? systemUser.GetCurSystemUserPrinciple();
}
