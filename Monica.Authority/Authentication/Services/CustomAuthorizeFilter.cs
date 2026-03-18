using Microsoft.AspNetCore.Mvc.Filters;

namespace Monica.Authority.Authentication.Services;

public class CustomAuthorizeFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity == null)
        {
            return;
        }

        if (!context.HttpContext.User.Identity.IsAuthenticated)
        {
            return;
        }
    }
}