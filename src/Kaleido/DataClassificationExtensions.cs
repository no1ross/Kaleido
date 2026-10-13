using System.Reflection;
using Microsoft.Extensions.Compliance.Classification;

namespace Kaleido;

// The data classifications a property declares (DataClassificationAttribute, e.g. [PHI]); one rule for
// the registry, which publishes them, and the event payload writer, which redacts by them.
// [NoDataClassification] (DataClassification.None) is not a classification.
internal static class DataClassificationExtensions
{
    public static DataClassification[] GetDataClassifications(this ICustomAttributeProvider? member) =>
        (member?.GetCustomAttributes(typeof(DataClassificationAttribute), inherit: true) ?? [])
            .OfType<DataClassificationAttribute>()
            .Select(x => x.Classification)
            .Where(x => x != DataClassification.None)
            .Distinct()
            .ToArray();

    // As published: "{taxonomy}:{value}", e.g. "PriorAuth:PHI".
    public static IReadOnlyList<string> GetDataClassificationNames(this ICustomAttributeProvider? member) =>
        [.. member.GetDataClassifications().Select(x => $"{x.TaxonomyName}:{x.Value}")];
}
