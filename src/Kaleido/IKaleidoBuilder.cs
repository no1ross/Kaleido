using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
