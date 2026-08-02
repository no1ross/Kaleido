using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Participant.Planning;

internal class StepCandidateValidator : IStepCandidateValidator
{
    public void Validate(IReadOnlyCollection<StepCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            if (candidate.Status ==
                StepCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateCandidate(candidate);
        }
    }

    private static void ValidateCandidate(
        StepCandidate candidate)
    {
        if (candidate.Step is null)
        {
            throw new InvalidOperationException(
                $"Candidate '{candidate.StepName}' does not contain a hydrated step.");
        }

        var validationResults =
            new List<ValidationResult>();

        var validationContext =
            new ValidationContext(
                candidate.Step);

        var valid =
            Validator.TryValidateObject(
                candidate.Step,
                validationContext,
                validationResults,
                validateAllProperties: true);

        if (valid)
        {
            return;
        }

        candidate.Status =
            StepCandidateStatus.Invalid;

        foreach (var validationResult in validationResults)
        {
            candidate.AddError(
                    StepProcessingMessageCode.ValidationFailed,
                    validationResult.ErrorMessage
                        ?? "Validation failed.");
        }
    }
}
