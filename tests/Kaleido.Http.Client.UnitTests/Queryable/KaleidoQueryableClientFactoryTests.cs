using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Queryable.UnitTests;

public sealed class KaleidoQueryableClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoClientCatalog> catalog = new();
    private readonly IKaleidoQueryableClient member = Mock.Of<IKaleidoQueryableClient>();

    private KaleidoQueryableClientFactory CreateSut()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("MemberService", member);

        return new(services.BuildServiceProvider(), catalog.Object);
    }

    private static KaleidoClientDefinition Member =>
        new("MemberService", "member", RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    [Fact]
    public void GetClient_ForAnUnconfiguredName_FailsWithUnknownClient()
    {
        catalog.Setup(x => x.Clients).Returns([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut().GetClient("Member"));

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
    }

    [Fact]
    public void GetClient_ResolvesTheClientRegisteredUnderTheConfiguredName()
    {
        catalog.Setup(x => x.Find("memberservice")).Returns(Member);

        Assert.Same(member, CreateSut().GetClient("memberservice"));
    }

    [Fact]
    public void TryGetClientForService_FindsTheClientByItsRoutePrefix()
    {
        catalog.Setup(x => x.FindByService("member")).Returns(Member);

        Assert.True(CreateSut().TryGetClientForService("member", out var client));
        Assert.Same(member, client);
    }

    [Fact]
    public void TryGetClientForService_ForAServiceWithoutAClient_ReturnsFalse()
    {
        Assert.False(CreateSut().TryGetClientForService("codeset", out var client));
        Assert.Null(client);
    }
}
