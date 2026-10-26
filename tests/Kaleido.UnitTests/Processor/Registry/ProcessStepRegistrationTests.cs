using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Processor.Registry.UnitTests;

public sealed class ProcessStepRegistrationTests
    : SutFixture
{
    private sealed class TestPhiAttribute() : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute(new("Test", "PHI"));

    private sealed class ChargeStep : IProcessStep
    {
        [TestPhi]
        [Description("The member's name.")]
        public string MemberName { get; init; } = string.Empty;

        [Display(Name = "Amount", Prompt = "0.00")]
        public decimal Amount { get; init; }
    }

    private sealed record ChargeResult
    {
        [TestPhi]
        [Description("The member charged.")]
        public string MemberName { get; init; } = string.Empty;
    }

    private sealed class LookupStep : IProcessStep
    {
        [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberId", Display = ["DisplayName", "DateOfBirth"])]
        public Guid MemberId { get; init; }

        [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberEnrollmentId")]
        public Guid MemberEnrollmentId { get; init; }

        [Lookup(Service = "provider", Source = "Provider", Field = "ProviderId", Group = "requesting")]
        public Guid RequestingProviderId { get; init; }

        [Lookup(Service = "provider", Source = "Provider", Field = "ProviderId", Group = "servicing")]
        public Guid ServicingProviderId { get; init; }

        public DateOnly DateOfService { get; init; }
    }

    private sealed class AnswerStep : IInformationStep
    {
        public required string InformationRequestId { get; init; }

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }

    private static readonly AuthorizationMetadata Secured = new("step-policy", ["internal"]);

    private static ProcessStepMetadata Metadata(string name) =>
        new()
        {
            Name = name,
            Description = name + " description",
            DisplayName = name + " display",
            Authorization = Secured,
            Destructive = true,
            External = true,
            RequiresConfirmation = true,
        };

    private static ProcessStepRegistration CreateSut(
        Type stepType,
        Type? resultType = null,
        IReadOnlyCollection<ProcessStepRegistration>? dependencies = null,
        IReadOnlyCollection<ProcessStepRegistration>? availableAfter = null,
        bool repeatable = false) =>
        new(stepType, resultType, typeof(object), dependencies ?? [], availableAfter ?? [], [], new RepeatableOptions { Enabled = repeatable }, Metadata(stepType.Name));

    private static ProcessorStepRegistryItem RegistryItem(ProcessStepRegistration registration)
    {
        var describer = new Mock<ITypeDescriber>();
        describer.Setup(x => x.GetDescriptor(It.IsAny<System.Reflection.PropertyInfo>()))
            .Returns((System.Reflection.PropertyInfo property) => new DataTypeDescriptor(property.PropertyType.Name));
        var constraints = new Mock<IConstraintMapper>();
        constraints.Setup(x => x.Map(It.IsAny<System.Reflection.PropertyInfo>())).Returns([]);

        return registration.ToRegistryItem(describer.Object, constraints.Object);
    }

    [Theory]
    [InlineData(typeof(AnswerStep), true)]
    [InlineData(typeof(ChargeStep), false)]
    public void IsInformationStep_IsTrueForAnInformationStep(Type stepType, bool expected)
    {
        Assert.Equal(expected, CreateSut(stepType).IsInformationStep);
    }

    [Fact]
    public void ToSummary_CarriesTheMetadataAndTheDeclaredBehavior()
    {
        var summary = CreateSut(typeof(ChargeStep), repeatable: true).ToSummary();

        Assert.Equal(nameof(ChargeStep), summary.Name);
        Assert.Equal("ChargeStep description", summary.Description);
        Assert.Equal("ChargeStep display", summary.DisplayName);
        Assert.True(summary.Repeatable);
        Assert.False(summary.IsInformationStep);
        Assert.True(summary.Destructive && summary.External && summary.RequiresConfirmation);
        Assert.Equal("step-policy", summary.Authorization?.Policy);
    }

    [Fact]
    public void ToRegistryItem_DescribesEveryInput_WithItsTypeTextAndClassifications()
    {
        var item = RegistryItem(CreateSut(typeof(ChargeStep)));

        var name = Assert.Single(item.Fields, x => x.Name == nameof(ChargeStep.MemberName));
        var amount = Assert.Single(item.Fields, x => x.Name == nameof(ChargeStep.Amount));
        Assert.Equal("String", name.DataType.Type);
        Assert.Equal("The member's name.", name.Description);
        Assert.Equal(["Test:PHI"], name.Classifications);
        Assert.Equal("Amount", amount.DisplayName);
        Assert.Equal("0.00", amount.Prompt);
        Assert.Empty(amount.Classifications);
        Assert.True(item.Destructive && item.External && item.RequiresConfirmation);
        Assert.Equal("step-policy", item.Authorization?.Policy);
    }

    [Fact]
    public void ToRegistryItem_DescribesTheResult_OrNoneWithoutAResultType()
    {
        var output = Assert.Single(RegistryItem(CreateSut(typeof(ChargeStep), typeof(ChargeResult))).Result?.OutputFields ?? []);

        Assert.Equal(nameof(ChargeResult.MemberName), output.Name);
        Assert.Equal("The member charged.", output.Description);
        Assert.Equal(["Test:PHI"], output.Classifications);
        Assert.Null(RegistryItem(CreateSut(typeof(ChargeStep))).Result);
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(DayOfWeek))]
    public void ToRegistryItem_AScalarResult_HasNoOutputFields(Type resultType)
    {
        var result = RegistryItem(CreateSut(typeof(ChargeStep), resultType)).Result;

        Assert.NotNull(result);
        Assert.Empty(result.OutputFields);
    }

    // An information step's input is its pending request, not its fixed shape.
    [Fact]
    public void ToRegistryItem_AnInformationStep_PublishesNoFields()
    {
        var item = RegistryItem(CreateSut(typeof(AnswerStep)));

        Assert.True(item.IsInformationStep);
        Assert.Empty(item.Fields);
    }

    [Fact]
    public void ToRegistryItem_PublishesLookups_GroupedByQuery_WithTheGroupsDisplayFields()
    {
        var item = RegistryItem(CreateSut(typeof(LookupStep)));

        var memberId = Assert.Single(item.Fields, x => x.Name == nameof(LookupStep.MemberId)).Lookup;
        var enrollmentId = Assert.Single(item.Fields, x => x.Name == nameof(LookupStep.MemberEnrollmentId)).Lookup;

        Assert.NotNull(memberId);
        Assert.Equal("member", memberId.Service);
        Assert.Equal("Member", memberId.Source);
        Assert.Equal("MemberSearch", memberId.View);
        Assert.Equal("MemberId", memberId.Field);
        Assert.Equal("member/Member/MemberSearch", memberId.Group);
        Assert.Equal(["DisplayName", "DateOfBirth"], memberId.Display);

        // Same query, same result: the display declared on MemberId applies to the whole group.
        Assert.Equal(memberId.Group, enrollmentId?.Group);
        Assert.Equal(memberId.Display, enrollmentId?.Display);
        Assert.Equal("MemberEnrollmentId", enrollmentId?.Field);
    }

    [Fact]
    public void ToRegistryItem_ExplicitGroups_KeepTwoLookupsOfTheSameQueryApart_AndPlainInputsHaveNone()
    {
        var item = RegistryItem(CreateSut(typeof(LookupStep)));

        var requesting = Assert.Single(item.Fields, x => x.Name == nameof(LookupStep.RequestingProviderId)).Lookup;
        var servicing = Assert.Single(item.Fields, x => x.Name == nameof(LookupStep.ServicingProviderId)).Lookup;

        Assert.Equal("requesting", requesting?.Group);
        Assert.Equal("servicing", servicing?.Group);
        Assert.Null(requesting?.View);
        Assert.Null(Assert.Single(item.Fields, x => x.Name == nameof(LookupStep.DateOfService)).Lookup);
    }

    [Fact]
    public void ToRegistryItem_ListsRelationships_AsSummariesOrderedByName()
    {
        var b = CreateSut(typeof(LookupStep));
        var a = CreateSut(typeof(ChargeStep));

        var item = RegistryItem(CreateSut(typeof(AnswerStep), dependencies: [b, a], availableAfter: [a]));

        Assert.Equal([nameof(ChargeStep), nameof(LookupStep)], item.Dependencies.Select(x => x.Name));
        Assert.Equal([nameof(ChargeStep)], item.AvailableAfter.Select(x => x.Name));
        Assert.Empty(item.AvailableUntil);
    }
}
