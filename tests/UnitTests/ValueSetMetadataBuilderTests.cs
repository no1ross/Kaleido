//using System;
//using Kaleido;
//using Kaleido.Attributes;
//using Kaleido.Metadata;
//using Xunit;

//namespace Core.Tests
//{
//    public class RecordMetadataBuilderTests
//    {
//        [Fact]
//        public void Build_WithoutAttribute_Throws()
//        {
//            Assert.Throws<InvalidOperationException>(() => RecordMetadataBuilder.Build(typeof(NoAttributeRecord)));
//        }

//        [Fact]
//        public void Build_WithAttributes_BuildsMetadata()
//        {
//            var meta = RecordMetadataBuilder.Build(typeof(RecordWithAttributes));
//            Assert.Equal("myset", meta.Name);
//            Assert.Single(meta.Fields);
//            var f = meta.Fields[0];
//            Assert.True(f.IsFilterable);
//            Assert.True(f.IsSearchable);
//            Assert.True(f.IsSortable);
//        }

//        private class NoAttributeRecord { public string? X { get; set; } }

//        [KaleidoRecord("myset", "1", "s")]
//        [Pageable(10, 100)]
//        private class RecordWithAttributes
//        {
//            [Filterable(FilterOperator.Eq)]
//            [Searchable(1, MatchMode.Contains)]
//            [Sortable]
//            public string? Name { get; set; }
//        }
//    }
//}
