using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace RefactorMCP.Tests.Examples;

/// <summary>
/// EXAMPLES.md documents the command line names users type. Those names drift
/// every time a method is renamed, so they are checked against the dispatcher.
/// </summary>
public class DocumentedToolNamesTests
{
    private static readonly Regex FencedBlock = new(@"```[a-zA-Z]*\n(.*?)```", RegexOptions.Singleline);

    private static readonly Regex[] NamePatterns =
    {
        new("\"tool\"\\s*:\\s*\"([^\"]+)\""),
        new(@"--json\s+([A-Za-z][\w-]*)"),
        new(@"--cli\s+([a-z][\w-]*)"),
    };

    [Fact]
    public void EveryToolNameInExamplesResolves()
    {
        var examplesPath = Path.Combine(
            Path.GetDirectoryName(TestUtilities.GetSolutionPath())!, "EXAMPLES.md");
        Assert.True(File.Exists(examplesPath), $"EXAMPLES.md not found at {examplesPath}");

        var documented = ExtractToolNames(File.ReadAllText(examplesPath)).ToList();
        Assert.NotEmpty(documented);

        var unresolved = documented.Where(name => ToolDispatcher.Default.Resolve(name) is null).ToList();

        Assert.True(
            unresolved.Count == 0,
            "EXAMPLES.md documents tools the dispatcher cannot resolve: " + string.Join(", ", unresolved));
    }

    [Fact]
    public void ExamplesDocumentTheWholeToolSurface()
    {
        var examplesPath = Path.Combine(
            Path.GetDirectoryName(TestUtilities.GetSolutionPath())!, "EXAMPLES.md");
        var documented = ExtractToolNames(File.ReadAllText(examplesPath))
            .Select(ToolDispatcher.NormalizeName)
            .ToHashSet(StringComparer.Ordinal);

        // Tools intentionally left out of the examples: session plumbing and
        // listing commands are described in README.md instead.
        var undocumentedTools = ToolDispatcher.Default.ListTools()
            .Where(tool => !tool.IsPrompt)
            .Select(tool => tool.Name)
            .Where(name => !documented.Contains(ToolDispatcher.NormalizeName(name)))
            .ToList();

        Assert.True(
            undocumentedTools.Count == 0,
            "Tools missing from EXAMPLES.md: " + string.Join(", ", undocumentedTools));
    }

    private static IEnumerable<string> ExtractToolNames(string markdown)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match block in FencedBlock.Matches(markdown))
        {
            foreach (var pattern in NamePatterns)
            {
                foreach (Match match in pattern.Matches(block.Groups[1].Value))
                    names.Add(match.Groups[1].Value);
            }
        }

        return names;
    }
}
