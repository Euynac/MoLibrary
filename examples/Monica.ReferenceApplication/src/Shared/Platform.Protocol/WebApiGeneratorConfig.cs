using Monica.WebApi.Annotations;

[assembly: WebApiGenerationConfig(
    "api/v1",
    DomainName = "Ordering",
    RpcClientTargets = RpcClientGenerationTargets.Http)]
