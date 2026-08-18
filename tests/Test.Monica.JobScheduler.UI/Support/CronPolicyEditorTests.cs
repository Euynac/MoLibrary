using AwesomeAssertions;
using Monica.JobScheduler.UI.UIJobScheduler.Support;
using Monica.JobScheduler.Utils;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Support;

public sealed class CronPolicyEditorTests
{
    private static readonly DateTimeOffset OBSERVED_AT =
        new(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("0 12 * * *")]
    [InlineData("0 9 * * MON-FRI")]
    [InlineData("0 0 1 * *")]
    [InlineData("0 22-2 * * *")]
    [InlineData("0 */3 * * *")]
    [InlineData("0 0 L * *")]
    [InlineData("0 0 15W * *")]
    [InlineData("0 0 * * FRI#3")]
    [InlineData("0 0 ? * MON")]
    [InlineData("0 */5 * * * *")]
    [InlineData("0 0 12 * * *")]
    [InlineData("0 0 9 * * MON-FRI")]
    [InlineData("0 0 0 1 * *")]
    public void Evaluate_WhenExpressionIsDocumented_ShouldUseRuntimeParser(string expression)
    {
        var result = CronPolicyEditor.Evaluate(expression, "UTC", null, null, OBSERVED_AT);

        result.IsValid.Should().BeTrue(result.Error);
        result.Fields.Should().HaveCount(expression.Split(' ').Length);
    }

    [Fact]
    public void ConvertMode_WhenFiveFields_ShouldPrependZeroSeconds()
    {
        var result = CronPolicyEditor.ConvertMode(
            "*/5 * * * *",
            CronExpressionMode.SixFieldsWithSeconds);

        result.IsSuccess.Should().BeTrue();
        result.Expression.Should().Be("0 */5 * * * *");
    }

    [Fact]
    public void ConvertMode_WhenSecondsAreZero_ShouldRemoveSeconds()
    {
        var result = CronPolicyEditor.ConvertMode(
            "0 */5 * * * *",
            CronExpressionMode.FiveFields);

        result.IsSuccess.Should().BeTrue();
        result.Expression.Should().Be("*/5 * * * *");
    }

    [Fact]
    public void EvaluateAndConvertMode_WhenFieldsUseMixedWhitespace_ShouldCanonicalizeLikeRuntime()
    {
        const string sixFields = "0\t*/5\n*  *\r\n*\t*";

        var evaluation = CronPolicyEditor.Evaluate(sixFields, "UTC", null, null, OBSERVED_AT);
        var fiveFieldConversion = CronPolicyEditor.ConvertMode(
            sixFields,
            CronExpressionMode.FiveFields);
        var sixFieldConversion = CronPolicyEditor.ConvertMode(
            "*/5\t*\n* *\r\n*",
            CronExpressionMode.SixFieldsWithSeconds);

        evaluation.IsValid.Should().BeTrue(evaluation.Error);
        evaluation.Mode.Should().Be(CronExpressionMode.SixFieldsWithSeconds);
        fiveFieldConversion.IsSuccess.Should().BeTrue();
        fiveFieldConversion.Expression.Should().Be("*/5 * * * *");
        sixFieldConversion.IsSuccess.Should().BeTrue();
        sixFieldConversion.Expression.Should().Be("0 */5 * * * *");
    }

    [Fact]
    public void ConvertMode_WhenSecondsAreNonZero_ShouldRefuseCadenceChange()
    {
        var result = CronPolicyEditor.ConvertMode(
            "15 */5 * * * *",
            CronExpressionMode.FiveFields);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(CronExpressionConversionFailure.NonZeroSeconds);
        result.Expression.Should().Be("15 */5 * * * *");
    }

    [Theory]
    [InlineData((int)CronSimpleScheduleMode.EveryMinute, "* * * * *", "7 * * * * *")]
    [InlineData((int)CronSimpleScheduleMode.EveryHour, "0 * * * *", "7 0 * * * *")]
    [InlineData((int)CronSimpleScheduleMode.EveryDay, "23 14 * * *", "7 23 14 * * *")]
    [InlineData((int)CronSimpleScheduleMode.EveryWeek, "23 14 * * 3", "7 23 14 * * 3")]
    [InlineData((int)CronSimpleScheduleMode.EveryMonth, "23 14 17 * *", "7 23 14 17 * *")]
    public void BuildSimpleExpression_WhenPresetSelected_ShouldBuildBothShapes(
        int modeValue,
        string fiveFields,
        string sixFields)
    {
        var settings = new CronSimpleScheduleSettings
        {
            Mode = (CronSimpleScheduleMode)modeValue,
            Second = 7,
            Minute = 23,
            Hour = 14,
            DayOfMonth = 17,
            DayOfWeek = 3
        };

        var actualFiveFields = CronPolicyEditor.BuildSimpleExpression(
            settings,
            CronExpressionMode.FiveFields);
        var actualSixFields = CronPolicyEditor.BuildSimpleExpression(
            settings,
            CronExpressionMode.SixFieldsWithSeconds);

        actualFiveFields.Should().Be(fiveFields);
        actualSixFields.Should().Be(sixFields);
        CronHelper.Parse(actualFiveFields).Should().NotBeNull();
        CronHelper.Parse(actualSixFields).Should().NotBeNull();
    }

