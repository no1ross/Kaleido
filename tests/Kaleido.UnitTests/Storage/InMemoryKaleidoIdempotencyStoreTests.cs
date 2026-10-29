using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoIdempotencyStoreTests
    : SutFixture
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private InMemoryKaleidoIdempotencyStore CreateSut(ILogger<InMemoryKaleidoIdempotencyStore>? logger = null) =>
        new(time, logger ?? new Mock<ILogger<InMemoryKaleidoIdempotencyStore>>().Object);

    private IdempotentResult Admission(string key, DateTimeOffset? expiresUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            AdmissionId = Guid.NewGuid(),
            RequestHash = hash,
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = time.GetUtcNow().AddMinutes(5),
            ExpiresUtc = expiresUtc ?? time.GetUtcNow().AddHours(1),
        };

    private static IdempotentResult Completed(IdempotentResult admission, string response) =>
        admission with { State = IdempotentResultState.Completed, StatusCode = 200, Response = response };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        var sut = CreateSut();
        var first = Admission("svc:alice:k1");

        Assert.Null(await sut.AdmitAsync(first, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
        Assert.Equal("hash", existing.RequestHash);
        Assert.Equal(first.AdmissionId, existing.AdmissionId);
    }

    // #285: two attempts with one key at the same time never both run.
    [Fact]
    public async Task AdmitAsync_InParallel_AdmitsExactlyOne()
    {
        var sut = CreateSut();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => sut.AdmitAsync(Admission("svc:alice:race"), TestContext.Current.CancellationToken))));

        Assert.Single(outcomes, x => x is null);
    }

    [Fact]
    public async Task CompleteAsync_ByTheAdmission_IsAccepted_AndAdmitAsyncThenReturnsTheAnswer()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(Completed(admission, """{"processId":"p"}"""), TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.Completed, existing.State);
        Assert.Equal(200, existing.StatusCode);
        Assert.Equal("""{"processId":"p"}""", existing.Response);
    }

    [Fact]
    public async Task CompleteAsync_WithAnUnknownOutcome_KeepsTheKeyRefusing()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(admission with { State = IdempotentResultState.OutcomeUnknown }, TestContext.Current.CancellationToken));

        Assert.Equal(IdempotentResultState.OutcomeUnknown, (await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken))?.State);
    }

    // A finalized record is final: neither a second completion nor a release changes it.
    [Fact]
    public async Task CompleteAsync_And_ReleaseAsync_DoNotTouchAFinalizedRecord()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:done");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(Completed(admission, "{}"), TestContext.Current.CancellationToken);

        Assert.False(await sut.CompleteAsync(Completed(admission, "{\"again\":true}"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken);
        Assert.Equal("{}", existing?.Response);
    }

    [Fact]
    public async Task ReleaseAsync_ByTheAdmission_FreesTheKey()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:failed");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:failed"), TestContext.Current.CancellationToken));
    }

    // #441: admission A expires, the key is admitted again as B, then A finishes late. A's completion
    // and release are refused, and B's record is untouched - even with an identical request.
    [Fact]
    public async Task AnAdmissionThatOutlivedItsRecord_CannotCompleteOrReleaseTheNextAdmission()
    {
        var sut = CreateSut();
        var a = Admission("svc:alice:k", time.GetUtcNow().AddMinutes(-1));
        var b = Admission("svc:alice:k");
        await sut.AdmitAsync(a, TestContext.Current.CancellationToken);
        Assert.Null(await sut.AdmitAsync(b, TestContext.Current.CancellationToken));

        Assert.False(await sut.CompleteAsync(Completed(a, "{\"from\":\"a\"}"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(a.Key, a.AdmissionId, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(b.AdmissionId, existing.AdmissionId);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
    }

    [Fact]
    public async Task CompleteAsync_And_ReleaseAsync_ForAKeyWithoutARecord_AreRefused()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:none");

        Assert.False(await sut.CompleteAsync(Completed(admission, "{}"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));
        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:none"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdmitAsync_OverAnExpiredRecord_AdmitsTheKeyAgain()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:old", time.GetUtcNow().AddMinutes(-1)), TestContext.Current.CancellationToken);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:old"), TestContext.Current.CancellationToken));
    }

    // A record is kept until its retention ends: just before, the key still refuses; at the end it is
    // admitted again.
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task AdmitAsync_AroundTheEndOfTheRetention(int ticksFromTheEnd, bool admittedAgain)
    {
        var sut = CreateSut();
        var expires = time.GetUtcNow().AddHours(1);
        await sut.AdmitAsync(Admission("svc:alice:kept", expires), TestContext.Current.CancellationToken);

        time.SetUtcNow(expires + TimeSpan.FromTicks(ticksFromTheEnd));

        Assert.Equal(admittedAgain, await sut.AdmitAsync(Admission("svc:alice:kept"), TestContext.Current.CancellationToken) is null);
    }

    [Fact]
    public void Constructor_WarnsOnceThatTheStoreIsInMemoryOnly()
    {
        var logger = new Mock<ILogger<InMemoryKaleidoIdempotencyStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        var log = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, (LogLevel)log.Arguments[0]);
        Assert.Contains("InMemoryKaleidoIdempotencyStore is active", log.Arguments[2].ToString());
    }
}
