namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Marks a module that can contribute middleware or endpoints when composed by an ASP.NET Core host.
/// </summary>
/// <remarks>
/// This interface describes capability only. A generic host omits web contributions while continuing to execute
/// the module's host-builder and service-registration lifecycle.
/// </remarks>
public interface IWebModule : IModule
{
}

/// <summary>
/// Marks a web-capable module whose composition is invalid without an ASP.NET Core host adapter.
/// </summary>
/// <remarks>
/// Use this marker only when omitting the module's web contributions would leave its non-web registrations unusable
/// or misleading. Monica rejects such modules before it mutates the host builder or service collection.
/// </remarks>
public interface IWebHostRequiredModule : IWebModule
{
}
