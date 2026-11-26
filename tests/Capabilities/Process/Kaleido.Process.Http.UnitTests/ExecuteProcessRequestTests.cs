using System.Text.Json;

namespace Kaleido.Process.Http.UnitTests;

public sealed class ExecuteProcessRequestTests : Kaleido.UnitTests.SutFixture<ExecuteProcessRequest>
{
    protected override ExecuteProcessRequest CreateSut() => new()
    {
        Steps = [new ProcessStepRequest
        {
            StepName = "ApproveStep",
            Request = JsonSerializer.SerializeToElement(new { Code = "A-1" }, KaleidoJsonOptions.Options),
        }],
    };

    [Fact]
    public void HttpMultiStepBody_CarriesRawStepInputsNotAProcessId()
    {
        var json = JsonSerializer.Serialize(Sut, KaleidoJsonOptions.Options);

        using var document = JsonDocument.Parse(json);
        var step = Assert.Single(document.RootElement.GetProperty("steps").EnumerateArray());
        Assert.Equal("ApproveStep", step.GetProperty("stepName").GetString());
        Assert.Equal("A-1", step.GetProperty("request").GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("processId", out _));
        Assert.Equal("ApproveStep", Assert.Single(Assert.IsType<ExecuteProcessRequest>(JsonSerializer.Deserialize<ExecuteProcessRequest>(json, KaleidoJsonOptions.Options)).Steps).StepName);
    }
}
