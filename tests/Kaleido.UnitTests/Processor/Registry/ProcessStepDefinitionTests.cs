using Kaleido.UnitTests;

namespace Kaleido.Processor.Registry.UnitTests;

public sealed class ProcessStepDefinitionTests
    : SutFixture
{
    private sealed record StepA : IProcessStep;

    private sealed record StepB : IProcessStep;

    private static ProcessStepDefinition CreateSut(string name = "StepA") =>
        new()
        {
            StepType = typeof(StepA),
            HandlerType = typeof(object),
            Metadata = new ProcessStepMetadata { Name = name, Description = "d", Version = "1.0.0", DisplayName = name },
        };

    // Relationships are collected as types while steps are discovered, resolved to definitions later.
    [Fact]
    public void TheRelationshipTypes_AreCollectedInOrder_EachInItsOwnList()
    {
        var sut = CreateSut();

        sut.AddDependencyType(typeof(StepA));
        sut.AddDependencyType(typeof(StepB));
        sut.AddAvailableAfterType(typeof(StepB));
        sut.AddAvailableUntilType(typeof(StepA));

        Assert.Equal([typeof(StepA), typeof(StepB)], sut.DependencyTypes);
        Assert.Equal([typeof(StepB)], sut.AvailableAfterTypes);
        Assert.Equal([typeof(StepA)], sut.AvailableUntilTypes);
    }

    [Fact]
    public void TheResolvedRelationships_AreCollected_EachInItsOwnList()
    {
        var sut = CreateSut();
        var other = CreateSut("StepB");

        sut.AddDependency(other);
        sut.AddAvailableAfter(other);
        sut.AddAvailableUntil(sut);

        Assert.Equal([other], sut.Dependencies);
        Assert.Equal([other], sut.AvailableAfter);
        Assert.Equal([sut], sut.AvailableUntil);
    }

    [Fact]
    public void ANewDefinition_HasNoRelationships()
    {
        var sut = CreateSut();

        Assert.Empty(sut.DependencyTypes);
        Assert.Empty(sut.AvailableAfterTypes);
        Assert.Empty(sut.AvailableUntilTypes);
        Assert.Empty(sut.Dependencies);
        Assert.Empty(sut.AvailableAfter);
        Assert.Empty(sut.AvailableUntil);
    }
}
