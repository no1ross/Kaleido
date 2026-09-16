using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

/// <summary>
/// Builder returned by <c>AddQueryable()</c>. Use to chain queryable-specific registrations
/// such as <c>AddQueryableAspNetCore()</c>.
/// </summary>
public interface IQueryableBuilder
    : IKaleidoBuilder
{
}