using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Monica.Authority.Identity.Abstractions;

namespace Monica.Authority.Identity.Services;

public class CurrentPrincipalAccessor(IHttpContextAccessor httpContextAccessor, ISystemUserManager systemUser) : ICurrentPrincipalAccessor
{
    protected virtual ClaimsPrincipal? GetClaimsPrincipal()
    {
        return httpContextAccessor.HttpContext?.User ?? Thread.CurrentPrincipal as ClaimsPrincipal;
    }

    // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
    public ClaimsPrincipal Principal => GetClaimsPrincipal() ?? systemUser.GetCurSystemUserPrinciple();
}
