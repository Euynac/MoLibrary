# Monica Testing Samples

Current Monica unit-testing baseline is defined by these sample projects:

- `Monica.UnitTests`
  - Shared assertion helpers
  - Module-system scope helpers
  - Deterministic localization and theme test doubles
  - Application service, sociable host, and repository fixtures
- `tests/Test.Monica.UI`
  - Monica UI foundation tests
- `tests/Test.Monica.JobScheduler`
  - Module guide tests
  - Sociable application fixture tests
  - In-memory provider tests
  - Facade tests for `Res<T>` flows
  - Service validation tests
- `tests/Test.Monica.JobScheduler.UI`
  - UI module service-registration tests
  - bUnit component tests
  - Page-shell error-state tests
  - UI support class tests

## Recommended First Targets

For infrastructure modules:

- `Modules/`
- `Facades/`
- one stable in-memory provider or support class

For UI modules:

- module registration or dependency behavior
- one stable component
- one page-shell state test
- one pure support or mapping class

## Reuse Guidance

- Put cross-project helpers into `Monica.UnitTests`, not into each test project.
- Keep test project folder depth aligned with the source project.
- When a new module follows an existing sample, copy the sample structure first and then adapt the assertions.
- For business service test projects, use the `monica-application-unit-testing` skill. It contains the canonical `Test.{Service}` folder layout, collection fixture pattern, and handler/domain-service/repository templates.
