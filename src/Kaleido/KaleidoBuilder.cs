using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido;

/// <summary>
/// The builder returned by <c>AddKaleido</c>: the shared registration state that Kaleido
/// subsystems and providers extend through extension methods (e.g. <c>AddHttp</c>,
/// <c>AddEventPublisher</c>).
/// </summary>
public interface IKaleidoBuilder
{
    /// <summary>
    /// The application DI service collection. Used by subsystem builders to register their services.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// The set of assemblies registered via KaleidoServiceOptions.Assemblies.
    /// Consumed by Process and Queryable subsystem builders for step/context type scanning.
    /// </summary>
    IReadOnlyCollection<Assembly> Assemblies { get; }

    /// <summary>
    /// The application configuration root. Passed once to <c>AddKaleido(IConfiguration)</c>
    /// and flows through all subsystem builders automatically. Subsystem registration methods
    /// read from this rather than requiring a parameter on every chained call.
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>
    /// The validated service-level identity and options. Set by <c>AddKaleido()</c> and
    /// available to all subsystem builders (Process, Queryable, Registry) without any DI lookup.
    /// </summary>
    KaleidoServiceOptions ServiceOptions { get; }
}

internal sealed class KaleidoBuilder : IKaleidoBuilder
{
    private readonly Dictionary<string, Assembly> _assemblies = [];

    public KaleidoBuilder(IServiceCollection services, IConfiguration configuration, KaleidoServiceOptions serviceOptions)
    {
        this.Services = services;
        this.Configuration = configuration;
        this.ServiceOptions = serviceOptions;

        foreach (var assembly in serviceOptions.Assemblies
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.MissingAssembly,
                "KaleidoServiceOptions.Assemblies must contain at least one assembly."))
        {
            AddAssembly(assembly);
        }
    }

    public IServiceCollection Services { get; }

    public IConfiguration Configuration { get; }

    public KaleidoServiceOptions ServiceOptions { get; }

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.Values;

    internal bool AddAssembly(Assembly assembly)
    {
        return _assemblies.TryAdd(assembly.FullName ?? assembly.GetName().Name ?? string.Empty, assembly);
    }
}
