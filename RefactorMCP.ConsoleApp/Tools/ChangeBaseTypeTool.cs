using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;

[McpServerToolType]
public static class ChangeBaseTypeTool
{
    // Errors that mean code converts the class to its old base, as opposed to
    // using a member the old base declared.
    private static readonly HashSet<string> ConversionErrors = new(StringComparer.Ordinal)
    {
        "CS1503", "CS0029", "CS0266", "CS0030", "CS1929", "CS0039",
    };

    [McpServerTool, Description("Set, replace or remove the base class of a class, refusing when code relies on the old base")]
    public static async Task<string> ChangeBaseType(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the class")] string filePath,
        [Description("Name of the class")] string className,
        [Description("The new base class; omit to remove the base class")] string? newBaseType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var (type, declaration) = await TypeRefactoringHelpers.FindTypeAsync(document, className, cancellationToken);
            if (type.TypeKind != TypeKind.Class)
                throw new McpException($"Error: {className} is a {type.TypeKind.ToString().ToLowerInvariant()}; only a class has a base class");

            var model = await document.GetSemanticModelAsync(cancellationToken);
            var current = declaration.BaseList?.Types
                .FirstOrDefault(t => model!.GetTypeInfo(t.Type, cancellationToken).Type?.TypeKind == TypeKind.Class);
            var oldBase = type.BaseType?.SpecialType == SpecialType.System_Object ? "object" : type.BaseType?.ToDisplayString();

            Document changed;
            string description;
            if (newBaseType is null)
            {
                if (current is null)
                    throw new McpException($"Error: {className} has no base class to remove");

                changed = document.WithSyntaxRoot(
                    (await document.GetSyntaxRootAsync(cancellationToken))!.ReplaceNode(declaration, WithoutBase(declaration, current)));
                description = $"removed the base class {oldBase} from {className}";
            }
            else
            {
                changed = await WithNewBaseAsync(document, declaration, current, type, newBaseType, cancellationToken);
                description = $"changed the base class of {className} to {newBaseType}";
            }

            changed = await WithoutNeedlessNewModifiersAsync(changed, className, cancellationToken);
            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                changed.Project.Solution,
                errors => errors.All(e => ConversionErrors.Contains(e.Id))
                    ? $"Error: Code relies on {className} converting to {oldBase}: {TypeRefactoringHelpers.Describe(errors)}"
                    : $"Error: {className} or code using it relies on members of {oldBase} the new base lacks: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Successfully {description}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error changing base type: {ex.Message}", ex);
        }
    }

    private static async Task<Document> WithNewBaseAsync(
        Document document,
        TypeDeclarationSyntax declaration,
        BaseTypeSyntax? current,
        INamedTypeSymbol type,
        string newBaseType,
        CancellationToken cancellationToken)
    {
        var annotation = new SyntaxAnnotation();
        var parsed = SyntaxFactory.ParseTypeName(newBaseType).WithAdditionalAnnotations(annotation);
        TypeDeclarationSyntax updated;
        if (current is not null)
        {
            updated = declaration.ReplaceNode(current.Type, parsed.WithTriviaFrom(current.Type));
        }
        else
        {
            // The new base class goes first, as C# requires, ahead of the interfaces.
            updated = TypeRefactoringHelpers.AddBaseType(declaration, parsed, first: true);
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var changed = document.WithSyntaxRoot(root!.ReplaceNode(declaration, updated));
        (changed, var resolved) = await TypeRefactoringHelpers.ResolveTypeAsync(changed, annotation, newBaseType, cancellationToken);

        var reason = resolved.TypeKind switch
        {
            TypeKind.Class when resolved.IsStatic => "it is static",
            TypeKind.Class when resolved.IsSealed => "it is sealed",
            TypeKind.Class => null,
            TypeKind.Interface => "it is an interface",
            var kind => $"it is a {kind.ToString().ToLowerInvariant()}",
        };
        if (reason is not null)
            throw new McpException($"Error: {resolved.ToDisplayString()} cannot be the base class of {type.Name}: {reason}");

        // Deriving from one of its own subclasses would make the class its own ancestor.
        var derived = await SymbolFinder.FindDerivedClassesAsync(type, document.Project.Solution, transitive: true, cancellationToken: cancellationToken);
        var baseDefinition = resolved.OriginalDefinition.ToDisplayString();
        if (derived.Any(d => d.OriginalDefinition.ToDisplayString() == baseDefinition))
            throw new McpException($"Error: {resolved.ToDisplayString()} already derives from {type.Name}, so it cannot be its base class");

        return changed;
    }

    /// <summary>
    /// Drops the <c>new</c> modifier from members of the class that the
    /// compiler reports no longer hide anything under the changed base class.
    /// </summary>
    private static async Task<Document> WithoutNeedlessNewModifiersAsync(Document document, string className, CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var members = model.GetDiagnostics(cancellationToken: cancellationToken)
            .Where(d => d.Id == "CS0109")
            .Select(d => root.FindNode(d.Location.SourceSpan).AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault())
            .OfType<MemberDeclarationSyntax>()
            .Where(m => m.Parent is TypeDeclarationSyntax type && type.Identifier.ValueText == className)
            .Distinct()
            .ToList();
        if (members.Count == 0)
            return document;

        return document.WithSyntaxRoot(root.ReplaceNodes(members, (_, member) =>
        {
            var index = member.Modifiers.IndexOf(SyntaxKind.NewKeyword);
            if (index < 0)
                return member;

            // The token after new takes over what came before it, such as indentation.
            var removed = member.Modifiers[index];
            var next = removed.GetNextToken();
            member = member.ReplaceToken(next, next.WithLeadingTrivia(removed.LeadingTrivia.AddRange(next.LeadingTrivia)));
            return member.WithModifiers(member.Modifiers.RemoveAt(index));
        }));
    }

    internal static TypeDeclarationSyntax WithoutBase(TypeDeclarationSyntax declaration, BaseTypeSyntax current)
    {
        var list = declaration.BaseList!;
        if (list.Types.Count > 1)
            return declaration.WithBaseList(list.WithTypes(list.Types.Remove(current)));

        // Without a base list the name is followed by whatever followed the list.
        var before = list.GetFirstToken().GetPreviousToken();
        var trailing = list.GetLastToken().TrailingTrivia;
        return declaration
            .ReplaceToken(before, before.WithTrailingTrivia(trailing))
            .WithBaseList(null);
    }
}
