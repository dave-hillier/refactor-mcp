using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    /// <summary>
    /// Collects the fields a class keeps to itself. A field with no access
    /// modifier is private in C#, which is the usual style for backing fields,
    /// so a field only has to declare itself non-private to be excluded.
    /// </summary>
    internal class PrivateFieldInfoWalker : CSharpSyntaxWalker
    {
        public Dictionary<string, TypeSyntax> Infos { get; } = new();

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (IsPrivate(node.Modifiers))
            {
                foreach (var variable in node.Declaration.Variables)
                    Infos[variable.Identifier.ValueText] = node.Declaration.Type;
            }

            base.VisitFieldDeclaration(node);
        }

        private static bool IsPrivate(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.PrivateKeyword)
               || !modifiers.Any(modifier =>
                   modifier.IsKind(SyntaxKind.PublicKeyword)
                   || modifier.IsKind(SyntaxKind.ProtectedKeyword)
                   || modifier.IsKind(SyntaxKind.InternalKeyword));
    }
}
