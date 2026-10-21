using Kaleido.Exceptions;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteKaleidoServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private const string Database = "Data Source=:memory:";

    public static TheoryData<string> Registrations() => [nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteContextStore), nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteOutboxStore), nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteIdempotencyStore)];

    private static IKaleidoBuilder Register(IKaleidoBuilder builder, string registration, string connectionString) =>
        registration switch
        {
            nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteContextStore) => builder.AddSqliteContextStore(connectionString),
            nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteOutboxStore) => builder.AddSqliteOutboxStore(connectionString),
            _ => builder.AddSqliteIdempotencyStore(connectionString),
        };

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        return mock.Object;
    }

    private static Type? Registered<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService))?.ImplementationType;

    [Theory]
    [MemberData(nameof(Registrations))]
    public void EachRegistration_WithAnInvalidConnectionString_FailsBeforeRegisteringAnything(string registration)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => Register(CreateBuilder(services), registration, "NotAnOption=true"));

        Assert.Equal(SqliteErrorCodes.InvalidConnectionString, exception.Code);
        Assert.Empty(services);
    }

    [Theory]
    [MemberData(nameof(Registrations))]
    public void EachRegistration_WithABlankConnectionString_FailsBeforeRegisteringAnything(string registration)
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<ArgumentException>(() => Register(CreateBuilder(services), registration, "  "));

        Assert.Empty(services);
    }

    // Each store on its own: only what was asked for is registered.
    [Fact]
    public void AddSqliteContextStore_RegistersTheContextStoreAndTheTransactions_Only()
    {
        var services = new ServiceCollection();

        var builder = CreateBuilder(services);
        Assert.Same(builder, builder.AddSqliteContextStore(Database));

        Assert.Equal(typeof(SqliteKaleidoContextStore), Registered<IKaleidoContextStore>(services));
        Assert.Equal(typeof(SqliteKaleidoTransactions), Registered<IKaleidoTransactions>(services));
        Assert.Equal(typeof(SqliteKaleidoTransaction), Registered<ISqliteKaleidoTransaction>(services));
        Assert.Null(Registered<IKaleidoOutboxStore>(services));
        Assert.Null(Registered<IKaleidoIdempotencyStore>(services));
    }

    [Fact]
    public void AddSqliteOutboxStore_RegistersTheOutboxStore_Only()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteOutboxStore(Database);

        Assert.Equal(typeof(SqliteKaleidoOutboxStore), Registered<IKaleidoOutboxStore>(services));
        Assert.Null(Registered<IKaleidoContextStore>(services));
        Assert.Null(Registered<IKaleidoTransactions>(services));
    }

    [Fact]
    public void AddSqliteIdempotencyStore_RegistersTheIdempotencyStore_Only()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteIdempotencyStore(Database);

        Assert.Equal(typeof(SqliteKaleidoIdempotencyStore), Registered<IKaleidoIdempotencyStore>(services));
        Assert.Null(Registered<IKaleidoContextStore>(services));
        Assert.Null(Registered<IKaleidoOutboxStore>(services));
    }

    [Fact]
    public void TheThreeRegistrations_ShareOneDatabase()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteContextStore(Database).AddSqliteOutboxStore(Database).AddSqliteIdempotencyStore(Database);

        Assert.Single(services, x => x.ServiceType == typeof(SqliteKaleidoContextDbContext));
    }

    [Fact]
    public void ARegistrationWithAnotherConnectionString_Fails()
    {
        var builder = CreateBuilder(new ServiceCollection()).AddSqliteContextStore(Database);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddSqliteOutboxStore("Data Source=other.db"));

        Assert.Equal(SqliteErrorCodes.ConnectionStringMismatch, exception.Code);
    }

    [Fact]
    public void AddSqliteContextStore_WhenAnotherContextStoreIsRegistered_Fails()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoContextStore>());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder(services).AddSqliteContextStore(Database));

        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, exception.Code);
    }

    [Fact]
    public void WhenResolved_TheStoresAndTransactionsShareTheRequestsDbContext_WithoutAHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
            {
                o.ServiceName = "test-svc";
                o.AuthorizationMode = KaleidoAuthorizationMode.None;
                o.Assemblies = [typeof(SqliteKaleidoServiceCollectionExtensionsTests).Assembly];
                o.TypeFilter = _ => false;
            })
            .AddSqliteContextStore(Database)
            .AddSqliteOutboxStore(Database)
            .AddSqliteIdempotencyStore(Database);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.Equal(Database, dbContext.Database.GetConnectionString());
        Assert.IsType<SqliteKaleidoContextStore>(scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>());
        Assert.IsType<SqliteKaleidoOutboxStore>(scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
        Assert.IsType<SqliteKaleidoIdempotencyStore>(scope.ServiceProvider.GetRequiredService<IKaleidoIdempotencyStore>());
        Assert.IsType<SqliteKaleidoTransactions>(scope.ServiceProvider.GetRequiredService<IKaleidoTransactions>());
        Assert.DoesNotContain(services, x => x.ServiceType.Namespace == "Microsoft.Extensions.Diagnostics.HealthChecks");
    }
}
