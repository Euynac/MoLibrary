using Cronos;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Utils;

namespace Monica.JobScheduler.UI.UIJobScheduler.Support;

/// <summary>
/// Identifies the two Cron expression shapes accepted by the scheduler runtime.
/// </summary>
internal enum CronExpressionMode
{
    FiveFields,
    SixFieldsWithSeconds
}

/// <summary>
/// Identifies the operator-friendly schedule builders exposed by the policy workbench.
/// </summary>
internal enum CronSimpleScheduleMode
{
    EveryMinute,
    EveryHour,
    EveryDay,
    EveryWeek,
    EveryMonth,
    Custom
}

/// <summary>
/// Identifies whether one optional schedule boundary is inherited, explicitly removed, or replaced.
/// </summary>
internal enum CronBoundaryOverrideMode
{
    Inherit,
    None,
    Value
}

/// <summary>
/// Identifies why a configured-timezone boundary cannot be projected to UTC.
/// </summary>
internal enum CronBoundaryValidationFailure
{
    None,
    Incomplete,
    InvalidLocalTime
}

/// <summary>
/// Owns the UI representation of one optional boundary in the configured schedule timezone.
/// </summary>
public sealed class CronBoundaryDraft
{
    private TimeZoneInfo _timeZone = TimeZoneInfo.Utc;

    internal CronBoundaryOverrideMode Mode { get; private set; }

    internal DateTime? LocalDate { get; set; }

    internal TimeSpan? LocalTime { get; set; }

    internal DateTimeOffset? OverrideUtc => TryGetOverrideUtc(out var value, out _) ? value : null;

    internal bool HasOverride => Mode != CronBoundaryOverrideMode.Inherit;

    internal CronBoundaryValidationFailure ValidationFailure
    {
        get
        {
            TryGetOverrideUtc(out _, out var failure);
            return failure;
        }
    }

    internal bool IsAmbiguousLocalTime
    {
        get
        {
            if (Mode != CronBoundaryOverrideMode.Value || LocalDate is null || LocalTime is null)
            {
                return false;
            }

            var local = DateTime.SpecifyKind(LocalDate.Value.Date.Add(LocalTime.Value), DateTimeKind.Unspecified);
            return _timeZone.IsAmbiguousTime(local);
        }
    }

    internal static CronBoundaryDraft Create(
        DateTimeOffset? declaredUtc,
        bool hasOverride,
        DateTimeOffset? overrideUtc,
        TimeZoneInfo timeZone)
    {
        var mode = !hasOverride
            ? CronBoundaryOverrideMode.Inherit
            : overrideUtc is null
                ? CronBoundaryOverrideMode.None
                : CronBoundaryOverrideMode.Value;
        var visibleUtc = mode == CronBoundaryOverrideMode.Value ? overrideUtc : declaredUtc;
        var local = visibleUtc is null ? (DateTimeOffset?)null : TimeZoneInfo.ConvertTime(visibleUtc.Value, timeZone);
        return new CronBoundaryDraft
        {
            _timeZone = timeZone,
            Mode = mode,
            LocalDate = local?.Date,
            LocalTime = local?.TimeOfDay
        };
    }

    internal void SetMode(
        CronBoundaryOverrideMode mode,
        DateTimeOffset? declaredUtc,
        DateTimeOffset seedUtc)
    {
        Mode = mode;
        if (mode != CronBoundaryOverrideMode.Value || LocalDate is not null)
        {
            return;
        }

        var seed = declaredUtc ?? seedUtc;
        var local = TimeZoneInfo.ConvertTime(seed, _timeZone);
        LocalDate = local.Date;
        LocalTime = local.TimeOfDay;
    }

    internal void Reset(DateTimeOffset? declaredUtc)
    {
        Mode = CronBoundaryOverrideMode.Inherit;
        if (declaredUtc is not { } value)
        {
            LocalDate = null;
            LocalTime = null;
            return;
        }

        var local = TimeZoneInfo.ConvertTime(value, _timeZone);
        LocalDate = local.Date;
        LocalTime = local.TimeOfDay;
    }

