using Kaleido.Exceptions;
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
        var shared = configuration.GetSection("Kaleido").BindOver(new KaleidoClientOptions(), HttpClientErrorCodes.InvalidClientOptions);

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

    [Fact]
    public void ForClient_AnInvalidClientSetting_IsAConfigurationError()
    {
        var configuration = Configuration(new() { ["Kaleido:Clients:legacy:Resilience:TotalTimeout"] = "00:00:00" });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => Sut.ForClient(configuration, "legacy"));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.StartsWith("Kaleido:Clients:legacy:Resilience:TotalTimeout", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TotalTimeout")]
    [InlineData("HealthCheckTimeout")]
    public void Validate_ANonPositiveClientTimeout_IsAConfigurationError(string setting)
    {
        var options = setting == "TotalTimeout" ? Sut with { TotalTimeout = TimeSpan.Zero } : Sut with { HealthCheckTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoResilienceOptions.Validate(options, "Kaleido:Resilience"));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
    }

    [Fact]
    public void Validate_TheSharedRules_ApplyWithTheClientsCode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoResilienceOptions.Validate(Sut with { MaxRetryAttempts = -1 }, "Kaleido:Resilience"));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Equal("Kaleido:Resilience:MaxRetryAttempts must not be negative.", exception.Message);
    }
}
