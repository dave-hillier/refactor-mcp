using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Threading;

[McpServerToolType]
public static class ConvertMethodToLocalFunctionTool
{
    [McpServerTool, Description("Move a private method used by only one member into that member as a local function")]
    public static async Task<string> ConvertMethodToLocalFunction(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the method")] string methodName,
        [Description("Line of the method's declaration, to choose between overloads (1-based, optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath)
                ?? throw new McpException($"Error: File {filePath} not found in solution");
            var method = await FindMethod(document, methodName, line, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var symbol = model.GetDeclaredSymbol(method, cancellationToken)!;

            EnsureConvertible(method, symbol);
            var uses = await UsesAsync(symbol, method, solution, cancellationToken);
            var caller = SingleCaller(symbol, uses);
            var body = CallerBody(caller, uses, symbol);
            var callerDocument = solution.GetDocument(uses[0].Document.Id)!;
            var callerModel = (await callerDocument.GetSemanticModelAsync(cancellationToken))!;
            EnsureNamesKeepTheirMeaning(method, symbol, model, body, callerModel, cancellationToken);

            var editors = new Dictionary<DocumentId, DocumentEditor>
            {
                [document.Id] = await DocumentEditor.CreateAsync(document, cancellationToken),
            };
            if (!editors.ContainsKey(callerDocument.Id))
                editors[callerDocument.Id] = await DocumentEditor.CreateAsync(callerDocument, cancellationToken);

            var callerEditor = editors[callerDocument.Id];
            foreach (var use in uses)
                Unqualify(use.Node, symbol, callerModel, callerEditor, cancellationToken);

            var localFunction = LocalFunction(method, PositionTarget.IndentationOf(body.CloseBraceToken.Parent!) + 4);
            callerEditor.ReplaceNode(body, (current, _) => ((BlockSyntax)current).AddStatements(localFunction));
            editors[document.Id].RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

            foreach (var (id, editor) in editors)
                await RefactoringHelpers.WriteAndUpdateCachesAsync(solution.GetDocument(id)!, editor.GetChangedRoot());

            return $"Successfully converted method '{methodName}' to a local function of '{Name(caller)}' in {callerDocument.FilePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting method to local function: {ex.Message}", ex);
        }
    }

    private sealed record Use(Document Document, SyntaxNode Node);

    private static async Task<MethodDeclarationSyntax> FindMethod(Document document, string methodName, int? line, CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var candidates = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == methodName)
            .Where(m => line == null || m.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1 == line)
            .ToList();

        return candidates.Count switch
        {
            0 => throw new McpException($"Error: Method '{methodName}' not found"),
            1 => candidates[0],
            _ => throw new McpException($"Error: '{methodName}' has overloads; pass the line of the one to convert"),
        };
    }

