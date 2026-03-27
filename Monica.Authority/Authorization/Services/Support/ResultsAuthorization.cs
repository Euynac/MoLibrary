using Monica.Authority.Localization;
using Monica.Tool.Results;

namespace Monica.Authority.Authorization.Services.Support;

public static class ResultsAuthorization
{
    public static Res NotLogin(AuthorityMessageLocalizer authorityLocalizer)
    {
        return new Res(authorityLocalizer.GetNotLoggedInMessage(), ResStatus.Unauthorized);
    }
    public static Res AccessTokenExpired(AuthorityMessageLocalizer authorityLocalizer, string? msg = null)
    {
        return new Res(authorityLocalizer.GetAccessTokenExpiredMessage(), ResStatus.AccessTokenExpired)
            .AppendExtraInfo("detail", msg);
    }
    public static Res RefreshTokenExpired(AuthorityMessageLocalizer authorityLocalizer)
    {
        return new Res(authorityLocalizer.GetRefreshTokenExpiredMessage(), ResStatus.RefreshTokenExpired);
    }
}
