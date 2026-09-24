using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RefactorMCP.ConsoleApp.Tools.Moving;

namespace RefactorMCP.ConsoleApp.Tools.Generators;

[McpServerToolType]
public static class ReplaceErrorCodeWithExceptionTool
{
    [McpServerTool, Description("Make a method that returns an int error code (0 for success) or a bool (true for success) " +
        "return void and throw instead, and turn each caller's if on the result into a try/catch.")]
    public static async Task<string> ReplaceErrorCodeWithException(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method returning the error code")] string methodName,
        [Description("Exception type to throw and catch (optional, defaults to InvalidOperationException)")] string exceptionType = "InvalidOperationException",
        [Description("Line of the method's declaration (1-based, optional), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        var document = MovingSupport.DocumentOrThrow(solution, filePath);
        var method = (IMethodSymbol)await MovingSupport.FindDeclaredSymbolAsync(
            document, methodName, line, s => s is IMethodSymbol { MethodKind: MethodKind.Ordinary }, "method", cancellationToken);

        var updated = await ErrorCodeReplacement.ReplaceAsync(solution, method, exceptionType, cancellationToken);
        await MovingSupport.ApplyAsync(solution, updated, cancellationToken);
        return $"Successfully made {methodName} throw {exceptionType} instead of returning an error code";
    }
}

/// <summary>
/// Turns a method that reports failure through its return value into one
/// that throws, and the callers that tested the value into try/catch.
/// </summary>
internal static class ErrorCodeReplacement
{
    public static async Task<Solution> ReplaceAsync(
        Solution solution,
        IMethodSymbol method,
        string exceptionName,
        CancellationToken cancellationToken)
    {
        if (method.IsVirtual || method.IsOverride || method.IsAbstract || method.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterface(method))
            throw new McpException($"Error: {method.Name} is virtual, abstract, an override or an interface implementation; the other implementations would keep returning codes");

        object success = method.ReturnType.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_Boolean => true,
            _ => throw new McpException($"Error: {method.Name} returns {method.ReturnType.ToDisplayString()}; only an int or bool error code can be replaced"),
        };

        var declaration = (MethodDeclarationSyntax)await method.DeclaringSyntaxReferences[0].GetSyntaxAsync(cancellationToken);
        var document = solution.GetDocument(declaration.SyntaxTree)!;
        var model = (await document.GetSemanticModelAsync(cancellationToken))!;
        var exception = FindException(model.Compilation, exceptionName);
        var hasMessage = exception.InstanceConstructors.Any(c =>
            c.Parameters.Length == 1 && c.Parameters[0].Type.SpecialType == SpecialType.System_String
            && model.Compilation.IsSymbolAccessibleWithin(c, model.Compilation.Assembly));

        var returns = Returns(declaration);
        var failures = 0;
        var edits = new SyntaxEdits();
        StatementSyntax? thrownByExpressionBody = null;
        foreach (var (node, value) in returns)
        {
            var constant = model.GetConstantValue(value, cancellationToken);
            if (!constant.HasValue)
                throw new McpException($"Error: {method.Name} returns {value} at {SolutionEdits.Describe(value.GetLocation())}, which is not a constant error code");

            if (Equals(constant.Value, success))
            {
                edits.Replace(document.Id, node, rewritten => SuccessReturn((ReturnStatementSyntax)rewritten));
                continue;
            }

            failures++;
            var message = hasMessage
                ? method.ReturnType.SpecialType == SpecialType.System_Boolean
                    ? $"{method.Name} failed"
                    : $"{method.Name} returned error code {constant.Value}"
                : null;
            if (node is ReturnStatementSyntax)
                edits.Replace(document.Id, node, rewritten => Throw(exception, message).WithTriviaFrom(rewritten));
            else
                thrownByExpressionBody = Throw(exception, message);
        }

        if (failures == 0)
            throw new McpException($"Error: {method.Name} never returns an error code, so there is nothing to throw");

        edits.Replace(document.Id, declaration, rewritten => AsVoid((MethodDeclarationSyntax)rewritten, thrownByExpressionBody));

        foreach (var reference in await MemberReferences.FindAsync(solution, method, cancellationToken))
            RewriteCaller(reference, method, success, exception, edits);

        var updated = await edits.ApplyAsync(solution, cancellationToken);
        var errors = await TypeRefactoringHelpers.NewErrorsAsync(solution, updated, cancellationToken);
        if (errors.Count > 0)
            throw new McpException($"Error: Replacing the error code of {method.Name} would break the build: {TypeRefactoringHelpers.Describe(errors)}");

        return updated;
    }

    private static bool ImplementsInterface(IMethodSymbol method) =>
        method.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
            .Any(m => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(m), method));

