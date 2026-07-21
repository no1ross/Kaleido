using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kaleido.Metadata;
using Kaleido;
using Kaleido.Registry;

namespace Core.Tests
{
    public class RecordDispatcherTests
    {
        [Fact]
        public async Task QueryAsync_InvokesEngine_And_ReturnsResponse()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IRecordDescriptorFactory, RecordDescriptorFactory>();
            services.AddSingleton<IRecordRegistry>(new TestRegistry());
            services.AddScoped<IRecordQueryEngine<SampleRecord>, SampleEngine>();

            var provider = services.BuildServiceProvider();
            var providerFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var registry = provider.GetRequiredService<IRecordRegistry>();
            var descriptors = provider.GetRequiredService<IRecordDescriptorFactory>();
            var dispatcher = new RecordDispatcher(providerFactory, registry, descriptors);

            var response = await dispatcher.QueryAsync("sample", new KaleidoQueryRequest(null, null));

            Assert.NotNull(response);
            Assert.Equal(1, response.TotalCount);
            Assert.Single(response.Items);
            Assert.Equal("hello", ((SampleRecord)response.Items[0]).Text);
        }

        private sealed class TestRegistry : IRecordRegistry
        {
            private readonly RuntimeRecordMetadata _meta = new("sample","1","s",Array.Empty<RuntimeFieldMetadata>(),Array.Empty<RuntimeAllowedQueryMetadata>(),null);
            public IReadOnlyCollection<RecordRegistration> Registrations => new[] { new RecordRegistration("sample", typeof(SampleRecord), _meta) };
            public RecordRegistration? Find(string recordKey) => recordKey == "sample" ? new RecordRegistration("sample", typeof(SampleRecord), _meta) : null;

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

        private class SampleEngine : IRecordQueryEngine<SampleRecord>
        {
            public Task<QueryResult<SampleRecord>> ExecuteTypedAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default)
            {
                var items = new List<SampleRecord> { new SampleRecord { Text = "hello" } };
                var result = new QueryResult<SampleRecord>(items, 1, new RuntimeRecordMetadata("sample", "1", "s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null));
                return Task.FromResult(result);
            }

            Task<IRecordQueryResult> IRecordQueryEngine.ExecuteAsync(KaleidoQueryRequest request, CancellationToken cancellationToken)
            {
                var items = new List<SampleRecord> { new SampleRecord { Text = "hello" } };
                IRecordQueryResult result = new QueryResult<SampleRecord>(items, 1, new RuntimeRecordMetadata("sample", "1", "s", Array.Empty<RuntimeFieldMetadata>(), Array.Empty<RuntimeAllowedQueryMetadata>(), null));
                return Task.FromResult(result);
            }
        }

        private class SampleRecord { public string? Text { get; set; } }
    }
}
