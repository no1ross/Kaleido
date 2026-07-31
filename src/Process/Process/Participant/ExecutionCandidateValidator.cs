
using Kaleido.Process.Participant.Execution;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Participant;

internal static class ExecutionCandidateValidator
{
    public static void Validate(
        IReadOnlyCollection<ExecutionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            if (candidate.Status ==
                ExecutionCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateCandidate(candidate);
        }
    }

    private static void ValidateCandidate(
        ExecutionCandidate candidate)
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
            ExecutionCandidateStatus.Invalid;

        foreach (var validationResult in validationResults)
        {
            candidate.AddMessage(
                ProcessStepMessage.Error(
                    ProcessStepMessageCode.ValidationFailed,
                    validationResult.ErrorMessage
                        ?? "Validation failed."));
        }
    }
}
