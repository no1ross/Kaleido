using Kaleido.Queryable.FunctionalTests.Fixtures;
using Kaleido.Queryable.OpenApi;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System.Text.Json;

namespace Kaleido.Queryable.AspNetCore.Tests.OpenApi;

public sealed class OpenApiSchemaFactoryTests
    : IClassFixture<QueryableFixture>
{
    private readonly QueryableFixture _fixture;

    public OpenApiSchemaFactoryTests(
        QueryableFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void CreateRecordSchema_Should_Map_All_Field_Metadata()
    {
        using var scope =
            _fixture.CreateScope();

        var registry =
            scope.ServiceProvider
                .GetRequiredService<IRecordRegistry>();

        var registration =
            registry.Registrations.Single(
                x => x.RecordType == typeof(SampleKaleidoRecord));

        var schema =
            OpenApiSchemaFactory.CreateRecordSchema(
                registration);

        Assert.Equal(
            JsonSchemaType.Object,
            schema.Type);

        Assert.Equal(
            registration.Metadata.Description,
            schema.Description);

        Assert.Equal(
            registration.Metadata.Fields.Count,
            schema.Properties.Count);

        foreach (var field in registration.Metadata.Fields)
        {
            Assert.True(
                schema.Properties.ContainsKey(field.Name),
                $"Missing schema property '{field.Name}'.");

            var property =
                schema.Properties[field.Name];

            var descriptor =
                DataTypeMapper.GetDescriptor(
                    field.FieldType);

            Assert.NotNull(
                property.Type);

            var actualType =
                property.Type!.Value;

            var expectedType =
                ExpectedJsonSchemaType(
                    descriptor.Type);

            Assert.True(
                (actualType & expectedType)
                == expectedType,
                $"Field '{field.Name}' does not contain expected schema type '{expectedType}'.");

            Assert.Equal(
                descriptor.Format,
                property.Format);

            if (descriptor.Nullable)
            {
                Assert.True(
                    (actualType & JsonSchemaType.Null)
                    == JsonSchemaType.Null,
                    $"Field '{field.Name}' should be nullable.");
            }
            else
            {
                Assert.False(
                    (actualType & JsonSchemaType.Null)
                    == JsonSchemaType.Null,
                    $"Field '{field.Name}' should not be nullable.");
            }

            if (descriptor.EnumValues is not null)
            {
                Assert.NotNull(
                    property.Enum);

                Assert.Equal(
                    descriptor.EnumValues.Count,
                    property.Enum.Count);

                foreach (var enumValue in descriptor.EnumValues)
                {
                    Assert.Contains(
                        property.Enum,
                        x =>
                        {
                            using var document =
                                JsonDocument.Parse(
                                    x.ToJsonString());

                            var root =
                                document.RootElement;

                            return root.GetProperty("Value").GetInt32() == enumValue.Value
                                && root.GetProperty("Name").GetString() == enumValue.Name
                                && root.GetProperty("Description").GetString() == enumValue.Description;
                        });
                }
            }

            if (descriptor.ItemType is not null)
            {
                Assert.NotNull(
                    property.Items);

                var expectedItemType =
                    ExpectedJsonSchemaType(
                        descriptor.ItemType.Type);

                Assert.NotNull(
                    property.Items.Type);

                var actualItemType =
                    property.Items.Type!.Value;

                Assert.True(
                    (actualItemType & expectedItemType)
                    == expectedItemType);

                Assert.Equal(
                    descriptor.ItemType.Format,
                    property.Items.Format);
            }
        }
    }

    [Fact]
    public void CreateNamedQuerySchema_Should_Map_All_Parameter_Metadata()
    {
        using var scope =
            _fixture.CreateScope();

        var registry =
            scope.ServiceProvider
                .GetRequiredService<IRecordRegistry>();

        var registration =
            registry.Registrations.Single(
                x => x.RecordType == typeof(SampleKaleidoRecord));

        foreach (var namedQuery in registration.NamedQueryTypes)
        {
            var schema =
                OpenApiSchemaFactory.CreateNamedQuerySchema(
                    namedQuery);

            Assert.Equal(
                JsonSchemaType.Object,
                schema.Type);

            Assert.Equal(
                namedQuery.Metadata.Description,
                schema.Description);

            var parameters =
                namedQuery.Metadata.Parameters
                ?? [];

            Assert.Equal(
                parameters.Count,
                schema.Properties.Count);

            foreach (var parameter in parameters)
            {
                Assert.True(
                    schema.Properties.ContainsKey(
                        parameter.Name));

                var property =
                    schema.Properties[
                        parameter.Name];

                var descriptor =
                    DataTypeMapper.GetDescriptor(
                        parameter.Type);

                Assert.Equal(
                    ExpectedJsonSchemaType(
                        descriptor.Type),
                    property.Type);

                Assert.Equal(
                    descriptor.Format,
                    property.Format);

                Assert.Equal(
                    parameter.Description,
                    property.Description);

                if (parameter.Required)
                {
                    Assert.NotNull(
                        schema.Required);

                    Assert.Contains(
                        parameter.Name,
                        schema.Required);
                }
            }
        }
    }

    private static JsonSchemaType ExpectedJsonSchemaType(
        string type)
    {
        return type switch
        {
            "string" => JsonSchemaType.String,
            "integer" => JsonSchemaType.Integer,
            "number" => JsonSchemaType.Number,
            "boolean" => JsonSchemaType.Boolean,
            "array" => JsonSchemaType.Array,
            "object" => JsonSchemaType.Object,
            _ => JsonSchemaType.Object
        };
    }
}