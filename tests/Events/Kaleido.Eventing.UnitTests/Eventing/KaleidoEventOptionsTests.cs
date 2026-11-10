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

    // #552: the outbox health check's windows.
    [Fact]
    public void TheOutboxHealthDefaults_AreValid()
    {
        var sut = Sut;

        Assert.Equal(TimeSpan.FromSeconds(30), sut.RelayHeartbeatInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), sut.RelaySeenWithin);
        Assert.Equal(TimeSpan.FromMinutes(5), sut.BacklogMaxAge);
        Assert.Equal(10_000, sut.BacklogMaxCount);
        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, sut.OutboxHealthFailureStatus);
    }

    public static TheoryData<string> InvalidOutboxHealthSettings() =>
        ["heartbeat zero", "seen within the heartbeat", "backlog age zero", "backlog count zero", "healthy failure", "undefined failure"];

    [Theory]
    [MemberData(nameof(InvalidOutboxHealthSettings))]
    public void Validate_AnInvalidOutboxHealthSetting_ThrowsInvalidEventOptions(string setting)
    {
        var sut = Sut;
        var options = setting switch
        {
            "heartbeat zero" => sut with { RelayHeartbeatInterval = TimeSpan.Zero },
            "seen within the heartbeat" => sut with { RelaySeenWithin = sut.RelayHeartbeatInterval },
            "backlog age zero" => sut with { BacklogMaxAge = TimeSpan.Zero },
            "backlog count zero" => sut with { BacklogMaxCount = 0 },
            "healthy failure" => sut with { OutboxHealthFailureStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy },
            _ => sut with { OutboxHealthFailureStatus = (Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus)42 },
        };

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, Assert.Throws<KaleidoConfigurationException>(() => KaleidoEventOptions.Validate(options)).Code);
    }
}
