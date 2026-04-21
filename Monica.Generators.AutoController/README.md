# Monica.Generators.AutoController

`Monica.Generators.AutoController` is a source-generator package for two related jobs:

- Generate HTTP API controllers from Monica handler classes.
- Generate RPC client code from exported RPC metadata.

The RPC workflow is now build-integrated. You do not need `Monica.Generators.AutoController.Tool`, a CLI step, or any manual metadata extraction command.

## Package Roles

In a typical solution, the package participates in two kinds of projects:

- **Producer project**: an ASP.NET Core API project that exposes handlers and exports `*.rpc-metadata.json`.
- **Consumer project**: a shared protocol or contract project that contains `PublishedLanguages`, references the metadata files, and generates RPC client code.

## Controller Generation Basics

Add the generator to the target project and configure the assembly-level attribute:

```csharp
using Monica.WebApi.AutoControllers.Annotations;

[assembly: AutoControllerConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = "Flight"
)]
```

Typical handler shape:

```csharp
public sealed class QueryGetFlight : ApplicationService<QueryGetFlightRequest, QueryGetFlightResponse>
{
    public Task<QueryGetFlightResponse> Handle(QueryGetFlightRequest request)
    {
        // Implementation
    }
}
```

With CQRS naming conventions, the generator can infer:

- controller route from `DefaultRoutePrefix` and `DomainName`
- method route from the handler class name
- `GET` for query handlers and `POST` for command handlers

Explicit `[Route]`, `[HttpGet]`, `[HttpPost]`, and similar attributes still override the defaults.

## RPC Workflow Without Any Tool

The RPC integration uses MSBuild tasks shipped with this package:

1. The producer project compiles and emits `__RpcMetadata.g.cs`.
2. After compile, the package exports deterministic JSON metadata into a shared directory.
3. Before a consumer project compiles, the package loads that directory as Roslyn `AdditionalFiles`.
4. The RPC client generator reads those JSON files and emits the client interfaces and implementations.

If metadata files are missing, the package can bootstrap them by scanning producer source projects in the same repository. That means a developer can delete `RpcMetadata/` and rebuild without reaching for a separate tool.

## Producer Project Setup

### 1. Reference the generator

For local source-reference development:

```xml
<ItemGroup>
  <ProjectReference
    Include="..\..\..\..\MoLibrary\Monica.Generators.AutoController\Monica.Generators.AutoController.csproj"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
</ItemGroup>
```

For NuGet usage:

```xml
<ItemGroup>
  <PackageReference Include="Monica.Generators.AutoController" Version="x.y.z" PrivateAssets="all" />
</ItemGroup>
```

### 2. Configure controller generation

Add `AutoControllerConfig` in `Program.cs` or `AssemblyInfo.cs`:

```csharp
using Monica.WebApi.AutoControllers.Annotations;

[assembly: AutoControllerConfig(
    DefaultRoutePrefix = "api/v1",
    DomainName = nameof(EServicesDomain.Alarm)
)]
```

### 3. Tell the build where to export metadata

Set `MonicaRpcMetadataExportDirectory` in the producer `.csproj`:

```xml
<PropertyGroup>
  <MonicaRpcMetadataExportDirectory>..\..\..\Shared\Platform.Protocol\RpcMetadata</MonicaRpcMetadataExportDirectory>
</PropertyGroup>
```

When this property is present, the package automatically:

- enables `EmitCompilerGeneratedFiles`
- searches for `__RpcMetadata.g.cs` under the compiler-generated output
- exports `{AssemblyName}.rpc-metadata.json` after compile
- skips rewriting the file when the JSON content is unchanged

### 4. Use shared request and response contracts

Only handlers whose request and response types can be resolved to shared contract namespaces are exported safely. In practice, that means:

- put RPC contracts in the consumer/shared protocol project, usually under `PublishedLanguages`
- keep API-only DTOs out of RPC-facing handlers if you expect client generation for them

If a handler uses implementation-local types that are not available to the consumer project, the metadata exporter will not produce a usable shared contract entry for that handler.

## Consumer Project Setup

The consumer project is usually the shared protocol assembly that contains the request/response contracts and the generated clients.

### 1. Reference the generator

Use the same package or analyzer reference pattern as the producer project.

### 2. Add `RpcClientConfig`

Add assembly-level configuration:

```csharp
using Monica.WebApi.AutoControllers.Annotations;
using Monica.WebApi.RpcClient.Annotations;

[assembly: AutoControllerConfig(SkipGeneration = true)]
[assembly: RpcClientConfig(
    AddHttpImplementations = true,
    HttpImplementationType = typeof(OurHttpApi)
)]
```

