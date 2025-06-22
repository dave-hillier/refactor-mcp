using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;

[McpServerToolType, McpServerPromptType]
public static class ClassLengthMetricsTool
{
    [McpServerPrompt, Description("List all classes in the solution with their line counts")]
    public static async Task<string> ListClassLengths(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var classes = new List<(string name, int lines)>();
            foreach (var doc in solution.Projects.SelectMany(p => p.Documents))
            {
                var tree = await doc.GetSyntaxTreeAsync(cancellationToken);
                if (tree == null) continue;
                var root = await tree.GetRootAsync(cancellationToken);
                foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var span = tree.GetLineSpan(cls.Span);
                    var lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
                    classes.Add((cls.Identifier.Text, lines));
                }
            }
            if (classes.Count == 0)
                return "No classes found";

            var ordered = classes.OrderByDescending(c => c.lines)
                .Select(c => $"{c.name} - {c.lines} lines");
            return "Class lengths:\n" + string.Join("\n", ordered);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error analyzing classes: {ex.Message}", ex);
        }
    }
}
