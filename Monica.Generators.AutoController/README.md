# Monica.Generators.AutoController

`Monica.Generators.AutoController` provides:

- HTTP API controller generation for Monica handlers
- build-integrated RPC metadata export / consume
- generated HTTP and Local RPC client implementations

The authoritative end-user documentation now lives in the `Monica.Docs` project in this repository.

Primary page:

- `Monica.Docs/docs/zh-CN/scenarios/build-integrated-rpc.md`

This README is intentionally brief so package docs do not drift away from the published Monica.Docs content.

`FIPS2022/Directory.Build.props` and `FIPS2022/Directory.Build.targets` are examples of this pattern.

This extra setup is only for local source-reference development against the MoLibrary repository.

## NuGet Usage

After this package is published to NuGet, the project-level usage stays the same:

- producer projects still set `MonicaRpcMetadataExportDirectory`
- consumer projects still use `RpcMetadata` by default or override `MonicaRpcMetadataConsumeDirectory`
- consumer projects still declare `RpcClientConfig`
- shared request/response contracts still belong in the consumer/shared protocol project
- local modular-monolith hosts still use `UseLocalTransport()`, while distributed hosts still use `UseHttpTransport()`

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
