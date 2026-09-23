using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.ComponentModel;
using static HierarchyMemberHelpers;

[McpServerToolType]
public static class PullUpTool
{
    [McpServerTool, Description("Move a field into the base class, removing identical copies from the other subclasses")]
    public static async Task<string> PullUpField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the class declaring the field")] string className,
        [Description("Name of the field to pull up")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var target = await FindFieldAsync(document, className, fieldName, cancellationToken);
            var baseClass = await SourceBaseAsync(solution, target.ContainingType, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var map = TowardsBase(target.ContainingType);

            var used = SubclassOnlyUse(target.Field.Declaration.Type, model, target.ContainingType, target.Symbol, map)
                ?? SubclassOnlyUse(target.Variable, model, target.ContainingType, target.Symbol, map);
            if (used is not null)
                throw new McpException($"Error: {fieldName} uses {used}, which only {className} has");

            EnsureBaseLacks(target.ContainingType, fieldName, _ => true);

            var substituted = Substitute(target.Field, model, map);
            var moved = SingleVariable(substituted, substituted.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName));
            moved = WithModifiers(moved, WidenPrivate);

            var edits = new HierarchyEdits(solution);
            RemoveField(edits, document, target.Type, target.Field, target.Variable);
            edits.Replace(baseClass.Document, baseClass.Declaration, t => InsertMember(t, moved, TypeRefactoringHelpers.EndOfLine(t.SyntaxTree.GetRoot())));
            edits.Import(baseClass.Document, TypeRefactoringHelpers.NamespacesUsedBy(target.Field.Declaration.Type, model)
                .Concat(TypeRefactoringHelpers.NamespacesUsedBy(target.Variable, model)));

            var shape = FieldShape(target.Field, target.Variable, model, map);
            foreach (var sibling in await OtherSubclassesAsync(solution, baseClass, target.ContainingType, cancellationToken))
            {
                var siblingModel = (await sibling.Document.GetSemanticModelAsync(cancellationToken))!;
                var siblingMap = TowardsBase(sibling.Symbol);
                foreach (var member in sibling.Declaration.Members.Where(m => Declares(m, fieldName)))
                {
                    var same = member is FieldDeclarationSyntax field
                        && field.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName) is var variable
                        && field.Modifiers.Any(SyntaxKind.StaticKeyword) == target.Field.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && FieldShape(field, variable, siblingModel, siblingMap) == shape;
                    if (!same || !IsDirectSubclass(sibling.Symbol, baseClass.Symbol))
                        throw new McpException($"Error: {sibling.Symbol.Name} declares a different {fieldName}, which would hide the pulled-up field");

                    var siblingField = (FieldDeclarationSyntax)member;
                    RemoveField(edits, sibling.Document, sibling.Declaration, siblingField,
                        siblingField.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName));
                }
            }

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                await edits.ApplyAsync(cancellationToken),
                errors => $"Error: Pulling up {fieldName} would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Successfully pulled up {fieldName} from {className} to {baseClass.Symbol.Name}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error pulling up field: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Move a method into the base class, or declare it abstract there, removing identical copies from the other subclasses")]
    public static async Task<string> PullUpMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the class declaring the method")] string className,
        [Description("Name of the method to pull up")] string methodName,
        [Description("Declare the method abstract in the base class and override it in the subclasses, instead of moving its body")] bool makeAbstract = false,
        [Description("Line of the method's declaration, to choose between overloads (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var target = await FindMethodAsync(document, className, methodName, line, cancellationToken);
            var baseClass = await SourceBaseAsync(solution, target.ContainingType, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var map = TowardsBase(target.ContainingType);

            if (makeAbstract && !baseClass.Symbol.IsAbstract)
                throw new McpException($"Error: {baseClass.Symbol.Name} is not abstract, so it cannot declare an abstract method");

            SyntaxNode needed = makeAbstract ? target.Method.ParameterList : target.Method;
            var used = SubclassOnlyUse(needed, model, target.ContainingType, target.Symbol, map)
                ?? SubclassOnlyUse(target.Method.ReturnType, model, target.ContainingType, target.Symbol, map);
            if (used is not null)
                throw new McpException($"Error: {methodName} uses {used}, which only {className} has");

            EnsureBaseLacks(target.ContainingType, methodName, member => member is not IMethodSymbol method || SameParameters(method, target.Symbol));

            var edits = new HierarchyEdits(solution);
            var moved = Substitute(target.Method, model, map);
            var eol = TypeRefactoringHelpers.EndOfLine(target.Method.SyntaxTree.GetRoot());
            if (makeAbstract)
            {
                moved = AbstractDeclaration(moved);
                edits.Replace(document, target.Method, m => Override(m));
            }
            else
            {
                moved = WithModifiers(moved, WidenPrivate);
                edits.RemoveMember(document, target.Type, target.Method);
            }

            edits.Replace(baseClass.Document, baseClass.Declaration, t => InsertMember(t, moved, eol));
            edits.Import(baseClass.Document, TypeRefactoringHelpers.NamespacesUsedBy(makeAbstract ? target.Method.ParameterList : target.Method, model)
                .Concat(TypeRefactoringHelpers.NamespacesUsedBy(target.Method.ReturnType, model)));

            var shape = MethodShape(target.Method, model, map, withBody: !makeAbstract);
            foreach (var sibling in await OtherSubclassesAsync(solution, baseClass, target.ContainingType, cancellationToken))
            {
                var siblingModel = (await sibling.Document.GetSemanticModelAsync(cancellationToken))!;
                var siblingMap = TowardsBase(sibling.Symbol);
                var direct = IsDirectSubclass(sibling.Symbol, baseClass.Symbol);
                var matching = sibling.Declaration.Members.Where(m => Declares(m, methodName))
                    .Where(m => m is not MethodDeclarationSyntax other
                        || MethodShape(other, siblingModel, siblingMap, withBody: false) == MethodShape(target.Method, model, map, withBody: false))
                    .ToList();

                foreach (var member in matching)
                {
                    var same = member is MethodDeclarationSyntax other
                        && other.Modifiers.Any(SyntaxKind.StaticKeyword) == target.Method.Modifiers.Any(SyntaxKind.StaticKeyword)
                        && (makeAbstract || MethodShape(other, siblingModel, siblingMap, withBody: true) == shape);
                    if (!same || !direct)
                        throw new McpException($"Error: {sibling.Symbol.Name} declares a different {methodName}, which would hide the pulled-up method");

                    if (makeAbstract)
                        edits.Replace(sibling.Document, (MethodDeclarationSyntax)member, m => Override(m));
                    else
                        edits.RemoveMember(sibling.Document, sibling.Declaration, member);
                }

                if (makeAbstract && direct && matching.Count == 0 && !sibling.Symbol.IsAbstract)
                    throw new McpException($"Error: {sibling.Symbol.Name} does not implement {methodName}, which would be abstract");
            }

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                await edits.ApplyAsync(cancellationToken),
                errors => $"Error: Pulling up {methodName} would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return makeAbstract
                ? $"Successfully declared {methodName} abstract in {baseClass.Symbol.Name}"
                : $"Successfully pulled up {methodName} from {className} to {baseClass.Symbol.Name}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error pulling up method: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Refuses when the base class, or a class above it, already has a member
    /// the moved one would clash with or hide.
    /// </summary>
    private static void EnsureBaseLacks(INamedTypeSymbol subclass, string name, Func<ISymbol, bool> clashes)
    {
        for (var type = subclass.BaseType; type is not null && type.SpecialType != SpecialType.System_Object; type = type.BaseType)
        {
            if (type.GetMembers(name).Any(m => !m.IsImplicitlyDeclared && clashes(m)))
                throw new McpException($"Error: {type.Name} already has a member named {name}");
        }
    }

    private static bool SameParameters(IMethodSymbol left, IMethodSymbol right) =>
        left.Parameters.Length == right.Parameters.Length
        && left.Parameters.Zip(right.Parameters).All(p =>
            p.First.RefKind == p.Second.RefKind && SymbolEqualityComparer.Default.Equals(p.First.Type, p.Second.Type));

    /// <summary>
    /// The base class's other subclasses, direct or not, leaving out the class
    /// the member comes from and the classes below it.
    /// </summary>
    private static async Task<IReadOnlyList<SourceType>> OtherSubclassesAsync(
        Solution solution,
        SourceType baseClass,
        INamedTypeSymbol subclass,
        CancellationToken cancellationToken)
    {
        var all = await SubclassesAsync(solution, baseClass.Symbol, transitive: true, cancellationToken);
        return all
            .Where(s => !InheritsFrom(s.Symbol, subclass.OriginalDefinition))
            .ToList();
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol ancestor)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, ancestor))
                return true;
        }

        return false;
    }

    private static bool IsDirectSubclass(INamedTypeSymbol type, INamedTypeSymbol baseClass) =>
        SymbolEqualityComparer.Default.Equals(type.BaseType?.OriginalDefinition, baseClass.OriginalDefinition);

    private static bool Declares(MemberDeclarationSyntax member, string name) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Any(v => v.Identifier.ValueText == name),
        MethodDeclarationSyntax method => method.Identifier.ValueText == name,
        PropertyDeclarationSyntax property => property.Identifier.ValueText == name,
        EventDeclarationSyntax @event => @event.Identifier.ValueText == name,
        EventFieldDeclarationSyntax events => events.Declaration.Variables.Any(v => v.Identifier.ValueText == name),
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText == name,
        _ => false,
    };

    /// <summary>A field's type, name and initializer as the base class would read them.</summary>
    private static string FieldShape(FieldDeclarationSyntax field, VariableDeclaratorSyntax variable, SemanticModel model, IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map) =>
        Shape(Substitute(field.Declaration.Type, model, map)) + " " + Shape(Substitute(variable, model, map));

    /// <summary>A method's signature, and optionally its body, as the base class would read them.</summary>
    private static string MethodShape(MethodDeclarationSyntax method, SemanticModel model, IReadOnlyDictionary<ITypeParameterSymbol, TypeSyntax> map, bool withBody)
    {
        var parameterTypes = string.Join(",", method.ParameterList.Parameters.Select(p =>
            string.Join(" ", p.Modifiers.Select(m => m.Text)) + (p.Type is null ? "" : Shape(Substitute(p.Type, model, map)))));
        var signature = $"{method.TypeParameterList?.Parameters.Count ?? 0}({parameterTypes})";
        if (!withBody)
            return signature;

        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        return $"{Shape(Substitute(method.ReturnType, model, map))} {signature} {(body is null ? "" : Shape(Substitute(body, model, map)))}";
    }

    /// <summary>The abstract declaration a subclass's method overrides.</summary>
    private static MethodDeclarationSyntax AbstractDeclaration(MethodDeclarationSyntax method)
    {
        var declaration = WithModifiers(method, modifiers => WithModifier(
            WithoutModifiers(WidenPrivate(modifiers),
                SyntaxKind.VirtualKeyword, SyntaxKind.OverrideKeyword, SyntaxKind.NewKeyword,
                SyntaxKind.AsyncKeyword, SyntaxKind.SealedKeyword, SyntaxKind.ExternKeyword),
            SyntaxKind.AbstractKeyword));

        // The documentation stays with the implementation it describes.
        return declaration
            .WithAttributeLists(default)
            .WithParameterList(method.ParameterList.WithoutTrailingTrivia())
            .WithBody(null)
            .WithExpressionBody(null)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(method.GetTrailingTrivia()))
            .WithLeadingTrivia(method.GetLeadingTrivia().Where(t => !t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)));
    }

    /// <summary>A subclass's method made the override of the new abstract one.</summary>
    private static MethodDeclarationSyntax Override(MethodDeclarationSyntax method) =>
        method.Modifiers.Any(SyntaxKind.OverrideKeyword)
            ? method
            : WithModifiers(method, modifiers => WithModifier(
                WithoutModifiers(WidenPrivate(modifiers), SyntaxKind.VirtualKeyword, SyntaxKind.NewKeyword),
                SyntaxKind.OverrideKeyword));
}
