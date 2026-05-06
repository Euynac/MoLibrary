# Contributing to Monica

Thanks for helping improve Monica.

Monica is currently preparing the `1.0.0` stable release. During the release-candidate phase, breaking changes are still allowed when they improve the design.

## Development Setup

Prerequisites:

- .NET 10 SDK
- Git
- A C# IDE such as JetBrains Rider or Visual Studio

Restore, build, and test:

```bash
dotnet restore Monica.slnx
dotnet build Monica.slnx -c Release -m
dotnet test Monica.slnx -c Release --no-build -m
```

When running `dotnet` from WSL with a Windows `dotnet` executable, use Windows paths for project or solution arguments.

## Pull Requests

1. Open an issue or discussion first for broad design changes.
2. Keep changes focused on one feature, fix, or refactor.
3. Update public docs when public behavior changes.
4. Keep public XML documentation accurate for developer-facing APIs.
5. Run the relevant build and test commands before opening the pull request.

## Coding Standards

- Use English for code comments, XML documentation, and developer-facing annotations.
- Prefer primary constructors for dependency-injected classes with a single constructor.
- Public module options, guides, builder extensions, abstractions, and models should include useful XML documentation.
- Facades may return `Res` or `Res<T>`; internal services should use standard .NET return types and exceptions.
- Keep UI colors on MudBlazor palette variables or the approved Monica theme token contract.

## Release Process

Release tags use the `v` prefix:

```bash
git tag v1.0.0-rc.1
git push origin v1.0.0-rc.1
```

The release workflow builds, tests, packs, uploads package artifacts, publishes to NuGet when configured, and creates a GitHub pre-release for `*-rc.*` tags.

## Security Issues

Do not report security vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md).
