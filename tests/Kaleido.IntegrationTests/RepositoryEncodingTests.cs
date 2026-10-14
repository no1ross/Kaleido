using System.Text;
using System.Text.RegularExpressions;

namespace Kaleido.IntegrationTests;

public sealed partial class RepositoryEncodingTests
{
    private static readonly string[] ScannedRoots = ["src", "tools", "tests", "docs", "samples"];

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".angular", "TestResults", "artifacts", "coverage",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".md", ".txt", ".json", ".yml", ".yaml", ".xml", ".props", ".targets", ".csproj", ".slnx",
        ".editorconfig", ".http", ".ts", ".html", ".scss", ".css", ".razor", ".ps1", ".cmd", ".sh",
    };

    [Fact]
    public void RepositoryTextFiles_ContainNoReplacementCharactersOrMojibake()
    {
        var root = FindRepositoryRoot();
        var findings = new List<string>();

        foreach (var file in EnumerateTextFiles(root))
        {
            var lines = File.ReadAllLines(file, Encoding.UTF8);
            for (var index = 0; index < lines.Length; index++)
            {
                if (Corruption().IsMatch(lines[index]))
                {
                    findings.Add($"{Path.GetRelativePath(root, file)}:{index + 1}");
                }
            }
        }

        Assert.True(
            findings.Count == 0,
            "Text files contain U+FFFD or mojibake (UTF-8 decoded as another code page); restore the original characters:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, findings));
    }

    [GeneratedRegex("\uFFFD|\u00E2\u20AC|\u00C3[\u0080-\u00BF]|\u00C2[\u0080-\u00BF]|\u0393\u00C7")]
    private static partial Regex Corruption();

    private static IEnumerable<string> EnumerateTextFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root).Where(IsText))
        {
            yield return file;
        }

        var pending = new Stack<string>(
            ScannedRoots.Select(x => Path.Combine(root, x)).Where(Directory.Exists));

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(directory).Where(IsText))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory)
                         .Where(x => !SkippedDirectories.Contains(Path.GetFileName(x))))
            {
                pending.Push(child);
            }
        }
    }

    private static bool IsText(string path) =>
        TextExtensions.Contains(Path.GetExtension(path)) ||
        TextExtensions.Contains(Path.GetFileName(path));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kaleido.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Kaleido.slnx was not found above '{AppContext.BaseDirectory}'.");
    }
}
