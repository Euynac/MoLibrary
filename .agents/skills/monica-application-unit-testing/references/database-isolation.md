# Database Isolation

## PerScopeDatabase

Use `DatabaseIsolation.PerScopeDatabase` as the default. Each `NewScope()` gets a fresh SQLite in-memory connection and schema.

Best for:

- handler tests
- repository tests
- tests that mutate data
- tests that may run in any order

Trade-off: schema creation happens per test scope.

## SharedDatabaseWithTransaction

Use `DatabaseIsolation.SharedDatabaseWithTransaction` when a service has expensive schema creation and tests do not depend on provider behavior that escapes a transaction. The fixture keeps one SQLite in-memory connection for the collection; each scope starts a transaction and rolls it back on dispose.

Best for:

- read-heavy repository tests
- larger DbContexts where per-scope schema creation is too slow

Check carefully when using:

- `ExecuteUpdateAsync`
- raw SQL
- multiple DbContext instances in the same scope
- provider-specific behavior

## RealDatabase

Use `DatabaseIsolation.RealDatabase` only when the behavior is specific to a real provider dialect and SQLite is not representative. This should be uncommon in unit tests and normally needs a caller-supplied provider configuration.

Best for:

- provider-specific SQL translation
- migrations or schema behavior
- functions and indexes SQLite cannot model

Rules:

- never hardcode developer machine credentials in tests
- keep connection strings outside source control
- roll back per scope when possible
- mark these tests clearly if they are slower or environment-dependent

## Choosing Quickly

- Start with `PerScopeDatabase`.
- Move to `SharedDatabaseWithTransaction` only after measuring boot or schema cost.
- Move to `RealDatabase` only when SQLite gives false confidence.
