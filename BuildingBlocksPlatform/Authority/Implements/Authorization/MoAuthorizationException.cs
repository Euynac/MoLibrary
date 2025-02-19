using System.ComponentModel;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocksPlatform.Authority.Implements.Authorization;

public class MoAuthorizationException : Exception
{
    public AuthorizationFailure? Failure { get; }
    public Exception? FailureException { get; }

    public string? Reason { get; set; }

    public ExceptionType Type { get; set; }
    public string Title => Type.GetDescription()!;

    public enum ExceptionType
    {
        [Description("未知异常")]
        Unknown = 0,
        [Description("权限不足")]
        PermissionDenied,
        [Description("用户未登录")]
        NotLogin,
        [Description("访问令牌已过期")]
        AccessTokenExpired,
        [Description("刷新令牌已过期")]
        RefreshTokenExpired,
        [Description("用户令牌异常")]
        TokenException
    }

    public MoAuthorizationException(ExceptionType type) : base($"权限异常：{type}")
    {
        Type = type;
    }

    public MoAuthorizationException(AuthorizationFailure? failure) : base("认证失败")
    {
        Type = ExceptionType.PermissionDenied;
        Failure = failure;
        if (Failure != null)
        {
            Reason = Failure.FailureReasons.Select(p => p.Message).StringJoin(",");
            Reason = Failure.FailedRequirements.Select(p => p.ToString()).StringJoin(",") is { } requirements &&
                     !string.IsNullOrWhiteSpace(requirements)
                ? $"{Reason.BeIfNotWhiteSpace($"{Reason};")}Requirements:{requirements}"
                : Reason;
            if (Failure.FailedRequirements.Any(p => p is DenyAnonymousAuthorizationRequirement))
            {
                Type = ExceptionType.NotLogin;
            }
        }

    }
    public MoAuthorizationException(Exception? failure) : base($"认证失败：{failure?.Message}")
    {
        FailureException = failure;
        if (failure != null)
        {
            switch (failure)
            {
                case SecurityTokenExpiredException expired:
                    Reason = expired.Message;
                    Type = ExceptionType.AccessTokenExpired;
                    break;
                case SecurityTokenArgumentException:
                case SecurityTokenException:
                    Reason = $"用户Token异常：[{failure.GetType()}]{failure.Message}";
                    Type = ExceptionType.TokenException;
                    break;
                default:
                    Reason = $"[{failure.GetType().Name}]{failure.Message}";
                    break;
            }
        }
    }
}