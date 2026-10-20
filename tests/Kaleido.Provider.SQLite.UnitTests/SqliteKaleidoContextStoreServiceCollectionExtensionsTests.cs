using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;
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
    public void UseSqliteKaleidoContextStore_WhenConnectionStringIsValid_ReplacesTheThreeDefaultStores()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IKaleidoContextStore>());
        services.AddSingleton(Mock.Of<IKaleidoOutboxStore>());
        services.AddSingleton(Mock.Of<IKaleidoIdempotencyStore>());
        var builder = CreateBuilder(services);

        var result = builder.UseSqliteKaleidoContextStore("Data Source=:memory:");

        Assert.Same(builder, result);
        Assert.Equal(typeof(SqliteKaleidoContextStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoContextStore)).ImplementationType);
        Assert.Equal(typeof(SqliteKaleidoOutboxStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoOutboxStore)).ImplementationType);
        Assert.Equal(typeof(SqliteKaleidoIdempotencyStore), Assert.Single(services, x => x.ServiceType == typeof(IKaleidoIdempotencyStore)).ImplementationType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UseSqliteKaleidoContextStore_WhenConnectionStringIsBlank_FailsBeforeRegistration(
        string? connectionString)
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services);

        Assert.ThrowsAny<ArgumentException>(() =>
            builder.UseSqliteKaleidoContextStore(connectionString!));

        Assert.Empty(services);
    }

    [Fact]
    public void UseSqliteKaleidoContextStore_WhenResolved_ConfiguresSqliteWithoutAHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new KaleidoServiceOptions { ServiceName = "test-svc", InstanceId = Guid.NewGuid() });
        CreateBuilder(services).UseSqliteKaleidoContextStore("Data Source=:memory:");

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.Equal("Data Source=:memory:", dbContext.Database.GetConnectionString());
        Assert.IsType<SqliteKaleidoContextStore>(scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>());
        Assert.IsType<SqliteKaleidoOutboxStore>(scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
        Assert.IsType<SqliteKaleidoIdempotencyStore>(scope.ServiceProvider.GetRequiredService<IKaleidoIdempotencyStore>());
        Assert.DoesNotContain(services, x => x.ServiceType.Namespace == "Microsoft.Extensions.Diagnostics.HealthChecks");
    }

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        return mock.Object;
    }
}
