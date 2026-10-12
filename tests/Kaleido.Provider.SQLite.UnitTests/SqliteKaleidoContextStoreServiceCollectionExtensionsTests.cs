using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteKaleidoContextStoreServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData("NotAnOption=true")]
    [InlineData("Data Source=:memory:;Mode=NotARealMode")]
    public void UseSqliteKaleidoContextStore_WhenConnectionStringIsInvalid_FailsBeforeRegistration(
        string connectionString)
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services);

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            builder.UseSqliteKaleidoContextStore(connectionString));

        Assert.Equal("invalid_connection_string", exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void UseSqliteKaleidoContextStore_WhenConnectionStringIsValid_ReplacesDefaultStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IKaleidoContextStore>());
        var builder = CreateBuilder(services);

        var result = builder.UseSqliteKaleidoContextStore("Data Source=:memory:");

        Assert.Same(builder, result);
        var store = Assert.Single(services, x => x.ServiceType == typeof(IKaleidoContextStore));
        Assert.Equal(typeof(SqliteKaleidoContextStore), store.ImplementationType);
    }

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        return mock.Object;
    }
}
