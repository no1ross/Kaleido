using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Process.UnitTests;

public sealed class KaleidoProcessOptionsTests : SutFixture<KaleidoProcessOptions>
{
    protected override KaleidoProcessOptions CreateSut() => new();

    [Fact]
    public void Defaults_InheritTheServiceRuleAndDoNotMarkAnEntry()
    {
        Assert.False(Sut.IsEntryProcessor);
        Assert.Null(Sut.DefaultAuthorization);
        Assert.Equal("Kaleido:Process", ((IKaleidoOptions)Sut).ConfigurationPath);
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
    public void Validate_RejectsConflictingProcessDefault()
    {
        var options = Sut with
        {
            DefaultAuthorization = new AuthorizationMetadata("staff", []) { AllowAnonymous = true },
        };

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization,
            Assert.Throws<KaleidoConfigurationException>(() => ((IKaleidoOptions)options).Validate()).Code);
    }
}