    internal bool TryGetOverrideUtc(
        out DateTimeOffset? value,
        out CronBoundaryValidationFailure failure)
    {
        if (Mode is CronBoundaryOverrideMode.Inherit or CronBoundaryOverrideMode.None)
        {
            value = null;
            failure = CronBoundaryValidationFailure.None;
            return true;
        }

        if (LocalDate is null || LocalTime is null)
        {
            value = null;
            failure = CronBoundaryValidationFailure.Incomplete;
            return false;
        }

        var local = DateTime.SpecifyKind(LocalDate.Value.Date.Add(LocalTime.Value), DateTimeKind.Unspecified);
        if (_timeZone.IsInvalidTime(local))
        {
            value = null;
            failure = CronBoundaryValidationFailure.InvalidLocalTime;
            return false;
        }

        if (_timeZone.IsAmbiguousTime(local))
        {
            var earlierOffset = _timeZone.GetAmbiguousTimeOffsets(local).Max();
            value = new DateTimeOffset(local, earlierOffset).ToUniversalTime();
        }
        else
        {
            value = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, _timeZone), TimeSpan.Zero);
        }

        failure = CronBoundaryValidationFailure.None;
        return true;
    }
}

/// <summary>
/// Owns one prospective schedule draft while preserving the immutable configured timezone.
/// </summary>
public sealed class CronPolicyDraft
{
    private readonly TimeZoneInfo _timeZone;

    private CronPolicyDraft(
        string declaredExpression,
        string? overrideExpression,
        string timeZoneId,
        DateTimeOffset? declaredStartTimeUtc,
        DateTimeOffset? declaredEndTimeUtc,
        CronBoundaryDraft startBoundary,
        CronBoundaryDraft endBoundary,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? authoritativeNextOccurrenceUtc)
    {
        DeclaredExpression = declaredExpression;
        IsExpressionInherited = overrideExpression is null;
        DraftExpression = overrideExpression ?? string.Empty;
        TimeZoneId = timeZoneId;
        DeclaredStartTimeUtc = declaredStartTimeUtc;
        DeclaredEndTimeUtc = declaredEndTimeUtc;
        StartBoundary = startBoundary;
        EndBoundary = endBoundary;
        ObservedAtUtc = observedAtUtc;
        PreviewFromUtc = observedAtUtc.ToUniversalTime();
        AuthoritativeNextOccurrenceUtc = authoritativeNextOccurrenceUtc;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        Mode = CronPolicyEditor.DetectMode(EffectiveExpression);
        Rebuild();
    }

    internal string DeclaredExpression { get; }

    internal bool IsExpressionInherited { get; private set; }

    internal string DraftExpression { get; private set; }

    internal string? OverrideExpression => IsExpressionInherited ? null : DraftExpression;

    internal string EffectiveExpression => IsExpressionInherited ? DeclaredExpression : DraftExpression;

    internal string TimeZoneId { get; }

    internal DateTimeOffset? DeclaredStartTimeUtc { get; }

    internal DateTimeOffset? DeclaredEndTimeUtc { get; }

    internal CronBoundaryDraft StartBoundary { get; }

    internal CronBoundaryDraft EndBoundary { get; }

    internal DateTimeOffset ObservedAtUtc { get; }

    /// <summary>
    /// Gets the live clock position used for the prospective preview. It is intentionally independent from the
    /// authoritative observation captured when the workbench opened.
    /// </summary>
    internal DateTimeOffset PreviewFromUtc { get; private set; }

    internal DateTimeOffset? AuthoritativeNextOccurrenceUtc { get; }

    internal CronExpressionMode Mode { get; private set; }

    internal CronPolicyEvaluation Evaluation { get; private set; } = new();

    internal bool HasAnyOverride => !IsExpressionInherited
                                    || StartBoundary.HasOverride
                                    || EndBoundary.HasOverride;

    internal bool AreBoundariesValid =>
        StartBoundary.ValidationFailure == CronBoundaryValidationFailure.None
        && EndBoundary.ValidationFailure == CronBoundaryValidationFailure.None;

