using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Records;

internal static class QueryableRegistryProjection
{
    public static QueryableContextRegistryItem Project(
        QueryContextRegistration registration,
        IReadOnlyCollection<QueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(views);

        return Project(
            registration.Metadata,
            views.Select(x => x.Metadata));
    }

    public static QueryableContextRegistryItem Project(
        QueryContextMetadata metadata,
        IReadOnlyCollection<DelegatedQueryViewRegistration> views)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(views);

        return Project(
            metadata,
            views.Select(x => x.ViewMetadata));
    }

    public static QueryableContextRegistryItem Project(
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
                .OrderBy(x => x.Name)
                .Select(Project)
                .ToArray()
        };
    }

    public static QueryableFieldDescriptor Project(
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

    public static QueryableViewRegistryItem Project(
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

    public static QueryableParameterDescriptor Project(
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

    public static QueryableOutputFieldDescriptor Project(
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
