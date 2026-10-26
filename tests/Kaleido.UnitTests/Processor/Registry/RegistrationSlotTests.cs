using Kaleido.UnitTests;

namespace Kaleido.Processor.Registry.UnitTests;

public sealed class RegistrationSlotTests
    : SutFixture
{
    private sealed record StepA : IProcessStep;

    private sealed record StepResult(string Value);

    private static readonly ProcessStepMetadata Metadata =
        new() { Name = "StepA", Description = "d", DisplayName = "Step A" };

    private static ProcessStepDefinition Definition() =>
        new() { StepType = typeof(StepA), StepResultType = typeof(StepResult), HandlerType = typeof(object), Metadata = Metadata };

    private static RegistrationSlot CreateSut(ProcessStepDefinition definition, bool repeatable = false) =>
        new(definition, new RepeatableOptions { Enabled = repeatable }, getResultFromTask: null, invokeHandlerAsync: null);

    private static ProcessStepRegistration Other() =>
        new(typeof(StepA), null, typeof(object), [], [], [], new RepeatableOptions(), Metadata);

    [Fact]
    public void TheRegistration_IsBuiltFromTheDefinition()
    {
        var definition = Definition();

        var sut = CreateSut(definition, repeatable: true);

        Assert.Same(definition, sut.Definition);
        Assert.Equal(typeof(StepA), sut.Registration.StepType);
        Assert.Equal(typeof(StepResult), sut.Registration.StepResultType);
        Assert.Equal(typeof(object), sut.Registration.HandlerType);
        Assert.Same(Metadata, sut.Registration.Metadata);
        Assert.True(sut.Registration.Repeatable.Enabled);
    }

    // The registration is created before its relationships are known: what the slot adds later
    // shows through the registration's relationship lists.
    [Fact]
    public void RelationshipsAddedLater_ShowThroughTheRegistration()
    {
        var sut = CreateSut(Definition());
        var dependency = Other();
        var after = Other();
        var until = Other();

        sut.AddDependencies([dependency]);
        sut.AddAvailableAfter([after]);
        sut.AddAvailableUntil([until]);

        Assert.Equal([dependency], sut.Dependencies);
        Assert.Equal([after], sut.AvailableAfter);
        Assert.Equal([until], sut.AvailableUntil);
        Assert.Equal([dependency], sut.Registration.Dependencies);
        Assert.Equal([after], sut.Registration.AvailableAfter);
        Assert.Equal([until], sut.Registration.AvailableUntil);
    }

    [Fact]
    public void WithoutADefinition_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut(null!));
    }
}
