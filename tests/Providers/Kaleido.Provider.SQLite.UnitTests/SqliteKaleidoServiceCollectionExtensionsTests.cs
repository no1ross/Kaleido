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

    public static TheoryData<string> Registrations() => [nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteContextProvider), nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteOutboxProvider), nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteIdempotencyProvider)];

    private static IKaleidoBuilder Register(IKaleidoBuilder builder, string registration, string connectionString) =>
        registration switch
        {
            nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteContextProvider) => builder.AddSqliteContextProvider(connectionString),
            nameof(SqliteKaleidoServiceCollectionExtensions.AddSqliteOutboxProvider) => builder.AddSqliteOutboxProvider(connectionString),
            _ => builder.AddSqliteIdempotencyProvider(connectionString),
        };

    private static IKaleidoBuilder CreateBuilder(IServiceCollection services, IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var mock = new Mock<IKaleidoBuilder>();
        mock.SetupGet(x => x.Services).Returns(services);
        mock.SetupGet(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(
            configuration ?? new Dictionary<string, string?>()).Build());
        return mock.Object;
    }

    private static Type? Registered<TService>(IServiceCollection services) =>
        services.LastOrDefault(x => x.ServiceType == typeof(TService))?.ImplementationType;

    [Fact]
    public void AddSqliteProviders_WithANamedConnection_RegistersExactlyTheThreeServiceStores()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services, new Dictionary<string, string?> { ["ConnectionStrings:orders"] = Database });

        Assert.Same(builder, builder.AddSqliteProviders("orders"));
        Assert.Equal(typeof(SqliteProcessContextStore), Registered<IProcessContextStore>(services));
        Assert.Equal(typeof(SqliteProcessTransactions), Registered<IProcessTransactions>(services));
        Assert.Equal(typeof(SqliteKaleidoOutboxStore), Registered<IKaleidoOutboxStore>(services));
        Assert.Equal(typeof(SqliteProcessIdempotencyStore), Registered<IProcessIdempotencyStore>(services));
        Assert.Null(Registered<IKaleidoRegistryStore>(services));
        Assert.Single(services, x => x.ServiceType == typeof(SqliteKaleidoContextDbContext));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void AddSqliteProviders_WithoutANamedConnection_FailsBeforeRegistering(string? value)
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services, new Dictionary<string, string?> { ["ConnectionStrings:orders"] = value });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddSqliteProviders("orders"));

        Assert.Equal(SqliteErrorCodes.MissingConnectionString, exception.Code);
        Assert.Contains("ConnectionStrings:orders", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void AddSqliteProviders_WithAnInvalidNamedConnection_FailsBeforeRegistering()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services, new Dictionary<string, string?> { ["ConnectionStrings:orders"] = "NotAnOption=true" });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddSqliteProviders("orders"));

        Assert.Equal(SqliteErrorCodes.InvalidConnectionString, exception.Code);
        Assert.Empty(services);
    }

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
    public void AddSqliteContextProvider_RegistersTheContextStoreAndTheTransactions_Only()
    {
        var services = new ServiceCollection();

        var builder = CreateBuilder(services);
        Assert.Same(builder, builder.AddSqliteContextProvider(Database));

        Assert.Equal(typeof(SqliteProcessContextStore), Registered<IProcessContextStore>(services));
        Assert.Equal(typeof(SqliteProcessTransactions), Registered<IProcessTransactions>(services));
        Assert.Equal(typeof(SqliteProcessTransaction), Registered<ISqliteKaleidoTransaction>(services));
        Assert.Null(Registered<IKaleidoOutboxStore>(services));
        Assert.Null(Registered<IProcessIdempotencyStore>(services));
    }

    [Fact]
    public void AddSqliteOutboxProvider_RegistersTheOutboxStore_Only()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteOutboxProvider(Database);

        Assert.Equal(typeof(SqliteKaleidoOutboxStore), Registered<IKaleidoOutboxStore>(services));
        Assert.Null(Registered<IProcessContextStore>(services));
        Assert.Null(Registered<IProcessTransactions>(services));
    }

    [Fact]
    public void AddSqliteIdempotencyProvider_RegistersTheIdempotencyStore_Only()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteIdempotencyProvider(Database);

        Assert.Equal(typeof(SqliteProcessIdempotencyStore), Registered<IProcessIdempotencyStore>(services));
        Assert.Null(Registered<IProcessContextStore>(services));
        Assert.Null(Registered<IKaleidoOutboxStore>(services));
    }

    [Fact]
    public void TheThreeRegistrations_ShareOneDatabase()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteContextProvider(Database).AddSqliteOutboxProvider(Database).AddSqliteIdempotencyProvider(Database);

        Assert.Single(services, x => x.ServiceType == typeof(SqliteKaleidoContextDbContext));
    }

    [Fact]
    public void ARegistrationWithAnotherConnectionString_Fails()
    {
        var builder = CreateBuilder(new ServiceCollection()).AddSqliteContextProvider(Database);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddSqliteOutboxProvider("Data Source=other.db"));

        Assert.Equal(SqliteErrorCodes.ConnectionStringMismatch, exception.Code);
    }

    [Fact]
    public void AddSqliteContextProvider_WhenAnotherContextStoreIsRegistered_Fails()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IProcessContextStore>());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateBuilder(services).AddSqliteContextProvider(Database));

        Assert.Equal(ConfigurationErrorCodes.ConflictingStore, exception.Code);
    }

    [Fact]
    public void WhenResolved_TheStoresAndTransactionsShareTheRequestsDbContext_WithoutADatabaseHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-svc",
            ServiceVersion = "1",
            Assemblies = [typeof(SqliteKaleidoServiceCollectionExtensionsTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddEventing()
            .AddSqliteContextProvider(Database)
            .AddSqliteOutboxProvider(Database)
            .AddSqliteIdempotencyProvider(Database);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.Equal(Database, dbContext.Database.GetConnectionString());
        Assert.IsType<SqliteProcessContextStore>(scope.ServiceProvider.GetRequiredService<IProcessContextStore>());
        Assert.IsType<SqliteKaleidoOutboxStore>(scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>());
        Assert.IsType<SqliteProcessIdempotencyStore>(scope.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>());
        Assert.IsType<SqliteProcessTransactions>(scope.ServiceProvider.GetRequiredService<IProcessTransactions>());

        // The provider adds no database check of its own; the only check is Kaleido's outbox check (#552).
        var checks = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(["kaleido-outbox"], checks.Select(x => x.Name));
    }

    // #553: the control registry's store, in the registry host's own database (no process store with it).
    [Fact]
    public void AddSqliteRegistryProvider_RegistersTheRegistryStore_OnItsOwnDatabase()
    {
        var services = new ServiceCollection();

        CreateBuilder(services).AddSqliteRegistryProvider("Data Source=registry.db");

        Assert.Equal(typeof(SqliteKaleidoRegistryStore), Registered<IKaleidoRegistryStore>(services));
        Assert.Null(Registered<IProcessContextStore>(services));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SqliteKaleidoRegistryDbContext>();
        Assert.Equal("Data Source=registry.db", context.Database.GetConnectionString());
        Assert.IsType<SqliteKaleidoRegistryStore>(scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>());
    }

    [Theory]
    [InlineData("NotAnOption=true")]
    [InlineData("  ")]
    public void AddSqliteRegistryProvider_WithAnInvalidConnectionString_FailsBeforeRegisteringAnything(string connectionString)
    {
        var services = new ServiceCollection();

        Assert.ThrowsAny<Exception>(() => CreateBuilder(services).AddSqliteRegistryProvider(connectionString));

        Assert.Empty(services);
    }
}