    /// <summary>An accessible type deriving from <see cref="Exception"/>, by simple or qualified name.</summary>
    private static INamedTypeSymbol FindException(Compilation compilation, string name)
    {
        var baseException = compilation.GetTypeByMetadataName("System.Exception");
        var candidates = new List<INamedTypeSymbol>();
        if (compilation.GetTypeByMetadataName(name) is { } qualified)
            candidates.Add(qualified);

        var pending = new Stack<INamespaceSymbol>();
        pending.Push(compilation.GlobalNamespace);
        while (pending.Count > 0 && !name.Contains('.'))
        {
            var ns = pending.Pop();
            candidates.AddRange(ns.GetTypeMembers(name, 0));
            foreach (var child in ns.GetNamespaceMembers())
                pending.Push(child);
        }

        var exceptions = candidates
            .Where(t => compilation.IsSymbolAccessibleWithin(t, compilation.Assembly) && DerivesFrom(t, baseException))
            .OrderByDescending(t => t.Locations.Any(l => l.IsInSource))
            .ThenBy(t => t.ContainingNamespace.ToDisplayString() == "System" ? 0 : 1)
            .ToList();

        return exceptions.FirstOrDefault()
            ?? throw new McpException($"Error: No exception type named '{name}' found; it must derive from System.Exception");

        static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// The method's returns and the values they return, leaving out lambdas
    /// and local functions. An expression body counts as a return.
    /// </summary>
    private static List<(SyntaxNode Node, ExpressionSyntax Value)> Returns(MethodDeclarationSyntax declaration)
    {
        if (declaration.ExpressionBody is { } arrow)
            return new List<(SyntaxNode, ExpressionSyntax)> { (arrow.Expression, arrow.Expression) };

        return declaration.Body!
            .DescendantNodes(n => n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<ReturnStatementSyntax>()
            .Where(r => r.Expression is not null)
            .Select(r => ((SyntaxNode)r, r.Expression!))
            .ToList();
    }

    private static ReturnStatementSyntax SuccessReturn(ReturnStatementSyntax statement) =>
        statement.WithReturnKeyword(statement.ReturnKeyword.WithTrailingTrivia()).WithExpression(null);

    private static ThrowStatementSyntax Throw(INamedTypeSymbol exception, string? message)
    {
        var arguments = message is null
            ? SyntaxFactory.ArgumentList()
            : SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(message)))));
        return SyntaxFactory.ThrowStatement(
            SyntaxFactory.Token(SyntaxKind.ThrowKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                MovingSupport.QualifiedType(exception),
                arguments,
                null),
            SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>
    /// Declares the method void, gives an expression body that always fails a
    /// block that throws, and drops a bare <c>return;</c> left as the last
    /// statement unless a comment is on it.
    /// </summary>
    private static MethodDeclarationSyntax AsVoid(MethodDeclarationSyntax method, StatementSyntax? thrownByExpressionBody)
    {
        method = method.WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)).WithTriviaFrom(method.ReturnType));

        if (thrownByExpressionBody is not null)
        {
            return method
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block(thrownByExpressionBody))
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

        var body = method.Body!;
        if (body.Statements.LastOrDefault() is ReturnStatementSyntax { Expression: null } last
            && !last.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
        {
            method = method.WithBody(body.WithStatements(body.Statements.Remove(last)));
        }

        return method;
    }

    /// <summary>
    /// A call whose result is ignored stays as it is. A call tested for
    /// success by an <c>if</c> becomes a try whose catch runs the failure branch.
    /// </summary>
    private static void RewriteCaller(MemberReference reference, IMethodSymbol method, object success, INamedTypeSymbol exception, SyntaxEdits edits)
    {
        if (reference.Invocation is not { } call)
            throw Unsupported(reference.Name, method);

        ExpressionSyntax outer = call.Parent is ConditionalAccessExpressionSyntax access && access.WhenNotNull == call ? access : call;
        if (outer.Parent is ExpressionStatementSyntax)
            return;

        if (Test(outer, reference.Model, success) is not { } test || test.Condition.Parent is not IfStatementSyntax statement || statement.Condition != test.Condition)
            throw Unsupported(call, method);

        edits.Replace(reference.Document.Id, statement, rewritten =>
        {
            var ifStatement = (IfStatementSyntax)rewritten;
            var rewrittenCall = test.Call(ifStatement.Condition);
            var (onSuccess, onFailure) = test.SuccessWhenTrue
                ? (ifStatement.Statement, ifStatement.Else?.Statement)
                : (ifStatement.Else?.Statement, ifStatement.Statement);

            var tryBlock = SyntaxFactory.Block(
                new StatementSyntax[] { SyntaxFactory.ExpressionStatement(rewrittenCall.WithoutTrivia()) }.Concat(Statements(onSuccess)));
            var catchClause = SyntaxFactory.CatchClause()
                .WithDeclaration(SyntaxFactory.CatchDeclaration(MovingSupport.QualifiedType(exception)))
                .WithBlock(SyntaxFactory.Block(Statements(onFailure)));
            return SyntaxFactory.TryStatement(tryBlock, SyntaxFactory.SingletonList(catchClause), null)
                .WithTriviaFrom(ifStatement)
                .WithAdditionalAnnotations(Formatter.Annotation);
        });
    }

    private static IEnumerable<StatementSyntax> Statements(StatementSyntax? branch) => branch switch
    {
        null => Enumerable.Empty<StatementSyntax>(),
        BlockSyntax block => block.Statements,
        _ => new[] { branch },
    };

    /// <summary>
    /// How a condition tests the call: the whole condition, whether it is true
    /// on success, and how to find the call again in the rewritten condition.
    /// </summary>
    private sealed record SuccessTest(ExpressionSyntax Condition, bool SuccessWhenTrue, Func<ExpressionSyntax, ExpressionSyntax> Call);

    private static SuccessTest? Test(ExpressionSyntax call, SemanticModel model, object success)
    {
        if (success is true)
        {
            if (call.Parent is PrefixUnaryExpressionSyntax not && not.IsKind(SyntaxKind.LogicalNotExpression))
                return new SuccessTest(not, false, c => ((PrefixUnaryExpressionSyntax)c).Operand);
            return new SuccessTest(call, true, c => c);
        }

        if (call.Parent is BinaryExpressionSyntax binary && binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
        {
            var callOnLeft = binary.Left == call;
            var other = callOnLeft ? binary.Right : binary.Left;
            var constant = model.GetConstantValue(other);
            if (constant.HasValue && Equals(constant.Value, success))
            {
                return new SuccessTest(
                    binary,
                    binary.IsKind(SyntaxKind.EqualsExpression),
                    c => callOnLeft ? ((BinaryExpressionSyntax)c).Left : ((BinaryExpressionSyntax)c).Right);
            }
        }

        return null;
    }

    private static McpException Unsupported(SyntaxNode call, IMethodSymbol method) =>
        new($"Error: The call at {SolutionEdits.Describe(call.GetLocation())} uses the result of {method.Name} in a way a catch cannot replace: {call.Parent}");
}
