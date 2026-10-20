using Kaleido.UnitTests;

namespace Kaleido.Observability.UnitTests;

public sealed class KaleidoCorrelationContextTests
    : SutFixture
{
    private static KaleidoCorrelationContext CreateSut() => new();

    [Fact]
    public void IsEmpty_WithoutAnyCorrelation_IsTrue()
    {
        Assert.True(CreateSut().IsEmpty);
        Assert.True((CreateSut() with { RequestId = " ", CallingProcessorName = "", CallingStepName = " " }).IsEmpty);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("process")]
    [InlineData("processor")]
    [InlineData("step")]
    public void IsEmpty_WithAnyCorrelationField_IsFalse(string field)
    {
        var context = field switch
        {
            "request" => CreateSut() with { RequestId = "req-1" },
            "process" => CreateSut() with { ProcessId = Guid.NewGuid() },
            "processor" => CreateSut() with { CallingProcessorName = "intake" },
            _ => CreateSut() with { CallingStepName = "CaptureMember" },
        };

        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void IsEmpty_IgnoresTheCallerAndTheOrigin()
    {
        Assert.True((CreateSut() with { CallerName = "alice", CallerActor = "intake", Origin = "mcp" }).IsEmpty);
    }

    [Theory]
    [InlineData("alice", "sub-1")]
    [InlineData("alice", null)]
    [InlineData(null, "sub-1")]
    public void Caller_IsTheNameAndIdWhenEitherIsKnown(string? name, string? id)
    {
        Assert.Equal(new KaleidoCaller { Name = name, Id = id }, (CreateSut() with { CallerName = name, CallerId = id }).Caller);
    }

    [Fact]
    public void Caller_IsNullForAnAnonymousRequest()
    {
        Assert.Null(CreateSut().Caller);
    }
}
