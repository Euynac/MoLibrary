# Changelog

All notable changes to Monica are documented in this file.

This project follows semantic versioning for public NuGet packages. Release candidates may still include breaking changes before the stable `1.0.0` release.

## [1.0.0-rc.1] - 2026-05-06

### Added

- First public release candidate for the Monica modular .NET infrastructure framework.
- Typed DDD ProjectUnit conventions for application services, request DTOs, domain services, entities, repositories, domain events, configurations, recurring jobs, and triggered jobs.
- Composable infrastructure modules with the unified `Mo.Add*()` registration pattern.
- JobScheduler modules for recurring jobs, triggered jobs, metadata persistence, execution history, concurrency coordination, and dashboard UI.
- Built-in Blazor operational dashboards on top of `Monica.UI`.
- Configuration, Repository, WebApi, Logging, EventBus, StateStore, AI, Dapr, SignalR, Office, Profiling, and utility module families.
- Source generator packages for framework and AutoController workflows.
- Bundled agent skills under `.claude/skills/` and `.agents/skills/`.
- Bilingual README content and GitHub-hosted demo video reference.

### Notes

- This is a release candidate. APIs may still change before `1.0.0` stable.
- NuGet packages target .NET 10.
- `Monica.Experimental` and example projects are not intended for NuGet publishing.
