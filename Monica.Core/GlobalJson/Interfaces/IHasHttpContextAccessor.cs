using Microsoft.AspNetCore.Http;

namespace Monica.Core.GlobalJson.Interfaces;

public interface IHasHttpContextAccessor
{
    internal IHttpContextAccessor? HttpContextAccessor { get; set; }
}