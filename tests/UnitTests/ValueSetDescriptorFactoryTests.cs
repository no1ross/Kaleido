//using System;
//using System.Collections.Generic;
//using Kaleido;
//using Kaleido.Metadata;
//using Xunit;

//namespace Core.Tests
//{
//    public class RecordDescriptorFactoryTests
//    {
//        [Fact]
//        public void Create_DataTypes_AreMapped()
//        {
//            var fields = new List<RuntimeFieldMetadata>
//            {
//                new RuntimeFieldMetadata("s", typeof(string), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("i", typeof(int), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("l", typeof(long), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("d", typeof(decimal), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("g", typeof(Guid), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("dt", typeof(DateTime), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("enum", typeof(MatchMode), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//                new RuntimeFieldMetadata("obj", typeof(object), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false),
//            };

//            var meta = new RuntimeRecordMetadata("n","v","s", fields.ToArray(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var factory = new RecordDescriptorFactory();
//            var desc = factory.Create(meta);

//            Assert.Equal("string", desc.Fields[0].DataType.Type);
//            Assert.Equal("integer", desc.Fields[1].DataType.Type);
//            Assert.Equal("integer", desc.Fields[2].DataType.Type);
//            Assert.Equal("number", desc.Fields[3].DataType.Type);
//            Assert.Equal("string", desc.Fields[4].DataType.Type);
//            Assert.Equal("string", desc.Fields[5].DataType.Type);
//            Assert.Equal("string", desc.Fields[6].DataType.Type);
//            Assert.Equal("object", desc.Fields[7].DataType.Type);
//        }
//    }
//}
