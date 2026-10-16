using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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
    public void UseSqliteKaleidoContextStore_WhenResolved_ConfiguresSqliteAndTheHealthCheck()
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
        var registration = Assert.Single(
            provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal("kaleido-sqlite-context-store", registration.Name);
    }

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        return mock.Object;
    }
}
