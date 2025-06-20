using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class InterfaceCollectorWalker : TypeCollectorWalker<InterfaceDeclarationSyntax>
{
    public Dictionary<string, InterfaceDeclarationSyntax> Interfaces => Types;
}
