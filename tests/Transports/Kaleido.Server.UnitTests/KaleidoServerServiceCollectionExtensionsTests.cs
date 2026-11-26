using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Server.UnitTests;

public sealed class KaleidoServerServiceCollectionExtensionsTests : SutFixture
{
    private const string Staff = "staff";
    private const string Radiology = "radiology";

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static IKaleidoBuilder NewBuilder(IConfiguration? configuration = null) =>
        new ServiceCollection().AddKaleido(configuration ?? EmptyConfig(), new()
        {
            ServiceName = "test-service",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoServerServiceCollectionExtensionsTests).Assembly],
        });

    [Fact]
    public void AddServer_RegistersEffectiveOptionsOnce()
    {
        var builder = NewBuilder();

        Assert.Same(builder, builder.AddServer());
        var registration = Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoServerOptions));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
        Assert.Equal(KaleidoAuthorizationMode.Authenticated, Assert.IsType<KaleidoServerOptions>(registration.ImplementationInstance).AuthorizationMode);
    }

    // #288: leaving capabilities open must be an explicit None rather than the Authenticated default.
    [Fact]
    public void AddServer_ConfiguredNone_LeavesCapabilitiesOpen()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:AuthorizationMode"] = "None" })
            .Build();
        var builder = NewBuilder(config).AddServer();

        var registered = Assert.IsType<KaleidoServerOptions>(Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoServerOptions)).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.None, registered.AuthorizationMode);
    }

    [Fact]
    public void AddServer_LaysConfigurationOverCodeWithoutChangingIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:AuthorizationMode"] = "ZeroTrust",
                ["Kaleido:DefaultAuthorization:Roles:0"] = Radiology,
            })
            .Build();
        var roles = new List<string> { "admin" };
        var code = new KaleidoServerOptions
        {
            AuthorizationMode = KaleidoAuthorizationMode.None,
            DefaultAuthorization = new AuthorizationMetadata(Staff, roles),
        };
        var builder = NewBuilder(config).AddServer(code);
        roles.Add("other");

        var registered = Assert.IsType<KaleidoServerOptions>(Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoServerOptions)).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.ZeroTrust, registered.AuthorizationMode);
        Assert.Equal(Staff, registered.DefaultAuthorization.Policy);
        Assert.Equal([Radiology], registered.DefaultAuthorization.Roles); // replaced, not appended
        Assert.Equal(KaleidoAuthorizationMode.None, code.AuthorizationMode);
        Assert.Equal(["admin", "other"], code.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddServer_ClonesCodeRolesWithoutMutatingTheCaller()
    {
        var roles = new List<string> { "admin" };
        var code = new KaleidoServerOptions { DefaultAuthorization = new AuthorizationMetadata(null, roles) };
        var builder = NewBuilder().AddServer(code);
        roles.Add("other");

        var registered = Assert.IsType<KaleidoServerOptions>(Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoServerOptions)).ImplementationInstance);
        Assert.Equal(["admin"], registered.DefaultAuthorization.Roles);
        Assert.Equal(["admin", "other"], code.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddServer_BindsDefaultRuleFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DefaultAuthorization:Policy"] = Staff,
                ["Kaleido:DefaultAuthorization:Roles:0"] = Radiology,
            })
            .Build();
        var builder = NewBuilder(config).AddServer();

        var registered = Assert.IsType<KaleidoServerOptions>(Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoServerOptions)).ImplementationInstance);
        Assert.Equal(Staff, registered.DefaultAuthorization.Policy);
        Assert.Equal(Radiology, Assert.Single(registered.DefaultAuthorization.Roles));
        Assert.False(AuthorizationMetadata.Unspecified.IsExplicit());
    }

    [Fact]
    public void AddServer_InvalidCodeMode_ThrowsInvalidAuthorizationMode()
    {
        var builder = NewBuilder();

        Assert.Equal(ConfigurationErrorCodes.InvalidAuthorizationMode,
            Assert.Throws<KaleidoConfigurationException>(() => builder.AddServer(new() { AuthorizationMode = (KaleidoAuthorizationMode)123 })).Code);
    }

    [Fact]
    public void AddServer_UnreadableConfiguredMode_ThrowsInvalidOptions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:AuthorizationMode"] = "not-a-mode" })
            .Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => NewBuilder(config).AddServer());

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains("AuthorizationMode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddServer_InvalidCodeRule_ThrowsConflictingAuthorization()
    {
        var builder = NewBuilder();
        var code = new KaleidoServerOptions
        {
            DefaultAuthorization = new AuthorizationMetadata(Staff, []) { AllowAnonymous = true },
        };

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization,
            Assert.Throws<KaleidoConfigurationException>(() => builder.AddServer(code)).Code);
    }

    [Fact]
    public void AddServer_InvalidConfiguredRule_ThrowsConflictingAuthorization()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DefaultAuthorization:AllowAnonymous"] = "true",
                ["Kaleido:DefaultAuthorization:Roles:0"] = "admin",
            })
            .Build();

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization,
            Assert.Throws<KaleidoConfigurationException>(() => NewBuilder(config).AddServer()).Code);
    }

    [Fact]
    public void AddServer_CalledTwice_ThrowsDuplicateRegistration()
    {
        var builder = NewBuilder().AddServer();

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration,
            Assert.Throws<KaleidoConfigurationException>(() => builder.AddServer()).Code);
    }

    [Fact]
    public void AddServer_NullBuilder_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddServer());
    }
}
