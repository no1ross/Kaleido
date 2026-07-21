//using Kaleido;
//using Kaleido.Attributes;
//using Xunit;

//namespace Core.Tests
//{
//    public class RecordMetadataCatalogTests
//    {
//        [Fact]
//        public void GetMetadata_Caches_ByType()
//        {
//            var catalog = new RecordMetadataCatalog();
//            var a = catalog.GetMetadata(typeof(TestRecord));
//            var b = catalog.GetMetadata(typeof(TestRecord));
//            Assert.Same(a, b);
//        }

//        [KaleidoRecord("test", "1", "s")]
//        private class TestRecord
//        {
//            public string? Name { get; set; }
//        }
//    }
//}
