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

        Assert.Equal("registry", sut.ServiceName);
    }

    public static TheoryData<string> InvalidSettings() => ["no url", "relative url", "ftp url", "bad service name"];

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
            _ => sut with { ServiceName = "Registry!" },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoRegistryClientOptions.Validate(options));

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.StartsWith("Kaleido:Registry:", exception.Message, StringComparison.Ordinal);
    }

}
