using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

[McpServerToolType]
public static class IntroduceUsingDeclarationTool
{
    [McpServerTool, Description("Convert a using statement that ends its block into a using declaration (C# 8), unwrapping its body")]
    public static async Task<string> IntroduceUsingDeclaration(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the using statement (1-based)")] int line,
        [Description("Column on that line inside the using statement (1-based)")] int column,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await CaretTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var top = target.Enclosing<UsingStatementSyntax>()
                ?? throw new McpException($"Error: {line}:{column} is not on a using statement");

            // Usings stacked without braces between them are converted together.
            while (top.Parent is UsingStatementSyntax outer && outer.Statement == top)
                top = outer;
            var chain = new List<UsingStatementSyntax> { top };
            while (chain[^1].Statement is UsingStatementSyntax inner)
                chain.Add(inner);

            if (((CSharpParseOptions)target.Root.SyntaxTree.Options).LanguageVersion < LanguageVersion.CSharp8)
                throw new McpException("Error: A using declaration needs C# 8 or later");
            if (chain.FirstOrDefault(u => u.Declaration is null) is { } expression)
                throw new McpException($"Error: 'using ({expression.Expression})' declares no variable, which a using declaration needs");
            if (top.Parent is SwitchSectionSyntax)
                throw new McpException("Error: The using statement is directly in a switch section, where a using declaration is not allowed");
            if (top.Parent is not BlockSyntax block)
                throw new McpException("Error: The using statement is not in a block, so a declaration cannot take its place");
            if (block.Statements.Last() != top)
                throw new McpException("Error: The using statement is not the last statement of its block, so its resource would be disposed after the statements that follow");

            var body = chain[^1].Statement is BlockSyntax innermost
                ? innermost.Statements.ToList()
                : new List<StatementSyntax> { chain[^1].Statement };

            var moved = chain.SelectMany(u => u.Declaration!.Variables.Select(v => v.Identifier.ValueText))
                .Concat(body.SelectMany(DeclaredNames));
            var elsewhere = block.Statements.Where(s => s != top).SelectMany(s => s.DescendantNodesAndSelf()).SelectMany(DeclaredNames).ToHashSet();
            if (moved.FirstOrDefault(elsewhere.Contains) is { } clash)
                throw new McpException($"Error: '{clash}' is already declared elsewhere in the block, which the using declaration's scope would now include");

            var endOfLine = TypeRefactoringHelpers.EndOfLine(target.Root);
            var outdent = body.Count == 0 ? 0 : Column(body[0]) - Column(top);
            var statements = chain.Select(u => (StatementSyntax)Declaration(u, endOfLine))
                .Concat(body.Select(s => Outdent(s, outdent)));

            var index = block.Statements.IndexOf(top);
            var updated = block.WithStatements(block.Statements.RemoveAt(index).InsertRange(index, statements));

            await target.ApplyAsync(target.Root.ReplaceNode(block, updated), cancellationToken);
            return $"Successfully converted the using statement to a using declaration in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing using declaration: {ex.Message}", ex);
        }
    }

    /// <summary><c>using (var x = e)</c> as <c>using var x = e;</c>, keeping what followed the parenthesis.</summary>
    private static LocalDeclarationStatementSyntax Declaration(UsingStatementSyntax statement, SyntaxTrivia endOfLine)
    {
        var trailing = statement.CloseParenToken.TrailingTrivia;
        if (!trailing.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)))
            trailing = trailing.Add(endOfLine);

        var awaitKeyword = statement.AwaitKeyword;
        var usingKeyword = statement.UsingKeyword.WithTrailingTrivia(SyntaxFactory.Space);
        if (awaitKeyword == default)
            usingKeyword = usingKeyword.WithLeadingTrivia(statement.GetLeadingTrivia());

        return SyntaxFactory.LocalDeclarationStatement(
            awaitKeyword,
            usingKeyword,
            default,
            statement.Declaration!.WithoutTrivia(),
            SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailing));
    }

    private static int Column(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Character;

    /// <summary>
    /// Moves a statement left by <paramref name="columns"/>, shortening the
    /// indentation that starts each of its lines. The formatter is not used, as
    /// it would align a comment on the first line with a comment ending the line
    /// before.
    /// </summary>
    private static StatementSyntax Outdent(StatementSyntax statement, int columns)
    {
        if (columns <= 0)
            return statement;

        return statement.ReplaceTokens(statement.DescendantTokens(), (token, _) =>
        {
            var trivia = token.LeadingTrivia.ToList();
            for (var i = 0; i < trivia.Count; i++)
            {
                if (trivia[i].IsKind(SyntaxKind.WhitespaceTrivia) && (i == 0 || trivia[i - 1].IsKind(SyntaxKind.EndOfLineTrivia)))
                {
                    var text = trivia[i].ToString();
                    trivia[i] = SyntaxFactory.Whitespace(text[Math.Min(columns, text.Length - text.TrimStart().Length)..]);
                }
            }

            return token.WithLeadingTrivia(trivia);
        });
    }

    /// <summary>The names a node declares as locals, local functions or parameters.</summary>
    private static IEnumerable<string> DeclaredNames(SyntaxNode node) => node switch
    {
        LocalDeclarationStatementSyntax local => local.Declaration.Variables.Select(v => v.Identifier.ValueText),
        LocalFunctionStatementSyntax function => new[] { function.Identifier.ValueText },
        VariableDeclaratorSyntax declarator => new[] { declarator.Identifier.ValueText },
        SingleVariableDesignationSyntax designation => new[] { designation.Identifier.ValueText },
        ForEachStatementSyntax forEach => new[] { forEach.Identifier.ValueText },
        ParameterSyntax parameter => new[] { parameter.Identifier.ValueText },
        CatchDeclarationSyntax { Identifier.ValueText: { Length: > 0 } name } => new[] { name },
        _ => Array.Empty<string>(),
    };
}
