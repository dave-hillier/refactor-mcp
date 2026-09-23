using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>One entry of the new parameter list given to change-signature.</summary>
public sealed class SignatureParameter
{
    [Description("Name of an existing parameter, or of a new one")]
    public string Name { get; set; } = "";

    [Description("Type of a new parameter; leave out for an existing one")]
    public string? Type { get; set; }

    [Description("Expression existing calls pass for a new parameter")]
    public string? Value { get; set; }

    [Description("Default value declared for a new parameter")]
    public string? Default { get; set; }
}

[McpServerToolType]
public static class ChangeSignatureTool
{
    [McpServerTool, Description("Add, remove and reorder the parameters of a method or constructor, updating every call, override and interface implementation in the solution")]
    public static async Task<string> ChangeSignature(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("The new parameter list in order. Existing parameters are given by name; a new one also gives its type and either the value existing calls pass or a default")] SignatureParameter[] parameters,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        [Description("For each removed parameter the body still uses, by name, the expression that takes the place of each use")] Dictionary<string, string>? replacements = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);

            var replaced = solution;
            if (replacements is { Count: > 0 })
            {
                var expressions = ReplacementExpressions(method, parameters, replacements);
                (replaced, method) = await ReplaceParameterUsesAsync(solution, method, expressions, cancellationToken);
            }

            var slots = Slots(method, parameters);
            var changed = await SignatureChange.ApplyAsync(replaced, method, slots, cancellationToken);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            var signature = string.Join(", ", parameters.Select(p => p.Name));
            return $"Successfully changed the signature of '{methodName}' to ({signature})";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error changing signature: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Replaces every use of the given parameters, by ordinal, in the body of each
    /// member of the method's family, so the parameters can then be removed. The
    /// expressions usually name a parameter the signature change is about to add, so
    /// the solution in between need not compile. Returns the method as found again in
    /// the edited solution.
    /// </summary>
    internal static async Task<(Solution Solution, IMethodSymbol Method)> ReplaceParameterUsesAsync(
        Solution solution,
        IMethodSymbol method,
        IReadOnlyDictionary<int, ExpressionSyntax> replacements,
        CancellationToken cancellationToken = default)
    {
        method = method.OriginalDefinition;
        var family = await MethodFamily.FindAsync(solution, method, cancellationToken);
        var edits = new Dictionary<DocumentId, Dictionary<SyntaxNode, SyntaxNode>>();

        foreach (var member in family)
        {
            foreach (var (ordinal, expression) in replacements)
            {
                var parameter = member.Parameters[ordinal];
                foreach (var use in await ParameterUsage.UsesAsync(solution, member, parameter, cancellationToken))
                {
                    if (LocalVariableTarget.IsWrite(use))
                        throw new McpException(
                            $"Error: Parameter '{parameter.Name}' is assigned in the body of '{member.ContainingType.Name}.{member.Name}', so its uses cannot be replaced");

                    var replacement = ExpressionFacts.IsSimple(expression)
                        ? expression
                        : SyntaxFactory.ParenthesizedExpression(expression);
                    var document = solution.GetDocument(use.SyntaxTree)!;
                    if (!edits.TryGetValue(document.Id, out var nodes))
                        edits[document.Id] = nodes = new Dictionary<SyntaxNode, SyntaxNode>();
                    nodes[use] = replacement.WithTriviaFrom(use);
                }
            }
        }

        var updated = solution;
        foreach (var (documentId, nodes) in edits)
        {
            var root = await updated.GetDocument(documentId)!.GetSyntaxRootAsync(cancellationToken);
            updated = updated.WithDocumentSyntaxRoot(documentId, root!.ReplaceNodes(nodes.Keys, (original, _) => nodes[original]));
        }

        var project = SolutionEdits.ProjectOf(solution, method);
        return (updated, await SolutionEdits.ResolveAsync(updated, project, method, cancellationToken));
    }

    /// <summary>The replacement for each removed parameter, by its ordinal in the old signature.</summary>
    private static Dictionary<int, ExpressionSyntax> ReplacementExpressions(
        IMethodSymbol method,
        IReadOnlyList<SignatureParameter> parameters,
        IReadOnlyDictionary<string, string> replacements)
    {
        var expressions = new Dictionary<int, ExpressionSyntax>();
        foreach (var (name, text) in replacements)
        {
            var parameter = SolutionEdits.FindParameter(method, name);
            if (parameters.Any(p => p.Name == name && p.Type is null))
                throw new McpException($"Error: Parameter '{name}' is kept, so its uses are not replaced");

            var expression = SyntaxFactory.ParseExpression(text);
            if (expression.ContainsDiagnostics || expression.ToString() != text.Trim())
                throw new McpException($"Error: '{text}' is not an expression to replace '{name}' with");

            expressions[parameter.Ordinal] = expression;
        }

        return expressions;
    }

    private static List<ParameterSlot> Slots(IMethodSymbol method, IReadOnlyList<SignatureParameter> parameters)
    {
        var slots = new List<ParameterSlot>();
        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in parameters)
        {
            var existing = method.Parameters.FirstOrDefault(p => p.Name == parameter.Name);
            if (existing is not null && parameter.Type is not null)
                throw new McpException($"Error: '{method.Name}' already has a parameter named '{parameter.Name}'");

            if (!named.Add(parameter.Name))
                throw new McpException($"Error: The new signature lists parameter '{parameter.Name}' more than once");

            if (existing is not null)
            {
                slots.Add(ParameterSlot.Existing(existing.Ordinal));
                continue;
            }

            if (parameter.Type is null)
                throw new McpException(
                    $"Error: '{method.Name}' has no parameter named '{parameter.Name}'; give a type to add it as a new parameter");

            if (parameter.Value is null && parameter.Default is null)
                throw new McpException(
                    $"Error: New parameter '{parameter.Name}' needs a value for existing calls or a default");

            var declaration = ParameterSlot.ParseDeclaration(parameter.Type, parameter.Name, parameter.Default);
            var value = parameter.Value is null ? null : SyntaxFactory.ParseExpression(parameter.Value);
            slots.Add(ParameterSlot.Added(declaration, _ => value));
        }

        return slots;
    }
}
