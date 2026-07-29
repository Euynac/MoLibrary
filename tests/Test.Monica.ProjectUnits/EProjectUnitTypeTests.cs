using AwesomeAssertions;
using Monica.ProjectUnits.Models;
using Xunit;

namespace Test.Monica.ProjectUnits;

public sealed class EProjectUnitTypeTests
{
    [Fact]
    public void Values_ShouldPreservePublishedNumericContract()
    {
        var expected = new Dictionary<EProjectUnitType, int>
        {
            [EProjectUnitType.None] = 0,
            [EProjectUnitType.ApplicationService] = 1,
            [EProjectUnitType.CrudApplicationService] = 2,
            [EProjectUnitType.DomainService] = 3,
            [EProjectUnitType.Repository] = 4,
            [EProjectUnitType.DomainEvent] = 5,
            [EProjectUnitType.DomainEventHandler] = 6,
            [EProjectUnitType.LocalEventHandler] = 7,
            [EProjectUnitType.Seeder] = 8,
            [EProjectUnitType.RecurringJob] = 9,
            [EProjectUnitType.TriggeredJob] = 10,
            [EProjectUnitType.HttpApi] = 11,
            [EProjectUnitType.GrpcApi] = 12,
            [EProjectUnitType.StateStore] = 13,
            [EProjectUnitType.EventBus] = 14,
            [EProjectUnitType.Actor] = 15,
            [EProjectUnitType.Entity] = 16,
            [EProjectUnitType.RequestDto] = 17,
            [EProjectUnitType.Configuration] = 18,
            [EProjectUnitType.HostedService] = 19
        };

        Enum.GetValues<EProjectUnitType>().Should().HaveCount(expected.Count);
        foreach (var (unitType, value) in expected)
        {
            ((int)unitType).Should().Be(value);
        }
    }
}
