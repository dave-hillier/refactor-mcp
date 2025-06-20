using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

internal class FeatureFlagRewriter : CSharpSyntaxRewriter
{
    private readonly string _flagName;
    private readonly string _interfaceName;
    private readonly string _strategyField;
    private bool _done;
    private IfStatementSyntax? _targetIf;
    public SyntaxList<MemberDeclarationSyntax> GeneratedMembers { get; private set; }

    public FeatureFlagRewriter(string flagName)
    {
        _flagName = flagName;
        _interfaceName = $"I{flagName}Strategy";
        _strategyField = $"_{char.ToLower(flagName[0])}{flagName.Substring(1)}Strategy";
        GeneratedMembers = new SyntaxList<MemberDeclarationSyntax>();
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
        if (!_done && IsFlagCheck(node.Condition, _flagName))
        {
            _done = true;
            _targetIf = node;
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
        if (_done && _targetIf != null && node.Span.Contains(_targetIf.Span))
        {
            var fieldDecl = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName(_interfaceName))
                .AddVariables(SyntaxFactory.VariableDeclarator(_strategyField)))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            visited = visited.AddMembers(fieldDecl);
            GeneratedMembers = GeneratedMembers.AddRange(CreateStrategyTypes());
        }
        return visited;
    }

    public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (_done && _targetIf != null && node.Parent?.Span.Contains(_targetIf.Span) == true)
        {
            var paramName = _strategyField.TrimStart('_');
            var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(SyntaxFactory.IdentifierName(_interfaceName));
            if (!visited.ParameterList.Parameters.Any(p => p.Identifier.ValueText == paramName))
            {
                visited = visited.AddParameterListParameters(param);
                var assignment = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(_strategyField),
                        SyntaxFactory.IdentifierName(paramName)));
                visited = visited.WithBody(visited.Body!.WithStatements(visited.Body!.Statements.Insert(0, assignment)));
            }
        }
        return visited;
    }

    private SyntaxList<MemberDeclarationSyntax> CreateStrategyTypes()
    {
        var applyMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                "Apply")
            .WithModifiers(SyntaxFactory.TokenList())
            .WithBody(GetTrueBlock());
        var iface = SyntaxFactory.InterfaceDeclaration(_interfaceName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddMembers(applyMethod.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)).WithBody(null));

        var strat = SyntaxFactory.ClassDeclaration(_flagName + "Strategy")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(_interfaceName)))
            .AddMembers(applyMethod);

        var noBody = GetFalseBlock();
        var noStrat = SyntaxFactory.ClassDeclaration("No" + _flagName + "Strategy")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(_interfaceName)))
            .AddMembers(applyMethod.WithBody(noBody));

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
