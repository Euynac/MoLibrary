using Monica.Tool.MoResponse;

namespace Monica.Authority.Authorization.Services.Support;

public static class ResultsAuthorization
{
    public static Res NotLogin()
    {
        return new Res("用户未登录", ResponseCode.Unauthorized);
    }
    public static Res AccessTokenExpired(string? msg = null)
    {
        return new Res("用户访问凭证已过期", ResponseCode.AccessTokenExpired).AppendExtraInfo("detail", msg);
    }
    public static Res RefreshTokenExpired()
    {
        return new Res("用户刷新凭证已过期", ResponseCode.RefreshTokenExpired);
    }
}