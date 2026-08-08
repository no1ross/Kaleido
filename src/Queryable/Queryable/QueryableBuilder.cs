using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

internal sealed class QueryableBuilder
    : IQueryableBuilder
{
    private readonly IKaleidoBuilder _builder;

    public QueryableBuilder(
        IKaleidoBuilder builder)
    {
        _builder = builder;
    }

    public IServiceCollection Services
        => _builder.Services;

    public IReadOnlyCollection<Assembly> Assemblies
        => _builder.Assemblies;
}