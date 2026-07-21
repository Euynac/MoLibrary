namespace Monica.WebApi.Annotations;

/// <summary>
/// Selects transport implementations generated for published RPC contracts.
/// </summary>
[Flags]
public enum RpcClientGenerationTargets
{
    /// <summary>Generates the RPC interface without a transport implementation.</summary>
    None = 0,

    /// <summary>Generates an HTTP transport implementation.</summary>
    Http = 1,

    /// <summary>Generates an in-process mediator implementation.</summary>
    Local = 2
}
