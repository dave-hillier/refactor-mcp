using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class CalledMethodCollector : IdentifierUsageWalker
{
    public HashSet<string> CalledMethods { get; } = new();

    public CalledMethodCollector(HashSet<string> methodNames)
        : base(methodNames)
    {
    }

    protected override void RecordUsage(string name)
    {
        CalledMethods.Add(name);
    }

    protected override bool TryRecordInvocation(InvocationExpressionSyntax node)
    {
        var name = GetInvocationName(node);
        if (name != null && IsTarget(name))
        {
            RecordUsage(name);
            return true;
        }
        return false;
    }
}
