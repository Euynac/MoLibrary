using Microsoft.AspNetCore.Http;

namespace MoLibrary.Core.GlobalJson.Interfaces;

public interface IHasHttpContextAccessor
{
    internal IHttpContextAccessor? HttpContextAccessor { get; set; }
}