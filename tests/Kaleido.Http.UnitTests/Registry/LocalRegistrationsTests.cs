using Kaleido.Http.Registry;
using Kaleido.Queryable.Registry;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class LocalRegistrationsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoServiceOptions ServiceOptions = new() { ServiceName = "intake" };

    // No Process or Queryable registry at all: a host that only aggregates.
    private static LocalRegistrations CreateSut() =>
        new([], [], Mock.Of<IProcessorResponseFactory>(), ServiceOptions);

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

        return new LocalRegistrations([processorRegistry.Object], [queryableRegistry.Object], responses.Object, ServiceOptions);
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
    public void Queryables_AreThisServicesSources_InTheirPublishedShape()
    {
        var source = new QueryableSourceRegistryItem
        {
            SourceType = typeof(object),
            QueryContextType = typeof(object),
            ResultType = typeof(object),
            ParametersType = typeof(object),
            Name = "MemberSource",
            DisplayName = "Members",
        };

        var queryable = Assert.Single(CreateSut([], [source]).Queryables());

        Assert.Equal("MemberSource", queryable.Name);
        Assert.Equal("intake", queryable.ServiceName);
    }

    [Fact]
    public void WithNothingRegistered_BothAreEmpty()
    {
        var sut = CreateSut([], []);

        Assert.Empty(sut.Processes());
        Assert.Empty(sut.Queryables());
    }

    [Fact]
    public void AHostWithNoCapabilitiesOfItsOwn_LikeARouter_PublishesNothingLocally()
    {
        var sut = CreateSut();

        Assert.Empty(sut.Processes());
        Assert.Empty(sut.Queryables());
    }
}
