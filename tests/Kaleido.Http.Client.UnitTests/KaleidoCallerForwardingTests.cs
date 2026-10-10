namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoCallerForwardingTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoCallerForwarding CreateSut() => new();

    [Fact]
    public void OutsideAScope_NothingIsForwarded()
    {
        var sut = CreateSut();

        Assert.False(sut.IsForwarding);
        Assert.Null(sut.CallerAuthorization);
    }

    [Fact]
    public void InsideAScope_TheCallersAuthorizationIsForwarded_UntilDisposed()
    {
        var sut = CreateSut();

        using (sut.ForwardCaller("Bearer user-token"))
        {
            Assert.True(sut.IsForwarding);
            Assert.Equal("Bearer user-token", sut.CallerAuthorization);
        }

        Assert.False(sut.IsForwarding);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AnAnonymousCaller_IsForwardedAsAnonymous(string? authorization)
    {
        var sut = CreateSut();

        using (sut.ForwardCaller(authorization))
        {
            Assert.True(sut.IsForwarding);
            Assert.Null(sut.CallerAuthorization);
        }
    }

    [Fact]
    public void NestedScopes_RestoreTheOuterCaller()
    {
        var sut = CreateSut();

        using (sut.ForwardCaller("Bearer outer"))
        {
            using (sut.ForwardCaller("Bearer inner"))
            {
                Assert.Equal("Bearer inner", sut.CallerAuthorization);
            }

            Assert.Equal("Bearer outer", sut.CallerAuthorization);
        }
    }

    [Fact]
    public async Task TheScope_FlowsAcrossAwaits_AndDoesNotLeakIntoOtherFlows()
    {
        var sut = CreateSut();
        var other = Task.Run(async () =>
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            return sut.IsForwarding;
        }, TestContext.Current.CancellationToken);

        using (sut.ForwardCaller("Bearer user-token"))
        {
            await Task.Yield();

            Assert.Equal("Bearer user-token", sut.CallerAuthorization);
        }

        Assert.False(await other);
    }
}
