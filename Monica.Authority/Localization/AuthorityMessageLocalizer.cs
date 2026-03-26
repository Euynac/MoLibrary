using Microsoft.Extensions.Localization;
using Monica.Authority.Authorization.Exceptions;

namespace Monica.Authority.Localization;

public class AuthorityMessageLocalizer(IStringLocalizer<AuthorityResource> localizer)
{
    public string GetNotLoggedInMessage()
    {
        return localizer["Messages:NotLoggedIn"].Value;
    }

    public string GetAccessTokenExpiredMessage()
    {
        return localizer["Messages:AccessTokenExpired"].Value;
    }

    public string GetRefreshTokenExpiredMessage()
    {
        return localizer["Messages:RefreshTokenExpired"].Value;
    }

    public string GetTokenExceptionMessage()
    {
        return localizer["Messages:TokenException"].Value;
    }

    public string GetInvalidTokenMessage()
    {
        return localizer["Messages:InvalidToken"].Value;
    }

    public string GetMissingPermissionMessage(string permissionName)
    {
        return localizer["Messages:MissingPermission", permissionName].Value;
    }

    public string GetCurrentSystemUserNotConfiguredMessage()
    {
        return localizer["Errors:CurrentSystemUserNotConfigured"].Value;
    }

    public string GetSystemUserEnumNotConfiguredMessage(object userEnum)
    {
        var enumType = userEnum.GetType();
        return localizer["Errors:SystemUserEnumNotConfigured", userEnum, enumType.FullName ?? enumType.Name].Value;
    }

    public string GetAuthorizationExceptionTitle(AuthorizationException.ExceptionType type)
    {
        var localized = localizer[$"AuthorizationExceptionTitles:{type}"];
        return localized.ResourceNotFound ? type.ToString() : localized.Value;
    }
}
