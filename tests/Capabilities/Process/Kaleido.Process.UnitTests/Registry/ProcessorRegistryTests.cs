using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Registry.UnitTests;

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
            new TypeDescriber(),
            new ConstraintMapper(),
            new KaleidoServiceOptions { ServiceName = "intake", ServiceVersion = "1" },
            new KaleidoProcessOptions { IsEntryProcessor = isEntryProcessor },
            stepRegistry.Object,
            logger ?? Mock.Of<ILogger<ProcessorRegistry>>());
    }

    private static ProcessorRegistry CreateSut(
        TypeDescriber typeDescriber,
        ConstraintMapper constraintMapper,
        KaleidoServiceOptions serviceOptions,
        KaleidoProcessOptions processOptions,
        IProcessorStepRegistry stepRegistry,
        ILogger<ProcessorRegistry> logger) =>
        new(typeDescriber, constraintMapper, serviceOptions, processOptions, stepRegistry, logger);

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
        var options = new KaleidoServiceOptions { ServiceName = "intake", ServiceVersion = "1" };
        var processOptions = new KaleidoProcessOptions();
        var logger = Mock.Of<ILogger<ProcessorRegistry>>();

        Assert.Equal("typeDescriber", Assert.Throws<ArgumentNullException>(() => CreateSut(null!, new ConstraintMapper(), options, processOptions, stepRegistry, logger)).ParamName);
        Assert.Equal("constraintMapper", Assert.Throws<ArgumentNullException>(() => CreateSut(new TypeDescriber(), null!, options, processOptions, stepRegistry, logger)).ParamName);
        Assert.Equal("serviceOptions", Assert.Throws<ArgumentNullException>(() => CreateSut(new TypeDescriber(), new ConstraintMapper(), null!, processOptions, stepRegistry, logger)).ParamName);
        Assert.Equal("processOptions", Assert.Throws<ArgumentNullException>(() => CreateSut(new TypeDescriber(), new ConstraintMapper(), options, null!, stepRegistry, logger)).ParamName);
        Assert.Equal("stepRegistry", Assert.Throws<ArgumentNullException>(() => CreateSut(new TypeDescriber(), new ConstraintMapper(), options, processOptions, null!, logger)).ParamName);
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => CreateSut(new TypeDescriber(), new ConstraintMapper(), options, processOptions, stepRegistry, null!)).ParamName);
    }
}
