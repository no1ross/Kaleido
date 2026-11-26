using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.UnitTests;

public sealed class KaleidoQueryableOptionsTests : SutFixture<KaleidoQueryableOptions>
{
    protected override KaleidoQueryableOptions CreateSut() => new();

    [Fact]
    public void Defaults_InheritTheServiceRule()
    {
        Assert.Null(Sut.DefaultAuthorization);
        Assert.Equal("Kaleido:Queryable", ((IKaleidoOptions)Sut).ConfigurationPath);
        ((IKaleidoOptions)Sut).Validate();
    }

    [Fact]
    public void Snapshot_KeepsAuthorizationRolesIndependentOfTheCaller()
    {
        var roles = new List<string> { "reader" };
        var code = Sut with { DefaultAuthorization = new AuthorizationMetadata("staff", roles) };
        var snapshot = code.Snapshot();
        roles.Add("later");

        Assert.Equal(["reader"], snapshot.DefaultAuthorization?.Roles);
        Assert.Equal(["reader", "later"], code.DefaultAuthorization?.Roles);
        Assert.Null(Sut.Snapshot().DefaultAuthorization);
    }

    [Fact]
    public void Validate_RejectsConflictingQueryableDefault()
    {
        var options = Sut with
        {
            DefaultAuthorization = new AuthorizationMetadata(null, ["radiology"]) { AllowAnonymous = true },
        };

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization,
            Assert.Throws<KaleidoConfigurationException>(() => ((IKaleidoOptions)options).Validate()).Code);
    }
}
