using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Process;

internal sealed class ProcessorBuilder
    : IProcessorBuilder
{
    private readonly IKaleidoBuilder _builder;

    public ProcessorBuilder(
        IKaleidoBuilder builder)
    {
        _builder = builder;
    }

    public IServiceCollection Services
        => _builder.Services;

    public IReadOnlyCollection<Assembly> Assemblies
        => _builder.Assemblies;
}