using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Net;
using System.Text;
using Monica.Tool.Extensions;

namespace Monica.Tool.Results;

public static class ResExtensions
{
    /// <summary>
    /// Get the HttpStatusCode corresponding to the response code
    /// </summary>
    /// <returns></returns>
    public static HttpStatusCode? ToHttpStatusCode(this IResultEnvelope? response)
    {
        if (response == null) return null;
        switch (response.Status)
        {
            case ResStatus.Ok:
                return HttpStatusCode.OK;


            case ResStatus.Unauthorized:
            case ResStatus.RefreshTokenExpired:
            case ResStatus.AccessTokenExpired:
                return HttpStatusCode.Unauthorized;


            case ResStatus.Forbidden:
                return HttpStatusCode.Forbidden;


            case ResStatus.ValidateError:
            case ResStatus.ErrorWarning:
            case ResStatus.BadRequest:
                return HttpStatusCode.BadRequest;


            case ResStatus.InternalError:
                return HttpStatusCode.InternalServerError;


            case ResStatus.Unknown:
                return null;
            default:
                throw new ArgumentOutOfRangeException(response.ToString(), $"No HTTP status code mapping is defined for status {response.Status}.");
        }
    }

    /// <summary>
    /// [500] It needs to be checked after the microservice is called. If it is False, it should be an error in the service call and needs to be recorded in the microservice call log. Interface call exceptions are automatically AOPed by Mediator and logged by try catch.
    /// </summary>
    public static bool IsRemoteResultHealthy(this IResultEnvelope res) =>
        res.Status != ResStatus.InternalError && !IsMalformed(res);

    /// <summary>
    ///  [200] means the request is processed normally
    /// </summary>
    public static bool IsOk(this IResultEnvelope res) => res.Status == ResStatus.Ok;

    /// <summary>
    /// Request results from remote call Automatically validate and append information
    /// </summary>
    /// <param name="res"></param>
    /// <param name="originInfo">HTTP and other original responses</param>
    /// <returns></returns>
    public static void AttachOriginIfMalformed(this IResultEnvelope res, string originInfo)
    {
        if (IsMalformed(res))
        {
            res.AppendMetadata("originRes", originInfo);
        }
    }