    internal DateTimeOffset? EffectiveStartTimeUtc =>
        StartBoundary.HasOverride ? StartBoundary.OverrideUtc : DeclaredStartTimeUtc;

    internal DateTimeOffset? EffectiveEndTimeUtc =>
        EndBoundary.HasOverride ? EndBoundary.OverrideUtc : DeclaredEndTimeUtc;

    internal DateTimeOffset? ProposedNextOccurrenceUtc => Evaluation.Occurrences.FirstOrDefault()?.Utc;

    internal static CronPolicyDraft Create(
        string declaredExpression,
        string? overrideExpression,
        string timeZoneId,
        DateTimeOffset? declaredStartTimeUtc,
        DateTimeOffset? declaredEndTimeUtc,
        bool hasStartOverride,
        DateTimeOffset? overrideStartTimeUtc,
        bool hasEndOverride,
        DateTimeOffset? overrideEndTimeUtc,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? authoritativeNextOccurrenceUtc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new(
            declaredExpression,
            overrideExpression,
            timeZoneId,
            declaredStartTimeUtc,
            declaredEndTimeUtc,
            CronBoundaryDraft.Create(declaredStartTimeUtc, hasStartOverride, overrideStartTimeUtc, timeZone),
            CronBoundaryDraft.Create(declaredEndTimeUtc, hasEndOverride, overrideEndTimeUtc, timeZone),
            observedAtUtc,
            authoritativeNextOccurrenceUtc);
    }

    internal CronPolicyDraft Rebase(
        string declaredExpression,
        string timeZoneId,
        DateTimeOffset? declaredStartTimeUtc,
        DateTimeOffset? declaredEndTimeUtc,
        JobScheduleOverride? previousOverride,
        JobScheduleOverride? latestOverride,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? authoritativeNextOccurrenceUtc,
        out bool hasSameFieldConflict)
    {
        var expressionChanged = !string.Equals(
            OverrideExpression,
            previousOverride?.CronExpression,
            StringComparison.Ordinal);
        var startChanged = !BoundaryMatchesOverride(StartBoundary, previousOverride?.StartTimeUtc);
        var endChanged = !BoundaryMatchesOverride(EndBoundary, previousOverride?.EndTimeUtc);
        var originalMode = Mode;
        hasSameFieldConflict =
            expressionChanged
            && !string.Equals(
                previousOverride?.CronExpression,
                latestOverride?.CronExpression,
                StringComparison.Ordinal)
            && !string.Equals(OverrideExpression, latestOverride?.CronExpression, StringComparison.Ordinal)
            || startChanged
            && previousOverride?.StartTimeUtc != latestOverride?.StartTimeUtc
            && !BoundaryMatchesOverride(StartBoundary, latestOverride?.StartTimeUtc)
            || endChanged
            && previousOverride?.EndTimeUtc != latestOverride?.EndTimeUtc
            && !BoundaryMatchesOverride(EndBoundary, latestOverride?.EndTimeUtc);

        var rebased = Create(
            declaredExpression,
            latestOverride?.CronExpression,
            timeZoneId,
            declaredStartTimeUtc,
            declaredEndTimeUtc,
            latestOverride?.StartTimeUtc is not null,
            latestOverride?.StartTimeUtc?.Value,
            latestOverride?.EndTimeUtc is not null,
            latestOverride?.EndTimeUtc?.Value,
            observedAtUtc,
            authoritativeNextOccurrenceUtc);
        if (expressionChanged)
        {
            if (IsExpressionInherited)
            {
                rebased.ResetExpression();
            }
            else
            {
                rebased.SetExpression(DraftExpression);
                rebased.Mode = originalMode;
            }
        }

        if (startChanged)
        {
            CopyBoundaryDraft(StartBoundary, rebased.StartBoundary, declaredStartTimeUtc, observedAtUtc);
        }

        if (endChanged)
        {
            CopyBoundaryDraft(EndBoundary, rebased.EndBoundary, declaredEndTimeUtc, observedAtUtc);
        }

        rebased.Rebuild();
        return rebased;
    }

    internal void SetExpression(string? expression)
    {
        IsExpressionInherited = false;
        DraftExpression = expression ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(expression)
            && CronPolicyEditor.SplitFields(expression).Length is 5 or 6)
        {
            Mode = CronPolicyEditor.DetectMode(expression);
        }

