using Kaleido.Processor.Registry;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryMetadataMapperTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void ToWire_DataType_CopiesNestedItemTypesAndEnumValues()
    {
        var dataType = new DataTypeDescriptor(
            "array",
            ItemType: new DataTypeDescriptor("string", "enum", Nullable: true, EnumValues: [new EnumValueDescriptor(2, "High", "Urgent")]));

        var wire = dataType.ToWire();

        Assert.Equal("array", wire.Type);
        Assert.Null(wire.EnumValues);
        var item = Assert.IsType<DataTypeMetadata>(wire.ItemType);
        Assert.Equal(("string", "enum", true), (item.Type, item.Format, item.Nullable));
        Assert.Equal(new EnumValueMetadata(2, "High", "Urgent"), Assert.Single(item.EnumValues!));
    }

    [Fact]
    public void ToWire_Constraints_CopiesTypesAndParameters()
    {
        IReadOnlyCollection<ConstraintContract> constraints =
        [
            new() { Type = "Required" },
            new() { Type = "StringLength", Parameters = [new() { Name = "MaximumLength", Value = 10 }] },
        ];

        var wire = constraints.ToWire();

        Assert.Collection(
            wire,
            required => Assert.Equal(("Required", 0), (required.Type, required.Parameters.Count)),
            length =>
            {
                Assert.Equal("StringLength", length.Type);
                var parameter = Assert.Single(length.Parameters);
                Assert.Equal(("MaximumLength", (object?)10), (parameter.Name, parameter.Value));
            });
    }

    [Fact]
    public void ToWire_LookupAndPaging_AreNullWhenAbsent_AndCopiedWhenPresent()
    {
        Assert.Null(((LookupMetadata?)null).ToWire());
        Assert.Null(((PageableMetadata?)null).ToWire());

        var lookup = new LookupMetadata { Service = "member", Source = "Member", View = "Search", Field = "Id", Group = "g", Display = ["Name"] }.ToWire();
        Assert.NotNull(lookup);
        Assert.Equal(("member", "Member", "Search", "Id", "g"), (lookup.Service, lookup.Source, lookup.View, lookup.Field, lookup.Group));
        Assert.Equal(["Name"], lookup.Display);

        Assert.Equal(new Kaleido.Http.Queryable.QueryablePagingMetadata(5, 50, "Id"), new PageableMetadata(5, 50, "Id").ToWire());
    }
}
