using Kaleido.UnitTests;

namespace Kaleido.Process.Server.Idempotency.UnitTests;

public sealed class IdempotencyReplayTests : SutFixture<IdempotencyReplay>
{
    protected override IdempotencyReplay CreateSut() => CreateSut("binary/v1", [1, 2]);

    private static IdempotencyReplay CreateSut(string format, byte[] payload) => new(format, payload);

    [Fact]
    public void Constructor_RejectsEmptyFormatAndNullPayload()
    {
        Assert.Throws<ArgumentException>(() => CreateSut(" ", []));
        Assert.Throws<ArgumentNullException>(() => CreateSut("binary/v1", null!));
    }

    [Fact]
    public void Payload_SnapshotsBothInputAndReadback()
    {
        byte[] source = [1, 2];
        var sut = CreateSut("binary/v1", source);
        source[0] = 9;
        sut.Payload[1] = 8;

        Assert.Equal("binary/v1", sut.Format);
        Assert.Equal([1, 2], sut.Payload);
    }
}
