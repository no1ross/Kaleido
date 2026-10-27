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
}
