using Kaleido.Http.Processor;
using Kaleido.Processor.Registry;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessorResponseFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static ProcessorResponseFactory CreateSut() => new();

    [Fact]
    public void CreateRegistryResponse_WithEmptyRegistration_ReturnsResponse()
    {
        var sut = CreateSut();
        var registration = new ProcessorRegistryItem();
        var options = new KaleidoServiceOptions { ServiceName = "test-svc" };

        var result = sut.CreateRegistryResponse(registration, options);

        Assert.Equal("test-svc", result.ServiceName);
        Assert.Empty(result.Steps ?? []);
    }

    [Fact]
    public void CreateRegistryResponse_PublishesAStepInputsLookup()
    {
        var lookup = new LookupMetadata
        {
            Service = "member",
            Source = "Member",
            View = "MemberSearch",
            Field = "MemberId",
            Group = "member/Member/MemberSearch",
            Display = ["DisplayName"]
        };
        var registration = new ProcessorRegistryItem
        {
            Steps =
            [
                new ProcessorStepRegistryItem
                {
                    Name = "ValidateMemberStep",
                    Fields = [new ProcessorInputFieldDescriptor { Name = "MemberId", DataType = new DataTypeDescriptor("string"), Lookup = lookup }]
                }
            ]
        };

        var result = CreateSut().CreateRegistryResponse(registration, new KaleidoServiceOptions { ServiceName = "intake" });

        Assert.Equal(lookup, Assert.Single(Assert.Single(result.Steps ?? []).Fields).Lookup);
    }

    private static ProcessorStepSummary DeclaredSummary() =>
        new() { Name = "ChargeCardStep", Destructive = true, External = true, RequiresConfirmation = true };

    [Fact]
    public void CreateRegistryResponse_PublishesTheDeclaredBehaviorAndTheClassifications()
    {
        string[] phi = ["Test:PHI"];
        var registration = new ProcessorRegistryItem
        {
            Steps =
            [
                new ProcessorStepRegistryItem
                {
                    Name = "ChargeCardStep",
                    Destructive = true,
                    External = true,
                    RequiresConfirmation = true,
                    Fields = [new ProcessorInputFieldDescriptor { Name = "MemberName", DataType = new DataTypeDescriptor("string"), Classifications = phi }],
                    Result = new ProcessorStepResultDescriptor
                    {
                        OutputFields = [new ProcessorOutputFieldDescriptor { Name = "MemberName", DataType = new DataTypeDescriptor("string"), Classifications = phi }],
                    },
                },
            ],
            InitialSteps = [DeclaredSummary()],
        };

        var result = CreateSut().CreateRegistryResponse(registration, new KaleidoServiceOptions { ServiceName = "billing" });

        var step = Assert.Single(result.Steps ?? []);
        Assert.True(step.Destructive && step.External && step.RequiresConfirmation);
        Assert.Equal(phi, Assert.Single(step.Fields).Classifications);
        Assert.Equal(phi, Assert.Single(step.Result!.OutputFields).Classifications);
        var initial = Assert.Single(result.InitialSteps ?? []);
        Assert.True(initial.Destructive && initial.External && initial.RequiresConfirmation);
        Assert.Equal(step.ExecuteUrl, initial.ExecuteUrl);
        Assert.NotEmpty(initial.ExecuteUrl);
    }

    [Fact]
    public void CreateStepResponse_PublishesTheStepRelationshipsInNameOrder()
    {
        var registration = new ProcessorStepRegistryItem
        {
            Name = "ApproveStep",
            Dependencies = [new ProcessorStepSummary { Name = "ReviewStep" }, new ProcessorStepSummary { Name = "CaptureStep" }],
            AvailableAfter = [new ProcessorStepSummary { Name = "ZetaStep" }, new ProcessorStepSummary { Name = "AlphaStep" }],
            AvailableUntil = [new ProcessorStepSummary { Name = "CloseStep" }, new ProcessorStepSummary { Name = "ArchiveStep" }],
        };

        var response = CreateSut().CreateStepResponse(registration, "billing");

        Assert.False(response.IsInitial);
        Assert.Equal(["CaptureStep", "ReviewStep"], response.Dependencies.Select(x => x.Name));
        Assert.Equal(["AlphaStep", "ZetaStep"], response.AvailableAfter.Select(x => x.Name));
        Assert.Equal(["ArchiveStep", "CloseStep"], response.AvailableUntil.Select(x => x.Name));
        Assert.Null(response.Result);
        Assert.Equal(ProcessContractUrls.ExecuteStep("billing", "approvestep"), response.ExecuteUrl);
    }

    [Fact]
    public void CreateStepResponse_AStepAvailableOnlyAfterAnother_IsNotInitial()
    {
        var registration = new ProcessorStepRegistryItem
        {
            Name = "FollowUpStep",
            AvailableAfter = [new ProcessorStepSummary { Name = "IntakeStep" }],
        };

        var response = CreateSut().CreateStepResponse(registration, "billing");

        Assert.False(response.IsInitial);
        Assert.Empty(response.Dependencies);
    }

    [Fact]
    public void CreateStepResponse_AStepWithoutRelationships_IsInitial()
    {
        var response = CreateSut().CreateStepResponse(new ProcessorStepRegistryItem { Name = "IntakeStep" }, "billing");

        Assert.True(response.IsInitial);
    }

    [Fact]
    public void CreateStepSummaryAndRequiredStep_CarryTheDeclaredBehavior()
    {
        var sut = CreateSut();

        var summary = sut.CreateStepSummary(DeclaredSummary(), "billing");
        var required = sut.CreateRequiredStep(DeclaredSummary(), informationRequest: null, "billing");

        Assert.True(summary.Destructive && summary.External && summary.RequiresConfirmation);
        Assert.True(required.Destructive && required.External && required.RequiresConfirmation);
    }
}
