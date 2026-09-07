using AwesomeAssertions;
using Monica.Repository.Persistence.Models;
using Xunit;

namespace Test.Monica.Repository.Persistence.Models;

public sealed class RepositoryActivitySummaryTests
{
    [Fact]
    public void Create_WhenDatabaseNameProvided_ShouldScopeStateCountsToThatDatabase()
    {
        var rows = new List<RepositoryActivityRow>
        {
            Row("app", "active"),
            Row("app", "idle"),
            Row("app", "idle"),
            Row("other", "active"),
            Row("other", "idle")
        };

        var summary = RepositoryActivitySummary.Create(rows, "app");

        summary.TotalSessions.Should().Be(5);
        summary.DatabaseSessions.Should().Be(3);
        summary.ActiveSessions.Should().Be(1);
        summary.IdleSessions.Should().Be(2);
        summary.IdleInTransactionSessions.Should().Be(0);
    }

    [Fact]
    public void Create_WhenDatabaseNameDiffersInCase_ShouldStillScopeRows()
    {
        var rows = new List<RepositoryActivityRow> { Row("App", "active"), Row("APP", "idle") };

        var summary = RepositoryActivitySummary.Create(rows, "app");

        summary.DatabaseSessions.Should().Be(2);
        summary.ActiveSessions.Should().Be(1);
        summary.IdleSessions.Should().Be(1);
    }

    [Fact]
    public void Create_WhenDatabaseNameMissing_ShouldScopeAllRows()
    {
        var rows = new List<RepositoryActivityRow>
        {
            Row("app", "active"),
            Row("other", "idle")
        };

        var summary = RepositoryActivitySummary.Create(rows, null);

        summary.TotalSessions.Should().Be(2);
        summary.DatabaseSessions.Should().Be(2);
        summary.ActiveSessions.Should().Be(1);
        summary.IdleSessions.Should().Be(1);
    }

    [Fact]
    public void Create_WhenTransactionIdleStatesReported_ShouldLumpAbortedIntoIdleInTransaction()
    {
        var rows = new List<RepositoryActivityRow>
        {
            Row("app", "idle in transaction"),
            Row("app", "idle in transaction (aborted)"),
            Row("app", "idle")
        };

        var summary = RepositoryActivitySummary.Create(rows, "app");

        summary.IdleInTransactionSessions.Should().Be(2);
        summary.IdleSessions.Should().Be(1);
    }

    [Fact]
    public void Create_WhenStatesPaddedOrUnknown_ShouldNormalizeAndIgnoreUnknownStates()
    {
        var rows = new List<RepositoryActivityRow>
        {
            Row("app", " active "),
            Row("app", "disabled"),
            Row("app", null)
        };

        var summary = RepositoryActivitySummary.Create(rows, "app");

        summary.DatabaseSessions.Should().Be(3);
        summary.ActiveSessions.Should().Be(1);
        summary.IdleSessions.Should().Be(0);
        summary.IdleInTransactionSessions.Should().Be(0);
    }

    [Fact]
    public void Create_WhenRowsWaiting_ShouldCountWaitingSessions()
    {
        var rows = new List<RepositoryActivityRow>
        {
            Row("app", "active", waiting: true),
            Row("app", "idle", waiting: true),
            Row("app", "idle", waiting: false),
            Row("other", "active", waiting: true)
        };

        var summary = RepositoryActivitySummary.Create(rows, "app");

        summary.WaitingSessions.Should().Be(2);
    }

    private static RepositoryActivityRow Row(string database, string? state, bool? waiting = null)
    {
        return new RepositoryActivityRow
        {
            DatabaseName = database,
            State = state,
            Waiting = waiting
        };
    }
}
