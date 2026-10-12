using Microsoft.Extensions.Configuration;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoResilienceOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoResilienceOptions>
{
    protected override KaleidoResilienceOptions CreateSut() => new();

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Defaults_AreOn_WithConservativeSettings()
    {
        var options = Sut;

        Assert.True(options.Enabled);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(10), options.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TotalTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.HealthCheckTimeout);
    }

    // Three layers: framework defaults, the shared section, the client's own section (only what it names).
    [Fact]
    public void ForClient_OverridesOnlyTheSettingsTheClientNames()
    {
        var configuration = Configuration(new()
        {
            ["Kaleido:Resilience:MaxRetryAttempts"] = "2",
            ["Kaleido:Resilience:AttemptTimeout"] = "00:00:05",
            ["Kaleido:Clients:radiology:Resilience:AttemptTimeout"] = "00:00:20",
        });
        var shared = new KaleidoClientOptions();
        configuration.GetSection("Kaleido").Bind(shared);

        var radiology = shared.Resilience.ForClient(configuration, "radiology");
        var member = shared.Resilience.ForClient(configuration, "member");

        Assert.Equal(2, radiology.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(20), radiology.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), radiology.TotalTimeout);
        Assert.Equal(2, member.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(5), member.AttemptTimeout);
    }

    [Fact]
    public void ForClient_CanTurnResilienceOffForOneClient_WithoutChangingTheShared()
    {
        var configuration = Configuration(new() { ["Kaleido:Clients:legacy:Resilience:Enabled"] = "false" });
        var shared = Sut;

        Assert.False(shared.ForClient(configuration, "legacy").Enabled);
        Assert.True(shared.ForClient(configuration, "member").Enabled);
        Assert.True(shared.Enabled);
    }
}