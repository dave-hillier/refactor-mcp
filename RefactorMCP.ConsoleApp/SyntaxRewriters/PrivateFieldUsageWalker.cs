using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

internal class PrivateFieldUsageWalker : IdentifierUsageWalker
{
    public HashSet<string> UsedFields { get; } = new();

    public PrivateFieldUsageWalker(HashSet<string> privateFieldNames)
        : base(privateFieldNames)
    {
    }

    protected override void RecordUsage(string name)
    {
        UsedFields.Add(name);
    }
}
