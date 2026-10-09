using Kaleido.Http.Registry;
using Kaleido.Queryable.Registry;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class LocalRegistrationsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoServiceOptions ServiceOptions = new() { ServiceName = "intake" };

    private static LocalRegistrations CreateSut(
        IReadOnlyCollection<ProcessorRegistryItem> processors,
        IReadOnlyCollection<QueryableSourceRegistryItem> queryables)
    {
        var processorRegistry = new Mock<IProcessorRegistry>();
        processorRegistry.Setup(x => x.Registrations).Returns(processors);

        var queryableRegistry = new Mock<IQueryableRegistry>();
        queryableRegistry.Setup(x => x.Registrations).Returns(queryables);

        var responses = new Mock<IProcessorResponseFactory>();
        responses.Setup(x => x.CreateRegistryResponse(It.IsAny<ProcessorRegistryItem>(), ServiceOptions))
            .Returns((ProcessorRegistryItem _, KaleidoServiceOptions options) => new ProcessorRegistryResponse { Name = options.ServiceName, ServiceName = options.ServiceName });

        return new LocalRegistrations(processorRegistry.Object, queryableRegistry.Object, responses.Object, ServiceOptions);
    }

    [Fact]
    public void Processes_AreThisServicesProcessors_InTheirPublishedShape()
    {
        var processes = CreateSut([new ProcessorRegistryItem()], []).Processes().ToArray();

        var process = Assert.Single(processes);
        Assert.Equal("intake", process.Name);
        Assert.Equal("intake", process.ServiceName);
    }

    [Fact]
    public void WithNothingRegistered_BothAreEmpty()
    {
        var sut = CreateSut([], []);

        Assert.Empty(sut.Processes());
        Assert.Empty(sut.Queryables());
    }
}
