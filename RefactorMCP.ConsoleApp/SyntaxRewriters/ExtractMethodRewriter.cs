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
        SemanticModel? semanticModel = null,
        TextSpan? selection = null)
    {
        _containingMethod = containingMethod;
        _containingClass = containingClass;
        _statements = statements;
        _methodName = methodName;

        // A return, break or continue that ends the selection leaves the containing
        // method, switch section or loop, so it stays at the call site rather than only
        // leaving the new method.
        var keptReturn = statements.Count > 1 && statements.Last() is ReturnStatementSyntax { Expression: null } or BreakStatementSyntax or ContinueStatementSyntax
            ? statements.Last()
            : null;
        var extracted = keptReturn == null ? statements : statements.Take(statements.Count - 1).ToList();

        // Names can only be resolved against a semantic model, so without one the
        // extracted method keeps the shape it has always had: no parameters, returning
        // void. Callers that have a model get the parameters and return type inferred.
        var parameters = semanticModel == null
            ? new List<ExtractedParameter>()
            : FindParameters(containingMethod, extracted, semanticModel);
        var resultType = semanticModel == null
            ? null
            : FindResultType(containingMethod, extracted, semanticModel);
        var isAsync = semanticModel != null && ContainsAwait(extracted);
        var exitsBlock = extracted.Last() is ReturnStatementSyntax or ThrowStatementSyntax;
        // Statements that fall out of the bottom have no result to return, which the
        // caller sees as a null.
        var nullableResult = resultType != null && !exitsBlock;

        // Trivia in front of the selection, such as a comment introducing it, stays
        // with the call site; trivia inside the selection moves with the statements.
        var selectionStart = selection?.Start ?? statements.First().SpanStart;
        var (callSiteLeading, extractedLeading) = SplitLeadingTrivia(statements.First(), selectionStart);

        var newMethodBody = new List<StatementSyntax>(extracted);
        newMethodBody[0] = newMethodBody[0].WithLeadingTrivia(extractedLeading);
        if (nullableResult)
            newMethodBody.Add(SyntaxFactory.ReturnStatement(DefaultValue(resultType!)));

        var newMethodModifiers = new List<SyntaxToken> { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
        if (containingMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
            newMethodModifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (isAsync)
            newMethodModifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        var newMethodReturnType = resultType == null
            ? (isAsync
                ? (TypeSyntax)SyntaxFactory.IdentifierName("Task")
                : SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)))
            : WrapTaskIfAsync(
                ResultTypeName(resultType, nullableResult, semanticModel!, extracted.First().SpanStart),
                isAsync);

        var typeParameters = semanticModel == null
            ? new List<ITypeParameterSymbol>()
            : FindTypeParameters(containingMethod, extracted, parameters, resultType, semanticModel);

        _newMethod = NewMethod(containingMethod, methodName, newMethodReturnType, newMethodModifiers, parameters, typeParameters, newMethodBody);

        var callSite = BuildCallSite(parameters, ExplicitTypeArguments(parameters, typeParameters), resultType, isAsync, nullableResult);
        if (keptReturn != null)
            callSite.Add(keptReturn.WithoutTrivia());
        for (var i = 1; i < callSite.Count; i++)
            callSite[i] = callSite[i].WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        callSite[0] = callSite[0].WithLeadingTrivia(callSiteLeading);

        var container = statements.First().Parent!;
        if (container is not (BlockSyntax or SwitchSectionSyntax))
        {
            // A statement that is the body of an if, else or loop without braces is
            // replaced by the call, in a block of its own when the call takes several.
            var replacement = callSite.Count == 1
                ? callSite[0]
                : SyntaxFactory.Block(callSite.Select(s => s.WithoutLeadingTrivia()));
            _updatedMethod = containingMethod.ReplaceNode(statements.First(), replacement.WithTriviaFrom(statements.First()));
            return;
        }

        // The selected statements sit next to each other, so the call site replaces the
        // whole run in a single edit. Removing the statements one at a time would only
        // drop the first of them, because the nodes being removed come from the tree the
        // first removal already replaced.
        var siblings = container is BlockSyntax block ? block.Statements : ((SwitchSectionSyntax)container).Statements;
        var firstStatementIndex = siblings.IndexOf(statements.First());

        // A call site that ends in the block checking for a result is set apart from the
        // statement after it by a blank line, as the extracted method's own is.
        var endOfLine = EndOfLine(statements.Last());
        var nextIndex = firstStatementIndex + statements.Count;
        if (callSite.Count > 1 &&
            keptReturn == null &&
            nextIndex < siblings.Count &&
            !siblings[nextIndex].GetLeadingTrivia().Any(SyntaxKind.EndOfLineTrivia))
        {
            endOfLine = endOfLine.AddRange(endOfLine);
        }

        callSite[^1] = callSite[^1].WithTrailingTrivia(endOfLine);
        var kept = siblings
            .Where((s, i) => i < firstStatementIndex || i >= firstStatementIndex + statements.Count)
            .ToList();
        var updatedStatements = SyntaxFactory.List(kept.Take(firstStatementIndex)
            .Concat(callSite)
            .Concat(kept.Skip(firstStatementIndex)));

        _updatedMethod = containingMethod.ReplaceNode(container, container is BlockSyntax
            ? ((BlockSyntax)container).WithStatements(updatedStatements)
            : ((SwitchSectionSyntax)container).WithStatements(updatedStatements));
    }

    /// <summary>
    /// Extracts a single expression into a method that returns its value, and puts a
    /// call in its place. The method returns <paramref name="resultType"/> when given,
    /// otherwise the expression's own type.
    /// </summary>
    public ExtractMethodRewriter(
        MethodDeclarationSyntax containingMethod,
        ClassDeclarationSyntax? containingClass,
        ExpressionSyntax expression,
        string methodName,
        SemanticModel semanticModel,
        ITypeSymbol? resultType = null)
    {
        _containingMethod = containingMethod;
        _containingClass = containingClass;
        _statements = new List<StatementSyntax>();
        _methodName = methodName;

        var nodes = new List<ExpressionSyntax> { expression };
        var parameters = FindParameters(containingMethod, nodes, semanticModel);
        var typeInfo = semanticModel.GetTypeInfo(expression);
        resultType ??= typeInfo.Type ?? typeInfo.ConvertedType;
        var isAsync = ContainsAwait(nodes);
        var typeParameters = FindTypeParameters(containingMethod, nodes, parameters, resultType, semanticModel);

        var modifiers = new List<SyntaxToken> { SyntaxFactory.Token(SyntaxKind.PrivateKeyword) };
        if (containingMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        if (isAsync)
            modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));

        var returnType = WrapTaskIfAsync(resultType!.ToMinimalDisplayString(semanticModel, expression.SpanStart), isAsync);
        var body = new List<StatementSyntax> { SyntaxFactory.ReturnStatement(expression.WithoutTrivia()) };
        _newMethod = NewMethod(containingMethod, methodName, returnType, modifiers, parameters, typeParameters, body);

        var call = Call(parameters, ExplicitTypeArguments(parameters, typeParameters), isAsync);
        if (isAsync && expression.Parent is MemberAccessExpressionSyntax or ElementAccessExpressionSyntax or ConditionalAccessExpressionSyntax or InvocationExpressionSyntax)
            call = SyntaxFactory.ParenthesizedExpression(call);

        _updatedMethod = containingMethod.ReplaceNode(expression, call.WithTriviaFrom(expression));
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node == _containingMethod)
            return _updatedMethod;
        return base.VisitMethodDeclaration(node)!;
    }

    // The new method follows the method it was extracted from.
    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (_containingClass != null && node == _containingClass)
        {
            var index = node.Members.IndexOf(_containingMethod);
            visited = index < 0
                ? visited.AddMembers(_newMethod)
                : visited.WithMembers(visited.Members.Insert(index + 1, _newMethod));
        }
        return visited;
    }

    private static MethodDeclarationSyntax NewMethod(
        MethodDeclarationSyntax containingMethod,
        string methodName,
        TypeSyntax returnType,
        List<SyntaxToken> modifiers,
        List<ExtractedParameter> parameters,
        List<ITypeParameterSymbol> typeParameters,
        List<StatementSyntax> body)
    {
        var method = SyntaxFactory.MethodDeclaration(returnType, methodName)
            .WithModifiers(SyntaxFactory.TokenList(modifiers))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(p => p.Syntax))))
            .WithBody(SyntaxFactory.Block(body));
        if (typeParameters.Count == 0)
            return method;

        var names = typeParameters.Select(t => t.Name).ToHashSet();
        return method
            .WithTypeParameterList(SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(
                typeParameters.Select(t => SyntaxFactory.TypeParameter(t.Name)))))
            .WithConstraintClauses(SyntaxFactory.List(
                containingMethod.ConstraintClauses.Where(c => names.Contains(c.Name.Identifier.ValueText))));
    }

    // Type arguments are spelled out only when the arguments cannot infer them.
    private static List<ITypeParameterSymbol> ExplicitTypeArguments(
        List<ExtractedParameter> parameters,
        List<ITypeParameterSymbol> typeParameters) =>
        typeParameters.Any(t => !parameters.Any(p => Mentions(p.Type, t)))
            ? typeParameters
            : new List<ITypeParameterSymbol>();

    private sealed record ExtractedParameter(ParameterSyntax Syntax, ITypeSymbol Type);

    /// <summary>
    /// Finds the locals and parameters of the containing method that the extracted
    /// code reads, including those of a lambda or local function the code sits in.
    /// Values declared inside the code move with it, and fields and members of the
    /// class stay reachable, so neither becomes a parameter.
    /// </summary>
    private static List<ExtractedParameter> FindParameters<TNode>(
        MethodDeclarationSyntax containingMethod,
        List<TNode> nodes,
        SemanticModel semanticModel)
        where TNode : SyntaxNode
    {
        var owners = nodes.First().Ancestors()
            .TakeWhile(a => a != containingMethod)
            .Where(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            .Select(a => a is LocalFunctionStatementSyntax local ? semanticModel.GetDeclaredSymbol(local) : semanticModel.GetSymbolInfo(a).Symbol)
            .Append(semanticModel.GetDeclaredSymbol(containingMethod))
            .OfType<ISymbol>()
            .ToHashSet(SymbolEqualityComparer.Default);
        var extractedSpan = TextSpan.FromBounds(nodes.First().SpanStart, nodes.Last().Span.End);
        var parameters = new List<ExtractedParameter>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var identifier in nodes.SelectMany(s => s.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()))
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is not ILocalSymbol && symbol is not IParameterSymbol)
                continue;
            if (!owners.Contains(symbol.ContainingSymbol))
                continue;

            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (declaration != null && extractedSpan.Contains(declaration.Span))
                continue;
            if (!seen.Add(symbol))
                continue;

            var type = symbol is ILocalSymbol local ? local.Type : ((IParameterSymbol)symbol).Type;
            // A local declared with var is nullable in a nullable context, but where the
            // flow analysis knows it holds a value the parameter need not accept null.
            if (type.IsReferenceType &&
                type.NullableAnnotation == NullableAnnotation.Annotated &&
                symbol is ILocalSymbol &&
                semanticModel.GetTypeInfo(identifier).Nullability.FlowState == NullableFlowState.NotNull)
            {
                type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            }

            var syntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(symbol.Name))
                .WithType(SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(semanticModel, identifier.SpanStart)));
            parameters.Add(new ExtractedParameter(syntax, type));
        }

        return parameters;
    }

    /// <summary>
    /// The containing method's type parameters the extracted method needs: those its
    /// parameters or result mention, or that the statements name directly.
    /// </summary>
    private static List<ITypeParameterSymbol> FindTypeParameters<TNode>(
        MethodDeclarationSyntax containingMethod,
        List<TNode> nodes,
        List<ExtractedParameter> parameters,
        ITypeSymbol? resultType,
        SemanticModel semanticModel)
        where TNode : SyntaxNode
    {
        if (semanticModel.GetDeclaredSymbol(containingMethod) is not IMethodSymbol { TypeParameters.Length: > 0 } method)
            return new List<ITypeParameterSymbol>();

        var named = nodes
            .SelectMany(s => s.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(i => semanticModel.GetSymbolInfo(i).Symbol)
            .OfType<ITypeParameterSymbol>()
            .ToList();

        return method.TypeParameters
            .Where(t => parameters.Any(p => Mentions(p.Type, t)) ||
                        (resultType != null && Mentions(resultType, t)) ||
                        named.Contains(t, SymbolEqualityComparer.Default))
            .ToList();
    }

    private static bool Mentions(ITypeSymbol type, ITypeParameterSymbol typeParameter)
    {
        return type switch
        {
            ITypeParameterSymbol t => SymbolEqualityComparer.Default.Equals(t, typeParameter),
            IArrayTypeSymbol array => Mentions(array.ElementType, typeParameter),
            IPointerTypeSymbol pointer => Mentions(pointer.PointedAtType, typeParameter),
            INamedTypeSymbol named => named.TypeArguments.Any(a => Mentions(a, typeParameter)),
            _ => false,
        };
    }

    /// <summary>
    /// Divides a statement's leading trivia at the start of the selection: what comes
    /// before it belongs to the surrounding method, the rest to the statement.
    /// </summary>
    private static (SyntaxTriviaList Outside, SyntaxTriviaList Inside) SplitLeadingTrivia(StatementSyntax statement, int selectionStart)
    {
        var leading = statement.GetLeadingTrivia();
        var outside = leading.Where(t => t.Span.End <= selectionStart);
        var inside = leading.Where(t => t.Span.End > selectionStart);
        return (SyntaxFactory.TriviaList(outside), SyntaxFactory.TriviaList(inside));
    }

    // The call site ends its line the way the last extracted statement did, so a blank
    // line after the selection stays where it was.
    private static SyntaxTriviaList EndOfLine(StatementSyntax statement)
    {
        var endOfLine = statement.GetTrailingTrivia().LastOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        return endOfLine == default ? SyntaxFactory.TriviaList() : SyntaxFactory.TriviaList(endOfLine);
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

    private static bool ContainsAwait<TNode>(List<TNode> nodes)
        where TNode : SyntaxNode
    {
        return nodes
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
        List<ExtractedParameter> parameters,
        List<ITypeParameterSymbol> typeArguments,
        ITypeSymbol? resultType,
        bool isAsync,
        bool nullableResult)
    {
        var call = Call(parameters, typeArguments, isAsync);

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

    private ExpressionSyntax Call(List<ExtractedParameter> parameters, List<ITypeParameterSymbol> typeArguments, bool isAsync)
    {
        SimpleNameSyntax name = typeArguments.Count == 0
            ? SyntaxFactory.IdentifierName(_methodName)
            : SyntaxFactory.GenericName(_methodName).WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList<TypeSyntax>(typeArguments.Select(t => SyntaxFactory.IdentifierName(t.Name)))));
        ExpressionSyntax call = SyntaxFactory.InvocationExpression(name)
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Syntax.Identifier))))));
        return isAsync ? SyntaxFactory.AwaitExpression(call) : call;
    }

    private static string ResultVariableName(string methodName)
    {
        var name = methodName.EndsWith("Async")
            ? methodName.Substring(0, methodName.Length - "Async".Length)
            : methodName;
        return char.ToLowerInvariant(name[0]) + name.Substring(1) + "Result";
    }
}
