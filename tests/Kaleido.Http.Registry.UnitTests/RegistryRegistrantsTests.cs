namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryRegistrantsTests
    : SutFixture
{
    private static RegistryRegistrants CreateSut() =>
        new(new KaleidoRegistryOptions
        {
            Registrants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["radiology"] = ["radiology-client", "shared-client"],
                ["member"] = ["shared-client"],
            },
        });

    // The map turned around: each client id's services.
    [Fact]
    public void ServicesOf_AClient_AreTheServicesItMayRegister()
    {
        var sut = CreateSut();

        Assert.Equal(["radiology"], sut.ServicesOf("radiology-client"));
        Assert.Equal(["radiology", "member"], sut.ServicesOf("shared-client"));
        Assert.Empty(sut.ServicesOf("RADIOLOGY-CLIENT"));
        Assert.Empty(sut.ServicesOf("unknown"));
        Assert.Empty(sut.ServicesOf(null));
    }

    [Fact]
    public void MayRegister_OnlyTheServicesMappedToTheClient()
    {
        var sut = CreateSut();

        Assert.True(sut.MayRegister("radiology-client", "RADIOLOGY"));
        Assert.False(sut.MayRegister("radiology-client", "member"));
        Assert.False(sut.MayRegister(null, "radiology"));
    }
}