    /// <summary>
    ///  It is not a valid request, which means that the return value may not comply with this specification. You should pay attention to this situation and handle it specially.
    /// </summary>
    public static bool IsMalformed(this IResultEnvelope res)
    {
        //TODO needs to judge Res<T> when OK Data = null There is a specification issue
        return res.Status == ResStatus.Unknown;
    }
    /// <summary>
    /// Additional information for interface settings (duplication will overwrite)
    /// </summary>
    /// <param name="res"></param>
    /// <param name="name"></param>
    /// <param name="info"></param>
    public static T SetMetadata<T>(this T res, string name, object? info = null) where T : IResultEnvelope
    {
        res.Metadata ??= new ExpandoObject();
        res.Metadata.Set(name, info);
        return res;
    }
    /// <summary>
    /// Add additional information to the interface (add a suffix if the Name is repeated)
    /// </summary>
    /// <param name="res"></param>
    /// <param name="name"></param>
    /// <param name="info"></param>
    public static T AppendMetadata<T>(this T res, string name, object? info = null) where T : IResultEnvelope
    {
        res.Metadata ??= new ExpandoObject();
        res.Metadata.Append(name, info);
        return res;
    }

    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error, [MaybeNullWhen(true)]out T data)
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.ToRes();
        return true;
    }
    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.ToRes();
        return true;
    }
    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this Res<T?> res, [NotNullWhen(true)] out Res? error, out T? data) where T : struct
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.ToRes();
        return true;
    }
    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this Res<T?> res, [NotNullWhen(true)] out Res? error) where T : struct
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.ToRes();
        return true;
    }
    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed(this Res res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res;
        return true;
    }

    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this ResPaged<T> res, [NotNullWhen(true)] out Res? error, out ResPaged<T>.PageData data)
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.ToRes();
        return true;
    }

    /// <summary>
    /// [not 200] indicates a problem with the request
    /// </summary>
    public static bool IsFailed<T>(this ResPaged<T> res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.ToRes();
        return true;
    }

    /// <summary>
    /// [200] means the request is processed normally
    /// </summary>
    public static bool IsOk<T>(this ResPaged<T> res, out ResPaged<T>.PageData data)
    {
        data = res.Data;
        return res.IsOk();
    }
    /// <summary>
    /// [200] means the request is processed normally
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="res"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool IsOk<T>(this Res<T?> res, out T? data) where T : struct
    {
        data = res.Data!;
        return res.IsOk();
    }

    /// <summary>
    /// [200] means the request is processed normally
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="res"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool IsOk<T>(this Res<T> res,  [MaybeNullWhen(false)] out T data)
    {
        data = res.Data!;
        return res.IsOk();
    }

    /// <summary>
    /// The request is processed normally and a success description is provided.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="self"></param>
    /// <param name="hint"></param>
    /// <returns></returns>
    public static T Ok<T>(this T self, string hint) where T : IResultEnvelope
    {
        self.Status = ResStatus.Ok;
        self.Message = hint;
        return self;
    }

    /// <summary>
    /// Batch call results are converted to single call results.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="responses"></param>
    /// <returns></returns>
    public static Res<List<T>> ToBulkRes<T>(this IEnumerable<Res<T>> responses)
    {
        var list = new List<T?>();
        var result = new Res<List<T>>()
        {
            Status = ResStatus.Ok,
            Message = ""
        };
        var sb = new StringBuilder();
        foreach (var res in from response in responses select response)
        {
            if (res.Message is { } msg)
            {
                sb.AppendLine(msg);
            }

            if (res.Metadata is { } metadata)
            {
                result.Metadata ??= new ExpandoObject();
                result.Metadata.Append("bulk", metadata);
            }
            if (!res.IsOk(out var data) && result.Status == ResStatus.Ok)
            {
                result.Status = res.Status;
            }
            list.Add(data);
        }

        result.Message = sb.ToString().TrimEnd();
        result.Data = list.Cast<T>().ToList();
        return result;
    }

   
    /// <summary>
    /// Merge the return value information to preserve the information of the two Res. It is generally used when the two Res types are inconsistent.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="response"></param>
    public static T Merge<T>(this T self, IResultEnvelope response) where T : IResultEnvelope
    {
        self.AppendMetadata("originalMessage", self.Message);
        self.AppendMetadata("originalStatus", self.Status);
        response.Metadata ??= new ExpandoObject();
        self.Metadata!.Merge(response.Metadata);
        self.Message = response.Message;
        self.Status = response.Status;
        return self;
    }
    /// <summary>
    /// Additional information
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    public static T AppendMessage<T>(this T self, string? message)
        where T : IResultEnvelope
    {
        return Append(self, message);
    }
    /// <summary>
    /// Additional information
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    private static T Append<T>(this T self, string? message)
        where T : IResultEnvelope
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            self.Message += $";{message}";
            self.Message = self.Message.TrimStart(';');
        }
        return self;
    }
    /// <summary>
    /// append error
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    /// <param name="status"></param>
    public static T AppendFailure<T>(this T self, string? message, ResStatus status = ResStatus.BadRequest)
        where T : IResultEnvelope
    {
        self = Append(self, message);
        self.Status = status;
        return self;
    }

    /// <summary>
    /// Create a success response or failure response based on the upper layer response
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <typeparam name="TLastResponse"></typeparam>
    /// <param name="res"></param>
    /// <param name="okResponse"></param>
    /// <param name="fallback"></param>
    /// <returns></returns>
    public static async Task<TResponse> OkOrFallback<TLastResponse, TResponse>(this Task<TLastResponse> res,
        TResponse okResponse,
        TResponse? fallback = null) where TLastResponse : IResultEnvelope
        where TResponse : class, IResultEnvelope, new()
    {
        var lastRes = await res;
        return lastRes.IsOk() ? okResponse : (fallback ?? new TResponse()).AppendFailure(lastRes.Message);
    }

}
