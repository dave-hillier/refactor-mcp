using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

[McpServerToolType]
public static class ConstructorInjectionTool
{
    public readonly record struct MethodParameterPair(string MethodName, string ParameterName);

    [McpServerTool, Description("Convert method parameters to constructor injection (preferred for large C# file refactoring)")]
    public static async Task<string> ConvertToConstructorInjection(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Method and parameter pairs in the format Method:Parameter;...")] MethodParameterPair[] methodParameters,
        [Description("Use a public property instead of a private field")] bool useProperty = false)
    {
        try
        {
            return await RefactoringHelpers.RunWithSolutionOrFile(
                solutionPath,
                filePath,
                doc => ConvertWithSolution(doc, methodParameters, useProperty),
                path => ConvertSingleFile(path, methodParameters, useProperty));
        }
        catch (Exception ex)
        {
            throw new McpException($"Error performing constructor injection: {ex.Message}", ex);
        }
    }


    private static async Task<string> ConvertWithSolution(Document document, MethodParameterPair[] methodParameters, bool useProperty)
    {
        var sourceText = (await document.GetTextAsync()).ToString();
        var newText = ConvertInSource(sourceText, methodParameters, useProperty);
        if (newText.StartsWith("Error:"))
            return newText;

        var encoding = await RefactoringHelpers.GetFileEncodingAsync(document.FilePath!);
        await File.WriteAllTextAsync(document.FilePath!, newText, encoding);
        var newDoc = document.WithText(SourceText.From(newText, encoding));
        RefactoringHelpers.UpdateSolutionCache(newDoc);
        return $"Successfully injected parameters via constructor in {document.FilePath} (solution mode)";
    }

    private static async Task<string> ConvertSingleFile(string filePath, MethodParameterPair[] methodParameters, bool useProperty)
    {
        filePath = RefactoringHelpers.ResolvePath(filePath)!;

        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found");
        var (sourceText, encoding) = await RefactoringHelpers.ReadFileWithEncodingAsync(filePath);
        var newText = ConvertInSource(sourceText, methodParameters, useProperty);
        if (newText.StartsWith("Error:"))
            return newText;
        await File.WriteAllTextAsync(filePath, newText, encoding);
        RefactoringHelpers.UpdateFileCaches(filePath, newText);
        return $"Successfully injected parameters via constructor in {filePath} (single file mode)";
    }

    public static string ConvertInSource(string sourceText, MethodParameterPair[] methodParameters, bool useProperty)
    {
        var text = sourceText;
        foreach (var pair in methodParameters)
        {
            text = ConvertInSource(text, pair.MethodName, pair.ParameterName, useProperty);
            if (text.StartsWith("Error:"))
                return text;
        }
        return text;
    }

