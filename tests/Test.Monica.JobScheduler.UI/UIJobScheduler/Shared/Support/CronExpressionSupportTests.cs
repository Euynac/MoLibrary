using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.UI.Localization;
using Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;
using Monica.Modules;
using Monica.Testing.Localization;
using Monica.Testing.Results;
using Xunit;

namespace Test.Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

public class CronExpressionSupportTests
{
    [Fact]
    public void ValidateExpression_WhenExpressionIsEmpty_ShouldReturnFailure()
    {
        var support = CreateSupport();

        var result = support.ValidateExpression(string.Empty, CronFormat.Standard);

        result.ShouldFail(expectedMessage: "Services:Errors:ExpressionEmpty");
    }

    [Fact]
    public void BuildFromSimpleSettings_WhenDailyStandardScheduleIsRequested_ShouldReturnExpressionInData()
    {
        var support = CreateSupport();

        var result = support.BuildFromSimpleSettings(new SimpleSettings
        {
            Type = SimpleSettingsType.EveryDay,
            Minute = 15,
            Hour = 6
        }, CronFormat.Standard);

        var expression = result.ShouldSucceed();
        expression.Should().Be("15 6 * * *");
        result.Data.Should().Be("15 6 * * *");
    }

    [Fact]
    public void ConvertFormat_WhenConvertingStandardToQuartz_ShouldReturnConvertedExpressionInData()
    {
        var support = CreateSupport();

        var result = support.ConvertFormat("15 6 * * *", CronFormat.Standard, CronFormat.Quartz);

        var expression = result.ShouldSucceed();
        expression.Should().Be("0 15 6 * * *");
        result.Data.Should().Be("0 15 6 * * *");
    }

    private static CronExpressionSupport CreateSupport()
    {
        return new CronExpressionSupport(
            new EchoStringLocalizer<JobSchedulerResource>(),
            Options.Create(new ModuleJobSchedulerOption()));
    }
}
