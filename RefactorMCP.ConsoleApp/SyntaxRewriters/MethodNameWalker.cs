using Microsoft.CodeAnalysis.CSharp.Syntax;

internal class MethodNameWalker : NameCollectorWalker
{
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Add(node.Identifier.ValueText);
        base.VisitMethodDeclaration(node);
    }
}
