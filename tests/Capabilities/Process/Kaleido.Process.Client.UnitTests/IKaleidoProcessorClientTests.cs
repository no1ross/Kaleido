namespace Kaleido.Process.Client.UnitTests;

public sealed class IKaleidoProcessorClientTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void ContractAssembly_ReferencesOnlyTheNeutralProcessOwner()
    {
        var references = typeof(IKaleidoProcessorClient).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.Contains("Kaleido.Process", references);
        Assert.DoesNotContain(references, name => name is "Kaleido.Platform" or "Kaleido.Http" or "Kaleido.Process.Http" or "Kaleido.Registry.Client");
    }

    [Fact]
    public void Calls_UseExistingNeutralProcessResultsWithoutTransportValues()
    {
        var methods = typeof(IKaleidoProcessorClient).GetMethods();
        var execute = Assert.Single(methods, x => x.Name == nameof(IKaleidoProcessorClient.ExecuteAsync));
        Assert.Equal(typeof(ProcessRequest), execute.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), execute.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(Task<ProcessResult>), execute.ReturnType);

        var state = Assert.Single(methods, x => x.Name == nameof(IKaleidoProcessorClient.GetProcessStateAsync));
        Assert.Equal(typeof(Task<ProcessStateSnapshot>), state.ReturnType);

        var steps = methods.Where(x => x.Name == nameof(IKaleidoProcessorClient.ExecuteStepAsync)).ToArray();
        Assert.Equal(2, steps.Length);
        Assert.All(steps, step => Assert.Equal(typeof(Task<ProcessResult>), step.ReturnType));
        Assert.Contains(steps, step => step.IsGenericMethodDefinition && step.GetParameters()[1].ParameterType == typeof(Guid?));
        Assert.Contains(steps, step => step.GetParameters()[0].ParameterType == typeof(string)
            && step.GetParameters()[1].ParameterType == typeof(object));
        Assert.DoesNotContain(methods.SelectMany(x => x.GetParameters()), parameter =>
            parameter.ParameterType.FullName?.Contains("JsonElement", StringComparison.Ordinal) == true
            || parameter.ParameterType.Namespace?.StartsWith("Kaleido.Http", StringComparison.Ordinal) == true);
    }
}
