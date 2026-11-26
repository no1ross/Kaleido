using Kaleido.Queryable.Metadata;
using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Registry;

/// <summary>The Queryable registry: every query source with its views, as published to transports.</summary>
internal interface IQueryableRegistry
{
    /// <summary>The registered query sources (local and delegated), ordered by name.</summary>
    IReadOnlyCollection<QueryableSourceRegistryItem> Registrations { get; }

    IReadOnlyCollection<QueryableRegistryItem> Published { get; }
}

internal sealed partial class QueryableRegistry : IQueryableRegistry, IKaleidoLocalCapabilities
{

    public QueryableRegistry(
        IQuerySourceRegistry sourceRegistry,
        IQueryViewRegistry viewRegistry,
        KaleidoServiceOptions serviceOptions,
        ILogger<QueryableRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(sourceRegistry);
        ArgumentNullException.ThrowIfNull(viewRegistry);
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(logger);

        Registrations =
            sourceRegistry.Registrations
                .Select(source =>
                    Project(
                        source,
                        viewRegistry.Registrations
                            .Where(view => view.SourceType == source.SourceType)
                            .ToArray()))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        Published = [.. Registrations.Select(source => ToPublished(source, serviceOptions))];

        LogRegistryBuilt(logger, Registrations.Count);
    }

    public IReadOnlyCollection<QueryableSourceRegistryItem> Registrations { get; }

    public IReadOnlyCollection<QueryableRegistryItem> Published { get; }

    public KaleidoRegistry Capabilities() => new() { Queryables = Published };

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
                .ToArray(),
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
            Classifications = metadata.Classifications,
            IsFilterable = metadata.IsFilterable,
            FilterOperators = metadata.FilterOperators,
            IsSearchable = metadata.IsSearchable,
            SearchPriority = metadata.SearchPriority,
            MatchMode = metadata.MatchMode,
            IsSortable = metadata.IsSortable,
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
            Pageable = registration.Metadata.Pageable,
            Parameters = registration.Metadata.Parameters?
                .Select(Project)
                .ToArray()
                ?? [],
            OutputFields = registration.Metadata.OutputFields?
                .Select(Project)
                .ToArray()
                ?? [],
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
            Classifications = metadata.Classifications,
            Constraints = metadata.Constraints,
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
            DataType = metadata.DataType,
            Classifications = metadata.Classifications,
        };
    }

    private static QueryableRegistryItem ToPublished(QueryableSourceRegistryItem source, KaleidoServiceOptions service) => new()
    {
        ServiceName = service.ServiceName,
        ServiceVersion = service.ServiceVersion,
        Name = source.Name,
        Description = source.Description,
        DisplayName = source.DisplayName,
        Source = source.Source,
        Pageable = ToPublished(source.Pageable),
        Authorization = source.Authorization,
        Fields = [.. source.Fields.Select(field => new QueryableRegistryField
        {
            Name = field.Name,
            Description = field.Description,
            DataType = ToPublished(field.DataType),
            Classifications = field.Classifications,
            IsFilterable = field.IsFilterable,
            FilterOperators = [.. field.FilterOperators.Select(OperatorToken)],
            IsSearchable = field.IsSearchable,
            SearchPriority = field.SearchPriority,
            MatchMode = field.MatchMode is { } mode ? MatchToken(mode) : null,
            IsSortable = field.IsSortable,
        })],
        Parameters = [.. source.Parameters.Select(ToPublished)],
        OutputFields = [.. source.OutputFields.Select(ToPublished)],
        Views = [.. source.Views.Select(ToPublished)],
    };

    private static QueryableRegistryViewItem ToPublished(QueryableViewRegistryItem view) => new()
    {
        Name = view.Name,
        Description = view.Description,
        DisplayName = view.DisplayName,
        Pageable = ToPublished(view.Pageable),
        Authorization = view.Authorization,
        Parameters = [.. view.Parameters.Select(ToPublished)],
        OutputFields = [.. view.OutputFields.Select(ToPublished)],
    };

    private static QueryableRegistryParameter ToPublished(QueryableParameterDescriptor parameter) => new()
    {
        Name = parameter.Name,
        Description = parameter.Description,
        DataType = ToPublished(parameter.DataType),
        Classifications = parameter.Classifications,
        Constraints = [.. parameter.Constraints.Select(constraint => new ConstraintMetadata
        {
            Type = constraint.Type,
            Parameters = [.. constraint.Parameters.Select(value => new ConstraintParameterMetadata { Name = value.Name, Value = value.Value })],
        })],
    };

    private static QueryableRegistryOutputField ToPublished(QueryableOutputFieldDescriptor field) => new()
    {
        Name = field.Name,
        Description = field.Description,
        DataType = ToPublished(field.DataType),
        Classifications = field.Classifications,
    };

    private static QueryableRegistryPaging? ToPublished(PageableMetadata? paging) =>
        paging is null ? null : new(paging.DefaultSize, paging.MaxSize, paging.DefaultSortField);

    private static DataTypeMetadata ToPublished(DataTypeDescriptor descriptor) => new(
        descriptor.Type,
        descriptor.Format,
        descriptor.Nullable,
        descriptor.EnumValues?.Select(value => new EnumValueMetadata(value.Value, value.Name, value.Description)).ToArray(),
        descriptor.ItemType is { } item ? ToPublished(item) : null);

    private static string OperatorToken(FilterOperator operation) => operation switch
    {
        FilterOperator.Equals => "equals",
        FilterOperator.NotEquals => "notEquals",
        FilterOperator.GreaterThan => "greaterThan",
        FilterOperator.LessThan => "lessThan",
        FilterOperator.GreaterThanOrEqual => "greaterThanOrEqual",
        FilterOperator.LessThanOrEqual => "lessThanOrEqual",
        FilterOperator.Contains => "contains",
        FilterOperator.NotContains => "notContains",
        FilterOperator.StartsWith => "startsWith",
        FilterOperator.EndsWith => "endsWith",
        FilterOperator.In => "in",
        FilterOperator.NotIn => "notIn",
        FilterOperator.Between => "between",
        FilterOperator.NotBetween => "notBetween",
        FilterOperator.IsNull => "isNull",
        FilterOperator.IsNotNull => "isNotNull",
        FilterOperator.IsTrue => "isTrue",
        FilterOperator.IsFalse => "isFalse",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static string MatchToken(MatchMode mode) => mode switch
    {
        MatchMode.Exact => "exact",
        MatchMode.StartsWith => "startsWith",
        MatchMode.EndsWith => "endsWith",
        MatchMode.Contains => "contains",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Queryable registry built with {SourceCount} sources.")]
    private static partial void LogRegistryBuilt(ILogger logger, int sourceCount);
}
