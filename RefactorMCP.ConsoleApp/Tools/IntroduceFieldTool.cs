using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using System.Linq;

[McpServerToolType]
public static class IntroduceFieldTool
{
    [McpServerTool, Description("Introduce a new field from selected expression (preferred for large C# file refactoring). " +
        "A constant expression becomes a readonly field initialised where it is declared; any other expression is assigned to the field " +
        "just before the statement that uses it. With fieldType, adds a field of that type to the type containing the selection instead.")]
    public static async Task<string> IntroduceField(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Range in format 'startLine:startColumn-endLine:endColumn'")] string selectionRange,
        [Description("Name for the new field")] string fieldName,
        [Description("Access modifier (private, public, protected, internal)")] string accessModifier = "private",
        [Description("Type of a field to add to the type containing the selection, instead of introducing one from an expression (optional)")] string? fieldType = null)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => fieldType is null
                    ? IntroduceFieldWithSolution(doc, selectionRange, fieldName, accessModifier)
                    : AddFieldOfTypeWithSolution(doc, selectionRange, fieldName, accessModifier, fieldType),
                path => IntroduceFieldSingleFile(path, selectionRange, fieldName, accessModifier));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error introducing field: {ex.Message}", ex);
        }
    }

    private static async Task<string> IntroduceFieldWithSolution(Document document, string selectionRange, string fieldName, string accessModifier)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var span = TrimWhitespace(sourceText, RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange));

        var expression = SelectedExpression(syntaxRoot, span)
            ?? throw new McpException("Error: The selection is not an expression");
        if (FieldPropertyRefactoring.IsWrite(expression))
            throw new McpException("Error: The selected expression is assigned to, so it cannot be replaced by a field");

        var type = model.GetTypeInfo(expression).Type;
        if (type is null || type.SpecialType == SpecialType.System_Void || type.TypeKind == TypeKind.Error)
            throw new McpException("Error: The selected expression has no value to store in a field");

        if (UsesMethodTypeParameter(type))
            throw new McpException($"Error: The expression's type '{type.ToDisplayString()}' uses a type parameter of the method, which a field cannot name");

        var containingType = expression.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()
            ?? throw new McpException("Error: The expression is not inside a type");
        var typeSymbol = model.GetDeclaredSymbol(containingType)!;
        if (FieldPropertyRefactoring.HasMemberNamed(typeSymbol, fieldName))
            throw new McpException($"Error: The type already has a member named '{fieldName}'");

        var isStatic = model.GetEnclosingSymbol(expression.SpanStart)?.IsStatic == true;
        var isConstant = model.GetConstantValue(expression).HasValue;

        var editor = await DocumentEditor.CreateAsync(document);
        var reference = SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(expression);

        if (isConstant)
        {
            editor.ReplaceNode(expression, reference);
        }
        else
        {
            var statement = StatementToAssignBefore(expression);
            var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(fieldName),
                        expression.WithoutTrivia()))
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(FieldPropertyRefactoring.NewLine(statement))
                .WithAdditionalAnnotations(Formatter.Annotation);

            // The assignment and the statement together do what the statement
            // did, so the comments above it now sit above the pair.
            var rewritten = statement
                .ReplaceNode(expression, reference)
                .WithLeadingTrivia(FieldPropertyRefactoring.Indentation(statement));
            editor.InsertBefore(statement, assignment);
            editor.ReplaceNode(statement, rewritten);
        }

        var field = FieldDeclaration(
            FieldTypeSyntax(type, model, expression.SpanStart, assignedInMember: !isConstant),
            fieldName,
            accessModifier,
            isStatic,
            isReadOnly: isConstant,
            initializer: isConstant ? expression.WithoutTrivia() : null);
        editor.ReplaceNode(containingType, (node, _) => FieldPropertyRefactoring.AddField((TypeDeclarationSyntax)node, field));

        await FieldPropertyRefactoring.WriteChangesAsync(document.Project.Solution, editor.GetChangedDocument().Project.Solution);
        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {document.FilePath} (solution mode)";
    }

    /// <summary>
    /// Adds a field of a named type to the type containing the selection. The
    /// field holds a new instance when the type is a class that can be
    /// constructed without arguments, so code moved behind the field has an
    /// object to run against.
    /// </summary>
    private static async Task<string> AddFieldOfTypeWithSolution(Document document, string selectionRange, string fieldName, string accessModifier, string fieldType)
    {
        var sourceText = await document.GetTextAsync();
        var syntaxRoot = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var span = RefactoringHelpers.ParseSelectionRange(sourceText, selectionRange);

        var containingType = syntaxRoot.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault()
            ?? throw new McpException("Error: The selection is not inside a type");
        var typeSymbol = model.GetDeclaredSymbol(containingType)!;
        if (FieldPropertyRefactoring.HasMemberNamed(typeSymbol, fieldName))
            throw new McpException($"Error: The type already has a member named '{fieldName}'");

        var typeSyntax = SyntaxFactory.ParseTypeName(fieldType);
        var position = containingType.OpenBraceToken.Span.End;
        var fieldTypeSymbol = model.GetSpeculativeTypeInfo(position, typeSyntax, SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
        if (fieldTypeSymbol is null || fieldTypeSymbol.TypeKind == TypeKind.Error)
            throw new McpException($"Error: No type named '{fieldType}' is visible from {typeSymbol.Name}");

        var constructible = fieldTypeSymbol is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false } named
            && named.InstanceConstructors.Any(c => c.Parameters.Length == 0 && model.IsAccessible(position, c));
        var initializer = constructible
            ? SyntaxFactory.ObjectCreationExpression(typeSyntax).WithArgumentList(SyntaxFactory.ArgumentList())
            : null;

        var field = FieldDeclaration(typeSyntax, fieldName, accessModifier, isStatic: false, isReadOnly: constructible, initializer);
        var editor = await DocumentEditor.CreateAsync(document);
        editor.ReplaceNode(containingType, (node, _) => FieldPropertyRefactoring.AddField((TypeDeclarationSyntax)node, field));

        await FieldPropertyRefactoring.WriteChangesAsync(document.Project.Solution, editor.GetChangedDocument().Project.Solution);
        return $"Successfully added {accessModifier} field '{fieldName}' of type {fieldType} to {typeSymbol.Name} in {document.FilePath}";
    }

    private static TextSpan TrimWhitespace(SourceText text, TextSpan span)
    {
        var start = span.Start;
        var end = span.End;
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        return TextSpan.FromBounds(start, end);
    }

    /// <summary>The expression the selection covers exactly, or null when it covers anything else.</summary>
    private static ExpressionSyntax? SelectedExpression(SyntaxNode root, TextSpan span)
    {
        var node = root.FindNode(span, getInnermostNodeForTie: true);
        return node.AncestorsAndSelf()
            .TakeWhile(n => n.Span == span)
            .OfType<ExpressionSyntax>()
            .FirstOrDefault();
    }

    private static bool UsesMethodTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol parameter => parameter.TypeParameterKind == TypeParameterKind.Method,
        IArrayTypeSymbol array => UsesMethodTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(UsesMethodTypeParameter),
        _ => false,
    };

    /// <summary>
    /// The statement the field's assignment goes before. The statement must
    /// evaluate the expression exactly once each time it runs, or assigning
    /// the field ahead of it would change when, or whether, it is evaluated.
    /// </summary>
    private static StatementSyntax StatementToAssignBefore(ExpressionSyntax expression)
    {
        SyntaxNode child = expression;
        foreach (var ancestor in expression.Ancestors())
        {
            var conditional = ancestor switch
            {
                AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax => true,
                BinaryExpressionSyntax binary => binary.Right == child && (binary.IsKind(SyntaxKind.LogicalAndExpression)
                    || binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression)),
                AssignmentExpressionSyntax assignment => assignment.Right == child && assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression),
                ConditionalExpressionSyntax conditionalExpression => conditionalExpression.Condition != child,
                ConditionalAccessExpressionSyntax access => access.WhenNotNull == child,
                SwitchExpressionArmSyntax => true,
                WhileStatementSyntax or DoStatementSyntax => true,
                ForStatementSyntax loop => loop.Condition == child || loop.Incrementors.Contains(child),
                _ => false,
            };
            if (conditional)
                throw new McpException("Error: The expression may not be evaluated every time its statement runs, so it cannot be assigned to a field ahead of it");

            if (ancestor is StatementSyntax statement)
            {
                return statement.Parent is BlockSyntax
                    ? statement
                    : throw new McpException("Error: The statement containing the expression is not in a block, so there is nowhere to assign the field");
            }

            if (ancestor is ArrowExpressionClauseSyntax)
                throw new McpException("Error: The expression is in an expression-bodied member, which has no statement to assign the field before");

            if (ancestor is MemberDeclarationSyntax)
                break;

            child = ancestor;
        }

        throw new McpException("Error: The expression is not inside a method body");
    }

    /// <summary>
    /// The field's type as code at the expression would write it. A
    /// reference-type field assigned in a member rather than where it is
    /// declared is null until then, so in a nullable context it is declared
    /// nullable.
    /// </summary>
    private static TypeSyntax FieldTypeSyntax(ITypeSymbol type, SemanticModel model, int position, bool assignedInMember)
    {
        if (assignedInMember && type.IsReferenceType && model.GetNullableContext(position).AnnotationsEnabled())
            type = type.WithNullableAnnotation(NullableAnnotation.Annotated);

        var format = SymbolDisplayFormat.MinimallyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        return SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(model, position, format));
    }

    private static FieldDeclarationSyntax FieldDeclaration(
        TypeSyntax type,
        string fieldName,
        string accessModifier,
        bool isStatic,
        bool isReadOnly,
        ExpressionSyntax? initializer)
    {
        var modifiers = SyntaxFactory.TokenList(FieldPropertyRefactoring.AccessibilityToken(accessModifier));
        if (isStatic)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (isReadOnly)
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        var variable = SyntaxFactory.VariableDeclarator(fieldName);
        if (initializer is not null)
            variable = variable.WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

        return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(type, SyntaxFactory.SingletonSeparatedList(variable)))
            .WithModifiers(modifiers);
    }

    private static async Task<string> IntroduceFieldSingleFile(string filePath, string selectionRange, string fieldName, string accessModifier)
    {
        filePath = RefactoringHelpers.ResolvePath(filePath)!;

        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");

        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var model = await RefactoringHelpers.GetOrCreateSemanticModelAsync(filePath);
        var newText = IntroduceFieldInSource(sourceText, selectionRange, fieldName, accessModifier, model);
        if (newText.StartsWith("Error:"))
            return newText;

        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully introduced {accessModifier} field '{fieldName}' from {selectionRange} in {filePath} (single file mode)";
    }

    public static string IntroduceFieldInSource(string sourceText, string selectionRange, string fieldName, string accessModifier, SemanticModel? model = null)
    {
        var syntaxTree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = syntaxTree.GetRoot();
        var text = syntaxTree.GetText();
        var span = RefactoringHelpers.ParseSelectionRange(text, selectionRange);

        var selectedExpression = syntaxRoot.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .FirstOrDefault(e => span.Contains(e.Span) || e.Span.Contains(span));

        if (selectedExpression == null)
            throw new McpException("Error: Selected code is not a valid expression");

        var typeName = "var";
        if (model != null)
        {
            var typeInfo = model.GetTypeInfo(selectedExpression);
            if (typeInfo.Type != null)
                typeName = typeInfo.Type.ToDisplayString();
        }

        // Create the field declaration
        var accessModifierToken = accessModifier.ToLower() switch
        {
            "public" => SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            "protected" => SyntaxFactory.Token(SyntaxKind.ProtectedKeyword),
            "internal" => SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            _ => SyntaxFactory.Token(SyntaxKind.PrivateKeyword)
        };

        var fieldDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName(typeName))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(fieldName)
                .WithInitializer(SyntaxFactory.EqualsValueClause(selectedExpression)))))
            .WithModifiers(SyntaxFactory.TokenList(accessModifierToken));

        var fieldReference = SyntaxFactory.IdentifierName(fieldName);
        var containingClass = selectedExpression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null)
        {
            var exists = containingClass.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .Any(v => v.Identifier.ValueText == fieldName);
            if (exists)
                return $"Error: Field '{fieldName}' already exists";
        }
        var rewriter = new FieldIntroductionRewriter(selectedExpression, fieldReference, fieldDeclaration, containingClass);
        var newRoot = rewriter.Visit(syntaxRoot);

        var formattedRoot = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formattedRoot.ToFullString();
    }

}
