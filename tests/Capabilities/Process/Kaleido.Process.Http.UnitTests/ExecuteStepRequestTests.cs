using System.Text.Json;

namespace Kaleido.Process.Http.UnitTests;

public sealed class ExecuteStepRequestTests : Kaleido.UnitTests.SutFixture<ExecuteStepRequest<ExecuteStepRequestTests.SampleStep>>
{
    public sealed record SampleStep(string Code);

    protected override ExecuteStepRequest<SampleStep> CreateSut() => new()
    {
        ProcessStep = new SampleStep("A-1"),
    };

    [Fact]
    public void HttpStepBody_WrapsADeclaredStepWithoutAProcessIdOrStepName()
    {
        var json = JsonSerializer.Serialize(Sut, KaleidoJsonOptions.Options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("A-1", document.RootElement.GetProperty("processStep").GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("processId", out _));
        Assert.False(document.RootElement.TryGetProperty("stepName", out _));
        Assert.Equal("A-1", Assert.IsType<ExecuteStepRequest<SampleStep>>(JsonSerializer.Deserialize<ExecuteStepRequest<SampleStep>>(json, KaleidoJsonOptions.Options)).ProcessStep.Code);
    }
}
