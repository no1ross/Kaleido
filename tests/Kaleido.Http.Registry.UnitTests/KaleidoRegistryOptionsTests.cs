namespace Kaleido.Http.Registry.UnitTests;

public sealed class KaleidoRegistryOptionsTests
    : SutFixture<KaleidoRegistryOptions>
{
    protected override KaleidoRegistryOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreValid()
    {
        var sut = Sut;

        KaleidoRegistryOptions.Validate(sut);

        Assert.Equal(TimeSpan.FromSeconds(10), sut.HeartbeatInterval);
        Assert.Equal(3, sut.MissedHeartbeats);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.Lease);
        Assert.Equal(TimeSpan.FromHours(24), sut.Retention);
        Assert.Null(sut.ReaderPolicy);
        Assert.Empty(sut.Registrants);
        Assert.Empty(sut.Expected);
    }

    public static TheoryData<string> InvalidSettings() =>
        ["heartbeat zero", "missed zero", "retention within the lease", "registrant without clients", "registrant blank client", "registrant bad name", "expected without versions", "expected bad version"];

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Validate_AnInvalidSetting_IsAConfigurationError(string setting)
    {
        var sut = Sut;
        var options = setting switch
        {
            "heartbeat zero" => sut with { HeartbeatInterval = TimeSpan.Zero },
            "missed zero" => sut with { MissedHeartbeats = 0 },
            "retention within the lease" => sut with { Retention = sut.Lease },
            "registrant without clients" => sut with { Registrants = new Dictionary<string, IReadOnlyList<string>> { ["radiology"] = [] } },
            "registrant blank client" => sut with { Registrants = new Dictionary<string, IReadOnlyList<string>> { ["radiology"] = [" "] } },
            "registrant bad name" => sut with { Registrants = new Dictionary<string, IReadOnlyList<string>> { ["Radiology!"] = ["client"] } },
            "expected without versions" => sut with { Expected = [new KaleidoRegistryExpectedService { ServiceName = "radiology" }] },
            _ => sut with { Expected = [new KaleidoRegistryExpectedService { ServiceName = "radiology", Versions = ["-1"] }] },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoRegistryOptions.Validate(options));

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.StartsWith("Kaleido:Registry:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidRegistrantsAndExpectedServices_Pass()
    {
        KaleidoRegistryOptions.Validate(Sut with
        {
            Registrants = new Dictionary<string, IReadOnlyList<string>> { ["radiology"] = ["client"] },
            Expected = [new KaleidoRegistryExpectedService { ServiceName = "radiology", Versions = ["2026-11"] }],
        });
    }
}
