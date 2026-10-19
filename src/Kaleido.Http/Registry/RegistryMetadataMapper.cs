using Kaleido.Processor.Registry;

namespace Kaleido.Http.Registry;

// Projects core's registry metadata to the wire contracts in Kaleido.Http.Abstractions, which own
// their own types so the core registry descriptors stay internal (#356). The JSON is unchanged.
internal static class RegistryMetadataMapper
{
    public static DataTypeMetadata ToWire(this DataTypeDescriptor dataType) =>
        new(
            dataType.Type,
            dataType.Format,
            dataType.Nullable,
            dataType.EnumValues?.Select(v => new EnumValueMetadata(v.Value, v.Name, v.Description)).ToArray(),
            dataType.ItemType?.ToWire());

    public static IReadOnlyCollection<ConstraintMetadata> ToWire(this IReadOnlyCollection<ConstraintContract> constraints) =>
        [.. constraints.Select(c => new ConstraintMetadata
        {
            Type = c.Type,
            Parameters = [.. c.Parameters.Select(p => new ConstraintParameterMetadata { Name = p.Name, Value = p.Value })],
        })];

    public static ProcessFieldLookup? ToWire(this LookupMetadata? lookup) =>
        lookup is null
            ? null
            : new ProcessFieldLookup
            {
                Service = lookup.Service,
                Source = lookup.Source,
                View = lookup.View,
                Field = lookup.Field,
                Group = lookup.Group,
                Display = lookup.Display,
            };

    public static QueryablePagingMetadata? ToWire(this PageableMetadata? pageable) =>
        pageable is null ? null : new(pageable.DefaultSize, pageable.MaxSize, pageable.DefaultSortField);
}
