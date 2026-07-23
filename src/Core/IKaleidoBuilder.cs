using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

public interface IKaleidoBuilder
{
    IServiceCollection Services { get; }

    IReadOnlyCollection<Assembly> Assemblies { get; }
}
