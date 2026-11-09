namespace Kaleido.Registry.UnitTests;

public sealed class KaleidoRegistryCacheOptionsTests : SutFixture<KaleidoRegistryCacheOptions>
{
    protected override KaleidoRegistryCacheOptions CreateSut() => new();

    [Fact]
    public void Defaults_ProvideDistinctViewAndRevisionLifetimes()
    {
        var sut = Sut;

        KaleidoRegistryCacheOptions.Validate(sut);

        Assert.Equal("kaleido:registry", sut.KeyPrefix);
        Assert.Equal(TimeSpan.FromSeconds(1), sut.ViewTtl);
        Assert.Equal(TimeSpan.FromDays(365), sut.CapabilitiesTtl);
        Assert.Null(sut.ViewLocalTtl);
        Assert.Null(sut.CapabilitiesLocalTtl);
    }

    [Theory]
    [InlineData("key prefix empty", "KeyPrefix")]
    [InlineData("key prefix too long", "KeyPrefix")]
    [InlineData("view zero", "ViewTtl")]
    [InlineData("capabilities zero", "CapabilitiesTtl")]
    [InlineData("view local zero", "ViewLocalTtl")]
    [InlineData("view local too long", "ViewLocalTtl")]
    [InlineData("capabilities local zero", "CapabilitiesLocalTtl")]
    [InlineData("capabilities local too long", "CapabilitiesLocalTtl")]
    public void Validate_RejectsInvalidCacheSettings(string setting, string property)
    {
        var sut = Sut;
        var options = setting switch
        {
            "key prefix empty" => sut with { KeyPrefix = " " },
            "key prefix too long" => sut with { KeyPrefix = new string('a', 901) },
            "view zero" => sut with { ViewTtl = TimeSpan.Zero },
            "capabilities zero" => sut with { CapabilitiesTtl = TimeSpan.Zero },
            "view local zero" => sut with { ViewLocalTtl = TimeSpan.Zero },
            "view local too long" => sut with { ViewLocalTtl = sut.ViewTtl + TimeSpan.FromTicks(1) },
            "capabilities local zero" => sut with { CapabilitiesLocalTtl = TimeSpan.Zero },
            _ => sut with { CapabilitiesLocalTtl = sut.CapabilitiesTtl + TimeSpan.FromTicks(1) },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoRegistryCacheOptions.Validate(options));

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains($"Kaleido:Registry:Cache:{property}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsShorterLocalLifetimes()
    {
        KaleidoRegistryCacheOptions.Validate(Sut with
        {
            ViewLocalTtl = TimeSpan.FromMilliseconds(500),
            CapabilitiesLocalTtl = TimeSpan.FromDays(30),
        });
    }
}
