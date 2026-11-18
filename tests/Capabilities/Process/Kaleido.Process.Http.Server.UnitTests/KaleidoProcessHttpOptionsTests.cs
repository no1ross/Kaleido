namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class KaleidoProcessHttpOptionsTests : Kaleido.UnitTests.SutFixture<KaleidoProcessHttpOptions>
{
    protected override KaleidoProcessHttpOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreValidAtTheProcessHttpConfigurationPath()
    {
        var options = Sut;

        ((IKaleidoOptions)options).Validate();

        Assert.Equal("Kaleido:Process:Http", ((IKaleidoOptions)options).ConfigurationPath);
        Assert.False(options.IncludeFrameworkMessages);
        Assert.Equal(TimeSpan.FromHours(24), options.IdempotencyRetention);
        Assert.Equal(TimeSpan.FromMinutes(5), options.IdempotencyLease);
        Assert.Equal(TimeSpan.FromDays(7), options.IdempotencyUnknownOutcomeRetention);
    }

    [Theory]
    [InlineData("retention")]
    [InlineData("lease")]
    [InlineData("unknown")]
    [InlineData("short unknown")]
    public void InvalidLifetime_IsRefusedAtRegistration(string setting)
    {
        var options = setting switch
        {
            "retention" => Sut with { IdempotencyRetention = TimeSpan.Zero },
            "lease" => Sut with { IdempotencyLease = TimeSpan.Zero },
            "unknown" => Sut with { IdempotencyUnknownOutcomeRetention = TimeSpan.Zero },
            _ => Sut with { IdempotencyUnknownOutcomeRetention = TimeSpan.FromMinutes(4) },
        };

        var failure = Assert.Throws<KaleidoConfigurationException>(() => ((IKaleidoOptions)options).Validate());

        Assert.Equal(ConfigurationErrorCodes.InvalidHttpOptions, failure.Code);
        Assert.Contains("Kaleido:Process:Http:", failure.Message, StringComparison.Ordinal);
    }
}
