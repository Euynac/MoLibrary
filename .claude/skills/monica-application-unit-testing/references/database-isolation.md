# Database Isolation

Database state belongs to one `MonicaTestApplication` scenario. Configure the provider in the factory before host build, then create normal scopes from that application.

## PerScopeDatabase

Use `DatabaseIsolation.PerScopeDatabase` by default. Every `application.CreateScope()` receives a fresh SQLite in-memory connection and schema.

Best for:

- handler and repository scenarios
- tests that mutate data in one scope
- scenarios that must run in any order or in parallel

Trade-off: schema creation occurs for each scope. Data intentionally does not cross scope boundaries.

## SharedDatabaseWithTransaction

Use `DatabaseIsolation.SharedDatabaseWithTransaction` when schema creation is measurably expensive and the scenario needs one host-owned SQLite database. Each scope starts a transaction and rolls it back on disposal.

Check carefully with:

- overlapping scopes in one application
- `ExecuteUpdateAsync` or raw SQL
- several DbContext instances in one scope
- behavior that escapes a transaction

The database is shared only within one `MonicaTestApplication`; separate scenario hosts still own separate databases. Do not run overlapping transactions against one shared SQLite connection.

## RealDatabase

Use `DatabaseIsolation.RealDatabase` only when a real provider dialect is part of the behavior and SQLite is not representative.

- Apply provider registration before `CreateAsync` builds the host.
- Use unique database or schema names per scenario when possible.
- Keep credentials outside source control.
- Dispose provider resources with the scenario application.
- If isolation is impossible, serialize only the tests sharing that named database resource.

## Choosing Quickly

- Start with `PerScopeDatabase`.
- Use `SharedDatabaseWithTransaction` only after measuring schema cost and verifying single-connection transaction behavior.
- Use `RealDatabase` only to cover provider-specific behavior.
