using System.Diagnostics;

namespace Kaleido.Observability.UnitTests;

public sealed class ActivityExceptionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddExceptionEvent_AddsOpenTelemetryExceptionEvent()
    {
        using var activity = new Activity("test").Start();
        var exception = new InvalidOperationException("boom");

        activity.AddExceptionEvent(exception);

        var exceptionEvent = Assert.Single(activity.Events);
        Assert.Equal("exception", exceptionEvent.Name);

        var tags = exceptionEvent.Tags.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
        Assert.Equal("boom", tags["exception.message"]);
        Assert.Equal(exception.ToString(), tags["exception.stacktrace"]);
    }

    [Fact]
    public void AddExceptionEvent_WhenActivityIsNull_DoesNothing()
    {
        Activity? activity = null;

        activity.AddExceptionEvent(new InvalidOperationException("boom"));

        Assert.Null(activity);
    }
}