    [Fact]
    public void BuildSimpleExpression_WhenCustomSelected_ShouldIncludeCustomSecondsOnlyInSixFieldMode()
    {
        var settings = new CronSimpleScheduleSettings
        {
            Mode = CronSimpleScheduleMode.Custom,
            CustomSecond = "*/10",
            CustomMinute = "1,16,31,46",
            CustomHour = "8-18",
            CustomDayOfMonth = "?",
            CustomMonth = "JAN-MAR",
            CustomDayOfWeek = "MON-FRI"
        };

        var fiveFields = CronPolicyEditor.BuildSimpleExpression(settings, CronExpressionMode.FiveFields);
        var sixFields = CronPolicyEditor.BuildSimpleExpression(
            settings,
            CronExpressionMode.SixFieldsWithSeconds);

        fiveFields.Should().Be("1,16,31,46 8-18 ? JAN-MAR MON-FRI");
        sixFields.Should().Be("*/10 1,16,31,46 8-18 ? JAN-MAR MON-FRI");
        CronHelper.Parse(fiveFields).Should().NotBeNull();
        CronHelper.Parse(sixFields).Should().NotBeNull();
    }

    [Fact]
    public void Evaluate_WhenStartBoundaryIsAhead_ShouldPreviewFromBoundaryWithoutBackfill()
    {
        var start = new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);

        var result = CronPolicyEditor.Evaluate("0 * * * *", "UTC", start, null, OBSERVED_AT);

        result.IsValid.Should().BeTrue();
        result.Occurrences.Should().HaveCount(5);
        result.Occurrences[0].Utc.Should().Be(start);
    }

    [Fact]
    public void Evaluate_WhenEndBoundaryPassed_ShouldReturnNoFutureOccurrence()
    {
        var result = CronPolicyEditor.Evaluate(
            "0 * * * *",
            "UTC",
            null,
            OBSERVED_AT.AddMinutes(-1),
            OBSERVED_AT);

        result.IsValid.Should().BeTrue();
        result.Occurrences.Should().BeEmpty();
    }

    [Fact]
    public void PolicyDraft_WhenPreviewClockAdvances_ShouldKeepAuthoritativeObservationAndDropPastTicks()
    {
        var draft = CronPolicyDraft.Create(
            "* * * * *",
            null,
            "UTC",
            null,
            null,
            false,
            null,
            false,
            null,
            OBSERVED_AT,
            OBSERVED_AT.AddMinutes(1));
        var previewFrom = OBSERVED_AT.AddHours(2).AddSeconds(30);

        draft.RefreshEvaluation(previewFrom);

        draft.ObservedAtUtc.Should().Be(OBSERVED_AT);
        draft.PreviewFromUtc.Should().Be(previewFrom);
        draft.ProposedNextOccurrenceUtc.Should().Be(OBSERVED_AT.AddHours(2).AddMinutes(1));
        draft.Evaluation.Occurrences.Should().OnlyContain(occurrence => occurrence.Utc > previewFrom);
    }

    [Fact]
    public void BoundaryDraft_WhenLocalTimeIsSkippedByDst_ShouldRejectTheBoundary()
    {
        var timeZone = CreateDstTimeZone();
        var boundary = CronBoundaryDraft.Create(null, true, null, timeZone);
        boundary.SetMode(CronBoundaryOverrideMode.Value, null, OBSERVED_AT);
        boundary.LocalDate = new DateTime(2026, 3, 8);
        boundary.LocalTime = new TimeSpan(2, 30, 0);

        boundary.TryGetOverrideUtc(out var value, out var failure).Should().BeFalse();
        value.Should().BeNull();
        failure.Should().Be(CronBoundaryValidationFailure.InvalidLocalTime);
    }

    [Fact]
    public void BoundaryDraft_WhenLocalTimeRepeatsAtDstEnd_ShouldChooseEarlierUtcInstant()
    {
        var timeZone = CreateDstTimeZone();
        var boundary = CronBoundaryDraft.Create(null, true, null, timeZone);
        boundary.SetMode(CronBoundaryOverrideMode.Value, null, OBSERVED_AT);
        boundary.LocalDate = new DateTime(2026, 11, 1);
        boundary.LocalTime = new TimeSpan(1, 30, 0);

        boundary.TryGetOverrideUtc(out var value, out var failure).Should().BeTrue();
        failure.Should().Be(CronBoundaryValidationFailure.None);
        value.Should().Be(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Evaluate_WhenCadenceCrossesDstFallback_ShouldKeepUtcOccurrencesMonotonic()
    {
        var timeZone = CreateDstTimeZone();

        var result = CronPolicyEditor.Evaluate(
            "30 1 * * *",
            timeZone,
            null,
            null,
            new DateTimeOffset(2026, 10, 31, 6, 0, 0, TimeSpan.Zero));

        result.IsValid.Should().BeTrue(result.Error);
        result.Occurrences.Should().HaveCount(5);
        result.Occurrences.Select(item => item.Utc)
            .Should().BeInAscendingOrder();
        result.Occurrences[0].Utc.Should()
            .Be(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Evaluate_WhenDayOfMonthAndDayOfWeekAreConstrained_ShouldRequireBoth()
    {
        var result = CronPolicyEditor.Evaluate(
            "0 0 1 * MON",
            "UTC",
            null,
            null,
            new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero));

        result.IsValid.Should().BeTrue(result.Error);
        result.Occurrences.Should().NotBeEmpty();
        result.Occurrences[0].Utc.Should()
            .Be(new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static TimeZoneInfo CreateDstTimeZone()
    {
        var daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            3,
            2,
            DayOfWeek.Sunday);
        var daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            11,
            1,
            DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);
        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/Policy-DST",
            TimeSpan.FromHours(-5),
            "Test policy DST",
            "Test standard",
            "Test daylight",
            [rule]);
    }
}
