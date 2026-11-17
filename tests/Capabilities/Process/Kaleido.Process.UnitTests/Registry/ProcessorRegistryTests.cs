using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    public void Published_ProjectsProcessMetadataWithoutHttpBindingsOrClrTypes()
    {
        var step = new ProcessStepRegistration(
            typeof(RegistrationStep), typeof(RegistrationResult), typeof(object), [], [], [],
            new RepeatableOptions { Enabled = true },
            new ProcessStepMetadata
            {
                Name = nameof(RegistrationStep),
                Description = "Register a member",
                DisplayName = "Registration",
                Authorization = new Kaleido.Registry.AuthorizationMetadata("intake-policy", ["registrant"]),
            });
        var steps = new Mock<IProcessorStepRegistry>();
        steps.SetupGet(x => x.Registrations).Returns([step]);
        steps.SetupGet(x => x.InitialRegistrations).Returns([step]);
        var sut = CreateSut(new TypeDescriber(), new ConstraintMapper(),
            new KaleidoServiceOptions { ServiceName = "intake", ServiceVersion = "1", Description = "Intake" },
            new KaleidoProcessOptions { IsEntryProcessor = true }, steps.Object, Mock.Of<ILogger<ProcessorRegistry>>());

        var published = Assert.Single(sut.Published);
        Assert.Equal(("intake", "1", "Intake", true), (published.ServiceName, published.ServiceVersion, published.Description, published.IsEntryProcessor));
        Assert.Empty(published.Bindings.Http);
        Assert.Equal(nameof(RegistrationStep), Assert.Single(published.InitialSteps).Name);
        var detail = Assert.Single(published.Steps!);
        Assert.True(detail.IsInitial);
        Assert.True(detail.Repeatable);
        Assert.Equal("intake-policy", detail.Authorization?.Policy);
        Assert.Empty(detail.Bindings.Http);
        var member = Assert.Single(detail.Fields, x => x.Name == nameof(RegistrationStep.MemberName));
        Assert.Equal(("string", "The member's name."), (member.DataType.Type, member.Description));
        Assert.Equal(["Test:PHI"], member.Classifications);
        Assert.Contains(member.Constraints, constraint => constraint.Type == "Required");
        var lookup = Assert.Single(detail.Fields, x => x.Name == nameof(RegistrationStep.MemberId)).Lookup;
        Assert.Equal(("member", "MemberSource", "MemberId"), (lookup?.Service, lookup?.Source, lookup?.Field));
        Assert.Equal(["Test:PHI"], Assert.Single(detail.Result!.OutputFields).Classifications);
        Assert.DoesNotContain(typeof(Kaleido.Registry.ProcessRegistryItem).GetProperties(), property => property.PropertyType == typeof(Type));
    }

    private sealed class TestPhiAttribute() : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute(new("Test", "PHI"));

    private sealed class RegistrationStep : IProcessStep
    {
        [TestPhi]
        [Required]
        [Description("The member's name.")]
        public string MemberName { get; init; } = string.Empty;

        [Lookup(Service = "member", Source = "MemberSource", Field = "MemberId")]
        public Guid MemberId { get; init; }
    }

    private sealed class RegistrationResult
    {
        [TestPhi]
        public string MemberName { get; init; } = string.Empty;
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
