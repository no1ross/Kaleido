using Kaleido.Metadata;
using Kaleido.Samples.Shared;
using System.Reflection;

namespace Kaleido.UnitTests.Metadata
{
    public class RecordMetadataBuilderTests
    {
        [Fact]
        public void Build_Generic_Should_Return_Metadata()
        {
            var result =
                RecordMetadataBuilder.Build<SampleKaleidoRecord>();

            Assert.NotNull(result);

            Assert.Equal(
                "functional-records",
                result.Name);
        }

        [Fact]
        public void Build_Should_Read_Record_Attribute()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            Assert.Equal(
                "functional-records",
                result.Name);

            Assert.Equal(
                "1.0.0",
                result.Version);

            Assert.Equal(
                "CSV Functional Test Data",
                result.Source);
        }

        [Fact]
        public void Build_Should_Throw_When_Record_Attribute_Is_Missing()
        {
            Assert.Throws<InvalidOperationException>(
                () => RecordMetadataBuilder.Build(
                    typeof(NoRecordAttributeRecord)));
        }

        [Fact]
        public void Build_Should_Read_Pageable_Attribute()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            Assert.NotNull(
                result.Pageable);

            Assert.Equal(
                25,
                result.Pageable!.DefaultSize);

            Assert.Equal(
                500,
                result.Pageable.MaxSize);
        }

        [Fact]
        public void Build_Should_Read_Allowed_Queries()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            Assert.Equal(
                4,
                result.AllowedQueries.Count);

            Assert.Contains(
                result.AllowedQueries,
                x => x.Name == "active-records");

            Assert.Contains(
                result.AllowedQueries,
                x => x.Name == "records-by-category");

            Assert.Contains(
                result.AllowedQueries,
                x => x.Name == "high-amount-records");

            Assert.Contains(
                result.AllowedQueries,
                x => x.Name == "effective-on");
        }

        [Fact]
        public void Build_Should_Create_Field_Metadata_For_All_Public_Properties()
        {
            var expected =
                typeof(SampleKaleidoRecord)
                    .GetProperties(
                        BindingFlags.Public |
                        BindingFlags.Instance)
                    .Length;

            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            Assert.Equal(
                expected,
                result.Fields.Count);
        }

        [Fact]
        public void Build_Should_Read_Filterable_Field()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            var field =
                result.Fields.Single(
                    x => x.Name == nameof(SampleKaleidoRecord.Id));

            Assert.True(
                field.IsFilterable);

            Assert.Contains(
                FilterOperator.Eq,
                field.FilterOperators);

            Assert.Contains(
                FilterOperator.Gt,
                field.FilterOperators);
        }

        [Fact]
        public void Build_Should_Read_Searchable_Field()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            var field =
                result.Fields.Single(
                    x => x.Name == nameof(SampleKaleidoRecord.Code));

            Assert.True(
                field.IsSearchable);

            Assert.Equal(
                2,
                field.SearchPriority);

            Assert.Contains(
                MatchMode.Exact,
                field.MatchModes);

            Assert.Contains(
                MatchMode.Contains,
                field.MatchModes);
        }

        [Fact]
        public void Build_Should_Read_Sortable_Field()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            var field =
                result.Fields.Single(
                    x => x.Name == nameof(SampleKaleidoRecord.Amount));

            Assert.True(
                field.IsSortable);
        }

        [Fact]
        public void Build_Should_Mark_Field_As_Not_Searchable_When_Attribute_Is_Missing()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            var field =
                result.Fields.Single(
                    x => x.Name == nameof(SampleKaleidoRecord.IsActive));

            Assert.False(
                field.IsSearchable);

            Assert.Null(
                field.SearchPriority);

            Assert.Empty(
                field.MatchModes);
        }

        [Fact]
        public void Build_Should_Preserve_Property_Type()
        {
            var result =
                RecordMetadataBuilder.Build(
                    typeof(SampleKaleidoRecord));

            var field =
                result.Fields.Single(
                    x => x.Name == nameof(SampleKaleidoRecord.Status));

            Assert.Equal(
                typeof(RecordStatus),
                field.FieldType);
        }

        private sealed class NoRecordAttributeRecord
        {
        }
    }
}