        Rebuild();
    }

    internal CronExpressionConversion SetMode(CronExpressionMode mode)
    {
        var conversion = CronPolicyEditor.ConvertMode(EffectiveExpression, mode);
        if (conversion.IsSuccess)
        {
            Mode = mode;
            SetExpression(conversion.Expression);
        }

        return conversion;
    }

    internal void ResetExpression()
    {
        IsExpressionInherited = true;
        DraftExpression = string.Empty;
        Mode = CronPolicyEditor.DetectMode(DeclaredExpression);
        Rebuild();
    }

    internal void SetStartMode(CronBoundaryOverrideMode mode)
    {
        StartBoundary.SetMode(mode, DeclaredStartTimeUtc, PreviewFromUtc);
        Rebuild();
    }

    internal void SetEndMode(CronBoundaryOverrideMode mode)
    {
        EndBoundary.SetMode(mode, DeclaredEndTimeUtc, PreviewFromUtc);
        Rebuild();
    }

    internal void BoundaryChanged() => Rebuild();

    internal void RefreshEvaluation(DateTimeOffset previewFromUtc)
    {
        PreviewFromUtc = previewFromUtc.ToUniversalTime();
        Rebuild();
    }

    internal DateTimeOffset ToScheduleTime(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, _timeZone);

    private void Rebuild()
    {
        Evaluation = CronPolicyEditor.Evaluate(
            EffectiveExpression,
            TimeZoneId,
            EffectiveStartTimeUtc,
            EffectiveEndTimeUtc,
            PreviewFromUtc);
    }

    private static void CopyBoundaryDraft(
        CronBoundaryDraft source,
        CronBoundaryDraft target,
        DateTimeOffset? declaredUtc,
        DateTimeOffset observedAtUtc)
    {
        target.SetMode(source.Mode, declaredUtc, observedAtUtc);
        if (source.Mode != CronBoundaryOverrideMode.Value)
        {
            return;
        }

        target.LocalDate = source.LocalDate;
        target.LocalTime = source.LocalTime;
    }

    private static bool BoundaryMatchesOverride(
        CronBoundaryDraft draft,
        JobScheduleBoundaryOverride? boundaryOverride)
    {
        if (boundaryOverride is null)
        {
            return draft.Mode == CronBoundaryOverrideMode.Inherit;
        }

        if (boundaryOverride.Value is null)
        {
            return draft.Mode == CronBoundaryOverrideMode.None;
        }

        return draft.Mode == CronBoundaryOverrideMode.Value
               && draft.TryGetOverrideUtc(out var value, out _)
               && value == boundaryOverride.Value.Value.ToUniversalTime();
    }
}

/// <summary>
/// Identifies one parsed Cron field without coupling deterministic parsing to localization.
/// </summary>
internal enum CronPolicyFieldKind
{
    Second,
    Minute,
    Hour,
    DayOfMonth,
    Month,
    DayOfWeek
}

/// <summary>
/// Carries the current simple-schedule controls, including raw fields for custom mode.
/// </summary>
internal sealed record CronSimpleScheduleSettings
{
    internal CronSimpleScheduleMode Mode { get; set; } = CronSimpleScheduleMode.EveryMinute;

    internal int Second { get; set; }

    internal int Minute { get; set; }

    internal int Hour { get; set; }

    internal int DayOfMonth { get; set; } = 1;

    internal int DayOfWeek { get; set; } = 1;

    internal string CustomSecond { get; set; } = "0";

    internal string CustomMinute { get; set; } = "*";

    internal string CustomHour { get; set; } = "*";

    internal string CustomDayOfMonth { get; set; } = "*";

    internal string CustomMonth { get; set; } = "*";

    internal string CustomDayOfWeek { get; set; } = "*";
}

/// <summary>
/// Carries one parsed field and its exact source value.
/// </summary>
internal sealed record CronPolicyField(CronPolicyFieldKind Kind, string Value);

