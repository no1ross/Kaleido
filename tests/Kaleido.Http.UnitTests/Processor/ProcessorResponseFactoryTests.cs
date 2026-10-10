using Kaleido.Http.Processor;
using Kaleido.Processor.Registry;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessorResponseFactoryTests
    : Kaleido.UnitTests.SutFixture<ProcessorResponseFactory>
{
    protected override ProcessorResponseFactory CreateSut() => new();

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
}