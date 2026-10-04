using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteProcessorContextStoreServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData("NotAnOption=true")]
    [InlineData("Data Source=:memory:;Mode=NotARealMode")]
    public void UseSqliteProcessorContextStore_WhenConnectionStringIsInvalid_FailsBeforeRegistration(
        string connectionString)
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services);

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            builder.UseSqliteProcessorContextStore(connectionString));

        Assert.Equal("invalid_connection_string", exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void UseSqliteProcessorContextStore_WhenConnectionStringIsValid_ReplacesDefaultStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IProcessorContextStore>());
        var builder = CreateBuilder(services);

        var result = builder.UseSqliteProcessorContextStore("Data Source=:memory:");

        Assert.Same(builder, result);
        var store = Assert.Single(services, x => x.ServiceType == typeof(IProcessorContextStore));
        Assert.Equal(typeof(SqliteProcessorContextStore), store.ImplementationType);
    }

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        return mock.Object;
    }
}