    public static string ConvertInSource(string sourceText, string methodName, string parameterName, bool useProperty, SemanticModel? model = null)
    {
        var tree = model?.SyntaxTree ?? CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return $"Error: Method '{methodName}' not found";
        var parameter = method.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.ValueText == parameterName);
        if (parameter == null)
            return $"Error: Parameter '{parameterName}' not found";
        var index = method.ParameterList.Parameters.IndexOf(parameter);
        var type = parameter.Type ?? SyntaxFactory.ParseTypeName("object");
        var fieldName = useProperty ? char.ToUpper(parameterName[0]) + parameterName.Substring(1) : "_" + parameterName;
        var rewriter = new ConstructorInjectionRewriter(methodName, parameterName, index, type, fieldName, useProperty);
        var newRoot = rewriter.Visit(root)!;
        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    [McpServerTool, Description("Turn an object a method constructs for itself into a dependency the constructor receives and keeps in a field, with every construction of the class passing a new one")]
    public static async Task<string> InjectConstructorDependency(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local holding the constructed object, on its declaration or a use (1-based)")] int line,
        [Description("Column of the local's name on that line (1-based)")] int column,
        [Description("Name of the constructor parameter; the local's name when left out")] string? parameterName = null,
        [Description("Name of the field; the local's name with a leading underscore when left out")] string? fieldName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await LocalVariableTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var dependency = Dependency.Of(target);
            parameterName ??= target.Name;
            fieldName ??= "_" + target.Name;
            var type = dependency.Class;
            if (target.Local.ContainingType.GetMembers(fieldName).Any())
                throw new McpException($"Error: '{type.Identifier.ValueText}' already has a member named '{fieldName}'");
            if (dependency.Constructor?.ParameterList.Parameters.Any(p => p.Identifier.ValueText == parameterName) == true)
                throw new McpException($"Error: The constructor already has a parameter named '{parameterName}'");

            // The method reads the field instead of constructing the object, and the
            // constructor assigns the field from its new parameter.
            var mark = new SyntaxAnnotation();
            var editor = await target.EditorAsync();
            foreach (var reference in target.References())
                editor.ReplaceNode(reference, SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(reference));
            target.RemoveDeclarationStatement(editor);
            editor.ReplaceNode(type, (current, _) => WithField(
                (ClassDeclarationSyntax)current,
                dependency.Constructor,
                SyntaxFactory.ParseTypeName(dependency.TypeName),
                fieldName,
                parameterName).WithAdditionalAnnotations(mark));

            var solution = target.Document.Project.Solution;
            var edited = target.Document.WithSyntaxRoot(editor.GetChangedRoot());
            var constructor = await ConstructorAsync(edited, mark, cancellationToken);

            var value = dependency.Construction(target.Local.Type);
            var slots = constructor.Parameters.Where(p => !p.IsOptional && !p.IsParams).Select(p => ParameterSlot.Existing(p.Ordinal))
                .Append(ParameterSlot.Added(ParameterSlot.ParseDeclaration(dependency.TypeName, parameterName, null), _ => value))
                .Concat(constructor.Parameters.Where(p => p.IsOptional || p.IsParams).Select(p => ParameterSlot.Existing(p.Ordinal)))
                .ToList();
            var changed = await SignatureChange.ApplyAsync(edited.Project.Solution, constructor, slots, cancellationToken);

            foreach (var id in changed.GetChanges(solution).GetProjectChanges().SelectMany(p => p.GetChangedDocuments()).ToList())
            {
                var document = await Simplifier.ReduceAsync(changed.GetDocument(id)!, Simplifier.Annotation, cancellationToken: cancellationToken);
                document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken);
                changed = document.Project.Solution;
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);
            return $"Successfully injected '{target.Name}' through the constructor of '{type.Identifier.ValueText}' as '{fieldName}'";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error injecting constructor dependency: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The local a method initialises by constructing an object, in an instance method
    /// of a class with at most one constructor of its own. The object's arguments
    /// must mean the same wherever the class is constructed, so they read no local,
    /// parameter or instance member, and constructing it has no other side effect.
    /// </summary>
    private sealed record Dependency(
        ClassDeclarationSyntax Class,
        ConstructorDeclarationSyntax? Constructor,
        BaseObjectCreationExpressionSyntax Creation,
        string TypeName)
    {
        public static Dependency Of(LocalVariableTarget target)
        {
            var statement = target.DeclarationStatement
                ?? throw new McpException($"Error: '{target.Name}' is not declared by a local declaration statement");
            if (target.Declaration.Variables.Count != 1 || statement.UsingKeyword != default || target.SiblingStatements() is null)
                throw new McpException($"Error: '{target.Name}' is not declared on its own in a block");
            if (target.Declarator.Initializer?.Value is not BaseObjectCreationExpressionSyntax creation)
                throw new McpException($"Error: '{target.Name}' is not initialised by constructing an object");
            if (target.EnclosingMember() is not MethodDeclarationSyntax method || method.Modifiers.Any(SyntaxKind.StaticKeyword)
                || method.Parent is not ClassDeclarationSyntax type || type.ParameterList != null)
                throw new McpException($"Error: '{target.Name}' is not in an instance method of a class without a primary constructor");

            var write = target.References().FirstOrDefault(LocalVariableTarget.IsWrite);
            if (write != null)
                throw new McpException($"Error: '{target.Name}' is assigned after its declaration, so it is not one object for the whole method");

            foreach (var argument in creation.ArgumentList?.Arguments ?? default)
            {
                var readsState = argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Select(n => target.Model.GetSymbolInfo(n).Symbol)
                    .Any(s => s is ILocalSymbol or IParameterSymbol || s is IFieldSymbol or IPropertySymbol or IMethodSymbol && !s.IsStatic);
                if (readsState || ExpressionFacts.HasSideEffects(argument.Expression) || argument.Expression is ThisExpressionSyntax)
                    throw new McpException(
                        $"Error: The argument '{argument}' depends on the method's state, so the constructor's callers cannot pass it");
            }

            if (creation.Initializer != null)
                throw new McpException($"Error: '{target.Name}' is constructed with an initializer, which its callers would have to repeat");

            var constructors = type.Members.OfType<ConstructorDeclarationSyntax>().Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword)).ToList();
            if (constructors.Count > 1)
                throw new McpException($"Error: '{type.Identifier.ValueText}' has several constructors, so it is not clear which should take the dependency");
            if (constructors.FirstOrDefault()?.Initializer is { RawKind: (int)SyntaxKind.ThisConstructorInitializer })
                throw new McpException($"Error: The constructor of '{type.Identifier.ValueText}' calls another with this(...)");