/// <summary>
/// Carries one upcoming occurrence in UTC. Consumers convert it with the configured schedule timezone.
/// </summary>
internal sealed record CronPolicyOccurrence(DateTimeOffset Utc);

/// <summary>
/// Carries the result of exact runtime validation and a boundary-aware prospective preview.
/// </summary>
internal sealed record CronPolicyEvaluation
{
    internal bool IsValid { get; init; }

    internal string? Error { get; init; }

    internal CronExpressionMode? Mode { get; init; }

    internal IReadOnlyList<CronPolicyField> Fields { get; init; } = [];

    internal IReadOnlyList<CronPolicyOccurrence> Occurrences { get; init; } = [];
}

/// <summary>
/// Explains why a requested expression-shape conversion could not be performed safely.
/// </summary>
internal enum CronExpressionConversionFailure
{
    None,
    InvalidSource,
    NonZeroSeconds
}

/// <summary>
/// Carries a safe Cron shape conversion without silently changing cadence.
/// </summary>
internal sealed record CronExpressionConversion(
    bool IsSuccess,
    string Expression,
    CronExpressionConversionFailure Failure,
    string? Error = null);

/// <summary>
/// Provides deterministic Cron editing behavior aligned with the scheduler's <see cref="CronHelper.Parse"/> boundary.
/// </summary>
internal static class CronPolicyEditor
{
    private const int PREVIEW_COUNT = 5;

    internal static CronExpressionMode DetectMode(string expression) =>
        SplitFields(expression).Length == 6
            ? CronExpressionMode.SixFieldsWithSeconds
            : CronExpressionMode.FiveFields;

    internal static CronExpressionConversion ConvertMode(
        string expression,
        CronExpressionMode targetMode)
    {
        var source = Evaluate(expression, "UTC", null, null, DateTimeOffset.UnixEpoch);
        if (!source.IsValid || source.Mode is null)
        {
            return new(false, expression, CronExpressionConversionFailure.InvalidSource, source.Error);
        }

        if (source.Mode == targetMode)
        {
            return new(true, NormalizeExpression(expression), CronExpressionConversionFailure.None);
        }

        var fields = SplitFields(expression);
        if (targetMode == CronExpressionMode.SixFieldsWithSeconds)
        {
            return new(true, $"0 {string.Join(' ', fields)}", CronExpressionConversionFailure.None);
        }

        if (!string.Equals(fields[0], "0", StringComparison.Ordinal))
        {
            return new(false, expression, CronExpressionConversionFailure.NonZeroSeconds);
        }

        return new(true, string.Join(' ', fields.Skip(1)), CronExpressionConversionFailure.None);
    }