    private static void EnsureConvertible(MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Private || symbol.ExplicitInterfaceImplementations.Length > 0)
            throw new McpException($"Error: '{symbol.Name}' is not private, so code outside its class may use it");
        if (symbol.IsExtensionMethod || symbol.IsExtern || symbol.IsPartialDefinition || symbol.PartialImplementationPart != null ||
            symbol.PartialDefinitionPart != null || (method.Body == null && method.ExpressionBody == null))
        {
            throw new McpException($"Error: '{symbol.Name}' is an extension, extern or partial method, which a local function cannot be");
        }
    }

    /// <summary>Every reference to the method outside its own body.</summary>
    private static async Task<List<Use>> UsesAsync(IMethodSymbol symbol, MethodDeclarationSyntax method, Solution solution, CancellationToken cancellationToken)
    {
        var uses = new List<Use>();
        foreach (var reference in await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken))
        {
            foreach (var location in reference.Locations.Where(l => l.Location.IsInSource))
            {
                var root = (await location.Document.GetSyntaxRootAsync(cancellationToken))!;
                var node = root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
                if (location.Document.Id == solution.GetDocumentId(method.SyntaxTree) && method.Span.Contains(node.Span))
                    continue;

                uses.Add(new Use(location.Document, node));
            }
        }

        if (uses.Count == 0)
            throw new McpException($"Error: '{symbol.Name}' is never called, so there is no member to move it into");
        return uses;
    }

    /// <summary>The member, or accessor, holding every use.</summary>
    private static SyntaxNode SingleCaller(IMethodSymbol symbol, List<Use> uses)
    {
        var callers = uses.Select(u => CallerOf(u.Node)).Distinct().ToList();
        if (callers.Count > 1)
            throw new McpException($"Error: '{symbol.Name}' is called from {callers.Count} members; a local function can only belong to one");
        return callers[0];
    }

    private static SyntaxNode CallerOf(SyntaxNode node) =>
        node.Ancestors().First(a => a is AccessorDeclarationSyntax or MemberDeclarationSyntax);

    private static BlockSyntax CallerBody(SyntaxNode caller, List<Use> uses, IMethodSymbol symbol)
    {
        var body = caller switch
        {
            BaseMethodDeclarationSyntax method => method.Body,
            AccessorDeclarationSyntax accessor => accessor.Body,
            _ => null,
        };

        if (body == null || uses.Any(u => !body.Span.Contains(u.Node.Span)))
        {
            throw new McpException(
                $"Error: '{Name(caller)}' uses '{symbol.Name}' outside a block body, where a local function could not be declared; convert it to a block body first");
        }

        return body;
    }

    /// <summary>
    /// Inside the caller, the method's name and every name its body reads from outside it
    /// must still mean the same symbol: no local, parameter, local function or type
    /// parameter of the caller may hide them, and the caller may not use another overload.
    /// </summary>
    private static void EnsureNamesKeepTheirMeaning(
        MethodDeclarationSyntax method,
        IMethodSymbol symbol,
        SemanticModel model,
        BlockSyntax body,
        SemanticModel callerModel,
        CancellationToken cancellationToken)
    {
        var overload = body.DescendantNodes().OfType<SimpleNameSyntax>()
            .Where(n => n.Identifier.ValueText == symbol.Name)
            .Select(n => callerModel.GetSymbolInfo(n, cancellationToken).Symbol)
            .FirstOrDefault(s => s is IMethodSymbol m && !SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, symbol));
        if (overload != null)
            throw new McpException($"Error: The caller also uses another overload of '{symbol.Name}', which the local function would hide");

        var outerNames = method.DescendantNodes().OfType<SimpleNameSyntax>()
            .Where(n => !(n.Parent is MemberAccessExpressionSyntax access && access.Name == n))
            .Where(n => model.GetSymbolInfo(n, cancellationToken).Symbol is { } s &&
                        !s.DeclaringSyntaxReferences.Any(r => method.Span.Contains(r.Span)))
            .Select(n => n.Identifier.ValueText)
            .Append(symbol.Name)
            .Concat(symbol.TypeParameters.Select(t => t.Name))
            .Distinct();

        // Any position among the block's statements sees every local the block declares.
        var position = body.Statements.Last().SpanStart;
        foreach (var name in outerNames)
        {
            var hiding = callerModel.LookupSymbols(position, name: name).FirstOrDefault(s =>
                s is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or IMethodSymbol { MethodKind: MethodKind.LocalFunction } ||
                s is ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method });
            if (hiding != null)
                throw new McpException($"Error: '{name}' is already declared in the caller, where it would hide what '{symbol.Name}' refers to");
        }
    }

    /// <summary>
    /// A local function is called by its simple name, so <c>this.M()</c> and
    /// <c>Type.M()</c> lose their qualifier; a call on any other instance cannot be kept.
    /// </summary>
    private static void Unqualify(SyntaxNode node, IMethodSymbol symbol, SemanticModel model, SyntaxEditor editor, CancellationToken cancellationToken)
    {
        if (node is not SimpleNameSyntax name)
            name = node.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().First();

        if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
        {
            var qualifier = access.Expression;
            var ownQualifier = qualifier is ThisExpressionSyntax ||
                               symbol.IsStatic && model.GetSymbolInfo(qualifier, cancellationToken).Symbol is INamedTypeSymbol;
            if (!ownQualifier)
                throw new McpException($"Error: '{access}' calls '{symbol.Name}' on another instance, which a local function cannot be called on");

            editor.ReplaceNode(access, name.WithTriviaFrom(access));
        }
        else if (name.Parent is MemberBindingExpressionSyntax)
        {
            throw new McpException($"Error: '{name.Parent.Parent}' calls '{symbol.Name}' on another instance, which a local function cannot be called on");
        }
    }

    /// <summary>
    /// The method as a local function: the same signature and body without an
    /// accessibility, static when the method was, indented as a statement of the caller
    /// and set apart from the statement before it by a blank line.
    /// </summary>
    private static LocalFunctionStatementSyntax LocalFunction(MethodDeclarationSyntax method, int indentation)
    {
        var shifted = PositionTarget.Reindent(method, indentation - PositionTarget.IndentationOf(method));
        var modifiers = shifted.Modifiers.Where(m => !SyntaxFacts.IsAccessibilityModifier(m.Kind()));

        var function = SyntaxFactory.LocalFunctionStatement(
                shifted.AttributeLists,
                SyntaxFactory.TokenList(modifiers),
                shifted.ReturnType,
                shifted.Identifier,
                shifted.TypeParameterList,
                shifted.ParameterList,
                shifted.ConstraintClauses,
                shifted.Body,
                shifted.ExpressionBody)
            .WithSemicolonToken(shifted.SemicolonToken);

        // Blank lines above the method give way to the one blank line added here.
        var trivia = shifted.GetLeadingTrivia().ToList();
        var content = trivia.FindIndex(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia));
        var lineStart = trivia.FindLastIndex(content < 0 ? trivia.Count - 1 : content, t => t.IsKind(SyntaxKind.EndOfLineTrivia)) + 1;
        var leading = trivia.Skip(lineStart);
        var endOfLine = MemberBody.EndOfLine(method);
        function = function
            .WithoutLeadingTrivia()
            .WithLeadingTrivia(SyntaxFactory.TriviaList(endOfLine).AddRange(leading));
        if (!function.GetTrailingTrivia().Any(SyntaxKind.EndOfLineTrivia))
            function = function.WithTrailingTrivia(function.GetTrailingTrivia().Add(endOfLine));
        return function;
    }

    private static string Name(SyntaxNode caller) => caller switch
    {
        AccessorDeclarationSyntax accessor => $"{accessor.Keyword.ValueText} accessor",
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        _ => caller.Kind().ToString(),
    };
}
