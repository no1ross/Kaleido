using Kaleido.Exceptions;

namespace Kaleido.UnitTests;

public sealed class KaleidoResilienceSettingsTests
    : SutFixture<KaleidoResilienceSettings>
{
    private const string Section = "Kaleido:Section:Resilience";
    private const string Code = "area_invalid_options";

    protected override KaleidoResilienceSettings CreateSut() => new();

    [Fact]
    public void Validate_TheDefaults_Pass()
    {
        KaleidoResilienceSettings.Validate(Sut, Section, Code);
    }

    [Fact]
    public void Validate_WhenTheSettingsAreNull_ThrowsTheAreasCode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoResilienceSettings.Validate(null, Section, Code));

        Assert.Equal(Code, exception.Code);
        Assert.Equal($"{Section} must not be null.", exception.Message);
    }

    // Every rule, reported with the section's path and the area's code; nothing is adjusted.
    [Theory]
    [InlineData("AttemptTimeout", "AttemptTimeout must be positive.")]
    [InlineData("MaxRetryAttempts", "MaxRetryAttempts must not be negative.")]
    [InlineData("RetryDelay", "RetryDelay must be positive.")]
    [InlineData("FailureRatioZero", "CircuitBreakerFailureRatio must be greater than 0 and at most 1.")]
    [InlineData("FailureRatioAboveOne", "CircuitBreakerFailureRatio must be greater than 0 and at most 1.")]
    [InlineData("MinimumThroughput", "CircuitBreakerMinimumThroughput must be at least 2.")]
    [InlineData("SamplingDuration", "CircuitBreakerSamplingDuration must be at least 500 ms.")]
    [InlineData("BreakDuration", "CircuitBreakerBreakDuration must be at least 500 ms.")]
    public void Validate_AnInvalidValue_ThrowsTheAreasCode(string setting, string rule)
    {
        var sut = setting switch
        {
            "AttemptTimeout" => Sut with { AttemptTimeout = TimeSpan.Zero },
            "MaxRetryAttempts" => Sut with { MaxRetryAttempts = -1 },
            "RetryDelay" => Sut with { RetryDelay = TimeSpan.Zero },
            "FailureRatioZero" => Sut with { CircuitBreakerFailureRatio = 0 },
            "FailureRatioAboveOne" => Sut with { CircuitBreakerFailureRatio = 1.5 },
            "MinimumThroughput" => Sut with { CircuitBreakerMinimumThroughput = 1 },
            "SamplingDuration" => Sut with { CircuitBreakerSamplingDuration = TimeSpan.FromMilliseconds(100) },
            _ => Sut with { CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(100) },
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoResilienceSettings.Validate(sut, Section, Code));

        Assert.Equal(Code, exception.Code);
        Assert.Equal($"{Section}:{rule}", exception.Message);
    }
}
