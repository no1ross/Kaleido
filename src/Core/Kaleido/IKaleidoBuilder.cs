using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

public interface IKaleidoBuilder
{
    IServiceCollection Services { get; }

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
