using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Kaleido.Analyzers.UnitTests;

// #302: every consumer rule links to its entry in docs/ANALYZERS.md, and the entry exists.
public sealed class HelpLinksTests
    : global::Kaleido.UnitTests.SutFixture
{
    public static TheoryData<string, string> ConsumerRules()
    {
        var rules = new TheoryData<string, string>();

        foreach (var descriptor in typeof(HelpLinks).Assembly.GetTypes()
                     .Where(x => !x.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(x))
                     .SelectMany(x => ((DiagnosticAnalyzer)Activator.CreateInstance(x)!).SupportedDiagnostics))
        {
            rules.Add(descriptor.Id, descriptor.HelpLinkUri);
        }

        return rules;
    }

    [Fact]
    public void For_IsTheRulesAnchorOnTheAnalyzersPage() =>
        Assert.Equal($"{HelpLinks.AnalyzersDoc}#kal2001", HelpLinks.For("KAL2001"));

    [Theory]
    [MemberData(nameof(ConsumerRules))]
    public void EveryConsumerRule_LinksToItsEntry_AndTheEntryExists(string id, string helpLink)
    {
        Assert.Equal(HelpLinks.For(id), helpLink);
        Assert.Contains($"<a id=\"{id.ToLowerInvariant()}\"></a>", AnalyzersDoc.Value, StringComparison.Ordinal);
    }

    // docs/ANALYZERS.md, found from the test output by walking up to the repository root.
    private static readonly Lazy<string> AnalyzersDoc = new(() =>
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "docs", "ANALYZERS.md");

            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException("docs/ANALYZERS.md was not found above the test output directory.");
    });
}
