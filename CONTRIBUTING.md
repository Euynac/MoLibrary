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

## Commit Messages

Release notes are generated from commit messages between release tags. Commits that affect package behavior, public APIs, documentation, or migration guidance should use Conventional Commit prefixes:

```text
feat: add module-level metrics
fix: preserve Res<string> facade data
docs: update module registration guide
refactor: simplify JobScheduler state model
feat!: remove obsolete configuration API
```

Use `feat:` for new capabilities, `fix:` for bug fixes, `docs:` for documentation, `refactor:` for behavior-preserving restructuring, `perf:` for performance work, `build:` for build or packaging changes, `ci:` for workflow changes, `test:` for test-only changes, and `chore:` for maintenance. `feature:` is accepted as an alias, but `feat:` is preferred.

Breaking changes must use `!` after the type or a `BREAKING CHANGE:` footer. Direct commits to the release branch are allowed only when they follow the same format or are intentionally non-release maintenance.

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

The release workflow builds, tests, packs, uploads package artifacts, publishes to NuGet when configured, generates release notes from commit prefixes with `git-cliff`, and creates a GitHub pre-release for `*-rc.*` tags.

## Security Issues

Do not report security vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md).
