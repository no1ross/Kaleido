//using System.Text.Json.Nodes;
//using Kaleido;
//using Kaleido.Queryable.Metadata;
//using Kaleido.Queryable.Records;
//using Microsoft.OpenApi;

//namespace Kaleido.Queryable.OpenApi;

//public sealed class OpenApiSchemaFactory
//{
//    public static OpenApiSchema CreateRecordSchema(
//        RecordRegistration registration)
//    {
//        ArgumentNullException.ThrowIfNull(registration);

//        var schema =
//            new OpenApiSchema
//            {
//                Type = JsonSchemaType.Object,
//                Description = registration.Metadata.Description,
//                Properties =
//                    new Dictionary<string, IOpenApiSchema>(
//                        StringComparer.Ordinal)
//            };

//        foreach (var field in registration.Metadata.Fields)
//        {
//            schema.Properties.Add(
//                field.Name,
//                CreateFieldSchema(field));
//        }

//        return schema;
//    }

//    public static OpenApiSchema CreateNamedQuerySchema(
//        NamedQueryRegistration registration)
//    {
//        ArgumentNullException.ThrowIfNull(registration);

//        var schema =
//            new OpenApiSchema
//            {
//                Type = JsonSchemaType.Object,
//                Description = registration.Metadata.Description,
//                Properties =
//                    new Dictionary<string, IOpenApiSchema>(
//                        StringComparer.Ordinal)
//            };

//        if (registration.Metadata.Parameters is null ||
//            registration.Metadata.Parameters.Count == 0)
//        {
//            return schema;
//        }

//        var required =
//            new HashSet<string>(
//                StringComparer.Ordinal);

//        foreach (var parameter in registration.Metadata.Parameters)
//        {
//            schema.Properties.Add(
//                parameter.Name,
//                CreateParameterSchema(parameter));

//            if (parameter.Required)
//            {
//                required.Add(parameter.Name);
//            }
//        }

//        if (required.Count > 0)
//        {
//            schema.Required = required;
//        }

//        return schema;
//    }

//    private static OpenApiSchema CreateFieldSchema(
//        FieldMetadata field)
//    {
//        var descriptor =
//            DataTypeMapper.GetDescriptor(
//                field.FieldType);

//        return CreateSchema(
//            descriptor);
//    }

//    private static OpenApiSchema CreateParameterSchema(
//        QueryParameterMetadata parameter)
//    {
//        var descriptor =
//            DataTypeMapper.GetDescriptor(
//                parameter.Type);

//        var schema =
//            CreateSchema(
//                descriptor);

//        schema.Description =
//            parameter.Description;

//        return schema;
//    }

//    private static OpenApiSchema CreateSchema(
//        DataTypeDescriptor descriptor)
//    {
//        ArgumentNullException.ThrowIfNull(descriptor);

//        var schema =
//            new OpenApiSchema
//            {
//                Type =
//                    MapJsonSchemaType(
//                        descriptor.Type),

//                Format =
//                    descriptor.Format
//            };

//        if (descriptor.Nullable)
//        {
//            schema.Type |= JsonSchemaType.Null;
//        }

//        if (descriptor.ItemType is not null)
//        {
//            schema.Items =
//                CreateSchema(
//                    descriptor.ItemType);
//        }

//        if (descriptor.EnumValues is not null &&
//            descriptor.EnumValues.Count > 0)
//        {
//            schema.Enum =
//                descriptor.EnumValues
//                    .Select(value => JsonValue.Create(value))
//                    .Where(value => value is not null)
//                    .Cast<JsonNode>()
//                    .ToList();
//        }

//        return schema;
//    }

//    private static JsonSchemaType MapJsonSchemaType(
//        string type)
//    {
//        return type switch
//        {
//            "string" =>
//                JsonSchemaType.String,

//            "integer" =>
//                JsonSchemaType.Integer,

//            "number" =>
//                JsonSchemaType.Number,

//            "boolean" =>
//                JsonSchemaType.Boolean,

//            "array" =>
//                JsonSchemaType.Array,

//            "object" =>
//                JsonSchemaType.Object,

//            _ =>
//                JsonSchemaType.Object
//        };
//    }
//}