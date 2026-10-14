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

        var failures = ValidateProperties(step);
        if (failures.Count == 0)
        {
            failures = ValidateObject(step);
        }

        if (failures.Count == 0)
        {
            return;
        }

        candidate.Status = StepCandidateStatus.Invalid;

        foreach (var (code, message) in failures)
        {
            candidate.AddError(code, message);
        }
    }

    private static List<(string Code, string Message)> ValidateProperties(object step)
    {
        var failures = new List<(string Code, string Message)>();

        foreach (var property in TypeDescriptor.GetProperties(step).Cast<PropertyDescriptor>())
        {
            var attributes = property.Attributes.OfType<ValidationAttribute>().ToArray();
            if (attributes.Length == 0)
            {
                continue;
            }

            var context = new ValidationContext(step)
            {
                MemberName = property.Name,
            };

            var value = property.GetValue(step);

            var required = attributes.OfType<RequiredAttribute>().FirstOrDefault();
            if (required?.GetValidationResult(value, context) is { } missing)
            {
                failures.Add((GetCode(required), GetMessage(missing)));
                continue;
            }

            foreach (var attribute in attributes.Where(x => x is not RequiredAttribute))
            {
                if (attribute.GetValidationResult(value, context) is { } failure)
                {
                    failures.Add((GetCode(attribute), GetMessage(failure)));
                }
            }
        }

        return failures;
    }

    private static List<(string Code, string Message)> ValidateObject(object step)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            step,
            new ValidationContext(step),
            results,
            validateAllProperties: true);

        return results.ConvertAll(x => (ProcessorErrorCodes.ValidationFailed, GetMessage(x)));
    }

    private static string GetMessage(ValidationResult result) =>
        result.ErrorMessage ?? "Validation failed.";

    private static string GetCode(ValidationAttribute attribute) =>
        GetAttributeCode(attribute.GetType()) ?? ProcessorErrorCodes.ValidationFailed;

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
