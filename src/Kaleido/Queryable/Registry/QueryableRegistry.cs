using Kaleido.Queryable.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Registry;

/// <summary>The Queryable registry: every query source with its views, as published to transports.</summary>
public interface IQueryableRegistry
{
    /// <summary>The registered query sources (local and delegated), ordered by name.</summary>
    IReadOnlyCollection<QueryableSourceRegistryItem> Registrations { get; }
}

internal sealed class QueryableRegistry : IQueryableRegistry
{
    private readonly IReadOnlyCollection<QueryableSourceRegistryItem> _registrations;

    public QueryableRegistry(
        IQuerySourceRegistry sourceRegistry,
        IQueryViewRegistry viewRegistry,
        ILogger<QueryableRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(sourceRegistry);
        ArgumentNullException.ThrowIfNull(viewRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _registrations =
            sourceRegistry.Registrations
                .Select(source =>
                    Project(
                        source,
                        viewRegistry.Registrations
                            .Where(view => view.SourceType == source.SourceType)
                            .ToArray()))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        logger.LogInformation(
            "Queryable registry built with {SourceCount} sources.",
            _registrations.Count);
    }

    public IReadOnlyCollection<QueryableSourceRegistryItem> Registrations =>
        _registrations;

    private static QueryableSourceRegistryItem Project(
        QuerySourceRegistration registration,
        IReadOnlyCollection<QueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(views);

        var metadata = registration.Metadata;

        return new QueryableSourceRegistryItem
        {
            SourceType = registration.SourceType,
            QueryContextType = registration.QueryContextType,
            ResultType = registration.ResultType,
            ParametersType = registration.ParametersType,
            Name = metadata.Name,
            Description = metadata.Description,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            Source = metadata.Source,
            Pageable = metadata.Pageable,
            Authorization = metadata.Authorization,
            Fields = metadata.Fields
                .Select(Project)
                .ToArray(),
            Parameters = metadata.Parameters
                .Select(Project)
                .ToArray(),
            OutputFields = metadata.OutputFields
                .Select(Project)
                .ToArray(),
            Views = views
                .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(view => Project(view, metadata.Authorization))
                .ToArray()
        };
    }

    private static QueryableFieldDescriptor Project(
        FieldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new QueryableFieldDescriptor
        {
            Name = metadata.Name,
            Description = metadata.Description,
            FieldType = metadata.FieldType,
            DataType = metadata.DataType,
            IsFilterable = metadata.IsFilterable,
            FilterOperators = metadata.FilterOperators,
            IsSearchable = metadata.IsSearchable,
            SearchPriority = metadata.SearchPriority,
            MatchMode = metadata.MatchMode,
            IsSortable = metadata.IsSortable
        };
    }

    private static QueryableViewRegistryItem Project(
        QueryViewRegistration registration,
        AuthorizationMetadata sourceAuthorization)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new QueryableViewRegistryItem
        {
            QueryViewType = registration.QueryViewType,
            ViewType = registration.ViewType,
            ViewParametersType = registration.ViewParametersType,
            Name = registration.Metadata.Name,
            Authorization = registration.Metadata.Authorization
                ?? sourceAuthorization,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Pageable = registration.Metadata.Pageable,
            Parameters = registration.Metadata.Parameters?
                .Select(Project)
                .ToArray()
                ?? [],
            OutputFields = registration.Metadata.OutputFields?
                .Select(Project)
                .ToArray()
                ?? []
        };
    }

    private static QueryableParameterDescriptor Project(
        QueryParameterMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new QueryableParameterDescriptor
        {
            Name = metadata.Name,
            Description = metadata.Description,
            FieldType = metadata.Type,
            DataType = metadata.DataType,
            Constraints = metadata.Constraints
        };
    }

    private static QueryableOutputFieldDescriptor Project(
        QueryOutputFieldMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new QueryableOutputFieldDescriptor
        {
            Name = metadata.Name,
            Description = metadata.Description,
            FieldType = metadata.Type,
            DataType = metadata.DataType
        };
    }
}
