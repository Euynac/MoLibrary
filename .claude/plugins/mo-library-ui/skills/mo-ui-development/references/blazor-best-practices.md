# Blazor UI Best Practices

## Overview

This document defines best practices for developing Blazor UI in the MoLibrary framework, focusing on code quality, maintainability, and user experience.

## 1. Component Architecture Principles

### 1.1 Component Hierarchy

- **Base Components (Common)**: Reusable atomic components
- **Business Components (Business)**: Feature-specific composite components
- **Page Components (Pages)**: Complete page-level components

### 1.2 Single Responsibility Principle

- Each component handles one specific function
- Complex features combine multiple simple components
- Separate presentation logic from business logic

### 1.3 Component Communication

- Use Parameters for parent-to-child communication
- Use EventCallback for child-to-parent events
- Use state containers for complex state management

## 2. Lifecycle Best Practices

### 2.1 Avoid Time-Consuming Operations in OnInitializedAsync

```csharp
// Wrong
protected override async Task OnInitializedAsync()
{
    // Never perform time-consuming data loading here
    await LoadLargeDataSetAsync();
    // Never perform JavaScript interop here
    await JSRuntime.InvokeVoidAsync("initializeChart");
}

// Correct
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        // Load data after first render
        await LoadLargeDataSetAsync();
        // JavaScript interop belongs here
        await JSRuntime.InvokeVoidAsync("initializeChart");
        StateHasChanged();
    }
}
```

### 2.2 Use CancellationToken for Async Operations

```csharp
@implements IAsyncDisposable

@code {
    private CancellationTokenSource? _cts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _cts = new CancellationTokenSource();
            await LoadDataAsync(_cts.Token);
        }
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await DataService.GetDataAsync(cancellationToken);
            ProcessData(data);
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

## 3. Performance Optimization

### 3.1 Use @key for List Rendering

```razor
@foreach (var item in Items)
{
    <div @key="item.Id">
        <ItemComponent Item="@item" />
    </div>
}
```

### 3.2 Avoid Unnecessary Rerenders

```csharp
// Control rendering with ShouldRender
protected override bool ShouldRender()
{
    // Only rerender when data actually changes
    return _hasDataChanged;
}
```

### 3.3 Use Virtualization for Large Data Sets

```razor
<MudVirtualize Items="@LargeDataSet" Context="item">
    <ItemTemplate>
        <ItemDisplay Item="@item" />
    </ItemTemplate>
</MudVirtualize>
```

## 4. Style Management

### 4.1 Use CSS Variables

```css
:root {
    --mo-primary-color: #7e6fff;
    --mo-spacing-unit: 8px;
    --mo-border-radius: 4px;
}

.mo-component {
    padding: calc(var(--mo-spacing-unit) * 2);
    border-radius: var(--mo-border-radius);
}
```

### 4.2 Component Style Isolation

- Use `.razor.css` files for component style isolation
- Avoid global style selectors
- Use BEM naming convention or CSS Modules

### 4.3 Responsive Design

```css
.mo-container {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: var(--mo-spacing-unit);
}

@media (max-width: 768px) {
    .mo-container {
        grid-template-columns: 1fr;
    }
}
```

## 5. State Management

### 5.1 Use State Containers

```csharp
public class AppStateContainer
{
    private string _userName = string.Empty;

    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            NotifyStateChanged();
        }
    }

    public event Action? OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

### 5.2 Use State Containers in Components

```csharp
@inject AppStateContainer AppState
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        AppState.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        AppState.OnChange -= StateHasChanged;
    }
}
```

## 6. Error Handling

### 6.1 Use ErrorBoundary

```razor
<ErrorBoundary>
    <ChildContent>
        <ComplexComponent />
    </ChildContent>
    <ErrorContent Context="exception">
        <MudAlert Severity="Severity.Error">
            Error: @exception.Message
        </MudAlert>
    </ErrorContent>
</ErrorBoundary>
```

### 6.2 Service Layer Error Handling

```csharp
public async Task<Res<TData>> GetDataAsync()
{
    try
    {
        var data = await FetchDataAsync();
        return Res.Ok(data);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to get data");
        return Res.Fail($"Failed to get data: {ex.Message}");
    }
}
```

## 7. Form Handling

### 7.1 Use EditForm with Validation

```razor
<EditForm Model="@model" OnValidSubmit="@HandleValidSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />

    <MudTextField @bind-Value="model.Name"
                  Label="Name"
                  For="@(() => model.Name)" />

    <MudButton ButtonType="ButtonType.Submit"
               Variant="Variant.Filled"
               Color="Color.Primary">
        Submit
    </MudButton>
</EditForm>
```

### 7.2 Custom Validation

```csharp
public class CustomValidator : ComponentBase
{
    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    protected override void OnInitialized()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(
                $"{nameof(CustomValidator)} requires a cascading parameter of type {nameof(EditContext)}.");
        }

        CurrentEditContext.OnValidationRequested += ValidateModel;
    }

    private void ValidateModel(object? sender, ValidationRequestedEventArgs e)
    {
        // Custom validation logic
    }
}
```

