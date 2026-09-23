using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Sdk;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// Runs every fixture under <c>Catalog/</c>. Each case copies its
/// <c>before/</c> files into a generated solution, applies its steps through
/// <see cref="CatalogAdapter"/>, then checks the result compiles and matches
/// <c>after/</c>, or that the expected error was reported and nothing changed.
///
/// Set <c>CATALOG_UPDATE=1</c> to rewrite <c>after/</c> from the actual output
/// of every success case, for review as a diff.
/// </summary>
public class CatalogTests
{
    private static readonly string CatalogRoot =
        Path.Combine(Path.GetDirectoryName(TestUtilities.GetSolutionPath())!, "Catalog");

    private static bool UpdateMode => Environment.GetEnvironmentVariable("CATALOG_UPDATE") == "1";

    /// <summary>
    /// Every case, or those whose id starts with <c>CATALOG_FILTER</c>, such as
    /// <c>primitives/extract-method</c>. Several prefixes may be separated by commas.
    /// </summary>
    public static IEnumerable<object[]> Cases()
    {
        var prefixes = (Environment.GetEnvironmentVariable("CATALOG_FILTER") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return CatalogCase.Discover(CatalogRoot)
            .Where(id => prefixes.Length == 0 || prefixes.Any(p => id.StartsWith(p, StringComparison.Ordinal)))
            .Select(id => new object[] { id });
    }

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public async Task Case(string id)
    {
        var catalogCase = CatalogCase.Load(CatalogRoot, id);
        using var workspace = await CatalogWorkspace.CreateAsync(catalogCase);
        var loaded = await RefactoringHelpers.GetOrLoadSolution(workspace.SolutionPath);

        // A fixture that does not compile tests nothing, implemented or not.
        var beforeDiagnostics = await workspace.CompileAsync(loaded);
        var beforeErrors = beforeDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(beforeErrors.Count == 0, $"before/ does not compile:\n{Describe(beforeErrors)}");

        if (!catalogCase.IsUnimplemented)
        {
            await Run(catalogCase, workspace, loaded, beforeDiagnostics);
            return;
        }

        try
        {
            await Run(catalogCase, workspace, loaded, beforeDiagnostics);
        }
        catch (Exception ex) when (ex is XunitException or NotSupportedException or InvalidOperationException)
        {
            throw new Xunit.SkipException($"unimplemented: {FirstLine(ex.Message)}");
        }

        Assert.Fail($"{id} is marked unimplemented but passes. Remove \"status\" from its case.json.");
    }

    private static async Task Run(
        CatalogCase catalogCase,
        CatalogWorkspace workspace,
        Solution loaded,
        IReadOnlyList<Diagnostic> beforeDiagnostics)
    {
        var steps = catalogCase.Steps;
        string? error = null;
        string? failedStep = null;

        for (var i = 0; i < steps.Count; i++)
        {
            var context = new StepContext(steps[i], workspace, isFirstStep: i == 0);
            var (tool, arguments) = await CatalogAdapter.TranslateAsync(context);
            var result = await ToolDispatcher.Default.InvokeAsync(tool, arguments);
            if (result.IsError)
            {
                error = result.Text;
                failedStep = steps.Count == 1 ? steps[i].Refactoring : $"step {i + 1} ({steps[i].Refactoring})";
                break;
            }
        }

        if (catalogCase.ExpectsError)
        {
            AssertExpectedError(catalogCase, error, failedStep);
            AssertUnchanged(catalogCase, workspace);
            return;
        }

        Assert.True(error is null, $"{failedStep} failed: {error}");

        if (UpdateMode)
            WriteAfter(catalogCase, workspace);

        var afterDiagnostics = await workspace.CompileAsync(loaded);
        AssertNoNewDiagnostics(beforeDiagnostics, afterDiagnostics);
        AssertMatchesAfter(catalogCase, workspace);
    }

    private static void AssertExpectedError(CatalogCase catalogCase, string? error, string? failedStep)
    {
        var definition = catalogCase.Definition;
        Assert.True(error is not null, "expected an error, but every step succeeded");

        if (definition.ErrorCode is not null)
        {
            // The last step is the one a composite expects to refuse.
            var refactoring = catalogCase.Steps[^1].Refactoring;
            var fragment = CatalogAdapter.MessageFor(refactoring, definition.ErrorCode);
            Assert.True(
                error!.Contains(fragment, StringComparison.Ordinal),
                $"expected error '{definition.ErrorCode}' from {failedStep}, got: {error}");
        }

        if (definition.ErrorContains is not null)
        {
            Assert.True(
                error!.Contains(definition.ErrorContains, StringComparison.Ordinal),
                $"expected the error to contain '{definition.ErrorContains}', got: {error}");
        }
    }

    /// <summary>A refused refactoring leaves every file as <c>before/</c> had it, markers aside.</summary>
    private static void AssertUnchanged(CatalogCase catalogCase, CatalogWorkspace workspace)
    {
        var expected = CatalogWorkspace.SourceFiles(catalogCase.BeforeDirectory)
            .ToDictionary(
                relative => relative,
                relative => workspace.MarkersFor(relative).Text,
                StringComparer.Ordinal);

        AssertSameFiles(expected, workspace.ReadSources(), "before/ (the refusal should change nothing)");
    }

    private static void AssertMatchesAfter(CatalogCase catalogCase, CatalogWorkspace workspace)
    {
        var expected = CatalogWorkspace.SourceFiles(catalogCase.AfterDirectory)
            .ToDictionary(
                relative => relative,
                relative => File.ReadAllText(Path.Combine(catalogCase.AfterDirectory, relative)),
                StringComparer.Ordinal);

        AssertSameFiles(expected, workspace.ReadSources(), "after/");
    }

    private static void AssertSameFiles(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual,
        string expectedName)
    {
        var missing = expected.Keys.Except(actual.Keys).ToList();
        var unexpected = actual.Keys.Except(expected.Keys).ToList();
        Assert.True(missing.Count == 0, $"files in {expectedName} that do not exist afterwards: {string.Join(", ", missing)}");
        Assert.True(unexpected.Count == 0, $"files that exist afterwards but not in {expectedName}: {string.Join(", ", unexpected)}");

        foreach (var (path, expectedText) in expected)
        {
            var difference = FirstDifference(Normalize(expectedText), Normalize(actual[path]));
            Assert.True(difference is null, $"{path} differs from {expectedName}\n{difference}");
        }
    }

    /// <summary>
    /// The refactoring must not introduce errors, nor more warnings of any
    /// kind than <c>before/</c> already had.
    /// </summary>
    private static void AssertNoNewDiagnostics(IReadOnlyList<Diagnostic> before, IReadOnlyList<Diagnostic> after)
    {
        var errors = after.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, $"the result does not compile:\n{Describe(errors)}");

        var beforeCounts = before.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Count());
        var added = after
            .GroupBy(d => d.Id)
            .Where(g => g.Count() > beforeCounts.GetValueOrDefault(g.Key))
            .SelectMany(g => g)
            .ToList();
        Assert.True(added.Count == 0, $"the result has warnings before/ did not:\n{Describe(added)}");
    }

    private static void WriteAfter(CatalogCase catalogCase, CatalogWorkspace workspace)
    {
        if (Directory.Exists(catalogCase.AfterDirectory))
            Directory.Delete(catalogCase.AfterDirectory, recursive: true);

        foreach (var (relative, text) in workspace.ReadSources())
        {
            var destination = Path.Combine(catalogCase.AfterDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, text);
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    /// <summary>The first differing line with a little context, or null when the texts match.</summary>
    private static string? FirstDifference(string expected, string actual)
    {
        if (expected == actual)
            return null;

        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var line = 0;
        while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
            line++;

        var from = Math.Max(0, line - 2);
        var report = new StringBuilder();
        report.AppendLine($"first difference at line {line + 1}");
        report.AppendLine("expected:");
        foreach (var text in expectedLines.Skip(from).Take(6))
            report.AppendLine($"  |{text}");
        report.AppendLine("actual:");
        foreach (var text in actualLines.Skip(from).Take(6))
            report.AppendLine($"  |{text}");
        report.AppendLine("full actual:");
        report.Append(actual);
        return report.ToString();
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("\n", diagnostics.Select(d => $"  {d.Location.GetLineSpan()}: {d.Id} {d.GetMessage()}"));

    private static string FirstLine(string text) => text.Split('\n')[0];
}
