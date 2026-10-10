using Kaleido.Samples.PriorAuth.Radiology.Process.Services;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

/// <summary>
/// The details a caller may state about a requested service up front, stored under the binding
/// keys the business uses on its questions. Which question proposes which detail is configuration;
/// this only names what the service accepts.
/// </summary>
public static class StatedServiceDetails
{
    public const string BodyPartKey = "BodyPart";

    public const string LateralityKey = "Laterality";

    public const string ContrastKey = "Contrast";

    public static string? ToJson(
        MriBodyPart? bodyPart,
        Laterality? laterality,
        ContrastOption? contrast) =>
        ConfiguredInformationRequests.SerializeStatedDetails(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [BodyPartKey] = Stated(bodyPart),
                [LateralityKey] = Stated(laterality),
                [ContrastKey] = Stated(contrast)
            });

    // Unknown (0) means "not stated".
    private static string? Stated<T>(T? value)
        where T : struct, Enum =>
        value is { } given && Convert.ToInt32(given, System.Globalization.CultureInfo.InvariantCulture) != 0
            ? given.ToString()
            : null;
}
