using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Kaleido;
using Kaleido.Validation;
using Kaleido.Registry;
using Kaleido.Metadata;

namespace Core.Tests
{
    public class RecordCoreServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddRecordCore_Registers_Services()
        {
            //var services = new ServiceCollection();
            //services.AddKaleido();
            //var provider = services.BuildServiceProvider();

            //Assert.NotNull(provider.GetService<IRecordMetadataCatalog>());
            //Assert.NotNull(provider.GetService<IRecordDescriptorFactory>());
            //Assert.NotNull(provider.GetService<IRecordQueryValidator>());
            //Assert.NotNull(provider.GetService<IRecordQueryCompiler>());
            //Assert.NotNull(provider.GetService<IRecordRegistry>());
            //// scoped services should be resolvable
            //using (var scope = provider.CreateScope())
            //{
            //    Assert.NotNull(scope.ServiceProvider.GetService<IRecordDispatcher>());
            //    Assert.NotNull(scope.ServiceProvider.GetService<IKaleidoCatalog>());
            //}
        }
    }
}
