using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Monica.Core.Modularity.Abstractions;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Services;
using Monica.Modules;
using Monica.Testing.Hosting;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class ModuleSeederTests
{
    [Fact]
    public async Task AddSeeder_WhenHostStarts_ShouldRunAfterApplicationStartedAndRegisterReadinessCheck()
    {
        var factory = new SeederTestApplicationFactory();

        await using var application = await factory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var state = application.Services.GetRequiredService<ISeederState>();
        await WaitFor.UntilAsync(
            _ => Task.FromResult(state.GetSnapshot().IsCompleted),
            cancellationToken: TestContext.Current.CancellationToken);
        var report = await application.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        application.ModuleSnapshots.Should().Contain(static module => module.ModuleType == typeof(ModuleSeeder));
        state.GetSnapshot().StartedAtUtc.Should().NotBeNull();
        report.Entries.Should().ContainKey("monica.seeder");
        report.Entries["monica.seeder"].Status.Should().Be(HealthStatus.Healthy);
    }

    private sealed class SeederTestApplicationFactory : MonicaTestApplicationFactory<SeederBase>
    {
        protected override void ConfigureMonica(IMonicaBuilder builder)
        {
            builder.AddSeeder();
        }
    }
}
