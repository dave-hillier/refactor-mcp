using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ExtractMethodRewriter : CSharpSyntaxRewriter
{
    private readonly MethodDeclarationSyntax _containingMethod;
    private readonly ClassDeclarationSyntax? _containingClass;
    private readonly List<StatementSyntax> _statements;
    private readonly string _methodName;
    private readonly MethodDeclarationSyntax _newMethod;
    private readonly MethodDeclarationSyntax _updatedMethod;

    public ExtractMethodRewriter(
        MethodDeclarationSyntax containingMethod,
        ClassDeclarationSyntax? containingClass,
        List<StatementSyntax> statements,
        string methodName,
        SemanticModel? semanticModel = null)
    {
        _containingMethod = containingMethod;
        _containingClass = containingClass;
        _statements = statements;
        _methodName = methodName;

        // Names can only be resolved against a semantic model, so without one the
        // extracted method keeps the shape it has always had: no parameters, returning
        // void. Callers that have a model get the parameters and return type inferred.
        var parameters = semanticModel == null
            ? new List<ParameterSyntax>()
            : FindParameters(containingMethod, statements, semanticModel);
        var resultType = semanticModel == null
            ? null
            : FindResultType(containingMethod, statements, semanticModel);
        var isAsync = semanticModel != null && ContainsAwait(statements);
        var exitsBlock = statements.Last() is ReturnStatementSyntax or ThrowStatementSyntax;
        // Statements that fall out of the bottom have no result to return, which the
        // caller sees as a null.
        var nullableResult = resultType != null && !exitsBlock;

        var newMethodBody = new List<StatementSyntax>(statements);
        if (nullableResult)
            newMethodBody.Add(SyntaxFactory.ReturnStatement(DefaultValue(resultType!)));

        var newMethodModifiers = new List<SyntaxToken> { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
        if (isAsync)
            newMethodModifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        var newMethodReturnType = resultType == null
            ? (isAsync
                ? (TypeSyntax)SyntaxFactory.IdentifierName("Task")
                : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)))
            : WrapTaskIfAsync(
                ResultTypeName(resultType, nullableResult, semanticModel!, statements.First().SpanStart),
                isAsync);

        _newMethod = SyntaxFactory.MethodDeclaration(newMethodReturnType, methodName)
            .WithModifiers(SyntaxFactory.TokenList(newMethodModifiers))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithBody(SyntaxFactory.Block(newMethodBody));

        var callSite = BuildCallSite(parameters, resultType, isAsync, nullableResult);
        for (var i = 1; i < callSite.Count; i++)
            callSite[i] = callSite[i].WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // The selected statements sit next to each other, so the call site replaces the
        // whole run in a single edit. Removing the statements one at a time would only
        // drop the first of them, because the nodes being removed come from the tree the
        // first removal already replaced.
        var body = containingMethod.Body!;
        var firstStatementIndex = body.Statements.IndexOf(statements.First());
        var kept = body.Statements
            .Where((s, i) => i < firstStatementIndex || i >= firstStatementIndex + statements.Count)
            .ToList();
        var updatedStatements = kept.Take(firstStatementIndex)
            .Concat(callSite)
            .Concat(kept.Skip(firstStatementIndex));

        _updatedMethod = containingMethod.WithBody(
            body.WithStatements(SyntaxFactory.List(updatedStatements)));
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node == _containingMethod)
            return _updatedMethod;
        return base.VisitMethodDeclaration(node)!;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (_containingClass != null && node == _containingClass)
        {
            visited = visited.AddMembers(_newMethod);
        }
        return visited;
    }

    /// <summary>
    /// Finds the locals and parameters of the containing method that the extracted
    /// statements read. Values declared inside the statements move with them, and
    /// fields and members of the class stay reachable, so neither becomes a parameter.
    /// </summary>
    private static List<ParameterSyntax> FindParameters(
        MethodDeclarationSyntax containingMethod,
        List<StatementSyntax> statements,
        SemanticModel semanticModel)
    {
        var containingSymbol = semanticModel.GetDeclaredSymbol(containingMethod);
        var extractedSpan = TextSpan.FromBounds(statements.First().SpanStart, statements.Last().Span.End);
        var parameters = new List<ParameterSyntax>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var identifier in statements.SelectMany(s => s.DescendantNodes().OfType<IdentifierNameSyntax>()))
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not ILocalSymbol && symbol is not IParameterSymbol)
                continue;
            if (!SymbolEqualityComparer.Default.Equals(symbol.ContainingSymbol, containingSymbol))
                continue;

            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (declaration != null && extractedSpan.Contains(declaration.Span))
                continue;
            if (!seen.Add(symbol))
                continue;

            var type = symbol is ILocalSymbol local ? local.Type : ((IParameterSymbol)symbol).Type;
            parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(symbol.Name))
                .WithType(SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(semanticModel, identifier.SpanStart))));
        }

        return parameters;
    }

    /// <summary>
    /// The type the extracted statements return, or null when they return nothing.
    /// The containing method's own return type is the one its return statements were
    /// written against, unwrapped when the method is async.
    /// </summary>
    private static ITypeSymbol? FindResultType(
        MethodDeclarationSyntax containingMethod,
        List<StatementSyntax> statements,
        SemanticModel semanticModel)
    {
        if (!ReturnStatements(statements).Any(r => r.Expression != null))
            return null;

        var returnType = semanticModel.GetTypeInfo(containingMethod.ReturnType).Type;
        if (returnType is INamedTypeSymbol named && IsTaskLike(named) && named.TypeArguments.Length == 1)
            return named.TypeArguments[0];

        return returnType?.SpecialType == SpecialType.System_Void ? null : returnType;
    }

    private static IEnumerable<ReturnStatementSyntax> ReturnStatements(List<StatementSyntax> statements)
    {
        return statements
            .SelectMany(s => s.DescendantNodesAndSelf(CanContainAwaitOrReturn))
            .OfType<ReturnStatementSyntax>();
    }

    private static bool ContainsAwait(List<StatementSyntax> statements)
    {
        return statements
            .SelectMany(s => s.DescendantNodesAndSelf(CanContainAwaitOrReturn))
            .Any(n => n is AwaitExpressionSyntax);
    }

    // A lambda or local function owns its own await expressions and returns.
    private static bool CanContainAwaitOrReturn(SyntaxNode node)
    {
        return node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax;
    }

    private static bool IsTaskLike(INamedTypeSymbol type)
    {
        return type.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks" &&
               type.Name is "Task" or "ValueTask";
    }

    /// <summary>
    /// The return type name of the extracted method. A method whose statements can fall
    /// out of the bottom comes back empty handed, so it returns a nullable type.
    /// </summary>
    private static string ResultTypeName(ITypeSymbol resultType, bool nullableResult, SemanticModel semanticModel, int position)
    {
        var name = resultType.ToMinimalDisplayString(semanticModel, position);
        if (!nullableResult || name.EndsWith("?"))
            return name;
        if (resultType is INamedTypeSymbol { IsReferenceType: true } || NeedsValueAccess(resultType))
            return name + "?";

        return name;
    }

    // A value that is not itself nullable, so unwrapping it takes a .Value.
    private static bool NeedsValueAccess(ITypeSymbol resultType)
    {
        return resultType.IsValueType &&
               resultType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    private static TypeSyntax WrapTaskIfAsync(string resultTypeName, bool isAsync)
    {
        return SyntaxFactory.ParseTypeName(isAsync ? $"Task<{resultTypeName}>" : resultTypeName);
    }

    private static ExpressionSyntax DefaultValue(ITypeSymbol resultType)
    {
        var isNullable = resultType.IsReferenceType ||
                         resultType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        return SyntaxFactory.LiteralExpression(
            isNullable ? SyntaxKind.NullLiteralExpression : SyntaxKind.DefaultLiteralExpression);
    }

    private List<StatementSyntax> BuildCallSite(
        List<ParameterSyntax> parameters,
        ITypeSymbol? resultType,
        bool isAsync,
        bool nullableResult)
    {
        ExpressionSyntax call = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(_methodName))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))))));
        if (isAsync)
            call = SyntaxFactory.AwaitExpression(call);

        if (resultType == null)
            return new List<StatementSyntax> { SyntaxFactory.ExpressionStatement(call) };

        // The statements always leave the method, so the call can return straight away.
        // Otherwise a null result means the extracted statements fell out of the bottom,
        // and the caller carries on with the rest of its own statements.
        if (!nullableResult)
            return new List<StatementSyntax> { SyntaxFactory.ReturnStatement(call) };

        var resultName = ResultVariableName(_methodName);
        ExpressionSyntax result = NeedsValueAccess(resultType)
            ? SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(resultName),
                SyntaxFactory.IdentifierName("Value"))
            : SyntaxFactory.IdentifierName(resultName);

        return new List<StatementSyntax>
        {
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(resultName))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(call))))),
            SyntaxFactory.IfStatement(
                SyntaxFactory.BinaryExpression(
                    SyntaxKind.NotEqualsExpression,
                    SyntaxFactory.IdentifierName(resultName),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                SyntaxFactory.Block(SyntaxFactory.ReturnStatement(result)))
        };
    }

    private static string ResultVariableName(string methodName)
    {
        var name = methodName.EndsWith("Async")
            ? methodName.Substring(0, methodName.Length - "Async".Length)
            : methodName;
        return char.ToLowerInvariant(name[0]) + name.Substring(1) + "Result";
    }
}
