using Kaleido.IntegrationTests.TestArtifacts;
using Kaleido.Queryable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests;

public sealed class DbContextDependencyTests
{
    [Fact]
    public void AddQueryable_WithDbContextDependencyBeforeAddKaleido_BuildsServiceProviderSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Register DbContext BEFORE AddKaleido - this should work
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));

        // Act
        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
        {
            o.ServiceName = "test-integration";
            o.Assemblies = new[] { typeof(DbContextDependencyTests).Assembly };
            o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.IntegrationTests.TestArtifacts") ?? false;
        });

        // This should NOT throw - the service provider should be able to construct
        // the QueryContextSource with its DbContext dependency
        using var provider = services.BuildServiceProvider();

        // Assert
        var dbContext = provider.GetService<TestDbContext>();
        Assert.NotNull(dbContext);

        var source = provider.GetService<IQueryContextSource<TestQueryContext>>();
        Assert.NotNull(source);
    }
}
