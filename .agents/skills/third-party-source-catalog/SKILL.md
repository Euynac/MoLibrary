---
name: third-party-source-catalog
description: Catalog, download, and register third-party library source trees for AI agents. Use proactively when debugging behavior across a third-party dependency or SDK boundary, when exact package-version semantics matter, when an agent needs to inspect external dependency source code, cache a GitHub tag or fallback clone, resolve a NuGet package version to source, scan manually added local source folders, or maintain shared source manifests across Codex, Claude Code, and similar CLI agents.
---

# Third-Party Source Catalog

Use this skill to keep a shared catalog of third-party source trees that multiple agent CLIs can reuse from the same repository.

## Quick Start

Run the script from this skill folder:

```bash
python3 scripts/source_catalog.py doctor
python3 scripts/source_catalog.py repo add-gh owner/repo --alias short-name
python3 scripts/source_catalog.py repo refresh owner/repo
python3 scripts/source_catalog.py repo tags owner/repo --match 1.2.3
python3 scripts/source_catalog.py repo fetch owner/repo --tag v1.2.3
python3 scripts/source_catalog.py repo fetch-nuget Package.Id 1.2.3
python3 scripts/source_catalog.py repo list short-name
```

If the source was added manually instead of being downloaded by the script:

```bash
python3 scripts/source_catalog.py local add /absolute/path/to/source
python3 scripts/source_catalog.py local scan /absolute/path/to/source-parent
```

The script checks `gh` and `git` before every command. If either tool is missing or not configured, fix that first.

## Shared Runtime

All agents share the same runtime root by default:

```text
.tmp/third-party-source-catalog/
```

Important paths under that root:

- `state/config.json`: bootstrap config used to remember the active runtime root.
- `state/catalog.json`: catalog records for GitHub and local sources.
- `state/catalog.lock`: automatic inter-process lock for catalog mutations.
- `repos/<repo_id>/manifest.stub.json`: script-owned stub metadata.
- `repos/<repo_id>/manifest.json`: agent-authored enriched manifest.
- `repos/<repo_id>/sources/`: managed downloaded sources.
- `repos/<repo_id>/archives/`: downloaded tag archives.

Change the active runtime root when the user wants the skill to work from another location:

```bash
python3 scripts/source_catalog.py config set-root /absolute/path/to/new-root
```

This updates future operations only. Existing cached data is not moved automatically.

## Workflow

1. Run `doctor` to verify `gh` authentication and `git` identity.
2. Register GitHub repos with `repo add-gh`, or register existing local source trees with `local add`.
3. Use `repo refresh` to pull tag metadata from GitHub.
4. Use `repo tags <query>` to inspect exact cached tag names, optionally refreshing or filtering them.
5. Use `repo fetch --tag <tag>` to prefer a tag archive. If the tag does not exist, the script falls back to shallow clone automatically.
6. For NuGet dependencies, prefer `repo fetch-nuget <package-id> <version>` to discover the package repository and resolve the best exact source tag automatically.
7. Use `repo list [query]` to inspect the catalog or resolve one source path with fuzzy search.
8. Use `local scan [path]` to discover manually added local source roots under a directory tree.
9. After a source is registered, inspect the code and write `manifest.json` for the selected repo when richer retrieval metadata is useful.

## Commands

- `doctor`
  Validate `gh`, `gh auth status`, `git`, and `git config user.name/user.email`.
- `config show`
  Print bootstrap and active runtime paths.
- `config set-root <abs-path>`
  Change the active runtime root for future catalog operations.
- `repo add-gh <owner/repo> [--alias <name>]`
  Add or update a GitHub-backed catalog record.
- `repo remove <query>`
  Remove a catalog record and delete only the managed cache under the runtime root.
- `repo refresh [<query> | --all]`
  Refresh GitHub tag metadata.
- `repo list [query]`
  List all records, or resolve one fuzzy match and print the preferred absolute source path.
- `repo tags <query> [--match <text>] [--limit <n>] [--refresh]`
  List cached GitHub tags for one repository, with optional filtering and metadata refresh.
- `repo fetch <query> [--tag <tag>] [--force]`
  Download a tag archive when possible, otherwise shallow clone.
- `repo fetch-nuget <package-id> <version> [--alias <name>] [--force]`
  Read exact NuGet package metadata, register its GitHub repository, resolve the most likely version tag or package commit, and fetch that source tree.
- `local add <abs-path> [--alias <name>]`
  Register one existing local source tree immediately.
- `local scan [path] [--max-depth <n>] [--update-existing]`
  Discover multiple local source roots recursively.

## Manifests

The script creates or updates `manifest.stub.json`. Treat it as script-owned metadata.

When deeper retrieval context is useful, inspect the source and create `manifest.json` yourself. Keep it concise and focused on source navigation. Recommended fields:

- `summary`
- `languages`
- `build_systems`
- `package_managers`
- `important_directories`
- `entry_points_or_public_api`
- `retrieval_keywords`
- `notes`

The script reads `manifest.json` when present and does not overwrite it.

## Search Rules

- Fuzzy matching uses canonical name, aliases, GitHub name, local folder names, manifest markers, and enriched manifest keywords.
- A unique best match prints full record details and the preferred absolute path.
- Ambiguous matches print candidates so the agent can refine the query.

## Concurrency and Progress

- Catalog-mutating commands acquire an automatic inter-process lock before loading state and hold it through the atomic save. Concurrent agents wait instead of overwriting one another's catalog snapshots.
- Read-only `repo list` and cached `repo tags` commands remain available while another process downloads source.
- Long tag downloads and archive extraction report phase and percentage progress. Do not start a duplicate fetch merely because a large archive takes time to extract.

## Local Source Rules

- Prefer `local add` when the user points to one known source tree.
- Use `local scan` when the user has manually dropped many source trees into one folder.
- Local git repos try to infer GitHub identity from the `origin` remote first.
- When no GitHub identity is available, the catalog falls back to folder-based local metadata.
- Recursive scan skips internal cache folders created by this skill.
