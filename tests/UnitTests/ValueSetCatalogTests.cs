using System;
using System.Collections.Generic;
using Kaleido;
using Kaleido.Metadata;
using Kaleido.Registry;
using Xunit;

namespace Core.Tests
{
    public class RecordCatalogTests
    {
        [Fact]
        public void GetAll_ReturnsDescriptors_ForAllRegistrations()
        {
            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
            var reg1 = new RecordRegistration("a", typeof(object), meta);
            var reg2 = new RecordRegistration("b", typeof(string), meta);

            var registry = new TestRegistry(new[] { reg1, reg2 });
            var descriptors = new RecordDescriptorFactory();
            var dispatcher = new TestDispatcher();
            var catalog = new KaleidoCatalog(registry, dispatcher, descriptors);

            var all = catalog.GetAll();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void Get_ReturnsDescriptorOrNull()
        {
            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
            var reg = new RecordRegistration("x", typeof(object), meta);
            var registry = new TestRegistry(new[] { reg });
            var descriptors = new RecordDescriptorFactory();
            var dispatcher = new TestDispatcher();
            var catalog = new KaleidoCatalog(registry, dispatcher, descriptors);

            Assert.NotNull(catalog.Get("x"));
            Assert.Null(catalog.Get("nope"));
        }

        [Fact]
        public async Task QueryAsync_DelegatesToDispatcher()
        {
            var meta = new RuntimeRecordMetadata("n","v","s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null);
            var reg = new RecordRegistration("x", typeof(object), meta);
            var registry = new TestRegistry(new[] { reg });
            var descriptors = new RecordDescriptorFactory();
            var dispatcher = new TestDispatcher();
            var catalog = new KaleidoCatalog(registry, dispatcher, descriptors);

            var resp = await catalog.QueryAsync("x", new KaleidoQueryRequest(null, null));
            Assert.Equal(0, resp.TotalCount);
        }

        private class TestRegistry : IRecordRegistry
        {
            private readonly RecordRegistration[] _regs;
            public TestRegistry(RecordRegistration[] regs) => _regs = regs;
            public IReadOnlyCollection<RecordRegistration> Registrations => _regs;
            public RecordRegistration? Find(string recordKey) => Array.Find(_regs, r => r.Name == recordKey);

            public RecordRegistration? Find(Type recordType)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyCollection<RecordRegistration> GetAll()
            {
                throw new NotImplementedException();
            }

            public RecordRegistration GetRequired(string name)
            {
                throw new NotImplementedException();
            }

            public RecordRegistration GetRequired(Type recordType)
            {
                throw new NotImplementedException();
            }
        }

        private class TestDispatcher : IRecordDispatcher
        {
            public Task<KaleidoQueryResponse> QueryAsync(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new KaleidoQueryResponse(new RecordDescriptor("n","v","s",Array.Empty<FieldDescriptor>(),Array.Empty<AllowedQueryDescriptor>(),null), 0, Array.Empty<object>()));
            }

            public Task<KaleidoQueryResponse<TRecord>> QueryAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class
            {
                throw new NotImplementedException();
            }
        }
    }
}
