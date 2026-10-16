using Kaleido.Processor.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Registry.UnitTests;

public sealed class ProcessorRegistryTests
    : SutFixture
{
    private static ProcessorRegistry CreateSut(
        bool isEntryProcessor = false,
        ILogger<ProcessorRegistry>? logger = null)
    {
        var stepRegistry = new Mock<IProcessorStepRegistry>();
        stepRegistry.SetupGet(x => x.Registrations).Returns([]);
        stepRegistry.SetupGet(x => x.InitialRegistrations).Returns([]);

        return CreateSut(
            Mock.Of<ITypeDescriber>(),
            Mock.Of<IConstraintMapper>(),
            new KaleidoServiceOptions { ServiceName = "intake", IsEntryProcessor = isEntryProcessor },
            stepRegistry.Object,
            logger ?? Mock.Of<ILogger<ProcessorRegistry>>());
    }

    private static ProcessorRegistry CreateSut(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        KaleidoServiceOptions serviceOptions,
        IProcessorStepRegistry stepRegistry,
        ILogger<ProcessorRegistry> logger) =>
        new(typeDescriber, constraintMapper, serviceOptions, stepRegistry, logger);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Registrations_DescribeTheProcessor(bool isEntryProcessor)
    {
        var registration = Assert.Single(CreateSut(isEntryProcessor).Registrations);

        Assert.Equal(isEntryProcessor, registration.IsEntryProcessor);
        Assert.Empty(registration.Steps);
        Assert.Empty(registration.InitialSteps);
    }

    [Fact]
    public void Constructor_LogsTheBuiltRegistryOnceAtInformation()
    {
        var logger = new Mock<ILogger<ProcessorRegistry>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger: logger.Object);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Information, logged.Arguments[0]);
        Assert.Equal("Process registry built for processor intake with 0 steps (0 initial).", logged.Arguments[2].ToString());
    }

    [Fact]
    public void Constructor_WhenADependencyIsNull_Throws()
    {
        var stepRegistry = Mock.Of<IProcessorStepRegistry>();
        var options = new KaleidoServiceOptions { ServiceName = "intake" };
        var logger = Mock.Of<ILogger<ProcessorRegistry>>();

        Assert.Equal("typeDescriber", Assert.Throws<ArgumentNullException>(() => CreateSut(null!, Mock.Of<IConstraintMapper>(), options, stepRegistry, logger)).ParamName);
        Assert.Equal("constraintMapper", Assert.Throws<ArgumentNullException>(() => CreateSut(Mock.Of<ITypeDescriber>(), null!, options, stepRegistry, logger)).ParamName);
        Assert.Equal("serviceOptions", Assert.Throws<ArgumentNullException>(() => CreateSut(Mock.Of<ITypeDescriber>(), Mock.Of<IConstraintMapper>(), null!, stepRegistry, logger)).ParamName);
        Assert.Equal("stepRegistry", Assert.Throws<ArgumentNullException>(() => CreateSut(Mock.Of<ITypeDescriber>(), Mock.Of<IConstraintMapper>(), options, null!, logger)).ParamName);
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => CreateSut(Mock.Of<ITypeDescriber>(), Mock.Of<IConstraintMapper>(), options, stepRegistry, null!)).ParamName);
    }
}
