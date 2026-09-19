using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

internal sealed class KaleidoBuilder : IKaleidoBuilder
{
    public KaleidoBuilder(IServiceCollection services, IConfiguration configuration, KaleidoServiceOptions serviceOptions)
    {
        Services = services;
        Configuration = configuration;
        ServiceOptions = serviceOptions;
    }

    public IServiceCollection Services { get; }

    public IConfiguration Configuration { get; }

    public KaleidoServiceOptions ServiceOptions { get; }

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.Values;

    private readonly Dictionary<string, Assembly> _assemblies = [];

    internal bool AddAssembly(Assembly assembly)
    {
        return _assemblies.TryAdd(assembly.FullName ?? assembly.GetName().Name!, assembly);
    }
}
