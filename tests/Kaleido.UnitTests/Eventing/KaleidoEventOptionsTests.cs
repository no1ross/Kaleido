using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventOptionsTests
    : SutFixture<KaleidoEventOptions>
{
    protected override KaleidoEventOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreValid()
    {
        var sut = Sut;

        KaleidoEventOptions.Validate(sut);

        Assert.Equal(20, sut.BatchSize);
        Assert.Equal(1, sut.MaxConcurrentPublishes);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.Resilience.AttemptTimeout);
        Assert.Equal(9, sut.Resilience.MaxRetryAttempts);
        Assert.Equal("Events", KaleidoEventOptions.SectionName);
    }

    [Fact]
    public void Validate_WhenOptionsIsNull_ThrowsInvalidEventOptions()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(null));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
        Assert.Equal("Kaleido:Events must not be null.", exception.Message);
    }

    [Fact]
    public void Validate_WhenResilienceIsNull_ThrowsInvalidEventOptions()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(Sut with { Resilience = null! }));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
        Assert.Equal("Kaleido:Events:Resilience must not be null.", exception.Message);
    }

    // The resilience rules themselves are KaleidoResilienceSettings'; here they report the events path.
    [Theory]
    [InlineData("BatchSize", "Kaleido:Events:BatchSize must be positive.")]
    [InlineData("MaxConcurrentPublishes", "Kaleido:Events:MaxConcurrentPublishes must be positive.")]
    [InlineData("AttemptTimeout", "Kaleido:Events:Resilience:AttemptTimeout must be positive.")]
    public void Validate_InvalidValue_ThrowsInvalidEventOptions(string setting, string message)
    {
        var sut = setting switch
        {
            "BatchSize" => Sut with { BatchSize = 0 },
            "MaxConcurrentPublishes" => Sut with { MaxConcurrentPublishes = 0 },
            _ => Sut with { Resilience = Sut.Resilience with { AttemptTimeout = TimeSpan.Zero } },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(sut));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
        Assert.Equal(message, exception.Message);
    }
}
