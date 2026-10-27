using Kaleido.Guide.Examples.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.Guide;

// docs/guide/deployment.md, "MCP on several instances: one key ring", run: two instances configured as
// the guide shows share their Data Protection keys - what one protects, the other reads (#484).
public sealed class DeploymentExampleTests : IDisposable
{
    private readonly DirectoryInfo _sharedKeys = Directory.CreateTempSubdirectory("kaleido-guide-keys-");

    public void Dispose() => _sharedKeys.Delete(recursive: true);

    private ServiceProvider Instance()
    {
        var services = new ServiceCollection();
        Operations.AddSharedKeyRing(services, _sharedKeys);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void TwoInstancesWithTheSharedKeyRing_ReadWhatTheOtherProtected()
    {
        using var first = Instance();
        using var second = Instance();

        var payload = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("guide").Protect("from the first instance");

        Assert.Equal("from the first instance", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("guide").Unprotect(payload));
        Assert.NotEmpty(_sharedKeys.GetFiles("key-*.xml"));
    }
}