    internal static string BuildSimpleExpression(
        CronSimpleScheduleSettings settings,
        CronExpressionMode mode)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var body = settings.Mode switch
        {
            CronSimpleScheduleMode.EveryMinute => "* * * * *",
            CronSimpleScheduleMode.EveryHour => "0 * * * *",
            CronSimpleScheduleMode.EveryDay => $"{settings.Minute} {settings.Hour} * * *",
            CronSimpleScheduleMode.EveryWeek => $"{settings.Minute} {settings.Hour} * * {settings.DayOfWeek}",
            CronSimpleScheduleMode.EveryMonth => $"{settings.Minute} {settings.Hour} {settings.DayOfMonth} * *",
            CronSimpleScheduleMode.Custom => string.Join(
                ' ',
                settings.CustomMinute,
                settings.CustomHour,
                settings.CustomDayOfMonth,
                settings.CustomMonth,
                settings.CustomDayOfWeek),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.Mode, null)
        };

        if (mode == CronExpressionMode.FiveFields)
        {
            return body;
        }

        var second = settings.Mode == CronSimpleScheduleMode.Custom
            ? settings.CustomSecond
            : settings.Second.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{second} {body}";
    }

    internal static CronPolicyEvaluation Evaluate(
        string? expression,
        string? timeZoneId,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Invalid();
        }

        var fields = SplitFields(expression);
        if (fields.Length is not (5 or 6))
        {
            return Invalid();
        }

        var mode = fields.Length == 6
            ? CronExpressionMode.SixFieldsWithSeconds
            : CronExpressionMode.FiveFields;
        var parsedFields = ParseFields(fields, mode);

        try
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                return Invalid(mode, parsedFields);
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return EvaluateParsed(expression, timeZone, startTimeUtc, endTimeUtc, observedAtUtc, mode, parsedFields);
        }
        catch (Exception exception) when (exception is CronFormatException
                                          or ArgumentException
                                          or TimeZoneNotFoundException
                                          or InvalidTimeZoneException)
        {
            return Invalid(mode, parsedFields, exception.Message);
        }
    }

    internal static CronPolicyEvaluation Evaluate(
        string expression,
        TimeZoneInfo timeZone,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var fields = SplitFields(expression);
        if (fields.Length is not (5 or 6))
        {
            return Invalid();
        }

        var mode = fields.Length == 6
            ? CronExpressionMode.SixFieldsWithSeconds
            : CronExpressionMode.FiveFields;
        var parsedFields = ParseFields(fields, mode);
        try
        {
            return EvaluateParsed(expression, timeZone, startTimeUtc, endTimeUtc, observedAtUtc, mode, parsedFields);
        }
        catch (Exception exception) when (exception is CronFormatException or ArgumentException)
        {
            return Invalid(mode, parsedFields, exception.Message);
        }
    }

    private static CronPolicyEvaluation EvaluateParsed(
        string expression,
        TimeZoneInfo timeZone,
        DateTimeOffset? startTimeUtc,
        DateTimeOffset? endTimeUtc,
        DateTimeOffset observedAtUtc,
        CronExpressionMode mode,
        IReadOnlyList<CronPolicyField> parsedFields)
    {
        if (startTimeUtc is { } start && endTimeUtc is { } end && start > end)
        {
            return Invalid(mode, parsedFields);
        }

        var cron = CronHelper.Parse(NormalizeExpression(expression));
        var searchFrom = observedAtUtc.ToUniversalTime();
        if (startTimeUtc is { } startBoundary && searchFrom < startBoundary)
        {
            searchFrom = startBoundary.AddTicks(-1);
        }

        var occurrences = new List<CronPolicyOccurrence>(PREVIEW_COUNT);
        while (occurrences.Count < PREVIEW_COUNT)
        {
            var next = cron.GetNextOccurrence(searchFrom, timeZone);
            if (next is null)
            {
                break;
            }

            var nextUtc = next.Value.ToUniversalTime();
            if (endTimeUtc is { } endBoundary && nextUtc > endBoundary)
            {
                break;
            }

            occurrences.Add(new(nextUtc));
            searchFrom = nextUtc;
        }

        return new()
        {
            IsValid = true,
            Mode = mode,
            Fields = parsedFields,
            Occurrences = occurrences
        };
    }

    private static CronPolicyEvaluation Invalid(
        CronExpressionMode? mode = null,
        IReadOnlyList<CronPolicyField>? fields = null,
        string? error = null) => new()
        {
            IsValid = false,
            Error = error,
            Mode = mode,
            Fields = fields ?? []
        };

    private static IReadOnlyList<CronPolicyField> ParseFields(
        IReadOnlyList<string> fields,
        CronExpressionMode mode)
    {
        var kinds = mode == CronExpressionMode.SixFieldsWithSeconds
            ? new[]
            {
                CronPolicyFieldKind.Second,
                CronPolicyFieldKind.Minute,
                CronPolicyFieldKind.Hour,
                CronPolicyFieldKind.DayOfMonth,
                CronPolicyFieldKind.Month,
                CronPolicyFieldKind.DayOfWeek
            }
            : new[]
            {
                CronPolicyFieldKind.Minute,
                CronPolicyFieldKind.Hour,
                CronPolicyFieldKind.DayOfMonth,
                CronPolicyFieldKind.Month,
                CronPolicyFieldKind.DayOfWeek
            };

        return fields.Select((value, index) => new CronPolicyField(kinds[index], value)).ToArray();
    }

    internal static string NormalizeExpression(string expression) => string.Join(' ', SplitFields(expression));

    internal static string[] SplitFields(string expression) =>
        expression.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
