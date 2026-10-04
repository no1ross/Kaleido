using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Processor.Planning;

internal interface IStepCandidateValidator
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates);
}

internal sealed class StepCandidateValidator : IStepCandidateValidator
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
        var step = candidate.Step;
        if (step is null)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Candidate '{candidate.StepName}' does not contain a hydrated step.");
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(step);

        var valid = Validator.TryValidateObject(
            step,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (valid)
        {
            return;
        }

        candidate.Status = StepCandidateStatus.Invalid;

        foreach (var validationResult in validationResults)
        {
            candidate.AddError(
                GetValidationCode(step, validationResult),
                validationResult.ErrorMessage ?? "Validation failed.");
        }
    }

    private static string GetValidationCode(object step, ValidationResult result)
    {
        var members = result.MemberNames.ToArray();
        if (members.Length != 1 || string.IsNullOrWhiteSpace(members[0]))
        {
            return ProcessorErrorCodes.ValidationFailed;
        }

        var property = TypeDescriptor.GetProperties(step)[members[0]];
        if (property is null)
        {
            return ProcessorErrorCodes.ValidationFailed;
        }

        var context = new ValidationContext(step)
        {
            MemberName = members[0]
        };

        var value = property.GetValue(step);
        foreach (var attribute in property.Attributes.OfType<ValidationAttribute>())
        {
            var code = GetAttributeCode(attribute.GetType());
            if (code is null)
            {
                continue;
            }

            var failure = attribute.GetValidationResult(value, context);
            if (failure is not null &&
                string.Equals(failure.ErrorMessage, result.ErrorMessage, StringComparison.Ordinal))
            {
                return code;
            }
        }

        return ProcessorErrorCodes.ValidationFailed;
    }

    private static string? GetAttributeCode(Type type)
    {
        if (type == typeof(RequiredAttribute))
        {
            return ProcessorErrorCodes.Required;
        }

        if (type == typeof(StringLengthAttribute) ||
            type == typeof(MinLengthAttribute) ||
            type == typeof(MaxLengthAttribute) ||
            type == typeof(LengthAttribute))
        {
            return ProcessorErrorCodes.InvalidLength;
        }

        if (type == typeof(RangeAttribute))
        {
            return ProcessorErrorCodes.OutOfRange;
        }

        if (type == typeof(RegularExpressionAttribute) ||
            type == typeof(EmailAddressAttribute) ||
            type == typeof(PhoneAttribute) ||
            type == typeof(UrlAttribute) ||
            type == typeof(CreditCardAttribute) ||
            type == typeof(FileExtensionsAttribute))
        {
            return ProcessorErrorCodes.InvalidFormat;
        }

        return null;
    }
}
