# Changelog

All notable changes to Monica are documented in this file.

This project follows semantic versioning for public NuGet packages. Release candidates may still include breaking changes before the stable `1.0.0` release.

## [Unreleased]

### Added

- Host-bound `builder.AddMonica(monica => ...)` composition with deterministic module-graph validation.
- Stable, Integrations, and Labs package tiers with CI-enforced dependency direction.
- A dedicated `Monica.ProjectUnits` package, official `Monica.Templates`, and the Ordering reference application.
- Host-scoped Object Mapping and ProjectUnits facades for Minimal API and UI consumers.

### Changed

- Logging, localization, JSON serialization, mapping, ProjectUnits, AutoController configuration, DataChannel pipelines, scheduler time zones, and runtime catalogs are owned by each host.
- `Monica.Framework` is a focused application package instead of an all-dependencies bundle.
- RPC client helpers now live in `Monica.WebApi`, alongside the transport feature they configure.
- `Microsoft.OpenApi`, `System.Security.Cryptography.Xml`, and the SQLite native bundle were upgraded to supported releases with published security fixes.
- Authentication accepts access tokens from the `Authorization` header by default; query-string tokens require an explicit, path-scoped opt-in.
- Configuration persistence and Entity Framework integration now use deterministic, warning-free schemas and read-only serializer options.
- Unhandled-exception responses are safe by default; stack traces and request snapshots now require an explicit trusted-development opt-in.
- Template and reference hosts expose ASP.NET Core health checks instead of hardcoded health payloads.

### Removed

- Ambient `Mo` registration, `builder.UseMonica()`, `Mo.RegisterInstantly(...)`, and process-wide `LogManager` state.
- The unauthenticated JWT decode endpoint and global query-string token extraction.
- Global mutable clock, DataChannel, principal, localization, JSON, mapping, and runtime-environment state.
- Unused process-wide debug helpers and the unbounded delayed-task scheduler.

## [1.0.0-rc.2] - 2026-05-09

### Added

- Global Monica application defaults, including the root `Mo.ConfigApplication(...)` API.
- Monica endpoint metadata and endpoint port constraints for Monica-owned routes.
- OpenTelemetry metrics dashboards and in-process collector support.
- SignalR send diagnostics with method-level metrics and target drill-downs.
- Terminal and read-only file access capabilities for AI-assisted workflows.
- Monica UI localization skill guidance and validation support.

### Fixed

- Static web asset behavior for NuGet package consumers.
- UI shell setup for static web assets.
- Snapshot summary resource service name and version display.
- SignalR dispatch proxy compatibility by allowing runtime proxy derivation.

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
