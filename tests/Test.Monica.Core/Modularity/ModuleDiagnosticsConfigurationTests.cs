using AwesomeAssertions;
using Monica.Core;
using Monica.Core.Modularity.Diagnostics.Models;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class ModuleDiagnosticsConfigurationTests
{
    [Fact]
    public void Validate_WhenBudgetsAreAbsentOrEntirelyUnset_ShouldAcceptTheConfiguration()
    {
        var absent = new MonicaModuleSystemOptions();
        var unset = new MonicaModuleSystemOptions
        {
            StartupPerformanceBudgets = new ModuleStartupPerformanceBudgets()
        };

        Action validateAbsent = absent.Validate;
        Action validateUnset = unset.Validate;

        validateAbsent.Should().NotThrow();
        validateUnset.Should().NotThrow();
    }

    [Theory]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.TotalComposition), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.ServiceRegistration), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.TypeDiscovery), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.AggregateBarrierWait), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.LongestModuleCallback), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.LongestStartupQueue), 0L)]
    [InlineData(nameof(ModuleStartupPerformanceBudgets.TotalComposition), -1L)]
    public void Validate_WhenConfiguredBudgetIsNotPositive_ShouldRejectThatBudget(
        string propertyName,
        long ticks)
    {
        var budgets = new ModuleStartupPerformanceBudgets();
        SetBudget(budgets, propertyName, TimeSpan.FromTicks(ticks));
        var options = new MonicaModuleSystemOptions
        {
            StartupPerformanceBudgets = budgets
        };

        Action validate = options.Validate;

        validate.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(propertyName);
    }

    [Fact]
    public void OptionDiagnosticsExposureMode_WhenNotConfigured_ShouldDefaultToRedacted()
    {
        var options = new MonicaModuleSystemOptions();

        options.OptionDiagnosticsExposureMode.Should().Be(ModuleOptionDiagnosticsExposureMode.Redacted);
    }

    [Fact]
    public void Validate_WhenOptionDiagnosticsExposureModeIsUndefined_ShouldRejectTheConfiguration()
    {
        var options = new MonicaModuleSystemOptions
        {
            OptionDiagnosticsExposureMode = (ModuleOptionDiagnosticsExposureMode)int.MaxValue
        };

        Action validate = options.Validate;

        validate.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(MonicaModuleSystemOptions.OptionDiagnosticsExposureMode));
    }

    [Fact]
    public void Build_WhenNestedSensitivityRulesAreConfigured_ShouldRetainExactPaths()
    {
        var policy = new ModuleOptionDiagnosticsPolicy<OptionProjectionProbe>()
            .MarkSensitive(static option => option.Nested.Secret);

        var definition = policy.Build();

        definition.SensitivePaths.Should().Equal("Nested.Secret");
    }

    [Fact]
    public void MarkSensitive_WhenExpressionIsNotAPropertyPath_ShouldRejectTheRule()
    {
        var policy = new ModuleOptionDiagnosticsPolicy<OptionProjectionProbe>();

        Action configure = () => policy.MarkSensitive(static option => option.Value.ToUpperInvariant());

        configure.Should().Throw<ArgumentException>()
            .WithParameterName("property")
            .WithMessage("*property path*");
    }

    private static void SetBudget(
        ModuleStartupPerformanceBudgets budgets,
        string propertyName,
        TimeSpan value)
    {
        switch (propertyName)
        {
            case nameof(ModuleStartupPerformanceBudgets.TotalComposition):
                budgets.TotalComposition = value;
                break;
            case nameof(ModuleStartupPerformanceBudgets.ServiceRegistration):
                budgets.ServiceRegistration = value;
                break;
            case nameof(ModuleStartupPerformanceBudgets.TypeDiscovery):
                budgets.TypeDiscovery = value;
                break;
            case nameof(ModuleStartupPerformanceBudgets.AggregateBarrierWait):
                budgets.AggregateBarrierWait = value;
                break;
            case nameof(ModuleStartupPerformanceBudgets.LongestModuleCallback):
                budgets.LongestModuleCallback = value;
                break;
            case nameof(ModuleStartupPerformanceBudgets.LongestStartupQueue):
                budgets.LongestStartupQueue = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyName), propertyName, "Unknown budget property.");
        }
    }

    private sealed class OptionProjectionProbe
    {
        public string Value { get; init; } = "value";

        public NestedOptionProjectionProbe Nested { get; init; } = new();

    }

    private sealed class NestedOptionProjectionProbe
    {
        public string Secret { get; init; } = "secret";
    }
}
