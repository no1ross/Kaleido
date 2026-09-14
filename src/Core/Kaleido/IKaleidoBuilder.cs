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
}
