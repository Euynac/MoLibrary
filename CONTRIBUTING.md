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

When changing canonical Agent Skills, also run:

```bash
python3 scripts/validate_agent_skills.py
python3 scripts/sync_agent_skills.py --write
python3 scripts/sync_agent_skills.py --check
python3 scripts/test_agent_skills.py
```

Edit Monica-owned skills only under `skills/<name>/`. Their matching `.agents/skills/<name>` and `.claude/skills/<name>` directories are generated projections and must not be edited directly. The generator owns only catalog-managed Monica directories: unrelated external skills, files, and caches in either projection root are preserved and ignored by projection checks. Monica-owned skills use portable `SKILL.md` frontmatter containing only `name` and `description`. Skill release versions live in `.monica/agent-skill-catalog.json` and `.monica/agent-skill-index.json`, not in skill frontmatter.

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

Before committing, check whether the diff is breaking from a consumer's point of view. A change is breaking when existing host applications, module authors, package consumers, documented examples, or automation may need to change code, configuration, routes, serialized data, package references, or operational assumptions.

Breaking-change indicators include renamed or removed public APIs, option properties, guide methods, builder extensions, annotations, abstractions, models, modules, facades, configuration keys, endpoints, response shapes, package IDs, documented usage, default behavior, validation rules, exception behavior, persistence formats, service discovery identity, OpenTelemetry resource identity, or Swagger document naming.

Use both forms for clarity when a breaking change exists:

```text
feat!: introduce host-bound Monica composition

BREAKING CHANGE: replace ambient Mo registration with builder.AddMonica(monica => ...); each host now owns its module graph and runtime catalogs.
```

## Coding Standards

- Use English for code comments, XML documentation, and developer-facing annotations.
- Prefer primary constructors for dependency-injected classes with a single constructor.
- Public module options, guides, builder extensions, abstractions, and models should include useful XML documentation.
- Facades may return `Res` or `Res<T>`; internal services should use standard .NET return types and exceptions.
- Keep UI colors on MudBlazor palette variables or the approved Monica theme token contract.

## Release Process

Release tags use the `v` prefix:

```bash
git tag v1.0.0-rc.7
git push origin v1.0.0-rc.7
```

The release workflow builds, tests, packs, uploads package artifacts, publishes to NuGet when configured, generates release notes from commit prefixes with `git-cliff`, and creates a GitHub pre-release for `*-rc.*` tags. It also validates the canonical Agent Skills with both the portable Agent Skills validator and Codex validator, installs `monica-guide` from the pushed immutable tag for Codex and Claude Code, verifies the discovered installed directory against the release's exact per-file and per-skill digests, and publishes the catalog, resolved commit, release index, manifest, and skill-tree archive only after discovery succeeds.

The checked-in index cannot contain the commit of the tag that contains it. After the release assets are published, roll the verified index forward in a follow-up commit before advertising the tag or preparing the next release:

```bash
curl --fail --location \
  "https://github.com/Tairitsua/Monica/releases/download/<tag>/agent-skill-index.json" \
  --output .tmp/<tag>-agent-skill-index.json
python3 scripts/roll_forward_agent_skill_index.py \
  --released-index .tmp/<tag>-agent-skill-index.json \
  --expected-tag <tag> \
  --write
python3 scripts/sync_agent_skills.py --write
python3 scripts/validate_agent_skills.py
```

The roll-forward command accepts only additive history from the expected immutable release asset. A later release also downloads the previous tag's verified index and fails if the asset is unavailable, omits history, or rewrites an existing version mapping.

## Security Issues

Do not report security vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md).
