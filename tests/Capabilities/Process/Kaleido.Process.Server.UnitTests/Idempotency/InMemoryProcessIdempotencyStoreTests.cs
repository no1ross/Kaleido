using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Process.Server.Idempotency.UnitTests;

public sealed class InMemoryProcessIdempotencyStoreTests
    : SutFixture
{
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private InMemoryProcessIdempotencyStore CreateSut(ILogger<InMemoryProcessIdempotencyStore>? logger = null) =>
        new(time, logger ?? new Mock<ILogger<InMemoryProcessIdempotencyStore>>().Object);

    private IdempotencyRecord Admission(string key, DateTimeOffset? expiresUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            AdmissionId = Guid.NewGuid(),
            RequestHash = hash,
            State = IdempotencyState.InProgress,
            LeaseUntilUtc = time.GetUtcNow().AddMinutes(5),
            ExpiresUtc = expiresUtc ?? time.GetUtcNow().AddHours(1),
        };

    private static IdempotencyRecord Completed(IdempotencyRecord admission, string response) =>
        admission with { State = IdempotencyState.Completed, Replay = new IdempotencyReplay("test/binary", System.Text.Encoding.UTF8.GetBytes(response)) };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        var sut = CreateSut();
        var first = Admission("svc:alice:k1");

        Assert.Null(await sut.AdmitAsync(first, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotencyState.InProgress, existing.State);
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

        Assert.True(await sut.CompleteAsync(Completed(admission, "answer"), TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotencyState.Completed, existing.State);
        Assert.Equal("test/binary", existing.Replay?.Format);
        Assert.Equal("answer", System.Text.Encoding.UTF8.GetString(Assert.IsType<IdempotencyReplay>(existing.Replay).Payload));
    }

    [Fact]
    public async Task CompletedReplay_BytesCannotBeChangedByInputOrReadback()
    {
        var sut = CreateSut();
        var admission = Admission("bytes");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        byte[] input = [1, 2];
        await sut.CompleteAsync(admission with { State = IdempotencyState.Completed, Replay = new IdempotencyReplay("binary/v1", input) }, TestContext.Current.CancellationToken);
        input[0] = 9;

        var first = await sut.AdmitAsync(Admission("bytes"), TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], first?.Replay?.Payload);
        Assert.IsType<IdempotencyReplay>(first?.Replay).Payload[0] = 8;
        Assert.Equal([1, 2], (await sut.AdmitAsync(Admission("bytes"), TestContext.Current.CancellationToken))?.Replay?.Payload);
    }

    [Fact]
    public async Task CompleteAsync_WithAnUnknownOutcome_KeepsTheKeyRefusing()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(admission with { State = IdempotencyState.OutcomeUnknown }, TestContext.Current.CancellationToken));

        Assert.Equal(IdempotencyState.OutcomeUnknown, (await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken))?.State);
    }

    // A finalized record is final: neither a second completion nor a release changes it.
    [Fact]
    public async Task CompleteAsync_And_ReleaseAsync_DoNotTouchAFinalizedRecord()
    {
        var sut = CreateSut();
        var admission = Admission("svc:alice:done");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(Completed(admission, "{}"), TestContext.Current.CancellationToken);

        Assert.False(await sut.CompleteAsync(Completed(admission, "again"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken);
        Assert.Equal("{}", System.Text.Encoding.UTF8.GetString(Assert.IsType<IdempotencyReplay>(existing?.Replay).Payload));
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

        Assert.False(await sut.CompleteAsync(Completed(a, "late"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(a.Key, a.AdmissionId, TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(b.AdmissionId, existing.AdmissionId);
        Assert.Equal(IdempotencyState.InProgress, existing.State);
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
    public async Task AdmitAsync_AfterLeaseButBeforeExpiry_StillRefusesTheKey()
    {
        var sut = CreateSut();
        var admitted = Admission("lease");
        await sut.AdmitAsync(admitted, TestContext.Current.CancellationToken);
        time.SetUtcNow(admitted.LeaseUntilUtc.AddTicks(1));

        var existing = await sut.AdmitAsync(Admission("lease"), TestContext.Current.CancellationToken);
        Assert.Equal(admitted.AdmissionId, existing?.AdmissionId);
    }

    [Fact]
    public async Task InvalidReplayShape_IsRejectedWithoutChangingTheAdmission()
    {
        var sut = CreateSut();
        var admitted = Admission("shape");
        await sut.AdmitAsync(admitted, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.CompleteAsync(admitted with { State = IdempotencyState.Completed }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CompleteAsync(admitted with { State = IdempotencyState.OutcomeUnknown, Replay = new IdempotencyReplay("binary", []) }, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AdmitAsync(Completed(admitted, "bad"), TestContext.Current.CancellationToken));
        Assert.Equal(IdempotencyState.InProgress, (await sut.AdmitAsync(Admission("shape"), TestContext.Current.CancellationToken))?.State);
    }

    [Fact]
    public async Task CanceledOperations_DoNotMutateTheRecord()
    {
        var sut = CreateSut();
        var admission = Admission("cancel");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.AdmitAsync(admission, canceled.Token));
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.CompleteAsync(Completed(admission, "answer"), canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ReleaseAsync(admission.Key, admission.AdmissionId, canceled.Token));
        Assert.Equal(IdempotencyState.InProgress, (await sut.AdmitAsync(Admission("cancel"), TestContext.Current.CancellationToken))?.State);
    }

    [Fact]
    public void Constructor_WarnsOnceThatTheStoreIsInMemoryOnly()
    {
        var logger = new Mock<ILogger<InMemoryProcessIdempotencyStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        var log = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, (LogLevel)log.Arguments[0]);
        Assert.Contains("InMemoryProcessIdempotencyStore is active", log.Arguments[2].ToString());
    }
}
