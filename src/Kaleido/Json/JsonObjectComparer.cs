using System.Text.Json;

namespace Kaleido.Json;

public static class JsonObjectComparer
{
    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                WriteIndented = false
            };

    public static bool AreEqual(
        object? previousStep,
        object? currentStep)
    {
        if (ReferenceEquals(
                previousStep,
                currentStep))
        {
            return true;
        }

        if (previousStep is null ||
            currentStep is null)
        {
            return false;
        }

        if (previousStep.GetType() !=
            currentStep.GetType())
        {
            return false;
        }

        var previousJson =
            JsonSerializer.Serialize(
                previousStep,
                SerializerOptions);

        var currentJson =
            JsonSerializer.Serialize(
                currentStep,
                SerializerOptions);

        return string.Equals(
            previousJson,
            currentJson,
            StringComparison.Ordinal);
    }
}