namespace MoLibrary.Core.GlobalJson.Interfaces;

public interface IJudgeBackendInvoke : IHasHttpContextAccessor
{
    public const string X_BACKEND_INVOKE = "X-Backend-Invoke";
    /// <summary>
    /// 用于区分前后端调用。
    /// </summary>
    /// <returns></returns>
    public bool IsBackendInvoke()
    {
        if (HttpContextAccessor?.HttpContext is { } context)
        {
            return context.Request.Headers.ContainsKey(X_BACKEND_INVOKE);
        }

        return true;
    }
}