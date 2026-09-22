using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Replaces method and property bodies with a placeholder, for the
/// <c>summary://</c> resource.
///
/// Expression-bodied members are left as they are: an arrow clause has to hold an
/// expression, so there is no placeholder that parses in its place, and inventing
/// one would put an expression in the summary that is not in the source.
/// </summary>
internal class BodyOmitter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        return SyntaxFactory.Block(SyntaxFactory.ParseStatement("// ...\n"));
    }
}
