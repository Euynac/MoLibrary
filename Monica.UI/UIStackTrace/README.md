# UIStackTrace Module

## Overview

The UIStackTrace module provides rich .NET stack trace visualization with syntax highlighting, structured parsing, and support for nested inner exceptions. This module is designed to be a reusable component that can be used across multiple modules to display stack traces in an elegant and readable format.

## Features

- **Syntax Highlighting**: Color-coded display of exception types, methods, file paths, and line numbers
- **Inner Exception Support**: Visual indentation and separation for nested inner exceptions
- **Detailed Parameter Parsing**: Automatically extracts and displays method parameters with type information
- **Responsive Design**: Works seamlessly on desktop and mobile viewports
- **Theme-Aware**: Automatically adapts to dark/light mode using MudBlazor CSS variables
- **Graceful Degradation**: Falls back to plain text display if parsing fails
- **Zero External Dependencies**: All assets are local, supporting offline/intranet environments

## Components

### StackTraceViewer

A Blazor component that parses and displays .NET stack traces with rich formatting.

**Location**: `Monica.UI/UIStackTrace/Components/StackTraceViewer.razor`

**Usage**:

```razor
@using Monica.UI.UIStackTrace.Components

<StackTraceViewer Message="@exceptionStackTrace" />
```

**Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `Message` | `string?` | The raw .NET stack trace text to display and parse |

**Supported Formats**:

- Exception headers: `ExceptionType: Exception message`
- Stack frames: `   at Namespace.Class.Method(...) in File.cs:line 123`
- Inner exceptions: `---> InnerException: Message`
- Nested parameters with generic types

## Services

### StackTraceParserService

Parses raw .NET exception stack traces into structured data.

**Location**: `Monica.UI/UIStackTrace/Services/StackTraceParserService.cs`

**Registration**: Automatically registered as Singleton in the DIContainer

**Public Methods**:

```csharp
public ParseResult Parse(string? stackTraceText)
```

Parses a stack trace text and returns a `ParseResult` containing the parsed lines.

```csharp
public bool IsLikelyStackTrace(string? text)
```

Detects if given text looks like a .NET stack trace (useful for conditional rendering).

## Models

### StackTraceLine

Represents a single line in a parsed stack trace.

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `LineType` | `StackLineType` | The type of this line (ExceptionHeader, StackFrame, etc.) |
| `ExceptionType` | `string?` | Exception type name (e.g., System.InvalidOperationException) |
| `Message` | `string?` | Exception message text |
| `Namespace` | `string?` | Namespace part of the method |
| `ClassName` | `string?` | Class name part of the method |
| `MethodName` | `string?` | Method name |
| `Parameters` | `string?` | Raw parameter list (with brackets) |
| `ParsedParameters` | `List<MethodParameter>` | Structured parameter information |
| `FilePath` | `string?` | Full file path |
| `FileName` | `string?` | File name extracted from path |
| `LineNumber` | `int?` | Line number in source code |
| `RawContent` | `string?` | Original unparsed content |

### ParseResult

Contains the result of parsing a stack trace.

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | Whether parsing was successful |
| `Lines` | `List<StackTraceLine>` | The parsed stack trace lines |
| `ErrorMessage` | `string?` | Error message if parsing failed |
| `OriginalText` | `string?` | Original input text (for fallback display) |

### StackLineType

Enumeration of line types in a parsed stack trace:

```csharp
public enum StackLineType
{
    ExceptionHeader,   // Exception type and message
    StackFrame,        // "   at ..."
    InnerException,    // Inner exception marker
    PlainText,         // Other text lines
    ParseError         // Parsing error
}
```

### MethodParameter

Represents a parsed method parameter.

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string?` | Parameter type (e.g., "string", "Dictionary<string, int>") |
| `Name` | `string?` | Parameter name |

## Integration

### Using in Other Modules

To use the StackTraceViewer in another module:

1. **Add Project Reference** (if not already present):

   Add to your project's `.csproj`:
   ```xml
   <ProjectReference Include="..\Monica.UI\Monica.UI.csproj" />
   ```

2. **Update _Imports.razor**:

   ```razor
   @using Monica.UI.UIStackTrace.Components
   ```

3. **Add Module Dependency** (in your module's `ClaimDependencies` method):

   ```csharp
   DependsOnModule<ModuleUIStackTraceGuide>().Register();
   ```

4. **Use the Component**:

   ```razor
   <StackTraceViewer Message="@exception.StackTrace" />
   ```

### Current Integrations

The UIStackTrace module is currently used by:

- **Monica.JobScheduler.UI** - Displays job execution stack traces in the Job Instance Detail dialog
- **Monica.DataChannel** - Shows channel exception stack traces in the Exception Details dialog

## Styling

The component uses **CSS isolation** (`StackTraceViewer.razor.css`) and applies **MudBlazor CSS variables** for theming:

- Background colors adapt to light/dark mode
- Text colors use theme palette (error, warning, info, primary)
- Responsive font sizes for mobile devices

### Custom Styling

To override component styles in your application, use CSS isolation in your parent component:

```css
/* MyComponent.razor.css */
.my-container ::deep .stack-trace-container {
    max-height: 600px;  /* Your custom max height */
    font-size: 0.9rem;  /* Your custom font size */
}
```

## Examples

### Basic Usage

```razor
@page "/debug"
@using Monica.UI.UIStackTrace.Components
@inject ILogger<Debug> Logger

<h1>Exception Stack Trace Viewer</h1>

@if (stackTrace != null)
{
    <StackTraceViewer Message="@stackTrace" />
}

@code {
    private string? stackTrace;

    protected override void OnInitialized()
    {
        try
        {
            // Some operation that throws
            throw new InvalidOperationException("Test exception");
        }
        catch (Exception ex)
        {
            stackTrace = ex.ToString();
        }
    }
}
```

### Conditional Display

```razor
@using Monica.UI.UIStackTrace.Components
@using Monica.UI.UIStackTrace.Services
@inject StackTraceParserService ParserService

@if (ParserService.IsLikelyStackTrace(message))
{
    <StackTraceViewer Message="@message" />
}
else
{
    <p>@message</p>
}
```

### In a Dialog

```razor
@using Monica.UI.UIStackTrace.Components
@using MudBlazor

<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.ErrorOutline" Class="mr-3" />
            Exception Details
        </MudText>
    </TitleContent>
    <DialogContent>
        <StackTraceViewer Message="@Exception.StackTrace" />
    </DialogContent>
</MudDialog>
```

## Performance

- **Parser**: O(n) where n is the length of the stack trace text
- **Rendering**: Optimized for stack traces up to 10,000 lines
- **Service**: Registered as Singleton - reused across all requests

## Browser Compatibility

- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Full support
- Mobile browsers: Responsive design supported

## Dependencies

- **Monica.UI** (UICore module)
- **MudBlazor**: For theme variables and responsive design
- **.NET 8.0 or higher**

## License

Part of Monica framework - see project LICENSE file for details.
