using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableBuilderTests
{
    [Fact]
    public void Constructor_ExposesUnderlyingBuilderServicesAndAssemblies()
    {
        var services = new ServiceCollection();
        var assemblies = new[] { typeof(QueryableBuilderTests).Assembly };
        var builder = new TestKaleidoBuilder(services, assemblies);

        var queryableBuilder = new QueryableBuilder(builder);

        Assert.Same(services, queryableBuilder.Services);
        Assert.Same(assemblies, queryableBuilder.Assemblies);
    }

    private sealed class TestKaleidoBuilder : IKaleidoBuilder
    {
        public TestKaleidoBuilder(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
        {
            Services = services;
            Assemblies = assemblies;
        }

        public IServiceCollection Services { get; }
        public IReadOnlyCollection<Assembly> Assemblies { get; }
    }
}
