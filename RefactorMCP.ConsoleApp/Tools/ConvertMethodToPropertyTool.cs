using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using System.Linq;

[McpServerToolType]
public static class ConvertMethodToPropertyTool
{
    [McpServerTool, Description("Convert a parameterless method that returns a value into a get-only property, with its overrides, and turn every call into a property read")]
    public static async Task<string> ConvertMethodToProperty(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method to convert; its parameterless overload is converted")] string methodName,
        [Description("Name for the property (optional, the method name without a leading 'Get' by default)")] string? propertyName = null)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var methods = await FieldPropertyRefactoring.FindMethodsAsync(document, methodName);
            var method = methods.FirstOrDefault(m => m.Parameters.Length == 0)
                ?? throw new McpException($"Error: Method '{methodName}' takes parameters, which a property cannot");
            methodName = method.Name;
            CheckConvertible(method);

            propertyName ??= PropertyNameFor(methodName);
            var solution = document.Project.Solution;
            var overrides = (await SymbolFinder.FindOverridesAsync(method, solution)).OfType<IMethodSymbol>().ToList();
            foreach (var declaring in overrides.Append(method))
            {
                var taken = declaring.ContainingType.GetMembers(propertyName)
                    .Any(m => !m.IsImplicitlyDeclared && !SymbolEqualityComparer.Default.Equals(m, declaring));
                if (taken)
                    throw new McpException($"Error: The type {declaring.ContainingType.Name} already has a member named '{propertyName}'");
            }

            var locations = new List<ReferenceLocation>();
            foreach (var declaring in overrides.Append(method))
                locations.AddRange((await SymbolFinder.FindReferencesAsync(declaring, solution)).SelectMany(r => r.Locations));

            var declarations = new List<MethodDeclarationSyntax>();
            foreach (var declaring in overrides.Append(method))
                declarations.Add(await FieldPropertyRefactoring.DeclarationAsync<MethodDeclarationSyntax>(declaring));

            var calls = new List<(DocumentId Document, InvocationExpressionSyntax Call)>();
            foreach (var location in locations.DistinctBy(l => (l.Document.Id, l.Location.SourceSpan)))
            {
                var root = (await location.Document.GetSyntaxRootAsync())!;
                var reference = FieldPropertyRefactoring.ReferenceExpression(root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true));
                if (reference.Parent is not InvocationExpressionSyntax call || call.Expression != reference)
                    throw new McpException($"Error: '{methodName}' is used without being called at {location.Location.GetLineSpan()}, which a property cannot be");
                calls.Add((location.Document.Id, call));
            }

            var changed = solution;
            var documentIds = calls.Select(c => c.Document).Concat(declarations.Select(d => solution.GetDocument(d.SyntaxTree)!.Id)).Distinct();
            foreach (var documentId in documentIds)
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(documentId)!);
                foreach (var (_, call) in calls.Where(c => c.Document == documentId))
                    editor.ReplaceNode(call, (node, _) => PropertyRead((InvocationExpressionSyntax)node, propertyName));
                foreach (var declaration in declarations.Where(d => d.SyntaxTree == editor.OriginalRoot.SyntaxTree))
                    editor.ReplaceNode(declaration, (node, _) => Property((MethodDeclarationSyntax)node, propertyName));
                changed = editor.GetChangedDocument().Project.Solution;
            }

            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully converted method '{methodName}' to property '{propertyName}'";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting method to property: {ex.Message}", ex);
        }
    }

    private static void CheckConvertible(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
            throw new McpException($"Error: Method '{method.Name}' returns void, and a property must return a value");
        if (method.IsGenericMethod)
            throw new McpException($"Error: Method '{method.Name}' is generic, and a property cannot have type parameters");
        if (method.IsAsync)
            throw new McpException($"Error: Method '{method.Name}' is async, and a property cannot be");
        if (method.IsOverride)
            throw new McpException($"Error: Method '{method.Name}' is an override; convert the method it overrides instead");

        var implementsInterface = method.ExplicitInterfaceImplementations.Any()
            || method.ContainingType.AllInterfaces
                .SelectMany(i => i.GetMembers())
                .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method));
        if (implementsInterface)
            throw new McpException($"Error: Method '{method.Name}' implements an interface member, which would no longer be implemented");
    }

    /// <summary><c>GetTotal</c> becomes <c>Total</c>; a name without the prefix is kept.</summary>
    private static string PropertyNameFor(string methodName) =>
        methodName.Length > 3 && methodName.StartsWith("Get", StringComparison.Ordinal) && char.IsUpper(methodName[3])
            ? methodName[3..]
            : methodName;

    /// <summary><c>order.GetTotal()</c> becomes <c>order.Total</c>, keeping the call's trivia.</summary>
    private static ExpressionSyntax PropertyRead(InvocationExpressionSyntax call, string propertyName)
    {
        var name = SyntaxFactory.IdentifierName(propertyName);
        ExpressionSyntax read = call.Expression switch
        {
            MemberAccessExpressionSyntax access => access.WithName(name.WithTriviaFrom(access.Name)),
            MemberBindingExpressionSyntax binding => binding.WithName(name.WithTriviaFrom(binding.Name)),
            var other => name.WithTriviaFrom(other),
        };
        return read.WithLeadingTrivia(call.GetLeadingTrivia()).WithTrailingTrivia(call.GetTrailingTrivia());
    }

    /// <summary>
    /// The method as a get-only property. An expression body, or a block that
    /// only returns a value, becomes an expression body; any other block
    /// becomes the get accessor; an abstract method becomes <c>{ get; }</c>.
    /// </summary>
    private static PropertyDeclarationSyntax Property(MethodDeclarationSyntax method, string propertyName)
    {
        var property = SyntaxFactory.PropertyDeclaration(method.ReturnType, SyntaxFactory.Identifier(propertyName))
            .WithAttributeLists(method.AttributeLists)
            .WithModifiers(method.Modifiers);
        var trailing = method.GetTrailingTrivia();

        var returned = method.Body is { Statements: [ReturnStatementSyntax { Expression: { } value }] } body
            && !body.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            ? value
            : method.ExpressionBody?.Expression;

        if (returned is not null)
        {
            return property
                .WithIdentifier(property.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
                    returned.WithoutTrivia()))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailing));
        }

        if (method.Body is null)
        {
            var parsed = (PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("int P { get; }")!;
            return property
                .WithIdentifier(property.Identifier.WithTrailingTrivia(SyntaxFactory.Space))
                .WithAccessorList(parsed.AccessorList!.WithoutTrivia().WithTrailingTrivia(trailing));
        }

        var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, method.Body.WithoutTrivia());
        return property
            .WithIdentifier(property.Identifier.WithoutTrivia())
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter))
                .WithTrailingTrivia(trailing)
                .WithAdditionalAnnotations(Formatter.Annotation));
    }
}
