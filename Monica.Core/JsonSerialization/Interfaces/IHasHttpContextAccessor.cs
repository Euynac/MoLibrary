using Microsoft.AspNetCore.Http;

namespace Monica.Core.JsonSerialization.Interfaces;

public interface IHasHttpContextAccessor
{
    internal IHttpContextAccessor? HttpContextAccessor { get; set; }
}