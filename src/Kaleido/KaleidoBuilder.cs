using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido;

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

[ExcludeFromCodeCoverage]
internal sealed class KaleidoBuilder : IKaleidoBuilder
{
    private readonly IServiceCollection services;
    private readonly IConfiguration configuration;
    private readonly KaleidoServiceOptions serviceOptions;
    private readonly Dictionary<string, Assembly> _assemblies = [];

    public KaleidoBuilder(IServiceCollection services, IConfiguration configuration, KaleidoServiceOptions serviceOptions)
    {
        this.services = services;
        this.configuration = configuration;
        this.serviceOptions = serviceOptions;

        // Set assemblies from options, defaulting to calling and entry assemblies if not specified
        var assemblies = serviceOptions.Assemblies;
        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = new Assembly?[] { Assembly.GetCallingAssembly(), Assembly.GetEntryAssembly() }
                .OfType<Assembly>()
                .ToArray();
        }

        foreach (var assembly in assemblies)
        {
            AddAssembly(assembly);
        }
    }

    public IServiceCollection Services => services;

    public IConfiguration Configuration => configuration;

    public KaleidoServiceOptions ServiceOptions => serviceOptions;

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.Values;

    internal bool AddAssembly(Assembly assembly)
    {
        return _assemblies.TryAdd(assembly.FullName ?? assembly.GetName().Name ?? string.Empty, assembly);
    }
}
