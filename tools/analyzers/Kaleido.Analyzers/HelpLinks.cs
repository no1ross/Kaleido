namespace Kaleido.Analyzers;

/// <summary>
/// The help link of a consumer rule: its entry in docs/ANALYZERS.md (every KAL2xxx row carries an
/// anchor named after the lowercase id), so an IDE's "show help" opens the rule's explanation.
/// </summary>
public static class HelpLinks
{
    /// <summary>The published documentation page of the consumer rules.</summary>
    public const string AnalyzersDoc = "https://github.com/no1ross/Kaleido/blob/master/docs/ANALYZERS.md";

    /// <summary>The help link of the rule <paramref name="id"/> (e.g. <c>KAL2001</c>).</summary>
    public static string For(string id) => $"{AnalyzersDoc}#{id.ToLowerInvariant()}";
}
