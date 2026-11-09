using Kaleido.Exceptions;

namespace Kaleido.Http.Client.Configuration.UnitTests;

public sealed class KaleidoClientOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoClientOptions>
{
    protected override KaleidoClientOptions CreateSut() => new();

    [Fact]
    public void Validate_TheDefaults_Pass()
    {
        KaleidoClientOptions.Validate(Sut);

        Assert.Null(Sut.BaseUrl);
    }

    // Nothing is adjusted: an out-of-range shared setting stops startup, naming the setting.
    [Fact]
    public void Validate_AnInvalidResilienceValue_FailsWithItsCode()
    {
        var options = Sut with { Resilience = Sut.Resilience with { RetryDelay = TimeSpan.Zero } };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoClientOptions.Validate(options));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.StartsWith("Kaleido:Resilience:RetryDelay", exception.Message, StringComparison.Ordinal);
    }
}
