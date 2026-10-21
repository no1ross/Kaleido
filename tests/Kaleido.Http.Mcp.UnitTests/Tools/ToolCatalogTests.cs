using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolCatalogTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoTool Tool(string name, string url = "") =>
        new() { Definition = new Tool { Name = name }, Kind = KaleidoToolKind.Query, Url = url };

    private static readonly KaleidoTool Patient = Tool("clinic_patient", "/clinic/queryable/patients/query");

    private static readonly KaleidoTool StartVisit = Tool("clinic_start_visit", "/clinic/processes/steps/startvisit");

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
        var sut = CreateSut(query, step);

        Assert.Same(step, sut.FindStep("clinic", "StartVisitStep"));
        Assert.Null(sut.FindStep("radiology", "StartVisitStep"));
        Assert.Null(sut.FindStep("clinic", "startvisitstep"));
    }

    [Fact]
    public void FindByUrl_IgnoresCase()
    {
        Assert.Same(StartVisit, CreateSut(Patient, StartVisit).FindByUrl("/CLINIC/processes/steps/StartVisit"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/clinic/unknown")]
    public void FindByUrl_WithoutAKnownUrl_IsNull(string? url)
    {
        Assert.Null(CreateSut(Patient, Tool("get_process_state")).FindByUrl(url));
    }

    // Several tools may call the same URL (a front door's hidden step and its listed twin): the first wins.
    [Fact]
    public void FindByUrl_ForASharedUrl_ReturnsTheFirstTool()
    {
        var twin = Tool("clinic_patient_again", Patient.Url);

        Assert.Same(Patient, CreateSut(Patient, twin).FindByUrl(Patient.Url));
    }

    // A copy with other tools must not answer from the original's lookups.
    [Fact]
    public void ACopyWithOtherTools_FindsItsOwnTools()
    {
        var original = CreateSut(Patient);
        Assert.Same(Patient, original.Find(Patient.Definition.Name));
        Assert.Same(Patient, original.FindByUrl(Patient.Url));

        var copy = original with { Tools = [StartVisit] };

        Assert.Null(copy.Find(Patient.Definition.Name));
        Assert.Null(copy.FindByUrl(Patient.Url));
        Assert.Same(StartVisit, copy.Find(StartVisit.Definition.Name));
    }

    [Fact]
    public void Empty_HasNoTools()
    {
        Assert.Empty(ToolCatalog.Empty.Tools);
        Assert.Null(ToolCatalog.Empty.Find("clinic_patient"));
    }
}
