using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

internal class FeatureFlagRewriter : CSharpSyntaxRewriter
{
    private readonly string _flagName;
    private readonly string _interfaceName;
    private readonly string _strategyField;
    private string StrategyParameter => _strategyField.TrimStart('_');
    private bool _resolved;
    private IfStatementSyntax? _targetIf;
    public SyntaxList<MemberDeclarationSyntax> GeneratedMembers { get; private set; }

    public FeatureFlagRewriter(string flagName)
    {
        _flagName = flagName;
        _interfaceName = $"I{flagName}Strategy";
        _strategyField = $"_{char.ToLower(flagName[0])}{flagName.Substring(1)}Strategy";
        GeneratedMembers = new SyntaxList<MemberDeclarationSyntax>();
    }

    // The flag check is located before the tree is visited, so that members visited
    // ahead of it (a constructor declared first, in particular) can still take part
    // in the rewrite.
    public override SyntaxNode Visit(SyntaxNode? node)
    {
        if (!_resolved && node != null)
        {
            _resolved = true;
            _targetIf = node.DescendantNodesAndSelf()
                .OfType<IfStatementSyntax>()
                .FirstOrDefault(candidate => IsFlagCheck(candidate.Condition, _flagName));
        }
        return base.Visit(node)!;
    }

    private static bool IsFlagCheck(ExpressionSyntax condition, string flag)
    {
        if (condition is InvocationExpressionSyntax inv &&
            inv.Expression is MemberAccessExpressionSyntax ma &&
            ma.Name.Identifier.ValueText == "IsEnabled" &&
            inv.ArgumentList.Arguments.Count == 1)
        {
            var arg = inv.ArgumentList.Arguments[0].Expression;
            if (arg is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                return lit.Token.ValueText == flag;
        }
        return false;
    }

    public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
    {
        if (node == _targetIf)
        {
            var applyCall = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(_strategyField),
                        SyntaxFactory.IdentifierName("Apply"))));
            return applyCall.WithTriviaFrom(node);
        }
        return base.VisitIfStatement(node)!;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (_targetIf != null && node.Span.Contains(_targetIf.Span))
        {
            var fieldDecl = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(_interfaceName))
                .AddVariables(SyntaxFactory.VariableDeclarator(_strategyField)))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            visited = visited.AddMembers(fieldDecl);
            if (!HasInstanceConstructor(visited))
            {
                visited = visited.AddMembers(CreateStrategyConstructor(visited));
            }
            GeneratedMembers = GeneratedMembers.AddRange(CreateStrategyTypes());
        }
        return visited;
    }

    public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (_targetIf != null &&
            !node.Modifiers.Any(SyntaxKind.StaticKeyword) &&
            node.Parent?.Span.Contains(_targetIf.Span) == true)
        {
            if (!visited.ParameterList.Parameters.Any(p => p.Identifier.ValueText == StrategyParameter))
            {
                visited = visited.AddParameterListParameters(CreateStrategyParameter());
                var assignment = CreateStrategyAssignment();
                visited = visited.WithBody(visited.Body!.WithStatements(visited.Body!.Statements.Insert(0, assignment)));
            }
        }
        return visited;
    }

    // A class with no instance constructor cannot assign the injected strategy, so the
    // constructor that takes it is generated. A static constructor does not count: an
    // instance field cannot be assigned there.
    private static bool HasInstanceConstructor(ClassDeclarationSyntax node)
    {
        return node.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Any(ctor => !ctor.Modifiers.Any(SyntaxKind.StaticKeyword));
    }

    private ConstructorDeclarationSyntax CreateStrategyConstructor(ClassDeclarationSyntax node)
    {
        // A class with a primary constructor may only declare another one if it
        // chains to the primary constructor, and the primary constructor's
        // parameters are in scope nowhere else, so they are carried through.
        var primaryParameters = node.ParameterList?.Parameters ?? default;

        var constructor = SyntaxFactory.ConstructorDeclaration(node.Identifier.ValueText)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(primaryParameters.ToArray())
            .AddParameterListParameters(CreateStrategyParameter())
            .WithBody(SyntaxFactory.Block(CreateStrategyAssignment()));

        if (primaryParameters.Count > 0)
        {
            constructor = constructor.WithInitializer(
                SyntaxFactory.ConstructorInitializer(
                    SyntaxKind.ThisConstructorInitializer,
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                        primaryParameters.Select(parameter =>
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier)))))));
        }

        return constructor;
    }

    private ParameterSyntax CreateStrategyParameter()
    {
        return SyntaxFactory.Parameter(SyntaxFactory.Identifier(StrategyParameter))
            .WithType(SyntaxFactory.IdentifierName(_interfaceName));
    }

    private ExpressionStatementSyntax CreateStrategyAssignment()
    {
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(_strategyField),
                SyntaxFactory.IdentifierName(StrategyParameter)));
    }

    private SyntaxList<MemberDeclarationSyntax> CreateStrategyTypes()
    {
        var applyMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "Apply")
            .WithModifiers(SyntaxFactory.TokenList())
            .WithBody(GetTrueBlock());
        var publicApplyMethod = applyMethod.WithModifiers(
            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        var iface = SyntaxFactory.InterfaceDeclaration(_interfaceName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddMembers(applyMethod.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).WithBody(null));

        var strat = SyntaxFactory.ClassDeclaration(_flagName + "Strategy")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(_interfaceName)))
            .AddMembers(publicApplyMethod);

        var noBody = GetFalseBlock();
        var noStrat = SyntaxFactory.ClassDeclaration("No" + _flagName + "Strategy")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(_interfaceName)))
            .AddMembers(publicApplyMethod.WithBody(noBody));

        return new SyntaxList<MemberDeclarationSyntax>(new MemberDeclarationSyntax[] { iface, strat, noStrat });
    }

    private BlockSyntax GetTrueBlock()
    {
        if (_targetIf!.Statement is BlockSyntax block)
            return block;
        return SyntaxFactory.Block(_targetIf.Statement);
    }

    private BlockSyntax GetFalseBlock()
    {
        if (_targetIf!.Else == null) return SyntaxFactory.Block();
        var stmt = _targetIf.Else.Statement;
        if (stmt is BlockSyntax b) return b;
        return SyntaxFactory.Block(stmt);
    }
}
