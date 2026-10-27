using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoHttpOptionsTests
    : SutFixture<KaleidoHttpOptions>
{
    protected override KaleidoHttpOptions CreateSut() => new();

    [Fact]
    public void Validate_TheDefaults_Pass()
    {
        KaleidoHttpOptions.Validate(Sut);

        Assert.Equal(TimeSpan.FromHours(24), Sut.IdempotencyRetention);
        Assert.Equal(TimeSpan.FromMinutes(5), Sut.IdempotencyLease);
        Assert.Equal(TimeSpan.FromDays(7), Sut.IdempotencyUnknownOutcomeRetention);
        Assert.Equal("Kaleido:Http", KaleidoHttpOptions.SectionName);
    }

    // Nothing is adjusted: a non-positive duration stops startup, naming the setting.
    [Theory]
    [InlineData("IdempotencyRetention")]
    [InlineData("IdempotencyLease")]
    public void Validate_ANonPositiveDuration_FailsWithItsCode(string setting)
    {
        var options = setting == "IdempotencyLease"
            ? Sut with { IdempotencyLease = TimeSpan.Zero }
            : Sut with { IdempotencyRetention = TimeSpan.FromSeconds(-1) };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoHttpOptions.Validate(options));

        Assert.Equal(ConfigurationErrorCodes.InvalidHttpOptions, exception.Code);
        Assert.StartsWith($"Kaleido:Http:{setting} ", exception.Message, StringComparison.Ordinal);
    }

    // #441: an attempt whose outcome is unknown must outlive its lease; the replay retention is independent.
    [Fact]
    public void Validate_AnUnknownOutcomeRetentionShorterThanTheLease_FailsWithItsCode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoHttpOptions.Validate(
            Sut with { IdempotencyLease = TimeSpan.FromMinutes(10), IdempotencyUnknownOutcomeRetention = TimeSpan.FromMinutes(9) }));

        Assert.Equal(ConfigurationErrorCodes.InvalidHttpOptions, exception.Code);
        Assert.StartsWith("Kaleido:Http:IdempotencyUnknownOutcomeRetention ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AnUnknownOutcomeRetentionEqualToTheLease_AndShorterThanTheReplayRetention_Passes()
    {
        KaleidoHttpOptions.Validate(Sut with
        {
            IdempotencyLease = TimeSpan.FromMinutes(10),
            IdempotencyUnknownOutcomeRetention = TimeSpan.FromMinutes(10),
            IdempotencyRetention = TimeSpan.FromHours(1),
        });
    }
}
