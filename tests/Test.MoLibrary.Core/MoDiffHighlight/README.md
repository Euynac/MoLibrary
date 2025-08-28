# DiffHighlight Module Tests

This directory contains comprehensive unit and integration tests for the MoDiffHighlight module.

## Test Coverage

### Core Functionality Tests
- **MoDiffHighlightTests.cs**: Tests for the main IMoDiffHighlight interface implementation
- **DiffHighlightServiceTests.cs**: Tests for the service layer with dependency injection

### Algorithm Tests
- **MyersDiffAlgorithmTests.cs**: Comprehensive tests for the Myers diff algorithm implementation
  - Basic diff operations (insert, delete, modify)
  - Edge cases and performance
  - Algorithm optimality verification

### Renderer Tests
- **DiffHighlightRenderersTests.cs**: Tests for all output renderers
  - HTML renderer (GitHub-style output)
  - Markdown renderer (```diff format)
  - Plain text renderer (console-friendly output)

### Edge Cases & Error Handling
- **EdgeCasesAndErrorHandlingTests.cs**: Comprehensive edge case testing
  - Null input validation
  - Large text handling
  - Unicode and special character support
  - Performance and scalability tests
  - Thread safety verification

### Integration Tests
- **DiffHighlightIntegrationTests.cs**: End-to-end workflow testing
  - Complete diff highlighting workflow
  - Real-world scenarios (code diffs, configuration changes)
  - Performance testing with large files
  - Character-level diff highlighting

## Test Data Scenarios

The tests cover various scenarios including:

### Text Modifications
- Simple text changes
- Multi-line text modifications
- Line insertions and deletions
- Mixed line endings

### Code Scenarios
- Programming language diffs
- JSON/XML configuration changes
- Large file modifications

### Edge Cases
- Empty strings
- Unicode characters (🌍, 世界)
- Special characters (\t, \n, \r)
- Very large texts (10k+ lines)
- Binary-like data

### Performance Tests
- Scalability with increasing text size
- Concurrent access (thread safety)
- Memory efficiency
- Processing time validation

## Running the Tests

```bash
# Run all DiffHighlight tests
dotnet test --filter "Test.MoLibrary.Core.MoDiffHighlight"

# Run specific test class
dotnet test --filter "MyersDiffAlgorithmTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Architecture

The tests follow these principles:

1. **AAA Pattern**: Arrange, Act, Assert structure
2. **Test Isolation**: Each test is independent and can run in any order
3. **Mock Usage**: Using NSubstitute for dependency mocking
4. **Fluent Assertions**: For readable and comprehensive assertions
5. **Theory Tests**: For data-driven testing scenarios
6. **Performance Assertions**: Ensuring reasonable execution times

## Coverage Goals

- **Algorithm Accuracy**: Verify Myers diff produces optimal edit scripts
- **Renderer Output**: Ensure all renderers produce valid, formatted output
- **Error Handling**: Validate proper exception handling and logging
- **Performance**: Ensure scalable performance with large inputs
- **Thread Safety**: Verify concurrent usage scenarios
- **Integration**: Test complete end-to-end workflows

## Dependencies

The tests use the following frameworks:
- **xUnit**: Primary testing framework
- **FluentAssertions**: For expressive assertions
- **NSubstitute**: For mocking dependencies
- **ASP.NET Core TestHost**: For integration testing

All test dependencies are managed through the main test project file.