using System.ComponentModel.DataAnnotations;
using Kaleido.Exceptions;

namespace Kaleido.UnitTests.Processor.Planning;

public sealed class StepCandidateValidatorTests
    : SutFixture
{
    private StepCandidateValidator Sut =>
        CreateSut();

    private static StepCandidateValidator CreateSut() =>
        new();

    [Fact]
    public void Validate_WhenCandidateAlreadyInvalid_SkipsValidation()
    {
        var candidate =
            StepCandidate.Invalid(
                "test-step",
                ProcessorErrorCodes.InvalidRequest,
                "Already invalid.");

        candidate.Step = null;

        Sut.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.Single(candidate.Messages);
    }

    [Fact]
    public void Validate_WhenCandidateHasNoHydratedStep_Throws()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "test-step",
                Status = StepCandidateStatus.Built
            };

        var exception =
            Assert.Throws<KaleidoFrameworkException>(() =>
                Sut.Validate([candidate]));

        Assert.Equal(
            "Candidate 'test-step' does not contain a hydrated step.",
            exception.Message);
    }

    [Fact]
    public void Validate_WhenCandidateIsValid_DoesNotModifyCandidate()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "valid-step",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep
                {
                    Name = "Andrew",
                    Description = "Valid Description",
                    Quantity = 10
                }
            };

        Sut.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Built,
            candidate.Status);

        Assert.False(candidate.HasErrors);
        Assert.Empty(candidate.Messages);
    }

    [Fact]
    public void Validate_WhenRequiredPropertyMissing_MarksCandidateInvalid()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "invalid-step",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep
                {
                    Description = "Description",
                    Quantity = 10
                }
            };

        Sut.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.True(candidate.HasErrors);

        var message =
            Assert.Single(candidate.Messages);

        Assert.Equal(
            ProcessorErrorCodes.Required,
            message.Code);
    }

    [Fact]
    public void Validate_WhenMultipleValidationFailuresExist_AddsMessageForEachFailure()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "invalid-step",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep()
            };

        Sut.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.Equal(
            3,
            candidate.Messages.Count);

        Assert.Equal(2, candidate.Messages.Count(x => x.Code == ProcessorErrorCodes.Required));
        Assert.Contains(candidate.Messages, x => x.Code == ProcessorErrorCodes.OutOfRange);
    }

    [Fact]
    public void Validate_WhenRangeValidationFails_MarksCandidateInvalid()
    {
        var candidate =
            new StepCandidate
            {
                StepName = "range-step",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep
                {
                    Name = "Andrew",
                    Description = "Description",
                    Quantity = 0
                }
            };

        Sut.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.True(candidate.HasErrors);

        Assert.Contains(
            candidate.Messages,
            x => x.Code == ProcessorErrorCodes.OutOfRange);
    }

    [Fact]
    public void Validate_WhenDisplayedRequiredFieldFails_UsesStableRequiredCode()
    {
        var candidate = CreateCandidate(new DisplayNameStep());

        Sut.Validate([candidate]);

        Assert.Equal(ProcessorErrorCodes.Required, Assert.Single(candidate.Messages).Code);
    }

    [Fact]
    public void Validate_WhenLengthFails_UsesStableLengthCode()
    {
        var candidate = CreateCandidate(new LengthStep { Value = "too long" });

        Sut.Validate([candidate]);

        Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
        Assert.Equal(ProcessorErrorCodes.InvalidLength, Assert.Single(candidate.Messages).Code);
    }

    [Fact]
    public void Validate_WhenFormatFails_UsesStableFormatCode()
    {
        var candidate = CreateCandidate(new FormatStep { Email = "not-an-email" });

        Sut.Validate([candidate]);

        Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
        Assert.Equal(ProcessorErrorCodes.InvalidFormat, Assert.Single(candidate.Messages).Code);
    }

    [Fact]
    public void Validate_WhenCustomOrObjectLevelRulesFail_PreservesResultsWithGenericCode()
    {
        var candidates = new[]
        {
            CreateCandidate(new CustomPropertyStep()),
            CreateCandidate(new ClassRuleStep()),
            CreateCandidate(new ObjectRuleStep())
        };

        Sut.Validate(candidates);

        Assert.All(candidates, candidate =>
        {
            Assert.Equal(StepCandidateStatus.Invalid, candidate.Status);
            Assert.Equal(ProcessorErrorCodes.ValidationFailed, Assert.Single(candidate.Messages).Code);
        });
        Assert.Equal("Custom validation failed.", Assert.Single(candidates[0].Messages).Message);
        Assert.Equal("Class validation failed.", Assert.Single(candidates[1].Messages).Message);
        Assert.Equal("Object validation failed.", Assert.Single(candidates[2].Messages).Message);
    }

    [Fact]
    public void Validate_WhenMultipleCandidatesProvided_ValidatesEachCandidate()
    {
        var validCandidate =
            new StepCandidate
            {
                StepName = "valid",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep
                {
                    Name = "Andrew",
                    Description = "Valid",
                    Quantity = 5
                }
            };

        var invalidCandidate =
            new StepCandidate
            {
                StepName = "invalid",
                Status = StepCandidateStatus.Built,
                Step = new ValidationStep()
            };

        Sut.Validate(
            [
                validCandidate,
                invalidCandidate
            ]);

        Assert.Equal(
            StepCandidateStatus.Built,
            validCandidate.Status);

        Assert.False(validCandidate.HasErrors);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            invalidCandidate.Status);

        Assert.True(invalidCandidate.HasErrors);
    }

    private static StepCandidate CreateCandidate(object step) =>
        new()
        {
            StepName = "validation-step",
            Status = StepCandidateStatus.Built,
            Step = step
        };

    private sealed class ValidationStep
    {
        [Required]
        public string? Name { get; init; }

        [Required]
        public string? Description { get; init; }

        [Range(1, 100)]
        public int Quantity { get; init; }
    }

    private sealed class DisplayNameStep
    {
        [Display(Name = "Full name")]
        [Required]
        public string? Name { get; init; }
    }

    private sealed class LengthStep
    {
        [StringLength(4)]
        public string Value { get; init; } = string.Empty;
    }

    private sealed class FormatStep
    {
        [EmailAddress]
        public string Email { get; init; } = string.Empty;
    }

    private sealed class CustomPropertyStep
    {
        [CustomPropertyRule]
        public string Value { get; init; } = "valid";
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class CustomPropertyRuleAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
            new("Custom validation failed.");
    }

    [ClassRule]
    private sealed class ClassRuleStep
    {
        public string Value { get; init; } = "valid";
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class ClassRuleAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
            new("Class validation failed.", [nameof(ClassRuleStep.Value)]);
    }

    private sealed class ObjectRuleStep : IValidatableObject
    {
        public string Value { get; init; } = "valid";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
            [new ValidationResult("Object validation failed.", [nameof(Value)])];
    }
}