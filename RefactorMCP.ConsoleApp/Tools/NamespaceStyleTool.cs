using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.ComponentModel;
using System.Text;

/// <summary>
/// Switches a file between a namespace block and a file-scoped namespace. The
/// code inside moves a level of indentation out or in; everything else, down
/// to comment layout, stays as written. Lines inside a string literal that
/// spans lines are part of its value, so they are never shifted.
/// </summary>
[McpServerToolType]
public static class NamespaceStyleTool
{
    [McpServerTool, Description("Convert a file's namespace block to a file-scoped namespace declaration (C# 10 or later)")]
    public static async Task<string> ConvertToFileScopedNamespace(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var name = Path.GetFileName(filePath);

            var version = ((CSharpParseOptions)document.Project.ParseOptions!).LanguageVersion;
            if (version < LanguageVersion.CSharp10)
                throw new McpException($"Error: File-scoped namespaces need C# 10 or later; the project uses C# {version.ToDisplayString()}");

            var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().ToList();
            if (namespaces.Count == 0 || namespaces[0] is not NamespaceDeclarationSyntax block)
                throw new McpException($"Error: {name} has no namespace block to convert");
            if (namespaces.Count > 1)
                throw new McpException($"Error: {name} declares more than one namespace, and a file-scoped namespace must be the only one");
            if (root.Members.Count > 1)
                throw new McpException($"Error: {name} declares code outside the namespace, which a file-scoped namespace would move into it");

            var text = await document.GetTextAsync(cancellationToken);
            var eol = Eol(root);
            var bodyStart = text.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart).EndIncludingLineBreak;
            var closeLine = text.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart);
            var unit = IndentUnit(root, text, bodyStart, closeLine.Start);

            var result = new StringBuilder();
            result.Append(text.ToString(TextSpan.FromBounds(0, block.Name.Span.End))).Append(';').Append(eol).Append(eol);
            var lines = BodyLines(root, text, bodyStart, closeLine.Start)
                .SkipWhile(l => l.Text.Trim().Length == 0)
                .Select(l => l.Shiftable && l.Text.StartsWith(unit, StringComparison.Ordinal) ? l.Text[unit.Length..] : l.Text);
            foreach (var line in lines)
                result.Append(line);
            result.Append(text.ToString(TextSpan.FromBounds(closeLine.EndIncludingLineBreak, text.Length)));

            await ApplyAsync(solution, document, result.ToString(), cancellationToken);
            return $"Successfully converted {name} to a file-scoped namespace";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to a file-scoped namespace: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Convert a file-scoped namespace declaration to a namespace block")]
    public static async Task<string> ConvertToBlockNamespace(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var name = Path.GetFileName(filePath);
            var fileScoped = root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault()
                ?? throw new McpException($"Error: {name} has no file-scoped namespace to convert");

            var text = await document.GetTextAsync(cancellationToken);
            var eol = Eol(root);
            var bodyStart = text.Lines.GetLineFromPosition(fileScoped.SemicolonToken.SpanStart).EndIncludingLineBreak;
            var unit = IndentUnit(root, text, bodyStart, text.Length);

            var lines = BodyLines(root, text, bodyStart, text.Length)
                .SkipWhile(l => l.Text.Trim().Length == 0)
                .Reverse().SkipWhile(l => l.Text.Trim().Length == 0).Reverse()
                .ToList();

            var result = new StringBuilder();
            result.Append(text.ToString(TextSpan.FromBounds(0, fileScoped.SemicolonToken.SpanStart))).Append(eol);
            result.Append('{').Append(eol);
            foreach (var line in lines)
            {
                // A blank line stays empty, and a directive at column zero stays there.
                var indent = line.Shiftable && line.Text.Trim().Length > 0 && !line.Text.StartsWith('#');
                result.Append(indent ? unit + line.Text : line.Text);
            }

            if (lines.Count > 0 && !lines[^1].Text.EndsWith('\n'))
                result.Append(eol);
            result.Append('}').Append(eol);

            await ApplyAsync(solution, document, result.ToString(), cancellationToken);
            return $"Successfully converted {name} to a namespace block";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting to a namespace block: {ex.Message}", ex);
        }
    }

    /// <summary>A line of the namespace's contents, with its line break, and whether its indentation may change.</summary>
    private sealed record BodyLine(string Text, bool Shiftable);

    private static IEnumerable<BodyLine> BodyLines(SyntaxNode root, SourceText text, int start, int end)
    {
        foreach (var line in text.Lines.Where(l => l.Start >= start && l.Start < end))
        {
            // A line that starts inside a token, rather than in trivia, is
            // inside a string literal spanning lines.
            var token = root.FindToken(line.Start);
            var insideToken = token.SpanStart < line.Start && token.Span.End > line.Start;
            yield return new BodyLine(text.ToString(line.SpanIncludingLineBreak), !insideToken);
        }
    }

    /// <summary>
    /// One level of indentation as the file writes it: the leading whitespace
    /// of the least indented indented line, or four spaces.
    /// </summary>
    private static string IndentUnit(SyntaxNode root, SourceText text, int start, int end)
    {
        var indents = BodyLines(root, text, start, end)
            .Where(l => l.Shiftable && l.Text.Trim().Length > 0)
            .Select(l => l.Text[..(l.Text.Length - l.Text.TrimStart(' ', '\t').Length)])
            .Where(indent => indent.Length > 0)
            .ToList();

        return indents.Count == 0 ? "    " : indents.MinBy(i => i.Length)!;
    }

    private static string Eol(SyntaxNode root) => TypeRefactoringHelpers.EndOfLine(root).ToFullString();

    private static async Task ApplyAsync(Solution solution, Document document, string text, CancellationToken cancellationToken)
    {
        var changed = document.WithText(SourceText.From(text, Encoding.UTF8)).Project.Solution;
        await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
        await TypeRefactoringHelpers.ApplyAsync(solution, changed, cancellationToken);
    }
}
