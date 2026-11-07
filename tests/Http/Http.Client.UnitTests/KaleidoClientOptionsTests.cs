using Kaleido.Exceptions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoClientOptions>
{
    protected override KaleidoClientOptions CreateSut() => new();

    [Fact]
    public void Validate_TheDefaults_Pass()
    {
        KaleidoClientOptions.Validate(Sut);

        Assert.Equal(TimeSpan.FromSeconds(30), Sut.RegistryTtl);
        Assert.Empty(Sut.Clients);
    }

    [Fact]
    public void Validate_WithoutOrWithAZeroRegistryTtl_Passes()
    {
        KaleidoClientOptions.Validate(Sut with { RegistryTtl = null });
        KaleidoClientOptions.Validate(Sut with { RegistryTtl = TimeSpan.Zero });
    }

    // Nothing is adjusted: an out-of-range shared or per-client setting stops startup, naming the setting.
    [Theory]
    [InlineData("SharedTtl", "Kaleido:RegistryTtl")]
    [InlineData("ClientTtl", "Kaleido:Clients:member:RegistryTtl")]
    [InlineData("Resilience", "Kaleido:Resilience:RetryDelay")]
    public void Validate_AnInvalidValue_FailsWithItsCode(string setting, string path)
    {
        var options = setting switch
        {
            "SharedTtl" => Sut with { RegistryTtl = TimeSpan.FromSeconds(-1) },
            "ClientTtl" => Sut with { Clients = new Dictionary<string, KaleidoClientEntry> { ["member"] = new() { RegistryTtl = TimeSpan.FromSeconds(-1) } } },
            _ => Sut with { Resilience = Sut.Resilience with { RetryDelay = TimeSpan.Zero } },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoClientOptions.Validate(options));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.StartsWith(path, exception.Message, StringComparison.Ordinal);
    }
}
