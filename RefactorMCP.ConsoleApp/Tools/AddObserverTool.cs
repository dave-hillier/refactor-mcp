using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using RefactorMCP.ConsoleApp.Tools.Generators;
using RefactorMCP.ConsoleApp.Tools.Moving;

[McpServerToolType]
public static class AddObserverTool
{
    [McpServerTool, Description("Declare an event that a void method raises when it completes: an Action taking the " +
        "method's parameters, declared before the method and raised at its end and before each return")]
    public static async Task<string> AddObserver(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Name of the class containing the method")] string className,
        [Description("Name of the method to raise the event from")] string methodName,
        [Description("Name of the event to create")] string eventName,
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document,
            methodName,
            line,
            s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary } && s.ContainingType.Name == className,
            "method",
            cancellationToken);

        if (await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken) is not MethodDeclarationSyntax declaration
            || (declaration.Body is null && declaration.ExpressionBody is null))
            throw new McpException($"Error: {methodName} has no body to raise the event from");
        if (!method.ReturnsVoid)
            throw new McpException($"Error: {methodName} returns a value; only a void method raises an event when it completes");
        if (method.Parameters.Any(p => p.RefKind != RefKind.None))
            throw new McpException($"Error: {methodName} has a ref, out or in parameter, which an Action event cannot pass");
        if (!SyntaxFacts.IsValidIdentifier(eventName))
            throw new McpException($"Error: '{eventName}' is not a valid event name");
        if (method.ContainingType.GetMembers(eventName).Length > 0)
            throw new McpException($"Error: {method.ContainingType.Name} already has a member named '{eventName}'");

        var original = solution;
        document = solution.GetDocument(declaration.SyntaxTree)!;
        if (declaration.Body is null)
        {
            var (converted, block) = await BlockBodies.ConvertAsync(document, declaration, method, cancellationToken);
            (document, declaration) = (converted, (MethodDeclarationSyntax)block);
        }

        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var raise = SyntaxFactory.ParseStatement(
            $"{eventName}?.Invoke({string.Join(", ", declaration.ParameterList.Parameters.Select(p => p.Identifier.Text))});");
        var withRaises = RaiseOnExit(declaration, raise, model);

        var type = (TypeDeclarationSyntax)declaration.Parent!;
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var mark = new SyntaxAnnotation();
        var updatedType = type.ReplaceNode(declaration, withRaises.WithAdditionalAnnotations(mark));
        var updatedMethod = (MethodDeclarationSyntax)updatedType.GetAnnotatedNodes(mark).Single();
        var (eventDeclaration, laidOutMethod) = EventBefore(updatedMethod, EventDeclaration(declaration, method, eventName, model));
        updatedType = updatedType.ReplaceNode(updatedMethod, laidOutMethod);
        updatedType = updatedType.WithMembers(updatedType.Members.Insert(
            updatedType.Members.IndexOf(m => m.HasAnnotation(mark)),
            eventDeclaration));

        var updatedRoot = root.ReplaceNode(type, updatedType);
        if (!Binds(model, declaration.SpanStart, "Action"))
            updatedRoot = TypeRefactoringHelpers.AddUsings((CompilationUnitSyntax)updatedRoot, new[] { "System" });

        var changed = document.WithSyntaxRoot(updatedRoot);
        changed = await Formatter.FormatAsync(changed, Formatter.Annotation, cancellationToken: cancellationToken);
        await SolutionEdits.EnsureCompilesAsync(original, changed.Project.Solution, cancellationToken);
        await MovingSupport.ApplyAsync(original, changed.Project.Solution, cancellationToken);
        return $"Added event {eventName} raised by {className}.{methodName}";
    }

    /// <summary>
    /// <c>public event Action&lt;...&gt; Name;</c> taking the method's parameter
    /// types as written, static when the method is, and nullable where
    /// nullable annotations are enabled.
    /// </summary>
    private static EventFieldDeclarationSyntax EventDeclaration(
        MethodDeclarationSyntax declaration,
        IMethodSymbol method,
        string eventName,
        SemanticModel model)
    {
        var types = declaration.ParameterList.Parameters.Select(p => p.Type!.WithoutTrivia().ToString()).ToList();
        var type = types.Count == 0 ? "Action" : $"Action<{string.Join(", ", types)}>";
        if (model.GetNullableContext(declaration.SpanStart).AnnotationsEnabled())
            type += "?";

        var modifiers = method.IsStatic ? "public static event" : "public event";
        return (EventFieldDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"{modifiers} {type} {eventName};")!;
    }

    /// <summary>
    /// The event laid out just before the method: it takes the blank lines
    /// that preceded the method, and a blank line separates the two.
    /// </summary>
    private static (MemberDeclarationSyntax Event, MethodDeclarationSyntax Method) EventBefore(
        MethodDeclarationSyntax method,
        EventFieldDeclarationSyntax @event)
    {
        var trivia = method.GetLeadingTrivia();
        var blankLines = 0;
        while (blankLines < trivia.Count && trivia[blankLines].IsKind(SyntaxKind.EndOfLineTrivia))
            blankLines++;

        var eol = TypeRefactoringHelpers.EndOfLine(method.SyntaxTree.GetRoot());
        var indentation = BlockBodies.IndentationOf(method.GetFirstToken());
        var laidOutEvent = @event
            .WithLeadingTrivia(trivia.Take(blankLines).Append(SyntaxFactory.Whitespace(indentation)))
            .WithTrailingTrivia(eol);
        var laidOutMethod = method.WithLeadingTrivia(trivia.Skip(blankLines).Prepend(eol));
        return (laidOutEvent, laidOutMethod);
    }

    /// <summary>
    /// The method with <paramref name="raise"/> before every return that
    /// leaves it and at the end of its body when the end is reachable.
    /// Returns inside lambdas and local functions leave something else and
    /// are left alone.
    /// </summary>
    private static MethodDeclarationSyntax RaiseOnExit(MethodDeclarationSyntax method, StatementSyntax raise, SemanticModel model)
    {
        var body = method.Body!;
        var endReachable = body.Statements.Count == 0
            || model.AnalyzeControlFlow(body.Statements.First(), body.Statements.Last())!.EndPointIsReachable;

        var returns = body.DescendantNodes(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>()
            .ToList();
        var rewritten = body.ReplaceNodes(returns, (original, _) => original.Parent is BlockSyntax
            ? original.WithAdditionalAnnotations(RaiseBefore)
            : SyntaxFactory.Block(Elastic(raise), Elastic(original)).WithAdditionalAnnotations(Formatter.Annotation));
        rewritten = rewritten.ReplaceNodes(
            rewritten.DescendantNodes().OfType<BlockSyntax>().Where(b => b.Statements.Any(s => s.HasAnnotation(RaiseBefore))),
            (_, block) => block.WithStatements(SyntaxFactory.List(block.Statements.SelectMany(s => s.HasAnnotation(RaiseBefore)
                ? new[] { raise.WithLeadingTrivia(s.GetLeadingTrivia()).WithTrailingTrivia(Eol(method)), s.WithLeadingTrivia(Indentation(s)) }
                : new[] { s }))));

        if (endReachable)
            rewritten = AppendStatement(rewritten, raise, method);

        return method.WithBody(rewritten);
    }

    private static readonly SyntaxAnnotation RaiseBefore = new();

    /// <summary>
    /// Adds a statement at the end of a block, after any comment that sits
    /// before the closing brace.
    /// </summary>
    private static BlockSyntax AppendStatement(BlockSyntax block, StatementSyntax statement, MethodDeclarationSyntax method)
    {
        if (block.Statements.Count == 0)
            return block.AddStatements(statement).WithAdditionalAnnotations(Formatter.Annotation);

        var eol = Eol(method);
        var indentation = BlockBodies.IndentationOf(method.Body!.Statements[0].GetFirstToken());

        var closing = block.CloseBraceToken.LeadingTrivia;
        var lastLineBreak = closing.LastIndexOf(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        var before = closing.Take(lastLineBreak + 1);
        var after = closing.Skip(lastLineBreak + 1);

        var appended = statement
            .WithLeadingTrivia(before.Append(SyntaxFactory.Whitespace(indentation)))
            .WithTrailingTrivia(eol);
        return block
            .AddStatements(appended)
            .WithCloseBraceToken(block.CloseBraceToken.WithLeadingTrivia(after));
    }

    private static T Elastic<T>(T node) where T : SyntaxNode =>
        node.WithLeadingTrivia(SyntaxFactory.ElasticMarker).WithTrailingTrivia(SyntaxFactory.ElasticMarker);

    private static SyntaxTrivia Eol(SyntaxNode node) => TypeRefactoringHelpers.EndOfLine(node.SyntaxTree.GetRoot());

    private static SyntaxTriviaList Indentation(SyntaxNode node) =>
        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(BlockBodies.IndentationOf(node.GetFirstToken())));

    private static bool Binds(SemanticModel model, int position, string typeName) =>
        model.GetSpeculativeTypeInfo(position, SyntaxFactory.ParseTypeName(typeName), SpeculativeBindingOption.BindAsTypeOrNamespace)
            .Type is { TypeKind: not TypeKind.Error };

    private static int LastIndexOf(this SyntaxTriviaList list, Func<SyntaxTrivia, bool> predicate)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (predicate(list[i]))
                return i;
        }

        return -1;
    }
}
