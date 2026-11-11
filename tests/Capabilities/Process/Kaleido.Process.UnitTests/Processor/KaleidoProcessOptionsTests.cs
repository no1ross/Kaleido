using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

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
