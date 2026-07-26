using System.Linq.Expressions;
using System.Reflection;

namespace Kaleido.Queryable.Metadata;

public sealed record QueryParameterMetadata(
    string Name,
    Type Type,
    bool Required,
    string? Description,
    object? DefaultValue);