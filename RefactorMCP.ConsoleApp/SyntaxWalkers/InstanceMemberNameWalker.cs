using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    /// <summary>
    /// Collects the names of a class's fields and properties. Static members are
    /// skipped by default: a moved instance method must not end up qualified with
    /// <c>@this</c> against a member that has no instance.
    /// </summary>
    internal class InstanceMemberNameWalker : NameCollectorWalker
    {
        private readonly bool _includeStaticMembers;

        /// <param name="includeStaticMembers">
        /// Also collect static and const members. Used where a name has to be
        /// unique against everything the class declares, such as when generating
        /// the access member a move introduces.
        /// </param>
        public InstanceMemberNameWalker(bool includeStaticMembers = false)
        {
            _includeStaticMembers = includeStaticMembers;
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (_includeStaticMembers || !IsStatic(node.Modifiers))
            {
                foreach (var variable in node.Declaration.Variables)
                    Add(variable.Identifier.ValueText);
            }

            base.VisitFieldDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (_includeStaticMembers || !IsStatic(node.Modifiers))
                Add(node.Identifier.ValueText);

            base.VisitPropertyDeclaration(node);
        }

        // A const field is implicitly static.
        private static bool IsStatic(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.StaticKeyword) || modifiers.Any(SyntaxKind.ConstKeyword);
    }
}
