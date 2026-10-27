using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoHttpMapOptionsTests
    : SutFixture<KaleidoHttpMapOptions>
{
    protected override KaleidoHttpMapOptions CreateSut() => new();

    [Fact]
    public void Validate_TheDefaults_Pass()
    {
        KaleidoHttpMapOptions.Validate(Sut);

        Assert.False(Sut.AggregateRegistry);
        Assert.Equal("Kaleido:Http:Map", KaleidoHttpMapOptions.SectionName);
    }

    [Fact]
    public void Validate_UnsetOptionalSettingsAndZeroDurations_Pass()
    {
        KaleidoHttpMapOptions.Validate(Sut with { RegistryCacheTtl = null, RegistryDownstreamMaxParallelism = null, RegistryRefreshCooldown = TimeSpan.Zero });
        KaleidoHttpMapOptions.Validate(Sut with { RegistryCacheTtl = TimeSpan.Zero });
    }

    // Nothing is adjusted: an out-of-range setting stops startup, naming the setting.
    [Theory]
    [InlineData("RegistryCacheTtl")]
    [InlineData("RegistryRefreshCooldown")]
    [InlineData("RegistryDownstreamMaxParallelism")]
    public void Validate_AnOutOfRangeSetting_FailsWithItsCode(string setting)
    {
        var options = setting switch
        {
            "RegistryCacheTtl" => Sut with { RegistryCacheTtl = TimeSpan.FromSeconds(-1) },
            "RegistryRefreshCooldown" => Sut with { RegistryRefreshCooldown = TimeSpan.FromSeconds(-1) },
            _ => Sut with { RegistryDownstreamMaxParallelism = 0 },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoHttpMapOptions.Validate(options));

        Assert.Equal(ConfigurationErrorCodes.InvalidHttpOptions, exception.Code);
        Assert.StartsWith($"Kaleido:Http:Map:{setting} ", exception.Message, StringComparison.Ordinal);
    }
}
