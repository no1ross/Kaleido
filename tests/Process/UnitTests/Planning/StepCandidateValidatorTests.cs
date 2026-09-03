using Kaleido.Process.Planning;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Kaleido.Process.UnitTests.Processor.Planning;

public sealed class StepCandidateValidatorTests
{
    private readonly StepCandidateValidator _validator =
        new();

    [Fact]
    public void Validate_WhenCandidatesIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                _validator.Validate(null!));

        Assert.Equal("candidates", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenCandidateAlreadyInvalid_SkipsValidation()
    {
        var candidate =
            StepCandidate.Invalid(
                "test-step",
                StepProcessingMessageCode.InvalidRequest,
                "Already invalid.");

        candidate.Step = null;

        _validator.Validate([candidate]);

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
            Assert.Throws<InvalidOperationException>(() =>
                _validator.Validate([candidate]));

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

        _validator.Validate([candidate]);

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

        _validator.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.True(candidate.HasErrors);

        var message =
            Assert.Single(candidate.Messages);

        Assert.Equal(
            StepProcessingMessageCode.ValidationFailed,
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

        _validator.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.Equal(
            3,
            candidate.Messages.Count);

        Assert.All(
            candidate.Messages,
            message =>
                Assert.Equal(
                    StepProcessingMessageCode.ValidationFailed,
                    message.Code));
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

        _validator.Validate([candidate]);

        Assert.Equal(
            StepCandidateStatus.Invalid,
            candidate.Status);

        Assert.True(candidate.HasErrors);

        Assert.Contains(
            candidate.Messages,
            x => x.Code == StepProcessingMessageCode.ValidationFailed);
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

        _validator.Validate(
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

    private sealed class ValidationStep
    {
        [Required]
        public string? Name { get; init; }

        [Required]
        public string? Description { get; init; }

        [Range(1, 100)]
        public int Quantity { get; init; }
    }
}