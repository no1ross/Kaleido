using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

internal sealed class ParticipantBuilder
    : IParticipantBuilder
{
    private readonly IKaleidoBuilder _builder;

    public ParticipantBuilder(
        IKaleidoBuilder builder)
    {
        _builder = builder;
    }

    public IServiceCollection Services
        => _builder.Services;

    public IReadOnlyCollection<Assembly> Assemblies
        => _builder.Assemblies;
}