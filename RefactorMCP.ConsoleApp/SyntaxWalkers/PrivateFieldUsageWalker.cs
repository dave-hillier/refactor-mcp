using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
namespace RefactorMCP.ConsoleApp.SyntaxWalkers
{

    internal class PrivateFieldUsageWalker : TrackedNameWalker
    {
        public HashSet<string> UsedFields => Matches;

        public PrivateFieldUsageWalker(HashSet<string> privateFieldNames)
            : base(privateFieldNames)
        {
        }
    }
}
