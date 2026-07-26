using Kaleido.FunctionalTests.Infrastructure;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Kaleido.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.FunctionalTests.Fixtures;

public sealed class FunctionalFixture : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public FunctionalFixture()
    {
        var services = new ServiceCollection();

        services.AddSingleton<SampleKaleidoCsvData>();

        services.AddKaleido()
            .AddAssembly(typeof(SampleKaleidoRecord).Assembly)
            .AddAssembly(typeof(SampleKaleidoRecordSource).Assembly)
            .AddAssembly(typeof(ActiveRecordsQuery).Assembly)
            .AddQueryable(options =>
            {
                options.ValidateRegistrations = true;
            });

        _serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

        using var scope =
            _serviceProvider.CreateScope();

        _ = scope.ServiceProvider
            .GetRequiredService<IQueryableCatalog>();

        _ = scope.ServiceProvider
            .GetRequiredService<IRecordDispatcher>();

        _ = scope.ServiceProvider
            .GetRequiredService<IRecordRegistry>();

        _ = scope.ServiceProvider
            .GetRequiredService<IRecordSource<SampleKaleidoRecord>>();

        _ = scope.ServiceProvider
            .GetServices<IRecordNamedQuery<SampleKaleidoRecord>>();

        _ = scope.ServiceProvider
            .GetRequiredService<IRecordQueryEngine<SampleKaleidoRecord>>();
    }

    public IServiceScope CreateScope()
        => _serviceProvider.CreateScope();

    public T GetRequiredService<T>()
        where T : notnull
        => _serviceProvider.GetRequiredService<T>();

    public void Dispose()
        => _serviceProvider.Dispose();
}