## 8. Accessibility

### 8.1 Use Semantic HTML

```razor
<nav aria-label="Main navigation">
    <ul>
        <li><a href="/">Home</a></li>
        <li><a href="/about">About</a></li>
    </ul>
</nav>
```

### 8.2 Keyboard Navigation Support

```razor
<div @onkeydown="HandleKeyDown" tabindex="0">
    <!-- Keyboard navigable content -->
</div>

@code {
    private void HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowUp":
                // Handle up navigation
                break;
            case "ArrowDown":
                // Handle down navigation
                break;
        }
    }
}
```

## 9. Component Reuse Patterns

### 9.1 Generic Components

```razor
@typeparam TItem

<div class="mo-list">
    @foreach (var item in Items)
    {
        @ItemTemplate(item)
    }
</div>

@code {
    [Parameter, EditorRequired]
    public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();

    [Parameter, EditorRequired]
    public RenderFragment<TItem> ItemTemplate { get; set; } = null!;
}
```

### 9.2 Composition Over Inheritance

```razor
<!-- Base card component -->
<MoCard>
    <Header>
        @HeaderContent
    </Header>
    <Body>
        @BodyContent
    </Body>
    <Footer>
        @FooterContent
    </Footer>
</MoCard>

<!-- Specific business card -->
<UserCard User="@user">
    <Actions>
        <MudButton>Edit</MudButton>
        <MudButton>Delete</MudButton>
    </Actions>
</UserCard>
```

## 10. MudBlazor Specific Best Practices

### 10.1 Correct Icon Property Usage

```razor
<!-- Correct: Use @ prefix -->
<MudIconButton Icon="@Icons.Material.Filled.Add" />

<!-- Wrong: Missing @ prefix -->
<MudIconButton Icon="Icons.Material.Filled.Add" />
```

### 10.2 Explicit Type Parameters for Generic Components

```razor
<!-- Correct: Explicitly specify T type -->
<MudSwitch T="bool" @bind-Checked="@IsEnabled" />
<MudChip T="string" Value="@chipValue" />

<!-- Wrong: Missing type parameter -->
<MudSwitch @bind-Checked="@IsEnabled" />
```

### 10.3 Use MudBlazor Theme System

```csharp
// Configure theme in layout component
<MudThemeProvider Theme="@_theme" IsDarkMode="@_isDarkMode" />

@code {
    private MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#7e6fff",
            Secondary = "#ff4081"
        }
    };
}
```

## 11. Module Development Best Practices

### 11.1 Dependency Injection Configuration

Use module options by injecting `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>`:

```csharp
@inject IOptions<ModuleUIOption> Options

@code {
    protected override void OnInitialized()
    {
        var appBarName = Options.Value.UIAppBarName;
    }
}
```

### 11.2 Code Reuse Strategy

- Abstract common base classes or interfaces for similar components
- Encapsulate common UI interaction logic into reusable services
- Use MudBlazor component library for unified styles and themes

### 11.3 Performance Tips

- Split large components into smaller ones to reduce rerender scope
- Use `@key` directive to optimize list rendering
- Avoid complex calculations in templates

### 11.4 Unified Error Handling

- Use `ISnackbar` to display user-friendly error messages
- Log detailed debug information for troubleshooting
- Implement unified error handling mechanisms

```csharp
@inject ISnackbar Snackbar

private async Task HandleOperation()
{
    if ((await Service.ExecuteAsync()).IsFailed(out var error))
    {
        Snackbar.Add(error.Message, Severity.Error);
        return;
    }

    Snackbar.Add("Operation successful", Severity.Success);
}
```

## 12. Service Layer Best Practices

### 12.1 Return Value Standards

- All service method return values must not be null, use `Res<T>` or `Res` types
- Return `Res.Ok(data)` on success, `Res.Fail(errorMessage)` on failure
- Exceptions must be caught and return `Res.Fail`

### 12.2 Service Call Pattern

```csharp
// Inject service in Blazor page
@inject UserService UserService

@code {
    private async Task LoadDataAsync()
    {
        // Use IsFailed method to check result and get data or error
        if ((await UserService.GetDataAsync(parameter)).IsFailed(out var error, out var data))
        {
            // Handle error case
            Snackbar.Add($"Operation failed: {error.Message}", Severity.Error);
            return;
        }

        // Handle success case, data is not null
        ProcessData(data);
    }
}
```

### 12.3 Service Registration

Register services in module's `ConfigureServices` method:

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<UserService>();
}
```

## 13. Data Model Management

### 13.1 Model Organization

- Place page-related data models in `UI{ModuleName}/Models/` directory
- Use strongly-typed models, avoid dynamic types
- Model naming should clearly express purpose

### 13.2 Model Naming Conventions

- Request models: `{Feature}Request`
- Response models: `{Feature}Response`
- View models: `{Feature}ViewModel`

## Summary

Following these best practices helps build high-quality, maintainable, and high-performance Blazor applications. These are guiding principles that should be flexibly applied based on specific project requirements.
