using Kaleido.Exceptions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoRegistryClientOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoRegistryClientOptions>
{
    protected override KaleidoRegistryClientOptions CreateSut() => new() { Url = "https://registry.internal" };

    [Fact]
    public void Defaults_AreValid()
    {
        var sut = Sut;

        KaleidoRegistryClientOptions.Validate(sut);

        Assert.Equal(("registry", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10)), (sut.ServiceName, sut.CapabilitiesInterval, sut.AvailabilityInterval));
        Assert.Null(sut.Address);
    }

    public static TheoryData<string> InvalidSettings() => ["no url", "relative url", "ftp url", "relative address", "bad service name", "capabilities zero", "availability zero"];

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Validate_AnInvalidSetting_IsAConfigurationError(string setting)
    {
        var sut = Sut;
        var options = setting switch
        {
            "no url" => sut with { Url = null },
            "relative url" => sut with { Url = "/registry" },
            "ftp url" => sut with { Url = "ftp://registry" },
            "relative address" => sut with { Address = "code-sets" },
            "bad service name" => sut with { ServiceName = "Registry!" },
            "capabilities zero" => sut with { CapabilitiesInterval = TimeSpan.Zero },
            _ => sut with { AvailabilityInterval = TimeSpan.Zero },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoRegistryClientOptions.Validate(options));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.StartsWith("Kaleido:Registry:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AnAddress_IsAccepted()
    {
        KaleidoRegistryClientOptions.Validate(Sut with { Address = "https://code-sets.internal" });
    }
}
