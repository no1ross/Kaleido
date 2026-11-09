using Kaleido.Exceptions;

namespace Kaleido.Http.Client.Resilience.UnitTests;

public sealed class KaleidoResilienceOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoResilienceOptions>
{
    protected override KaleidoResilienceOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreOn_WithConservativeSettings()
    {
        var options = Sut;

        Assert.True(options.Enabled);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromSeconds(10), options.AttemptTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TotalTimeout);
    }

    [Fact]
    public void Validate_ANonPositiveClientTimeout_IsAConfigurationError()
    {
        var options = Sut with { TotalTimeout = TimeSpan.Zero };

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
