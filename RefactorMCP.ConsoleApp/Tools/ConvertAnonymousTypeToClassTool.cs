using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class ConvertAnonymousTypeToClassTool
{
    [McpServerTool, Description("Replace an anonymous type with a named class that keeps its value equality and ToString, constructing it throughout the containing member")]
    public static async Task<string> ConvertAnonymousTypeToClass(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file containing the anonymous object creation")] string filePath,
        [Description("Line of the anonymous object creation (1-based)")] int line,
        [Description("Column of the anonymous object creation (1-based)")] int column,
        [Description("Name of the new class")] string className,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
                ?? throw new McpException($"Error: File {filePath} not found in solution");
            var root = (CompilationUnitSyntax)(await document.GetSyntaxRootAsync(cancellationToken))!;
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var text = await document.GetTextAsync(cancellationToken);

            var position = text.Lines[line - 1].Start + column - 1;
            var creation = root.FindToken(position).Parent?.AncestorsAndSelf().OfType<AnonymousObjectCreationExpressionSyntax>().FirstOrDefault()
                ?? throw new McpException($"Error: There is no anonymous object creation at {line}:{column}");

            var anonymous = (INamedTypeSymbol)model.GetTypeInfo(creation, cancellationToken).Type!;
            var properties = anonymous.GetMembers().OfType<IPropertySymbol>().ToList();
            foreach (var property in properties)
            {
                if (!IsNamable(property.Type))
                    throw new McpException(
                        $"Error: The property '{property.Name}' has a type the class cannot name, because it is anonymous or uses a type parameter");
            }

            var containingType = creation.Ancestors().OfType<BaseTypeDeclarationSyntax>().First();
            var containingMember = creation.Ancestors().OfType<MemberDeclarationSyntax>().First(m => m.Parent == containingType);
            if (model.LookupNamespacesAndTypes(containingType.SpanStart, name: className).Any())
                throw new McpException($"Error: A type named '{className}' is already visible where the class would be declared");

            var annotate = TypeDeclarations.AnnotationsEnabled(model, creation.SpanStart);
            var values = properties
                .Select(p => new GeneratedValue(p.Name, p.Type.ToMinimalDisplayString(model, creation.SpanStart)))
                .ToList();

            var creations = containingMember.DescendantNodes().OfType<AnonymousObjectCreationExpressionSyntax>()
                .Where(c => SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(c, cancellationToken).Type, anonymous))
                .ToList();

            var newLine = TypeDeclarations.NewLine(root);
            var nested = containingType.Parent is TypeDeclarationSyntax;
            var declaration = GeneratedMembers.Parse(
                ClassText(className, values, nested ? "private" : "internal", annotate),
                GeneratedMembers.Indentation(containingType.GetFirstToken()),
                newLine);

            root = root.TrackNodes(creations.Append<SyntaxNode>(containingType));
            var current = creations.Select(c => root.GetCurrentNode(c)!).ToList();
            root = root.ReplaceNodes(current, (_, rewritten) => Construction(rewritten, className));
            root = root.InsertNodesAfter(root.GetCurrentNode(containingType)!, new[] { declaration });
            root = TypeDeclarations.WithUsings(
                root,
                TypeDeclarations.MissingImports(
                    model,
                    containingType.SpanStart,
                    ("System", "HashCode"),
                    ("System.Collections.Generic", "EqualityComparer")));

            var changed = solution.WithDocumentSyntaxRoot(document.Id, root);
            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully replaced {creations.Count} anonymous object creation(s) with class '{className}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting anonymous type to class: {ex.Message}", ex);
        }
    }

    /// <summary>Whether a type can be written in source outside the method: not anonymous and free of type parameters.</summary>
    private static bool IsNamable(ITypeSymbol type) => type switch
    {
        { IsAnonymousType: true } or ITypeParameterSymbol => false,
        IArrayTypeSymbol array => IsNamable(array.ElementType),
        IPointerTypeSymbol pointer => IsNamable(pointer.PointedAtType),
        INamedTypeSymbol named => named.TypeArguments.All(IsNamable) && (named.ContainingType is null || IsNamable(named.ContainingType)),
        _ => true,
    };

    /// <summary><c>new Name(values)</c>, the values of explicit and projected members in order.</summary>
    private static ExpressionSyntax Construction(SyntaxNode node, string className)
    {
        var creation = (AnonymousObjectCreationExpressionSyntax)node;
        var arguments = creation.Initializers.Select(i => SyntaxFactory.Argument(i.Expression.WithoutTrivia()));
        return SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.IdentifierName(className),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)).NormalizeWhitespace(),
                null)
            .WithTriviaFrom(creation);
    }

    private static string ClassText(string className, IReadOnlyList<GeneratedValue> values, string accessibility, bool annotate)
    {
        var members = new List<string> { GeneratedMembers.Constructor(className, values) };
        members.AddRange(values.Select(v => GeneratedMembers.Property(v, "get;")));
        members.Add(GeneratedMembers.ObjectEquals(className, values, annotate));
        members.Add(GeneratedMembers.GetHashCodeMethod(values));
        members.Add(GeneratedMembers.ToStringMethod(null, values));

        var text = new StringBuilder();
        text.Append($"{accessibility} sealed class {className}\n{{\n");
        text.Append(string.Join("\n\n", members.Select(m => string.Join("\n", m.Split('\n').Select(l => l.Length == 0 ? l : "    " + l)))));
        text.Append("\n}");
        return text.ToString();
    }
}
