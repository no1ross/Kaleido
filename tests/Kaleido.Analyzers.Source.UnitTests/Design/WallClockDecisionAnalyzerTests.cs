using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.WallClockDecisionAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

// #501: a wall-clock read that decides behavior is reported; a recorded timestamp is not.
public sealed class WallClockDecisionAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.WallClockDecisionAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.WallClockDecisionAnalyzer CreateSut() =>
        new();

    private static DiagnosticResult Expected(int location, string read) =>
        new DiagnosticResult("KAL0021", DiagnosticSeverity.Warning).WithLocation(location).WithArguments(read);

    [Fact]
    public async Task AComparison_Reports()
    {
        await RunAsync(@"
using System;
public sealed class Lease
{
    public bool Held(DateTimeOffset until) => until > {|#0:DateTimeOffset.UtcNow|};
    public bool Expired(DateTime expires) => {|#1:DateTime.UtcNow|} >= expires;
}",
            Expected(0, "DateTimeOffset.UtcNow"),
            Expected(1, "DateTime.UtcNow"));
    }

    [Fact]
    public async Task DateArithmetic_Reports()
    {
        await RunAsync(@"
using System;
public sealed class Retry
{
    public DateTimeOffset Next(TimeSpan delay) => {|#0:DateTimeOffset.UtcNow|} + delay;
    public TimeSpan Age(DateTimeOffset at) => {|#1:DateTimeOffset.Now|} - at;
    public DateTimeOffset Expires() => {|#2:DateTimeOffset.UtcNow|}.AddMinutes(5);
    public int Order(DateTime at) => {|#3:DateTime.Today|}.CompareTo(at);
    public int Static(DateTimeOffset at) => DateTimeOffset.Compare({|#4:DateTimeOffset.UtcNow|}, at);
}",
            Expected(0, "DateTimeOffset.UtcNow"),
            Expected(1, "DateTimeOffset.Now"),
            Expected(2, "DateTimeOffset.UtcNow"),
            Expected(3, "DateTime.Today"),
            Expected(4, "DateTimeOffset.UtcNow"));
    }

    // Through a member of the value, or a local it initializes or is assigned to.
    [Fact]
    public async Task ADecisionThroughAMemberOrALocal_Reports()
    {
        await RunAsync(@"
using System;
public sealed class Store
{
    public bool Due(long nextTicks) => nextTicks <= {|#0:DateTimeOffset.UtcNow|}.UtcTicks;

    public bool Expired(DateTimeOffset expires)
    {
        var now = {|#1:DateTimeOffset.UtcNow|};
        return expires <= now;
    }

    public bool Later(DateTimeOffset at)
    {
        DateTimeOffset now;
        now = {|#2:DateTimeOffset.UtcNow|};
        return (now) > at;
    }
}",
            Expected(0, "DateTimeOffset.UtcNow"),
            Expected(1, "DateTimeOffset.UtcNow"),
            Expected(2, "DateTimeOffset.UtcNow"));
    }

    // A timestamp that is only recorded is not a clock dependency (#501).
    [Fact]
    public async Task ARecordedTimestamp_NoDiagnostic()
    {
        await RunAsync(@"
using System;
public sealed record Step(DateTimeOffset? FirstExecuted, DateTimeOffset LastExecuted);
public sealed class Recorder
{
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public Step Executed(Step step)
    {
        var now = DateTimeOffset.UtcNow;
        Log(now);
        return step with { FirstExecuted = step.FirstExecuted ?? now, LastExecuted = now };
    }

    public void Save(DateTimeOffset created) =>
        Created = created == default ? DateTimeOffset.UtcNow : created;

    public DateTimeOffset Stamp(DateTimeOffset? given) => given ?? DateTime.Now;

    private static void Log(DateTimeOffset at) { }
}");
    }

    // A value that is compared, not the clock: the comparison's condition reads another value.
    [Fact]
    public async Task OtherDateMembersAndTimeProvider_NoDiagnostic()
    {
        await RunAsync(@"
using System;
public sealed class Clocked
{
    private readonly TimeProvider time = TimeProvider.System;
    public bool Held(DateTimeOffset until) => until > time.GetUtcNow();
    public bool Min(DateTimeOffset at) => at > DateTimeOffset.MinValue;
    public DateTimeOffset Today(DateTimeOffset at) => at.Date == at.Date ? at : at;
    public static DateTime UtcNow => default;
    public bool Own(DateTime at) => at > UtcNow;
}");
    }

    // A local that is reassigned from elsewhere and never compared is not a decision; another local
    // with the same name in another method does not count.
    [Fact]
    public async Task ALocalUsedOnlyToRecord_NoDiagnostic()
    {
        await RunAsync(@"
using System;
public sealed class Two
{
    public DateTimeOffset Recorded()
    {
        var now = DateTimeOffset.UtcNow;
        return now;
    }

    public bool Other(DateTimeOffset at)
    {
        var now = at;
        return now > at;
    }

    public Func<DateTimeOffset> Later() => () => { var now = DateTimeOffset.UtcNow; return now; };
}");
    }
}
