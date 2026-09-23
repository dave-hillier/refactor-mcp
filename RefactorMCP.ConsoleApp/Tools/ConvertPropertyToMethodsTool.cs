using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Linq;

[McpServerToolType]
public static class ConvertPropertyToMethodsTool
{
    [McpServerTool, Description("Convert a property into Get and Set methods, giving an auto-property a backing field, and turn every read and write into a call")]
    public static async Task<string> ConvertPropertyToMethods(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file declaring the property")] string filePath,
        [Description("Name of the property to convert")] string propertyName)
    {
        try
        {
            var document = await FieldPropertyRefactoring.GetDocumentAsync(solutionPath, filePath);
            var property = await FieldPropertyRefactoring.FindPropertyAsync(document, propertyName);
            propertyName = property.Name;
            var declaration = await FieldPropertyRefactoring.DeclarationAsync<PropertyDeclarationSyntax>(property);
            CheckConvertible(property, declaration);

            var getterName = "Get" + propertyName;
            var setterName = "Set" + propertyName;
            var isAuto = declaration.AccessorList is { } list && list.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
            var fieldName = FieldPropertyRefactoring.FieldNameFor(propertyName);
            var names = isAuto ? new[] { getterName, setterName, fieldName } : new[] { getterName, setterName };
            foreach (var name in names)
            {
                if (FieldPropertyRefactoring.HasMemberNamed(property.ContainingType, name))
                    throw new McpException($"Error: The type already has a member named '{name}'");
            }

            var solution = document.Project.Solution;
            var propertyDocument = solution.GetDocument(declaration.SyntaxTree)!;
            var locations = (await SymbolFinder.FindReferencesAsync(property, solution))
                .SelectMany(r => r.Locations)
                .Where(l => !(l.Document.Id == propertyDocument.Id && declaration.Span.Contains(l.Location.SourceSpan)))
                .ToList();

            var uses = new List<(DocumentId Document, ExpressionSyntax Use, Func<ExpressionSyntax, ExpressionSyntax> Replace)>();
            foreach (var location in locations)
            {
                var root = (await location.Document.GetSyntaxRootAsync())!;
                var reference = FieldPropertyRefactoring.ReferenceExpression(root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true));
                var where = location.Location.GetLineSpan();
                var (use, replace) = Rewrite(reference, property, isAuto, getterName, setterName, fieldName, where);
                uses.Add((location.Document.Id, use, replace));
            }

            var changed = solution;
            foreach (var documentId in uses.Select(u => u.Document).Append(propertyDocument.Id).Distinct())
            {
                var editor = await DocumentEditor.CreateAsync(changed.GetDocument(documentId)!);
                foreach (var (_, use, replace) in uses.Where(u => u.Document == documentId))
                    editor.ReplaceNode(use, (node, _) => replace((ExpressionSyntax)node).WithTriviaFrom(node).WithAdditionalAnnotations(Formatter.Annotation));

                if (documentId == propertyDocument.Id)
                {
                    var type = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First();
                    var methods = Methods(declaration, property, isAuto, getterName, setterName, fieldName, FieldPropertyRefactoring.NewLine(type));
                    editor.InsertAfter(declaration, methods.Skip(1));
                    editor.ReplaceNode(declaration, methods[0]);
                    if (isAuto)
                        editor.ReplaceNode(type, (node, _) => FieldPropertyRefactoring.AddField((TypeDeclarationSyntax)node, BackingField(declaration, property, fieldName)));
                }
                changed = editor.GetChangedDocument().Project.Solution;
            }

            await FieldPropertyRefactoring.WriteChangesAsync(solution, changed);
            return $"Successfully converted property '{propertyName}' to methods";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting property to methods: {ex.Message}", ex);
        }
    }

    private static void CheckConvertible(IPropertySymbol property, PropertyDeclarationSyntax declaration)
    {
        var implementsInterface = property.ExplicitInterfaceImplementations.Any()
            || property.ContainingType.AllInterfaces
                .SelectMany(i => i.GetMembers())
                .Any(m => SymbolEqualityComparer.Default.Equals(property.ContainingType.FindImplementationForInterfaceMember(m), property));
        if (property.IsVirtual || property.IsAbstract || property.IsOverride || implementsInterface
            || property.ContainingType.TypeKind == TypeKind.Interface)
            throw new McpException($"Error: Property '{property.Name}' is virtual, abstract, an override or an interface member, so its hierarchy would have to change too");

        if (declaration.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration)) == true)
            throw new McpException($"Error: Property '{property.Name}' has an init accessor, which no method can stand in for");
    }

    /// <summary>
    /// What replaces one use of the property. Reads call the getter; a write
    /// whose value is not used calls the setter, reading the old value
    /// first for compound assignments and increments. A get-only
    /// auto-property assigned by a constructor assigns its new field instead.
    /// The checks read the original use; the replacement is built from the
    /// use as other edits have left it, since a use can contain another.
    /// </summary>
    private static (ExpressionSyntax Use, Func<ExpressionSyntax, ExpressionSyntax> Replace) Rewrite(
        ExpressionSyntax reference,
        IPropertySymbol property,
        bool isAuto,
        string getterName,
        string setterName,
        string fieldName,
        FileLinePositionSpan where)
    {
        if (FieldPropertyRefactoring.IsInNameOf(reference))
            throw new McpException($"Error: '{property.Name}' is named by nameof at {where}, which would change the name it gives");

        switch (reference.Parent)
        {
            case AssignmentExpressionSyntax assignment when assignment.Left == reference:
                if (assignment.Parent is InitializerExpressionSyntax)
                    throw new McpException($"Error: '{property.Name}' is set in an object initializer at {where}, which cannot call a method");

                if (isAuto && property.SetMethod is null)
                    return (reference, current => WithName(current, fieldName));

                if (!IsValueUnused(assignment))
                    throw new McpException($"Error: The value of an assignment to '{property.Name}' is used at {where}, which a call to {setterName} cannot give");

                if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                {
                    return (assignment, current =>
                    {
                        var written = (AssignmentExpressionSyntax)current;
                        return Call(written.Left, setterName, written.Right.WithoutTrivia());
                    });
                }

                var operation = BinaryKindFor(assignment.Kind())
                    ?? throw new McpException($"Error: '{assignment.OperatorToken.ValueText}' on '{property.Name}' at {where} cannot be written as a call to {setterName}");
                EnsureReadOnce(reference, property, where);
                return (assignment, current =>
                {
                    var written = (AssignmentExpressionSyntax)current;
                    var combined = SyntaxFactory.BinaryExpression(operation, Call(written.Left, getterName), Parenthesized(written.Right.WithoutTrivia()));
                    return Call(written.Left, setterName, combined);
                });

            case PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax when FieldPropertyRefactoring.IsWrite(reference):
                var increment = (ExpressionSyntax)reference.Parent!;
                if (!IsValueUnused(increment))
                    throw new McpException($"Error: The value of an increment of '{property.Name}' is used at {where}, which a call to {setterName} cannot give");

                EnsureReadOnce(reference, property, where);
                var step = increment.IsKind(SyntaxKind.PostIncrementExpression) || increment.IsKind(SyntaxKind.PreIncrementExpression)
                    ? SyntaxKind.AddExpression
                    : SyntaxKind.SubtractExpression;
                return (increment, current =>
                {
                    var operand = current switch
                    {
                        PostfixUnaryExpressionSyntax postfix => postfix.Operand,
                        PrefixUnaryExpressionSyntax prefix => prefix.Operand,
                        _ => current,
                    };
                    var one = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1));
                    return Call(operand, setterName, SyntaxFactory.BinaryExpression(step, Call(operand, getterName), one));
                });

            default:
                return (reference, current => Call(current, getterName));
        }
    }

    /// <summary>
    /// True when nothing reads the value of the expression: it is a statement,
    /// a for-loop step, or the body of a member that returns nothing.
    /// </summary>
    private static bool IsValueUnused(ExpressionSyntax expression) => expression.Parent switch
    {
        ExpressionStatementSyntax => true,
        ForStatementSyntax loop => loop.Incrementors.Contains(expression) || loop.Initializers.Contains(expression),
        ArrowExpressionClauseSyntax arrow => arrow.Parent switch
        {
            MethodDeclarationSyntax method => method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword },
            LocalFunctionStatementSyntax function => function.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword },
            ConstructorDeclarationSyntax => true,
            AccessorDeclarationSyntax accessor => !accessor.IsKind(SyntaxKind.GetAccessorDeclaration),
            _ => false,
        },
        _ => false,
    };

    /// <summary>A compound write reads through the receiver as well as writing, so the receiver must be safe to evaluate twice.</summary>
    private static void EnsureReadOnce(ExpressionSyntax reference, IPropertySymbol property, FileLinePositionSpan where)
    {
        var receiver = reference is MemberAccessExpressionSyntax access ? access.Expression : null;
        if (receiver is not null && !IsPlainName(receiver))
            throw new McpException($"Error: Updating '{property.Name}' at {where} would evaluate '{receiver}' twice");
    }

    private static bool IsPlainName(ExpressionSyntax expression) => expression switch
    {
        ThisExpressionSyntax or BaseExpressionSyntax or IdentifierNameSyntax or PredefinedTypeSyntax => true,
        MemberAccessExpressionSyntax access => IsPlainName(access.Expression),
        _ => false,
    };

    private static SyntaxKind? BinaryKindFor(SyntaxKind compound) => compound switch
    {
        SyntaxKind.AddAssignmentExpression => SyntaxKind.AddExpression,
        SyntaxKind.SubtractAssignmentExpression => SyntaxKind.SubtractExpression,
        SyntaxKind.MultiplyAssignmentExpression => SyntaxKind.MultiplyExpression,
        SyntaxKind.DivideAssignmentExpression => SyntaxKind.DivideExpression,
        SyntaxKind.ModuloAssignmentExpression => SyntaxKind.ModuloExpression,
        SyntaxKind.AndAssignmentExpression => SyntaxKind.BitwiseAndExpression,
        SyntaxKind.OrAssignmentExpression => SyntaxKind.BitwiseOrExpression,
        SyntaxKind.ExclusiveOrAssignmentExpression => SyntaxKind.ExclusiveOrExpression,
        SyntaxKind.LeftShiftAssignmentExpression => SyntaxKind.LeftShiftExpression,
        SyntaxKind.RightShiftAssignmentExpression => SyntaxKind.RightShiftExpression,
        _ => null,
    };

    private static ExpressionSyntax Parenthesized(ExpressionSyntax expression) =>
        SyntaxFactory.ParenthesizedExpression(expression).WithAdditionalAnnotations(Simplifier.Annotation);

    /// <summary>The reference with its name replaced: <c>order.Quantity</c> becomes <c>order.GetQuantity</c>.</summary>
    private static ExpressionSyntax WithName(ExpressionSyntax reference, string name)
    {
        var identifier = SyntaxFactory.IdentifierName(name);
        return reference switch
        {
            MemberAccessExpressionSyntax access => access.WithName(identifier),
            MemberBindingExpressionSyntax binding => binding.WithName(identifier),
            _ => identifier,
        };
    }

    private static InvocationExpressionSyntax Call(ExpressionSyntax reference, string name, params ExpressionSyntax[] arguments) =>
        SyntaxFactory.InvocationExpression(
            WithName(reference.WithoutTrivia(), name),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(SyntaxFactory.Argument))));

    private static FieldDeclarationSyntax BackingField(PropertyDeclarationSyntax declaration, IPropertySymbol property, string fieldName)
    {
        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        if (property.IsStatic)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (property.SetMethod is null)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var variable = SyntaxFactory.VariableDeclarator(fieldName);
        if (declaration.Initializer is { } initializer)
            variable = variable.WithInitializer(SyntaxFactory.EqualsValueClause(initializer.Value.WithoutTrivia()));

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(declaration.Type.WithoutTrivia(), SyntaxFactory.SingletonSeparatedList(variable)))
            .WithModifiers(modifiers);
    }

    /// <summary>
    /// The getter, then the setter if there is one, in the property's place.
    /// The getter keeps the property's comments and attributes; each method
    /// takes the accessibility of its accessor where the accessor narrows it.
    /// </summary>
    private static List<MethodDeclarationSyntax> Methods(
        PropertyDeclarationSyntax declaration,
        IPropertySymbol property,
        bool isAuto,
        string getterName,
        string setterName,
        string fieldName,
        SyntaxTrivia newLine)
    {
        var accessors = declaration.AccessorList?.Accessors.ToList() ?? new List<AccessorDeclarationSyntax>();
        var getAccessor = accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        var setAccessor = accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        var indentation = FieldPropertyRefactoring.Indentation(declaration);
        var methods = new List<MethodDeclarationSyntax>();

        if (declaration.ExpressionBody is not null || getAccessor is not null)
        {
            var getter = SyntaxFactory.MethodDeclaration(declaration.Type.WithoutTrivia(), getterName)
                .WithAttributeLists(declaration.AttributeLists)
                .WithModifiers(Modifiers(declaration, getAccessor));
            getter = declaration.ExpressionBody is { } arrow
                ? WithExpression(getter, arrow.Expression)
                : WithBody(getter, getAccessor!, isAuto ? SyntaxFactory.IdentifierName(fieldName) : null);
            methods.Add(getter
                .WithLeadingTrivia(declaration.GetLeadingTrivia())
                .WithTrailingTrivia(newLine)
                .WithAdditionalAnnotations(Formatter.Annotation));
        }

        if (setAccessor is not null)
        {
            var setter = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), setterName)
                .WithModifiers(Modifiers(declaration, setAccessor))
                .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")).WithType(declaration.Type.WithoutTrivia()))));
            var assignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(fieldName), SyntaxFactory.IdentifierName("value"));
            setter = WithBody(setter, setAccessor, isAuto ? assignment : null);
            var leading = methods.Count == 0 ? declaration.GetLeadingTrivia() : SyntaxFactory.TriviaList(newLine).AddRange(indentation);
            methods.Add(setter
                .WithLeadingTrivia(leading)
                .WithTrailingTrivia(newLine)
                .WithAdditionalAnnotations(Formatter.Annotation));
        }

        methods[^1] = methods[^1].WithTrailingTrivia(declaration.GetTrailingTrivia());
        return methods;
    }

    /// <summary>The property's modifiers, with its accessibility replaced by the accessor's when the accessor has one.</summary>
    private static SyntaxTokenList Modifiers(PropertyDeclarationSyntax declaration, AccessorDeclarationSyntax? accessor)
    {
        var modifiers = declaration.Modifiers.Select(m => m.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.ElasticSpace)).ToList();
        if (accessor is { Modifiers.Count: > 0 })
        {
            var firstAccess = modifiers.FindIndex(IsAccessModifier);
            modifiers.RemoveAll(IsAccessModifier);
            modifiers.InsertRange(Math.Max(firstAccess, 0), accessor.Modifiers.Select(m => m.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.ElasticSpace)));
        }
        return SyntaxFactory.TokenList(modifiers);
    }

    private static bool IsAccessModifier(SyntaxToken modifier) =>
        modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.PrivateKeyword)
        || modifier.IsKind(SyntaxKind.ProtectedKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword);

    private static MethodDeclarationSyntax WithExpression(MethodDeclarationSyntax method, ExpressionSyntax expression) =>
        method
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(expression.WithoutTrivia()))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

    /// <summary>The accessor's body, block or expression, or the auto-property's field access when it has none.</summary>
    private static MethodDeclarationSyntax WithBody(MethodDeclarationSyntax method, AccessorDeclarationSyntax accessor, ExpressionSyntax? auto)
    {
        if (auto is not null)
            return WithExpression(method, auto);
        if (accessor.ExpressionBody is { } arrow)
            return WithExpression(method, arrow.Expression);

        // An accessor body written on one line, as `get { return _name; }`
        // often is, is laid out as a method body is.
        var body = accessor.Body!;
        var oneLine = !body.WithoutTrivia().DescendantTrivia().Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return method.WithBody(oneLine
            ? SyntaxFactory.Block(body.Statements.Select(s => s.WithoutTrivia()))
            : body.WithoutTrivia());
    }
}
