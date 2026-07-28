using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Carries the bound MVC action state exposed to execution-pipeline behaviors.
/// </summary>
/// <param name="HttpContext">The current HTTP context.</param>
/// <param name="Controller">The activated controller instance.</param>
/// <param name="ActionDescriptor">The selected controller action.</param>
/// <param name="ActionArguments">The bound and mutable action arguments passed to MVC.</param>
public sealed record MvcActionExecutionInput(
    HttpContext HttpContext,
    object Controller,
    ControllerActionDescriptor ActionDescriptor,
    IDictionary<string, object?> ActionArguments);
