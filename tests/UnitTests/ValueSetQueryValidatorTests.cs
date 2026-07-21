//using System;
//using System.Collections.Generic;
//using Kaleido;
//using Kaleido.Metadata;
//using Xunit;

//namespace Core.Tests
//{
//    public class RecordQueryValidatorTests
//    {
//        [Fact]
//        public void Validate_NullRequest_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, meta));
//        }

//        [Fact]
//        public void Validate_NamedQueryNotAllowed_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var req = new KaleidoQueryRequest("doesnotexist", null);
//            Assert.Throws<InvalidOperationException>(() => validator.Validate(req, meta));
//        }

//        [Fact]
//        public void Validate_FilterGroupEmpty_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", new[] { new RuntimeFieldMetadata("f", typeof(string), true, new[]{ FilterOperator.Eq }, false, null, Array.Empty<MatchMode>(), false) }, Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var group = new QueryFilterGroup(LogicalOperator.And, new List<IFilterExpression>());
//            var req = new KaleidoQueryRequest(null, new QueryBody(null, group, null, null));
//            Assert.Throws<InvalidOperationException>(() => validator.Validate(req, meta));
//        }

//        [Fact]
//        public void Validate_SearchMissingText_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", new[] { new RuntimeFieldMetadata("f", typeof(string), false, Array.Empty<FilterOperator>(), true, 1, new[]{ MatchMode.Contains }, false) }, Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var search = new QuerySearch("", MatchMode.Contains);
//            var req = new KaleidoQueryRequest(null, new QueryBody(search, null, null, null));
//            Assert.Throws<InvalidOperationException>(() => validator.Validate(req, meta));
//        }

//        [Fact]
//        public void Validate_SortFieldNotSortable_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", new[] { new RuntimeFieldMetadata("f", typeof(string), false, Array.Empty<FilterOperator>(), false, null, Array.Empty<MatchMode>(), false) }, Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var sort = new List<QuerySort>{ new QuerySort("f", SortDirection.Asc) };
//            var req = new KaleidoQueryRequest(null, new QueryBody(null, null, sort, null));
//            Assert.Throws<InvalidOperationException>(() => validator.Validate(req, meta));
//        }

//        [Fact]
//        public void Validate_PageWhenNotPageable_Throws()
//        {
//            var validator = new RecordQueryValidator();
//            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
//            var req = new KaleidoQueryRequest(null, new QueryBody(null, null, null, new QueryPage(1, null)));
//            Assert.Throws<InvalidOperationException>(() => validator.Validate(req, meta));
//        }

//        [Fact]
//        public void Validate_ValidRequest_DoesNotThrow()
//        {
//            var validator = new RecordQueryValidator();
//            var fields = new[] { new RuntimeFieldMetadata("f", typeof(string), true, new[]{ FilterOperator.Eq }, true, 1, new[]{ MatchMode.Contains }, true) };
//            var pageable = new RuntimePageableMetadata(10, 100);
//            var meta = new RuntimeRecordMetadata("n","v","s", fields, Array.Empty<RuntimeAllowedQueryMetadata>(), pageable);

//            var filter = new QueryFilter("f", FilterOperator.Eq, new List<object?>{ "v" });
//            var search = new QuerySearch("x", MatchMode.Contains, "f");
//            var sorts = new List<QuerySort>{ new QuerySort("f", SortDirection.Asc) };
//            var page = new QueryPage(5, null);
//            var req = new KaleidoQueryRequest(null, new QueryBody(search, filter, sorts, page));

//            validator.Validate(req, meta);
//        }
//    }
//}