            var typeName = target.Local.Type.ToMinimalDisplayString(target.Model, target.Declarator.SpanStart);
            return new Dependency(type, constructors.FirstOrDefault(), creation, typeName);
        }

        /// <summary>The construction each caller passes, its type fully qualified for the simplifier to shorten there.</summary>
        public ExpressionSyntax Construction(ITypeSymbol type) =>
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithAdditionalAnnotations(Simplifier.Annotation),
                Creation.ArgumentList?.WithoutTrivia() ?? SyntaxFactory.ArgumentList(),
                null);
    }

    /// <summary>
    /// The class with a <c>private readonly</c> field after its other fields, and its
    /// constructor assigning the field from a parameter of that name. A class without
    /// a constructor of its own gets one after the field, as accessible as the one the
    /// compiler provided; the parameter itself is added by the signature change.
    /// </summary>
    private static ClassDeclarationSyntax WithField(
        ClassDeclarationSyntax type,
        ConstructorDeclarationSyntax? original,
        TypeSyntax fieldType,
        string fieldName,
        string parameterName)
    {
        var field = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(fieldType.WithTrailingTrivia(SyntaxFactory.Space))
                .AddVariables(SyntaxFactory.VariableDeclarator(fieldName)))
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        type = (ClassDeclarationSyntax)FieldPropertyRefactoring.AddField(type, field);

        var assignment = SyntaxFactory.ParseStatement($"{fieldName} = {parameterName};").WithAdditionalAnnotations(Formatter.Annotation);
        var constructor = type.Members.OfType<ConstructorDeclarationSyntax>().FirstOrDefault(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (original != null && constructor != null)
            return type.ReplaceNode(constructor, constructor.WithBody(constructor.Body!.AddStatements(assignment)));

        var accessibility = type.Modifiers.Any(SyntaxKind.AbstractKeyword) ? "protected" : "public";
        var text = $"{accessibility} {type.Identifier.ValueText}()\n{{\n    {fieldName} = {parameterName};\n}}";
        var newLine = TypeDeclarations.NewLine(type);
        var newConstructor = GeneratedMembers.Parse(text, GeneratedMembers.MemberIndentation(type), newLine);

        // A field that became the first member ends with the blank line that set it
        // apart from the next one; that blank line now follows the constructor.
        var lastField = type.Members.OfType<FieldDeclarationSyntax>().Last();
        var trailing = lastField.GetTrailingTrivia();
        if (trailing.Count(t => t.IsKind(SyntaxKind.EndOfLineTrivia)) > 1)
        {
            type = type.ReplaceNode(lastField, lastField.WithTrailingTrivia(newLine));
            lastField = type.Members.OfType<FieldDeclarationSyntax>().Last();
            newConstructor = newConstructor.WithTrailingTrivia(newConstructor.GetTrailingTrivia().Add(newLine));
        }

        return type.WithMembers(type.Members.Insert(type.Members.IndexOf(lastField) + 1, newConstructor));
    }

    private static async Task<IMethodSymbol> ConstructorAsync(Document document, SyntaxAnnotation mark, CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var type = (ClassDeclarationSyntax)root.GetAnnotatedNodes(mark).Single();
        var constructor = type.Members.OfType<ConstructorDeclarationSyntax>().First(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword));
        return model.GetDeclaredSymbol(constructor, cancellationToken)!;
    }
}

