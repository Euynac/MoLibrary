using AwesomeAssertions;
using Monica.Core;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
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
    public void ExposeValue_WhenGetterReturnsAComplexType_ShouldRejectTheDeclaration()
    {
        var builder = new ModuleOptionDiagnosticsBuilder<OptionProjectionProbe>();

        Action declare = () => builder.ExposeValue(
            "ComplexGraph",
            static options => options.ComplexGraph);

        declare.Should().Throw<ArgumentException>()
            .WithParameterName("getter")
            .WithMessage("*not a supported diagnostic scalar*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1025)]
    public void ExposeValue_WhenRepresentationLimitIsOutsideTheBound_ShouldRejectTheDeclaration(
        int maxLength)
    {
        var builder = new ModuleOptionDiagnosticsBuilder<OptionProjectionProbe>();

        Action declare = () => builder.ExposeValue(
            "BoundedValue",
            static options => options.Value,
            maxLength);

        declare.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(maxLength));
    }

    [Fact]
    public void Project_WhenAllowListedGettersFail_ShouldKeepEveryEntryUnavailable()
    {
        var builder = new ModuleOptionDiagnosticsBuilder<OptionProjectionProbe>()
            .ExposeValue<string>("Value", static _ => throw new InvalidOperationException("value failure"))
            .ExposePresence("Secret", static _ => throw new InvalidOperationException("presence failure"))
            .ExposeCount("Items", static _ => throw new InvalidOperationException("count failure"));
        var projection = builder.Build(typeof(DiagnosticsProviderModule));

        var diagnostics = projection.Project(
            ModuleKey.FromModuleType(typeof(DiagnosticsProviderModule)),
            new OptionProjectionProbe());

        diagnostics.IsConfigured.Should().BeTrue();
        diagnostics.Entries.Should().HaveCount(3);
        diagnostics.Entries.Should().OnlyContain(entry =>
            entry.Kind == ModuleOptionDiagnosticValueKind.Unavailable
            && entry.Value == null
            && entry.IsPresent == null
            && entry.Count == null);
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
        internal string Value { get; init; } = "value";

        internal object ComplexGraph { get; } = new();
    }
}
