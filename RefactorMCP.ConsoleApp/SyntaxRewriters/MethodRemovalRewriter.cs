using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorMCP.ConsoleApp.SyntaxRewriters;

internal class MethodRemovalRewriter : DeclarationRemovalRewriter<MethodDeclarationSyntax>
{
    public MethodRemovalRewriter(string methodName)
        : base(methodName)
    {
    }

    protected override bool IsTarget(MethodDeclarationSyntax node)
        => node.Identifier.ValueText == Name;
}

