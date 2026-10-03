using Kaleido.Queryable.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Registry;

public interface IQueryableRegistry
{
    IReadOnlyCollection<QueryableContextRegistryItem> Registrations { get; }
}

internal sealed class QueryableRegistry : IQueryableRegistry
{
    private readonly IReadOnlyCollection<QueryableContextRegistryItem> _registrations;

    public QueryableRegistry(
        IQueryContextRegistry contextRegistry,
        IQueryViewRegistry viewRegistry,
        IDelegatedQueryViewRegistry delegatedViewRegistry,
        ILogger<QueryableRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(contextRegistry);
        ArgumentNullException.ThrowIfNull(viewRegistry);
        ArgumentNullException.ThrowIfNull(delegatedViewRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        var localRegistrations =
            contextRegistry.Registrations
                .Select(context =>
                    Project(
                        context,
                        viewRegistry.Registrations
                            .Where(view => view.QueryContextType == context.ContextType)
                            .ToArray()));

        var delegatedRegistrations =
            delegatedViewRegistry.Registrations
                .GroupBy(x => x.QueryMetadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    Project(
                        group.First().QueryMetadata,
                        group.ToArray()));

        _registrations =
            localRegistrations
                .Concat(delegatedRegistrations)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        logger.LogInformation(
            "Queryable registry built with {ContextCount} contexts.",
            _registrations.Count);
    }

    public IReadOnlyCollection<QueryableContextRegistryItem> Registrations =>
        _registrations;

    private static QueryableContextRegistryItem Project(
        QueryContextRegistration registration,
        IReadOnlyCollection<QueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(views);

        return new QueryableContextRegistryItem
        {
            ContextType = registration.ContextType,
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Source = registration.Metadata.Source,
            Kind = registration.Metadata.Kind,
            Pageable = registration.Metadata.Pageable,
            Authorization = registration.Metadata.Authorization,
            Fields = registration.Metadata.Fields
                .Select(Project)
                .ToArray(),
            Views = views
                .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(view => Project(view, registration.Metadata.Authorization))
                .ToArray()
        };
    }

    private static QueryableContextRegistryItem Project(
        QueryContextMetadata metadata,
        IReadOnlyCollection<DelegatedQueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(views);

        return new QueryableContextRegistryItem
        {
            ContextType = views.First().QueryContextType,
            Name = metadata.Name,
            Description = metadata.Description,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            Source = metadata.Source,
            Kind = metadata.Kind,
            Pageable = metadata.Pageable,
            Authorization = metadata.Authorization,
            Fields = metadata.Fields
                .Select(Project)
                .ToArray(),
            Views = views
                .OrderBy(x => x.ViewMetadata.Name, StringComparer.OrdinalIgnoreCase)
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
        AuthorizationMetadata? contextAuthorization)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new QueryableViewRegistryItem
        {
            QueryViewType = registration.QueryViewType,
            ViewType = registration.ViewType,
            ViewParametersType = registration.ViewParametersType,
            Name = registration.Metadata.Name,
            Authorization = registration.Metadata.Authorization
                ?? contextAuthorization
                ?? AuthorizationMetadata.Unspecified,
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

    private static QueryableViewRegistryItem Project(
        DelegatedQueryViewRegistration registration,
        AuthorizationMetadata? contextAuthorization)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new QueryableViewRegistryItem
        {
            QueryViewType = registration.QueryViewType,
            ViewType = registration.ViewType,
            ViewParametersType = registration.ViewParametersType,
            Name = registration.ViewMetadata.Name,
            Authorization = registration.ViewMetadata.Authorization
                ?? contextAuthorization
                ?? AuthorizationMetadata.Unspecified,
            Description = registration.ViewMetadata.Description,
            DisplayName = registration.ViewMetadata.DisplayName,
            Version = registration.ViewMetadata.Version,
            Pageable = registration.ViewMetadata.Pageable,
            Parameters = registration.ViewMetadata.Parameters?
                .Select(Project)
                .ToArray()
                ?? [],
            OutputFields = registration.ViewMetadata.OutputFields?
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