Notes:

- `SkipGeneration = true` avoids generating API controllers inside the contract project.
- `RpcClientConfig` enables RPC client generation.
- `HttpImplementationType` is optional. When specified, it must inherit from `HttpRpcApi` and remain compatible with the generated constructor expectations.

### 3. Keep contracts under `PublishedLanguages`

The bootstrap task uses the consumer project’s source code to resolve contract namespaces. A typical layout is:

```text
Platform.Protocol/
  PublishedLanguages/
    DomainAlarm/
    DomainFlight/
    DomainUser/
  RpcMetadata/
```

### 4. Keep metadata under `RpcMetadata`

By default, the package consumes:

```text
$(MSBuildProjectDirectory)/RpcMetadata/*.rpc-metadata.json
```

You do not need extra configuration if you use that default folder name.

If your project stores metadata elsewhere, override it:

```xml
<PropertyGroup>
  <MonicaRpcMetadataConsumeDirectory>CustomRpcMetadata</MonicaRpcMetadataConsumeDirectory>
</PropertyGroup>
```

## First Build and Deleted `RpcMetadata`

When a consumer project builds, the package runs a bootstrap step before compile if either of these exists:

- the configured consume directory
- a `PublishedLanguages` directory

During bootstrap, the package:

1. finds the repository root
2. scans `*.csproj` files under that repository
3. selects producer projects whose `MonicaRpcMetadataExportDirectory` points at the same consume directory
4. rebuilds any missing `*.rpc-metadata.json` by scanning the producer source code

This is why a fresh rebuild can recover from these cases:

- `RpcMetadata/` exists but some JSON files were deleted
- `RpcMetadata/` was removed entirely
- only `.gitkeep` remains

Recommended practice is still to commit the metadata files. Bootstrap is a recovery path, not a reason to treat generated metadata as disposable local-only output.

### Important limitation

Bootstrap can only reconstruct metadata when the producer source projects are present in the same repository and point to the same export directory. If you build a consumer project in isolation without the producer projects, the checked-in JSON files are the source of truth.

## Source-Reference Usage Inside One Repository

When a solution references `Monica.Generators.AutoController.csproj` directly, the analyzer project itself is present, but the MSBuild task assembly is not automatically laid out the same way as it is in a packed NuGet package.

In that scenario, add repository-level glue that:

- imports the package `.props` from the source checkout
- builds `Monica.Generators.AutoController.BuildTasks`
- shadow-copies the task DLL into each consuming project's `obj` folder
- imports the package `.targets`

`FIPS2022/Directory.Build.props` and `FIPS2022/Directory.Build.targets` are examples of this pattern.

This extra setup is only for local source-reference development against the MoLibrary repository.

## NuGet Usage

After this package is published to NuGet, the project-level usage stays the same:

- producer projects still set `MonicaRpcMetadataExportDirectory`
- consumer projects still use `RpcMetadata` by default or override `MonicaRpcMetadataConsumeDirectory`
- consumer projects still declare `RpcClientConfig`
- shared request/response contracts still belong in the consumer/shared protocol project

What changes with NuGet is only the infrastructure around the package:

- you use `PackageReference` instead of a source `ProjectReference`
- you do not need custom `Directory.Build.props` or `Directory.Build.targets` glue just to find the build tasks
- the package already carries the analyzer, build props/targets, and the build task assembly

So the answer is: **yes, the workflow is the same after publishing to NuGet, and the setup is actually simpler**.

## Recommended Repository Layout

```text
src/
  Services/
    Alarm/
      AlarmService.API/
  Shared/
    Platform.Protocol/
      PublishedLanguages/
      RpcMetadata/
```

Recommended ownership:

- API projects own handlers and export metadata.
- The shared protocol project owns contracts and generated RPC clients.
- `RpcMetadata/*.json` is checked into source control next to the consumer project.

## Troubleshooting

### No metadata file is exported

Check the producer project for all of the following:

- the generator is referenced
- `AutoControllerConfig` is present
- `MonicaRpcMetadataExportDirectory` is set
- the build actually produced `__RpcMetadata.g.cs`
- the handler contracts live in shared namespaces that the consumer can reference

### No RPC client code is generated

Check the consumer project for all of the following:

- `RpcClientConfig` is present
- the metadata JSON files are under `RpcMetadata/` or the configured consume directory
- the project contains the shared contract types under `PublishedLanguages`

### Rebuild after deleting metadata still fails

Bootstrap requires:

- producer source projects in the same repository
- matching export and consume directories
- readable shared contract source files in the consumer project

If those conditions are not true, restore the checked-in metadata files or build from a repository layout that includes both producers and the consumer project.
