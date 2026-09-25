using Kaleido.Queryable.Metadata;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Registry;

public interface IQueryableRegistry
{
    IReadOnlyCollection<QueryableContextRegistryItem> Registrations { get; }

    QueryableContextRegistryItem? Find(string name);

    QueryableContextRegistryItem GetRegistration(string name);
}

internal sealed class QueryableRegistry : IQueryableRegistry
{
    private readonly IReadOnlyCollection<QueryableContextRegistryItem> _registrations;
    private readonly IReadOnlyDictionary<string, QueryableContextRegistryItem> _byName;

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

        _byName =
            _registrations.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);

        logger.LogInformation(
            "Queryable registry built with {ContextCount} contexts.",
            _registrations.Count);
    }

    public IReadOnlyCollection<QueryableContextRegistryItem> Registrations =>
        _registrations;

    public QueryableContextRegistryItem? Find(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _byName.TryGetValue(name, out var registration);
        return registration;
    }

    public QueryableContextRegistryItem GetRegistration(
        string name) =>
        Find(name)
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Queryable registry item '{name}' is not registered.");

    private static QueryableContextRegistryItem Project(
        QueryContextRegistration registration,
        IReadOnlyCollection<QueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(views);

        return Project(
            registration.Metadata,
            views.Select(x => x.Metadata));
    }

    private static QueryableContextRegistryItem Project(
        QueryContextMetadata metadata,
        IReadOnlyCollection<DelegatedQueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(views);

        return Project(
            metadata,
            views.Select(x => x.ViewMetadata));
    }

    private static QueryableContextRegistryItem Project(
        QueryContextMetadata metadata,
        IEnumerable<QueryViewMetadata> views)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(views);

        return new QueryableContextRegistryItem
        {
            Name = metadata.Name,
            Description = metadata.Description,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            Source = metadata.Source,
            Kind = metadata.Kind,
            Pageable = metadata.Pageable,
            Fields = metadata.Fields
                .Select(Project)
                .ToArray(),
            Views = views
                .Where(x => x.Visibility == QueryViewVisibility.Public)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Project)
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
        QueryViewMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new QueryableViewRegistryItem
        {
            Name = metadata.Name,
            Description = metadata.Description,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            Visibility = metadata.Visibility,
            Pageable = metadata.Pageable,
            Parameters = metadata.Parameters?
                .Select(Project)
                .ToArray()
                ?? [],
            OutputFields = metadata.OutputFields?
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
            DataType = metadata.DataType
        };
    }
}
