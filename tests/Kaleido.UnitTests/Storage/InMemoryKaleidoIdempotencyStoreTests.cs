using Kaleido.Processor.Context;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage.UnitTests;

public sealed class InMemoryKaleidoIdempotencyStoreTests
    : SutFixture
{
    private static InMemoryKaleidoIdempotencyStore CreateSut(ILogger<InMemoryKaleidoIdempotencyStore>? logger = null) =>
        new(logger ?? new Mock<ILogger<InMemoryKaleidoIdempotencyStore>>().Object);

    private static IdempotentResult Admission(string key, DateTimeOffset? expiresUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            RequestHash = hash,
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            ExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(1),
        };

    private static IdempotentResult Completed(string key, string response) =>
        new()
        {
            Key = key,
            RequestHash = "hash",
            State = IdempotentResultState.Completed,
            StatusCode = 200,
            Response = response,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        var sut = CreateSut();

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:k1")));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"));
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
        Assert.Equal("hash", existing.RequestHash);
    }

    // #285: two attempts with one key at the same time never both run.
    [Fact]
    public async Task AdmitAsync_InParallel_AdmitsExactlyOne()
    {
        var sut = CreateSut();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => sut.AdmitAsync(Admission("svc:alice:race")))));

        Assert.Single(outcomes, x => x is null);
    }

    [Fact]
    public async Task CompleteAsync_ThenAdmitAsync_ReturnsTheCompletedAnswer()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:k1"));

        await sut.CompleteAsync(Completed("svc:alice:k1", """{"processId":"p"}"""));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"));
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.Completed, existing.State);
        Assert.Equal(200, existing.StatusCode);
        Assert.Equal("""{"processId":"p"}""", existing.Response);
    }

    [Fact]
    public async Task ReleaseAsync_FreesAKeyInProgress_ButKeepsACompletedOne()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:failed"));
        await sut.AdmitAsync(Admission("svc:alice:done"));
        await sut.CompleteAsync(Completed("svc:alice:done", "{}"));

        await sut.ReleaseAsync("svc:alice:failed");
        await sut.ReleaseAsync("svc:alice:done");

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:failed")));
        Assert.Equal(IdempotentResultState.Completed, (await sut.AdmitAsync(Admission("svc:alice:done")))?.State);
    }

    [Fact]
    public async Task AdmitAsync_OverAnExpiredRecord_AdmitsTheKeyAgain()
    {
        var sut = CreateSut();
        await sut.AdmitAsync(Admission("svc:alice:old", DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:old")));
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