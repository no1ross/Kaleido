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
