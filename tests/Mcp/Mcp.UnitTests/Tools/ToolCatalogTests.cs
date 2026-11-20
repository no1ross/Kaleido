using ModelContextProtocol.Protocol;

namespace Kaleido.Mcp.Tools.UnitTests;

public sealed class ToolCatalogTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoTool Tool(string name, KaleidoToolKind kind = KaleidoToolKind.Query) =>
        new() { Definition = new Tool { Name = name }, Kind = kind, Service = "clinic", CapabilityName = name };

    private static readonly KaleidoTool Patient = Tool("clinic_patient");

    private static readonly KaleidoTool StartVisit = Tool("clinic_start_visit", KaleidoToolKind.Step);

    private static ToolCatalog CreateSut(params KaleidoTool[] tools) => new() { Tools = tools };

    [Fact]
    public void Find_ByTheExactToolName()
    {
        var sut = CreateSut(Patient, StartVisit);

        Assert.Same(Patient, sut.Find("clinic_patient"));
        Assert.Null(sut.Find("CLINIC_PATIENT"));
        Assert.Null(sut.Find("clinic_chart"));
    }

    [Fact]
    public void FindStep_ByServiceAndStepName_AmongStepTools()
    {
        var step = new KaleidoTool { Definition = new Tool { Name = "clinic_start_visit" }, Kind = KaleidoToolKind.Step, Service = "clinic", CapabilityName = "StartVisitStep" };
        var query = new KaleidoTool { Definition = new Tool { Name = "clinic_start" }, Kind = KaleidoToolKind.Query, Service = "clinic", CapabilityName = "StartVisitStep" };
        var v2 = step with { Definition = new Tool { Name = "clinic_start_v2" }, ServiceVersion = "2" };
        var sut = CreateSut(query, step, v2);

        Assert.Same(step, sut.FindStep("clinic", "StartVisitStep"));
        Assert.Same(v2, sut.FindStep("clinic", "StartVisitStep", "2"));
        Assert.Null(sut.FindStep("clinic", "StartVisitStep", "3"));
        Assert.Null(sut.FindStep("radiology", "StartVisitStep"));
        Assert.Null(sut.FindStep("clinic", "startvisitstep"));
    }

    [Fact]
    public void FindStepMetadata_IgnoresCase()
    {
        var step = new ProcessStepResponse { Name = "StartVisitStep" };
        var sut = CreateSut(Patient, StartVisit) with
        {
            Processes = [new ProcessRegistryItem { ServiceName = "clinic", Name = "clinic", ServiceVersion = "1", Steps = [step] }],
        };

        Assert.Same(step, sut.FindStepMetadata("CLINIC", "startvisitstep", "1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void FindStepMetadata_WithoutAKnownStep_IsNull(string? name)
    {
        var sut = CreateSut(Patient, Tool("get_process_state")) with
        {
            Processes = [new ProcessRegistryItem { ServiceName = "clinic", Name = "clinic", Steps = [new ProcessStepResponse { Name = "StartVisitStep" }] }],
        };

        Assert.Null(sut.FindStepMetadata("clinic", name, serviceVersion: null));
    }

    // More than one publication of a version may carry the same step: metadata lookup stays deterministic.
    [Fact]
    public void FindStepMetadata_ForDuplicateNames_ReturnsTheFirstPublication()
    {
        var first = new ProcessStepResponse { Name = "StartVisitStep", DisplayName = "First" };
        var second = first with { DisplayName = "Second" };
        var sut = CreateSut(Patient) with
        {
            Processes =
            [
                new ProcessRegistryItem { ServiceName = "clinic", Name = "clinic", Steps = [first] },
                new ProcessRegistryItem { ServiceName = "clinic", Name = "clinic", Steps = [second] },
            ],
        };

        Assert.Same(first, sut.FindStepMetadata("clinic", "StartVisitStep", serviceVersion: null));
    }

    // A copy with other tools must not answer from the original's lookups.
    [Fact]
    public void ACopyWithOtherTools_FindsItsOwnTools()
    {
        var original = CreateSut(Patient);
        Assert.Same(Patient, original.Find(Patient.Definition.Name));
        var copy = original with { Tools = [StartVisit] };

        Assert.Null(copy.Find(Patient.Definition.Name));
        Assert.Same(StartVisit, copy.Find(StartVisit.Definition.Name));
    }

    [Fact]
    public void FindStepMetadata_MatchesThePublishedServiceVersionWithoutDerivingItFromAUrl()
    {
        var v1 = new ProcessStepResponse { Name = "Ask", Description = "First version" };
        var v2 = v1 with { Description = "Second version" };
        var sut = CreateSut() with
        {
            Processes =
            [
                new ProcessRegistryItem { ServiceName = "clinic", ServiceVersion = "1", Name = "clinic", Steps = [v1] },
                new ProcessRegistryItem { ServiceName = "clinic", ServiceVersion = "2", Name = "clinic", Steps = [v2] },
            ],
        };

        Assert.Same(v1, sut.FindStepMetadata("CLINIC", "ask", "1"));
        Assert.Same(v2, sut.FindStepMetadata("clinic", "Ask", "2"));
        Assert.Null(sut.FindStepMetadata("clinic", "Ask", "3"));
        Assert.Same(v1, (sut with { Tools = [] }).FindStepMetadata("clinic", "Ask", "1"));
    }

    [Fact]
    public void Empty_HasNoTools()
    {
        Assert.Empty(ToolCatalog.Empty.Tools);
        Assert.Null(ToolCatalog.Empty.Find("clinic_patient"));
    }
}
