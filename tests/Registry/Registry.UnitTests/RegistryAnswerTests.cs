using Kaleido.Platform.Registry;

namespace Kaleido.Registry.UnitTests;

public sealed class RegistryAnswerTests
    : SutFixture
{
    private static readonly RegistryLease Lease = new() { InstanceId = Guid.NewGuid(), HeartbeatInterval = TimeSpan.FromSeconds(10), LeaseUntilUtc = DateTimeOffset.UnixEpoch };

    private static RegistryAnswer CreateSut(string factory) =>
        factory switch
        {
            "leased" => RegistryAnswer.Leased(Lease),
            "removed" => RegistryAnswer.Removed,
            _ => RegistryAnswer.Failed(RegistryAnswerKind.Unknown, "code", "message"),
        };

    [Fact]
    public void Leased_CarriesTheLease()
    {
        var sut = CreateSut("leased");

        Assert.Equal((RegistryAnswerKind.Leased, Lease, null, null), (sut.Kind, sut.Lease, sut.Code, sut.Message));
    }

    [Fact]
    public void Removed_CarriesNothing()
    {
        var sut = CreateSut("removed");

        Assert.Equal((RegistryAnswerKind.Removed, null, null, null), (sut.Kind, sut.Lease, sut.Code, sut.Message));
    }

    [Fact]
    public void Failed_CarriesItsKindCodeAndMessage()
    {
        var sut = CreateSut("failed");

        Assert.Equal((RegistryAnswerKind.Unknown, null, "code", "message"), (sut.Kind, sut.Lease, sut.Code, sut.Message));
    }
}
