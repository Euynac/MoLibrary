# MoLibrary Repository Tests

This project contains unit and integration tests for the `MoRepository` implementation. The tests cover basic repository operations as well as complex scenarios with entity relationships and different database providers.

## Test Structure

The test suite is organized into the following categories:

### 1. Basic Repository Tests (MoRepositoryTests.cs)

Tests the fundamental CRUD operations and query methods defined in the `IMoBasicRepository` interface using an in-memory database:

- **Insert Operations**: Testing single and batch inserts
- **Query Operations**: GetAsync, FindAsync, GetListAsync with various parameters
- **Update Operations**: Testing single and batch updates
- **Delete Operations**: Testing deletion by entity, ID, and predicate
- **Existence Checks**: Testing ExistAsync method

### 2. Complex Entity Tests (ComplexEntityRepositoryTests.cs)

Tests repository operations with complex entity relationships:

- Entities with navigation properties
- One-to-many and many-to-many relationships
- Nested entity structures
- Testing TrackGraph functionality for updating complex object graphs

### 3. SQLite Tests (SQLiteRepositoryTests.cs)

Integration tests using SQLite as the database provider:

- Ensures compatibility with a real database provider
- Tests transaction behavior
- Tests performance characteristics

## Running the Tests

Tests can be run using the `dotnet test` command or through the test explorer in your IDE.

## Test Data Seeding

The tests use the following approaches for data seeding:

1. **In-memory database**: Uses EF Core's in-memory provider for fast testing without persistence
2. **SQLite database**: Creates a temporary SQLite database for each test run
3. **Auto-generated test data**: Uses deterministic data generation for consistent test results

## Mocking Strategy

The tests use Moq to mock the following dependencies:

- IDbContextProvider
- IMoServiceProvider
- IMoUnitOfWorkManager

## Best Practices

The tests follow these best practices:

1. **Isolated test environment**: Each test sets up its own isolated environment
2. **Proper cleanup**: All resources are properly disposed after test execution
3. **Descriptive naming**: Test names clearly indicate what's being tested
4. **Arrange-Act-Assert pattern**: Tests are structured with clear setup, action, and verification phases 