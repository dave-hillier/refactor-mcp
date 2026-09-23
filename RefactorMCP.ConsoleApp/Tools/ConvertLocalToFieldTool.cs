using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

[McpServerToolType]
public static class ConvertLocalToFieldTool
{
    [McpServerTool, Description("Promote a local variable to a private field of the containing type")]
    public static async Task<string> ConvertLocalToField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local's declaration or of a use of it (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        [Description("Name for the field (optional, defaults to the local's name)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");
            var fieldName = string.IsNullOrEmpty(name) ? target.Name : name;

            if (statement.UsingKeyword != default)
                throw new McpException($"Error: '{target.Name}' is a using declaration, which is disposed when the method ends");
            if (target.Declaration.Variables.Count > 1)
                throw new McpException("Error: The statement declares several locals; split it into one declaration per local first");
            if (target.Local.IsRef)
                throw new McpException($"Error: '{target.Name}' is a ref local, which a field cannot hold");
            if (target.SiblingStatements() is null)
                throw new McpException($"Error: The declaration of '{target.Name}' is not in a block");
            if (LocalVariableTarget.IsAnonymous(target.Local.Type))
                throw new McpException($"Error: '{target.Name}' has an anonymous type, which a field cannot be declared with");
            if (UsesMethodTypeParameter(target.Local.Type))
                throw new McpException($"Error: The type of '{target.Name}' uses a type parameter of the method, which a field cannot name");

            var type = statement.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()
                ?? throw new McpException($"Error: '{target.Name}' is not declared inside a type");
            EnsureNameIsFree(target, type, fieldName);

            var editor = await target.EditorAsync();
            AddField(editor, type, Field(target, fieldName));

            var initializer = target.Declarator.Initializer;
            if (statement.IsConst || initializer == null)
            {
                target.RemoveDeclarationStatement(editor);
            }
            else
            {
                // An array initializer is only allowed in a declaration.
                var value = initializer.Value is InitializerExpressionSyntax arrayInitializer && target.TypeSyntax() is ArrayTypeSyntax arrayType
                    ? SyntaxFactory.ArrayCreationExpression(arrayType, arrayInitializer)
                    : initializer.Value.WithoutTrivia();
                editor.ReplaceNode(statement, SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(fieldName),
                            value))
                    .WithTriviaFrom(statement)
                    .WithAdditionalAnnotations(Formatter.Annotation));
            }

            if (fieldName != target.Name)
            {
                foreach (var reference in target.References())
                    editor.ReplaceNode(reference, SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(reference));
            }

            await target.WriteAsync(editor);

            return $"Successfully converted local '{target.Name}' to field '{fieldName}' in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting local to field: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A private field of the local's type, static when the member using it is. A
    /// constant stays a constant. In a nullable context a reference type is nullable,
    /// since the field holds nothing until the member first assigns it.
    /// </summary>
    private static FieldDeclarationSyntax Field(LocalVariableTarget target, string fieldName)
    {
        var statement = target.DeclarationStatement!;
        var modifiers = new List<SyntaxToken> { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
        if (statement.IsConst)
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.ConstKeyword));
        else if (IsInStaticMember(target))
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        var fieldType = target.Local.Type;
        var nullableContext = target.Model.GetNullableContext(statement.SpanStart);
        if (!statement.IsConst &&
            nullableContext.AnnotationsEnabled() &&
            fieldType.IsReferenceType &&
            fieldType.NullableAnnotation != NullableAnnotation.Annotated)
        {
            fieldType = fieldType.WithNullableAnnotation(NullableAnnotation.Annotated);
        }

        var declarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(fieldName));
        if (statement.IsConst)
            declarator = declarator.WithInitializer(target.Declarator.Initializer!.WithoutTrivia());

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.ParseTypeName(fieldType.ToMinimalDisplayString(target.Model, statement.SpanStart)),
                    SyntaxFactory.SingletonSeparatedList(declarator)))
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>
    /// The field goes after the type's last field. A type without fields gets it as its
    /// first member, set apart from the member after it by a blank line.
    /// </summary>
    private static void AddField(SyntaxEditor editor, TypeDeclarationSyntax type, FieldDeclarationSyntax field)
    {
        var lastField = type.Members.OfType<FieldDeclarationSyntax>().LastOrDefault();
        if (lastField != null)
        {
            editor.InsertAfter(lastField, field.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
            return;
        }

        var first = type.Members.FirstOrDefault();
        if (first == null)
        {
            editor.ReplaceNode(type, (current, _) => ((TypeDeclarationSyntax)current).AddMembers(field));
            return;
        }

        editor.InsertBefore(first, field.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
        if (!first.GetLeadingTrivia().Any(SyntaxKind.EndOfLineTrivia))
        {
            editor.ReplaceNode(first, (current, _) =>
                current.WithLeadingTrivia(current.GetLeadingTrivia().Insert(0, SyntaxFactory.ElasticCarriageReturnLineFeed)));
        }
    }

    private static bool IsInStaticMember(LocalVariableTarget target)
    {
        var symbol = target.Local.ContainingSymbol;
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
            symbol = symbol.ContainingSymbol;
        return symbol.IsStatic;
    }

    private static bool UsesMethodTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol parameter => parameter.TypeParameterKind == TypeParameterKind.Method,
        IArrayTypeSymbol array => UsesMethodTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(UsesMethodTypeParameter),
        _ => false,
    };

    /// <summary>
    /// The field's name must not already name a member of the type or its bases, nor
    /// another local or parameter of the member, which would hide the field.
    /// </summary>
    private static void EnsureNameIsFree(LocalVariableTarget target, TypeDeclarationSyntax type, string fieldName)
    {
        var typeSymbol = target.Model.GetDeclaredSymbol(type)!;
        var member = target.Model.LookupSymbols(target.Declarator.SpanStart, typeSymbol, fieldName).FirstOrDefault();
        if (member != null)
            throw new McpException($"Error: '{typeSymbol.Name}' already has a member named '{fieldName}'");

        var hiding = target.EnclosingMember().DescendantNodes()
            .Any(n => n != target.Declarator && n switch
            {
                VariableDeclaratorSyntax v => v.Identifier.ValueText == fieldName,
                ParameterSyntax p => p.Identifier.ValueText == fieldName,
                SingleVariableDesignationSyntax d => d.Identifier.ValueText == fieldName,
                ForEachStatementSyntax f => f.Identifier.ValueText == fieldName,
                _ => false,
            });
        if (hiding)
            throw new McpException($"Error: '{fieldName}' is already declared in the member declaring '{target.Name}'");
    }
}
