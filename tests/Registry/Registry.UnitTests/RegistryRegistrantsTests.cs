namespace Kaleido.Registry.UnitTests;

// #553: the services a caller may register - from its app roles naming them (assigned in the IdP), and
// from the optional registrants map.
public sealed class RegistryRegistrantsTests
    : SutFixture
{
    private static RegistryRegistrants CreateSut(string prefix = "Kaleido.Registrant.") =>
        new(new KaleidoRegistryOptions
        {
            RegistrantRolePrefix = prefix,
            Registrants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["radiology"] = ["radiology-client", "shared-client"],
                ["member"] = ["shared-client"],
            },
        });

    private static RegistryCaller Caller(string? id, params string[] roles) => new(id, roles);

    // The map turned around: each client id's services.
    [Fact]
    public void ServicesOf_AMappedClient_AreTheServicesItMayRegister()
    {
        var sut = CreateSut();

        Assert.Equal(["radiology"], sut.ServicesOf(Caller("radiology-client")));
        Assert.Equal(["radiology", "member"], sut.ServicesOf(Caller("shared-client")));
        Assert.Empty(sut.ServicesOf(Caller("RADIOLOGY-CLIENT")));
        Assert.Empty(sut.ServicesOf(Caller("unknown")));
        Assert.Empty(sut.ServicesOf(Caller(null)));
    }

    // 150 services need no registry configuration: each one's app role names it.
    [Fact]
    public void ServicesOf_ACallerWithRoles_AreTheServicesItsRolesName_WithoutTheMap()
    {
        var sut = CreateSut();

        Assert.Equal(["code-sets", "provider-search"], sut.ServicesOf(Caller("code-sets-app", "Kaleido.Registrant.Code-Sets", "kaleido.registrant.provider-search", "Kaleido.RegistryReader", "Kaleido.Registrant.")));
    }

    // Roles and map together; each service once.
    [Fact]
    public void ServicesOf_RolesAndTheMap_AreAdded()
    {
        Assert.Equal(["radiology", "member"], CreateSut().ServicesOf(Caller("radiology-client", "Kaleido.Registrant.member", "Kaleido.Registrant.radiology")));
    }

    [Fact]
    public void ServicesOf_UsesTheConfiguredPrefix()
    {
        Assert.Equal(["code-sets"], CreateSut(prefix: "Registry.").ServicesOf(Caller(null, "Registry.code-sets", "Kaleido.Registrant.member")));
    }

    [Fact]
    public void MayRegister_OnlyTheServicesTheCallerMay()
    {
        var sut = CreateSut();

        Assert.True(sut.MayRegister(Caller("radiology-client"), "RADIOLOGY"));
        Assert.True(sut.MayRegister(Caller(null, "Kaleido.Registrant.code-sets"), "code-sets"));
        Assert.False(sut.MayRegister(Caller("radiology-client", "Kaleido.Registrant.code-sets"), "member"));
        Assert.False(sut.MayRegister(Caller(null), "radiology"));
    }

    [Fact]
    public void ServicesOf_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().ServicesOf(null!));
    }
}
