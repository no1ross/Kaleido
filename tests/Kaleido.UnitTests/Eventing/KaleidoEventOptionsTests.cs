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
    public void Copy_CopiesEveryValueIntoNewInstances()
    {
        var sut = Sut;
        sut.BatchSize = 5;
        sut.MaxConcurrentPublishes = 3;
        sut.Resilience.CircuitBreakerFailureRatio = 0.25;
        sut.Resilience.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);

        var copy = sut.Copy();
        sut.Resilience.MaxRetryAttempts = 1;

        Assert.NotSame(sut, copy);
        Assert.NotSame(sut.Resilience, copy.Resilience);
        Assert.Equal(5, copy.BatchSize);
        Assert.Equal(3, copy.MaxConcurrentPublishes);
        Assert.Equal(9, copy.Resilience.MaxRetryAttempts);
        Assert.Equal(0.25, copy.Resilience.CircuitBreakerFailureRatio);
        Assert.Equal(TimeSpan.FromSeconds(10), copy.Resilience.CircuitBreakerSamplingDuration);
        Assert.Equal(sut.Resilience.CircuitBreakerBreakDuration, copy.Resilience.CircuitBreakerBreakDuration);
        Assert.Equal(sut.Resilience.CircuitBreakerMinimumThroughput, copy.Resilience.CircuitBreakerMinimumThroughput);
        Assert.Equal(sut.Resilience.RetryDelay, copy.Resilience.RetryDelay);
        Assert.Equal(sut.Resilience.AttemptTimeout, copy.Resilience.AttemptTimeout);
    }

    [Fact]
    public void Validate_WhenOptionsIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => KaleidoEventOptions.Validate(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenResilienceIsNull_ThrowsInvalidEventOptions()
    {
        var sut = Sut;
        sut.Resilience = null!;

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(sut));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
        Assert.Equal("Kaleido:Events:Resilience must not be null.", exception.Message);
    }

    [Theory]
    [InlineData("BatchSize", "Kaleido:Events:BatchSize must be positive.")]
    [InlineData("MaxConcurrentPublishes", "Kaleido:Events:MaxConcurrentPublishes must be positive.")]
    [InlineData("AttemptTimeout", "Kaleido:Events:Resilience:AttemptTimeout must be positive.")]
    [InlineData("MaxRetryAttempts", "Kaleido:Events:Resilience:MaxRetryAttempts must not be negative.")]
    [InlineData("RetryDelay", "Kaleido:Events:Resilience:RetryDelay must be positive.")]
    [InlineData("FailureRatioZero", "Kaleido:Events:Resilience:CircuitBreakerFailureRatio must be greater than 0 and at most 1.")]
    [InlineData("FailureRatioAboveOne", "Kaleido:Events:Resilience:CircuitBreakerFailureRatio must be greater than 0 and at most 1.")]
    [InlineData("MinimumThroughput", "Kaleido:Events:Resilience:CircuitBreakerMinimumThroughput must be at least 2.")]
    [InlineData("SamplingDuration", "Kaleido:Events:Resilience:CircuitBreakerSamplingDuration must be at least 500 ms.")]
    [InlineData("BreakDuration", "Kaleido:Events:Resilience:CircuitBreakerBreakDuration must be at least 500 ms.")]
    public void Validate_InvalidValue_ThrowsInvalidEventOptions(string setting, string message)
    {
        var sut = Sut;
        switch (setting)
        {
            case "BatchSize": sut.BatchSize = 0; break;
            case "MaxConcurrentPublishes": sut.MaxConcurrentPublishes = 0; break;
            case "AttemptTimeout": sut.Resilience.AttemptTimeout = TimeSpan.Zero; break;
            case "MaxRetryAttempts": sut.Resilience.MaxRetryAttempts = -1; break;
            case "RetryDelay": sut.Resilience.RetryDelay = TimeSpan.Zero; break;
            case "FailureRatioZero": sut.Resilience.CircuitBreakerFailureRatio = 0; break;
            case "FailureRatioAboveOne": sut.Resilience.CircuitBreakerFailureRatio = 1.5; break;
            case "MinimumThroughput": sut.Resilience.CircuitBreakerMinimumThroughput = 1; break;
            case "SamplingDuration": sut.Resilience.CircuitBreakerSamplingDuration = TimeSpan.FromMilliseconds(100); break;
            default: sut.Resilience.CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(100); break;
        }

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(sut));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
        Assert.Equal(message, exception.Message);
    }
}
